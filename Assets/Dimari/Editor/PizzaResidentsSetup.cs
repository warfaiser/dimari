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
                report+="Готовых моделей обычных людей: "+pool.Count+"; заменено существующих людей: "+replaced+".\n";
            }
            else report+="Обычные люди пока не импортированы. Положите разные FBX в Assets/Dimari/Imports/People и повторите команду. Старые люди не удалены.\n";
            report+=Pests(pizza.transform,"Rat",4,0.18f,0.45f);
            report+=Pests(pizza.transform,"Cockroach",10,0.035f,0.18f);
            Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(pizza.scene);
            EditorUtility.DisplayDialog("Пиццерия",report+"\nМодели не скачиваются этой командой. Проверьте материалы, ориентацию и анимацию в Play, затем Ctrl+S.","OK");
        }
        static List<GameObject> PreparePeople()
        {
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
        static string Pests(Transform pizza,string type,int count,float length,float speed)
        {
            if(pizza.Find("Pizza pests "+type)!=null)return type+": уже установлены.\n";
            string folder="Assets/Dimari/Imports/"+type;
            if(!AssetDatabase.IsValidFolder(folder))return type+": нужна модель в "+folder+".\n";
            GameObject model=null;string modelPath=null;
            foreach(string guid in AssetDatabase.FindAssets("t:Model",new[]{folder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(!path.EndsWith(".fbx",System.StringComparison.OrdinalIgnoreCase)&&!path.EndsWith(".obj",System.StringComparison.OrdinalIgnoreCase))continue;
                model=AssetDatabase.LoadAssetAtPath<GameObject>(path);modelPath=path;break;
            }
            if(model==null)return type+": FBX/OBJ не найден. GLB требует отдельного импортера.\n";
            var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if(importer!=null)
            {
                var clips=importer.clipAnimations;if(clips.Length==0)clips=importer.defaultClipAnimations;
                bool changed=false;
                for(int i=0;i<clips.Length;i++)
                {string name=clips[i].name.ToLowerInvariant();if((name.Contains("run")||name.Contains("walk")||name.Contains("crawl"))&&!clips[i].loopTime){clips[i].loopTime=true;changed=true;}}
                if(changed){importer.clipAnimations=clips;importer.SaveAndReimport();model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);}
            }
            var group=new GameObject("Pizza pests "+type);group.transform.SetParent(pizza,false);Undo.RegisterCreatedObjectUndo(group,"Add pizza pests");
            AnimationClip run=null;
            foreach(Object obj in AssetDatabase.LoadAllAssetsAtPath(modelPath))if(obj is AnimationClip clip&&!clip.name.StartsWith("__preview__"))
            {string name=clip.name.ToLowerInvariant();if(name.Contains("run")||name.Contains("walk")||name.Contains("crawl")){run=clip;break;}}
            AnimatorController controller=null;
            if(run!=null&&!run.legacy)
            {
                string path=Output+"/"+type+"Run.controller";controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
                if(controller.layers[0].stateMachine.states.Length==0){var state=controller.layers[0].stateMachine.AddState("Run");state.motion=run;controller.layers[0].stateMachine.defaultState=state;}
            }
            for(int i=0;i<count;i++)
            {
                var wrapper=new GameObject(type+" "+(i+1));wrapper.transform.SetParent(group.transform,false);
                wrapper.transform.localPosition=new Vector3((i%2==0?-1:1)*(4.8f-(i%3)*0.4f),0.055f,-3.7f+(i%5)*1.6f);
                var visual=Object.Instantiate(model,wrapper.transform);visual.name="Visual";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
                foreach(Collider c in visual.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                foreach(Rigidbody b in visual.GetComponentsInChildren<Rigidbody>())Object.DestroyImmediate(b);
                Bounds bnd=new Bounds();bool first=true;foreach(Renderer r in visual.GetComponentsInChildren<Renderer>()){if(first){bnd=r.bounds;first=false;}else bnd.Encapsulate(r.bounds);}
                float extent=Mathf.Max(bnd.size.x,bnd.size.z);if(extent>0.0001f)visual.transform.localScale*=length/extent;
                float min=float.PositiveInfinity;foreach(Renderer r in visual.GetComponentsInChildren<Renderer>())min=Mathf.Min(min,r.bounds.min.y);
                if(!float.IsInfinity(min))visual.transform.position+=Vector3.up*(wrapper.transform.position.y-min);
                ConvertMaterials(visual,type);
                var animator=visual.GetComponentInChildren<Animator>();if(animator!=null){animator.applyRootMotion=false;if(controller!=null)animator.runtimeAnimatorController=controller;}
                var pest=wrapper.AddComponent<PizzaPest>();pest.room=pizza;pest.speed=speed;pest.radius=type=="Rat"?0.03f:0.008f;
            }
            return type+": добавлено "+count+(controller!=null?", найдена анимация бега.":", бег по полу без подтверждённого клипа лап.")+"\n";
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
                    Texture color=original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null;
                    mat.SetTexture("_BaseMap",color);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",0.25f);
                    if(original.HasProperty("_BumpMap")){Texture normal=original.GetTexture("_BumpMap");if(normal!=null){mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");}}
                    slots[i]=mat;EditorUtility.SetDirty(mat);
                }
                renderer.sharedMaterials=slots;
            }
        }
    }
}
