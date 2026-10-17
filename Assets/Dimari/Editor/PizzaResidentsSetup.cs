using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.City;

namespace Dimari.Editor
{
    public static class PizzaResidentsSetup
    {
        const string Output="Assets/Dimari/Generated/PizzaResidents";
        [MenuItem("Dimari/Пицца — продавец, новые люди и вредители")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")
            {EditorUtility.DisplayDialog("Пиццерия","Остановите Play и откройте Chapter2_Rogovo.","OK");return;}
            GameObject pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza==null)return;
            Directory.CreateDirectory(Output);Directory.CreateDirectory("Assets/Dimari/NPCs");AssetDatabase.Refresh();
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Populate pizza restaurant");
            string report="";
            Transform seller=pizza.transform.Find("Pizza seller Obzhorka");
            var woman=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AdministrationWoman/Generated/AdministrationWoman.prefab");
            if(seller==null&&woman!=null)
            {
                var agent=CityLifeSetup.Human(woman,pizza.transform,pizza.transform.TransformPoint(new Vector3(0,0.05f,3.5f)),pizza.transform.rotation*Quaternion.Euler(0,180,0),false);
                GameObject go=agent.gameObject;go.name="Pizza seller Obzhorka";
                // Stationary standing seller; no sidewalk movement and no seated dialogue classification.
                agent.graph=null;
                Undo.RegisterCreatedObjectUndo(go,"Add pizza seller");report+="Продавец добавлен за стойкой.\n";
            }
            var pool=PreparePeople();int replaced=0;
            if(pool.Count>0)
            {
                if(GameObject.Find("Dimari_CityLife")==null)CityLifeSetup.Install();
                foreach(var old in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))
                {
                    if(old.vehicle||old.name=="Pizza seller Obzhorka"||old.gameObject.scene!=pizza.scene)continue;
                    Vector3 position=old.transform.position;
                    if(old.seated)
                    {
                        var animator=old.GetComponentInChildren<Animator>();
                        if(animator!=null&&animator.isHuman){Transform hips=animator.GetBoneTransform(HumanBodyBones.Hips);if(hips!=null)position=hips.position-Vector3.up*0.12f;}
                    }
                    var fresh=CityLifeSetup.Human(pool[replaced%pool.Count],old.transform.parent,position,old.transform.rotation,old.seated);
                    fresh.graph=old.graph;fresh.node=old.node;fresh.previous=old.previous;fresh.speed=old.speed;
                    var dining=old.GetComponent<PizzaGuestEating>();
                    if(dining!=null)
                    {
                        var replacement=fresh.gameObject.AddComponent<PizzaGuestEating>();
                        replacement.cup=dining.cup;replacement.slice=dining.slice;replacement.platePosition=dining.platePosition;
                    }
                    Undo.RegisterCreatedObjectUndo(fresh.gameObject,"Replace population model");Undo.DestroyObjectImmediate(old.gameObject);replaced++;
                }
                // Visitors may not have existed when CityLife was installed without ordinary people.
                int count=0;foreach(var a in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))if(a.seated)count++;
                if(count==0)for(int i=0;i<4;i++)
                {
                    float x=i<2?-3.9f:3.9f;int side=i%2==0?-1:1;
                    var fresh=CityLifeSetup.Human(pool[i%pool.Count],pizza.transform,pizza.transform.TransformPoint(new Vector3(x,0.58f,-2.5f+side*0.8f)),pizza.transform.rotation*Quaternion.Euler(0,side<0?0:180,0),true);
                    Undo.RegisterCreatedObjectUndo(fresh.gameObject,"Add ordinary visitor");
                }
                int added=EnsureWalkers(pool);
                report+="Готовых моделей обычных людей: "+pool.Count+"; заменено существующих людей: "+replaced+"; добавлено новых прохожих: "+added+".\n";
            }
            else report+="Обычные люди пока не импортированы. Положите разные FBX в Assets/Dimari/Imports/People и повторите команду. Старые люди не удалены.\n";
            if(AssetDatabase.IsValidFolder("Assets/Dimari/Imports/Mouse"))report+=Pests(pizza.transform,"Mouse",3,0.16f,0.4f);
            else report+=Pests(pizza.transform,"Rat",4,0.18f,0.45f);
            report+=Pests(pizza.transform,"Cockroach",10,0.035f,0.18f);
            Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(pizza.scene);
            EditorUtility.DisplayDialog("Пиццерия",report+"\nМодели не скачиваются этой командой. Проверьте материалы, ориентацию и анимацию в Play, затем Ctrl+S.","OK");
        }
        [MenuItem("Dimari/City People — подключить горожан и посетителей")]
        public static void ConnectCityPeople() { Install(); }

        static List<GameObject> PreparePeople()
        {
            ImportCityPeople();
            const string folder="Assets/Dimari/Imports/People";
            if(AssetDatabase.IsValidFolder(folder))foreach(string guid in AssetDatabase.FindAssets("t:Model",new[]{folder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null||!path.EndsWith(".fbx",System.StringComparison.OrdinalIgnoreCase))continue;
                if(importer.animationType!=ModelImporterAnimationType.Human||importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel)
                {importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();}
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var animator=model.GetComponentInChildren<Animator>();
                if(animator==null||animator.avatar==null||!animator.avatar.isHuman||!animator.avatar.isValid){Debug.LogWarning("[Dimari] Не распознан Humanoid: "+path);continue;}
                var copy=Object.Instantiate(model);ConvertMaterials(copy,Path.GetFileNameWithoutExtension(path));
                PrefabUtility.SaveAsPrefabAsset(copy,"Assets/Dimari/NPCs/"+Path.GetFileNameWithoutExtension(path)+".prefab");Object.DestroyImmediate(copy);
            }
            var result=new List<GameObject>();foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dimari/NPCs"}))
            {
                var p=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var a=p.GetComponentInChildren<Animator>();
                if(a!=null&&a.avatar!=null&&a.avatar.isHuman&&a.avatar.isValid)result.Add(p);
            }
            return result;
        }
        static int EnsureWalkers(List<GameObject> pool)
        {
            foreach(var agent in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))
                if(!agent.vehicle&&!agent.seated&&agent.graph!=null&&agent.gameObject.scene==SceneManager.GetActiveScene())return 0;
            CityLifeGraph graph=null;
            foreach(var candidate in Object.FindObjectsByType<CityLifeGraph>(FindObjectsSortMode.None))
                if(candidate.name=="Sidewalk graph"&&candidate.gameObject.scene==SceneManager.GetActiveScene())graph=candidate;
            if(graph==null||graph.nodes==null)return 0;
            int added=0;var used=new List<Vector3>();
            for(int i=0;i<graph.nodes.Length&&added<12;i++)
            {
                if(graph.nodes[i].next==null||graph.nodes[i].next.Length==0)continue;
                Vector3 point=graph.nodes[i].position;bool close=false;
                foreach(Vector3 other in used)if(Vector3.Distance(point,other)<22)close=true;
                if(close)continue;used.Add(point);
                foreach(int node in new[]{i,graph.nodes[i].next[0]})
                {
                    var fresh=CityLifeSetup.Human(pool[added%pool.Count],graph.transform.parent,graph.nodes[node].position,Quaternion.identity,false);
                    fresh.graph=graph;fresh.node=node;fresh.speed=0.8f+(added%4)*0.12f;
                    Undo.RegisterCreatedObjectUndo(fresh.gameObject,"Add City People pedestrian");added++;
                }
            }
            return added;
        }

        static void ImportCityPeople()
        {
            int found=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                string compact=path.ToLowerInvariant().Replace(" ","").Replace("_","").Replace("-","");
                // Publisher folder names differ between package releases; do not assume one exact path.
                if(!compact.Contains("citypeople")||path.StartsWith("Assets/Dimari/"))continue;
                string name=Path.GetFileNameWithoutExtension(path);
                string lower=name.ToLowerInvariant();
                if(lower.Contains("demo")||lower.Contains("crowd")||lower.Contains("child")||lower.Contains("baby")||lower.Contains("kid"))continue;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)continue;
                Animator[] animators=prefab.GetComponentsInChildren<Animator>(true);
                if(animators.Length!=1||animators[0].avatar==null||!animators[0].avatar.isValid||!animators[0].avatar.isHuman)continue;
                if(prefab.GetComponentInChildren<Camera>(true)!=null)continue;
                GameObject copy=Object.Instantiate(prefab);
                try
                {
                    ConvertMaterials(copy,"CityPeople_"+guid.Substring(0,8));
                    string destination="Assets/Dimari/NPCs/CityPeople_"+guid.Substring(0,8)+".prefab";
                    PrefabUtility.SaveAsPrefabAsset(copy,destination);found++;
                    Debug.Log("[Dimari] City People: "+path+" → "+destination);
                }
                finally { Object.DestroyImmediate(copy); }
            }
            if(found==0)Debug.LogWarning("[Dimari] Не найдены Humanoid-префабы в папках City People. Откройте Imported Assets в Package Manager и пришлите путь к префабам. Исходный пакет не изменён.");
            else Debug.Log("[Dimari] Подготовлены модели City People: "+found+". Исходный пакет не изменён.");
        }

        public static bool FindPestModel(string type,out GameObject model,out string modelPath)
        {
            model=null;modelPath=null;int best=0;
            string folder="Assets/Dimari/Imports/"+type;
            if(!AssetDatabase.IsValidFolder(folder))return false;
            string[] guids=AssetDatabase.FindAssets("t:Model",new[]{folder});System.Array.Sort(guids,System.StringComparer.Ordinal);
            foreach(string guid in guids)
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(!path.EndsWith(".fbx",System.StringComparison.OrdinalIgnoreCase)&&!path.EndsWith(".obj",System.StringComparison.OrdinalIgnoreCase))continue;
                var candidate=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(candidate==null)continue;
                int vertices=0;
                foreach(var mesh in candidate.GetComponentsInChildren<MeshFilter>(true))if(mesh.sharedMesh!=null)vertices+=mesh.sharedMesh.vertexCount;
                foreach(var mesh in candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(mesh.sharedMesh!=null)vertices+=mesh.sharedMesh.vertexCount;
                if(vertices<=best)continue;best=vertices;model=candidate;modelPath=path;
            }
            return model!=null;
        }
        static bool IsRun(string name)
        {
            name=name.ToLowerInvariant();return name.Contains("run")||name.Contains("walk")||name.Contains("crawl")||name.Contains("sprint")||name.Contains("locomotion");
        }
        static AnimationClip RunClip(string modelPath,string folder)
        {
            var paths=new List<string>{modelPath};
            foreach(string guid in AssetDatabase.FindAssets("t:Model",new[]{folder}))
            {string path=AssetDatabase.GUIDToAssetPath(guid);if(path!=modelPath&&path.EndsWith(".fbx",System.StringComparison.OrdinalIgnoreCase))paths.Add(path);}
            foreach(string path in paths)
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)continue;
                bool changed=false;
                if(!importer.importAnimation){importer.importAnimation=true;changed=true;}
                if(importer.animationType==ModelImporterAnimationType.Human||importer.animationType==ModelImporterAnimationType.None)
                {importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.sourceAvatar=null;changed=true;}
                if(changed)importer.SaveAndReimport();
                var clips=importer.clipAnimations;if(clips.Length==0)clips=importer.defaultClipAnimations;
                for(int i=0;i<clips.Length;i++)
                {
                    // One anonymous FBX take is common for the downloaded cockroach walk cycle.
                    bool anonymous=clips.Length==1&&(clips[i].name.ToLowerInvariant().Contains("take")||clips[i].name.ToLowerInvariant().Contains("armature"));
                    if(!IsRun(clips[i].name)&&!anonymous)continue;
                    clips[i].loopTime=true;clips[i].loopPose=true;clips[i].lockRootPositionXZ=true;clips[i].lockRootHeightY=true;clips[i].lockRootRotation=true;
                }
                if(clips.Length>0){importer.clipAnimations=clips;importer.SaveAndReimport();}
                AnimationClip fallback=null;int total=0;
                foreach(Object obj in AssetDatabase.LoadAllAssetsAtPath(path))if(obj is AnimationClip clip&&!clip.name.StartsWith("__preview__"))
                {total++;fallback=clip;if(IsRun(clip.name))return clip;}
                if(total==1&&fallback!=null&&(fallback.name.ToLowerInvariant().Contains("take")||fallback.name.ToLowerInvariant().Contains("armature")))return fallback;
            }
            return null;
        }
        public static string Pests(Transform pizza,string type,int count,float length,float speed,bool replace=false)
        {
            Transform existing=pizza.Find("Pizza pests "+type);
            if(existing!=null&&!replace)return type+": уже установлены.\n";
            string folder="Assets/Dimari/Imports/"+type;
            if(!FindPestModel(type,out GameObject model,out string modelPath))
                return type+": не найдена геометрия FBX/OBJ в "+folder+". BLEND/GLB текущим установщиком не поддерживаются. Старые объекты сохранены.\n";
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            AnimationClip run=RunClip(modelPath,folder);model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            AnimatorController controller=null;
            if(run!=null&&!run.legacy)
            {
                string path=Output+"/"+type+"Run.controller";controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
                var machine=controller.layers[0].stateMachine;
                var state=machine.defaultState;
                if(state==null)state=machine.AddState("Run");
                state.motion=run;machine.defaultState=state;EditorUtility.SetDirty(state);EditorUtility.SetDirty(controller);
            }
            // Validate source before replacing only this species, never the restaurant or people.
            var group=new GameObject("Pizza pests "+type+" (preparing)");group.transform.SetParent(pizza,false);Undo.RegisterCreatedObjectUndo(group,"Add random pizza pests");
            int added=0;Physics.SyncTransforms();var occupied=new List<Vector3>();
            for(int i=0;i<count;i++)
            {
                Vector3 local=Vector3.zero;bool found=false;float radius=type=="Cockroach"?0.008f:0.035f;
                // Deterministic, distributed starting cells; runtime directions and pauses are random.
                for(int attempt=0;attempt<160;attempt++)
                {
                    int cell=(i*29+attempt*37)%160;
                    local=new Vector3(-5.2f+(cell%16)*0.69f,0.035f,-4.1f+(cell/16)*0.9f);
                    Vector3 world=pizza.TransformPoint(local);
                    bool close=false;foreach(Vector3 point in occupied)if((point-world).sqrMagnitude<0.12f){close=true;break;}
                    if(close||Physics.CheckSphere(world+Vector3.up*(radius+0.025f),radius,~0,QueryTriggerInteraction.Ignore))continue;
                    if(!Physics.Raycast(world+Vector3.up*0.1f,Vector3.down,out RaycastHit support,0.22f,~0,QueryTriggerInteraction.Ignore)||support.normal.y<0.85f||Mathf.Abs(pizza.InverseTransformPoint(support.point).y-0.03f)>0.07f)continue;
                    if(support.collider.attachedRigidbody!=null||support.collider.GetComponentInParent<CityLifeAgent>()!=null)continue;
                    local=pizza.InverseTransformPoint(support.point+Vector3.up*0.005f);occupied.Add(pizza.TransformPoint(local));found=true;break;
                }
                if(!found)continue;
                var wrapper=new GameObject(type+" "+(i+1));wrapper.transform.SetParent(group.transform,false);wrapper.transform.localPosition=local;
                var visual=Object.Instantiate(model,wrapper.transform);visual.name="Visual";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
                foreach(Collider c in visual.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
                foreach(Rigidbody b in visual.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(b);
                Bounds bnd=new Bounds();bool first=true;foreach(Renderer r in visual.GetComponentsInChildren<Renderer>()){if(first){bnd=r.bounds;first=false;}else bnd.Encapsulate(r.bounds);}
                float extent=Mathf.Max(bnd.size.x,bnd.size.z);if(extent>0.0001f)visual.transform.localScale*=length/extent;
                // Center visual horizontally on the steering root, and place feet on the floor.
                first=true;foreach(Renderer r in visual.GetComponentsInChildren<Renderer>()){if(first){bnd=r.bounds;first=false;}else bnd.Encapsulate(r.bounds);}
                if(!first)visual.transform.position+=new Vector3(wrapper.transform.position.x-bnd.center.x,wrapper.transform.position.y-bnd.min.y,wrapper.transform.position.z-bnd.center.z);
                ConvertMaterials(visual,type);
                var animator=visual.GetComponentInChildren<Animator>();
                foreach(var a in visual.GetComponentsInChildren<Animator>(true)){a.applyRootMotion=false;a.enabled=false;}
                if(controller!=null)
                {
                    if(animator==null)animator=visual.AddComponent<Animator>();
                    animator.enabled=true;animator.applyRootMotion=false;animator.runtimeAnimatorController=controller;
                }
                else if(run!=null&&run.legacy)
                {var animation=visual.GetComponent<Animation>();if(animation==null)animation=visual.AddComponent<Animation>();animation.AddClip(run,"DimariRun");animation.clip=run;animation.wrapMode=WrapMode.Loop;animation.playAutomatically=true;}
                var pest=wrapper.AddComponent<PizzaPest>();pest.room=pizza;pest.speed=speed;pest.radius=radius;added++;
            }
            if(added==0)
            {Undo.DestroyObjectImmediate(group);return type+": не найдены свободные точки пола. Старые объекты сохранены. Проверьте коллайдер пола пиццерии.\n";}
            if(existing!=null)Undo.DestroyObjectImmediate(existing.gameObject);
            group.name="Pizza pests "+type;
            return type+": добавлено "+added+" / "+count+(run!=null?", клип: "+run.name+".":", клип лап не распознан — только перемещение по полу.")+"\n";
        }
        static void ConvertMaterials(GameObject go,string prefix)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)return;
            foreach(Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    Material original=slots[i];if(original==null)continue;
                    if(original.shader!=null&&original.shader.name.StartsWith("Universal Render Pipeline/"))continue;
                    string safe=prefix+"_"+original.name;foreach(char c in Path.GetInvalidFileNameChars())safe=safe.Replace(c,'_');
                    string path=Output+"/"+safe+".mat";Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                    Texture color=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):(original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null);
                    Color tint=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):(original.HasProperty("_Color")?original.GetColor("_Color"):Color.white);
                    mat.SetTexture("_BaseMap",color);mat.SetColor("_BaseColor",tint);mat.SetFloat("_Smoothness",0.25f);
                    string textureProperty=original.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                    if(original.HasProperty(textureProperty)){mat.SetTextureScale("_BaseMap",original.GetTextureScale(textureProperty));mat.SetTextureOffset("_BaseMap",original.GetTextureOffset(textureProperty));}
                    if(original.HasProperty("_BumpMap")){Texture normal=original.GetTexture("_BumpMap");if(normal!=null){mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");}}
                    slots[i]=mat;EditorUtility.SetDirty(mat);
                }
                renderer.sharedMaterials=slots;
            }
        }
    }
}
