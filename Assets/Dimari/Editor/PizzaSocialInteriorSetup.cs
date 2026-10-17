using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Dimari.City;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class PizzaSocialInteriorSetup
    {
        const string Folder="Assets/Dimari/Generated/PizzaSocialInterior";
        const string Stamp="Dimari_PizzaSocialInterior_v1";
        static readonly Vector2[] Tables={new Vector2(-3.9f,-2.5f),new Vector2(3.9f,-2.5f),new Vector2(3.9f,0.45f)};
        static PizzaSocialInteriorSetup()
        {EditorApplication.delayCall+=Auto;EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Auto;}
        static void Auto()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}
            Apply(false);
        }
        [MenuItem("Dimari/Пицца — исправить поверхности и добавить компанию")]
        public static void Install(){Apply(true);}
        static Vector3 Seat(int i)
        {Vector2 table=Tables[i/4];int side=(i%4)/2==0?-1:1;return new Vector3(table.x+(i%2==0?-0.42f:0.42f),0.58f,table.y+side*0.8f);}
        static Vector3 SeatAnchor(CityLifeAgent guest)
        {
            var a=guest.GetComponentInChildren<Animator>();if(a!=null&&a.isHuman){Transform hips=a.GetBoneTransform(HumanBodyBones.Hips);if(hips!=null)return hips.position-Vector3.up*0.12f;}
            return guest.transform.position;
        }
        static void Apply(bool force)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")return;
            GameObject pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza==null)return;
            Transform old=pizza.transform.Find(Stamp);if(old!=null&&!force)return;
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Pizza surfaces and more conversations");
            try
            {
                if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
                var group=new GameObject(Stamp);group.transform.SetParent(pizza.transform,false);Undo.RegisterCreatedObjectUndo(group,"Pizza social interior update");
                int repaired=Repair(pizza.transform);
                if(pizza.GetComponent<PizzaInteriorVisibility>()==null)Undo.AddComponent<PizzaInteriorVisibility>(pizza);
                var guests=new List<CityLifeAgent>();
                foreach(var guest in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))
                {
                    if(!guest.seated||guest.gameObject.scene!=pizza.scene)continue;
                    Vector3 local=pizza.transform.InverseTransformPoint(SeatAnchor(guest));if(Mathf.Abs(local.x)<6&&Mathf.Abs(local.z)<5)guests.Add(guest);
                }
                var pool=new List<GameObject>();
                if(AssetDatabase.IsValidFolder("Assets/Dimari/NPCs"))foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dimari/NPCs"}))
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var animator=prefab.GetComponentInChildren<Animator>();
                    if(animator!=null&&animator.avatar!=null&&animator.avatar.isValid&&animator.isHuman)pool.Add(prefab);
                }
                bool sceneTemplates=pool.Count==0;
                // Clone seated templates without rescaling their already-seated height.
                if(sceneTemplates)foreach(var guest in guests)pool.Add(guest.gameObject);
                var assigned=new CityLifeAgent[12];int added=0;
                foreach(var guest in guests)
                {
                    Vector3 local=pizza.transform.InverseTransformPoint(SeatAnchor(guest));int best=-1;float nearest=float.PositiveInfinity;
                    for(int i=0;i<12;i++)if(assigned[i]==null){float d=(local-Seat(i)).sqrMagnitude;if(d<nearest){nearest=d;best=i;}}
                    if(best>=0)assigned[best]=guest;
                }
                for(int i=0;i<12;i++)
                {
                    Quaternion rotation=pizza.transform.rotation*Quaternion.Euler(0,(i%4)/2==0?0:180,0);
                    Vector3 position=pizza.transform.TransformPoint(Seat(i));
                    if(assigned[i]==null&&pool.Count>0)
                    {
                        if(sceneTemplates)
                        {
                            var clone=Object.Instantiate(pool[i%pool.Count],pizza.transform);clone.name="Pizza visitor";
                            var inheritedMeal=clone.GetComponent<PizzaGuestEating>();if(inheritedMeal!=null)Object.DestroyImmediate(inheritedMeal);
                            assigned[i]=clone.GetComponent<CityLifeAgent>();assigned[i].graph=null;
                        }
                        else assigned[i]=CityLifeSetup.Human(pool[i%pool.Count],pizza.transform,position,rotation,true);
                        Undo.RegisterCreatedObjectUndo(assigned[i].gameObject,"Seat additional pizza visitor");added++;
                    }
                    var guest=assigned[i];if(guest==null)continue;
                    Undo.RecordObject(guest.transform,"Align visitors to free bench places");guest.transform.rotation=rotation;
                    guest.transform.position+=position-SeatAnchor(guest);
                    foreach(var r in guest.GetComponentsInChildren<Renderer>())
                    {Undo.RecordObject(r,"Keep cafe guest visible");r.allowOcclusionWhenDynamic=false;if(r is SkinnedMeshRenderer skinned){skinned.updateWhenOffscreen=true;}}
                    Dining(guest,group.transform,pizza.transform,i);
                }
                CityAmbientConversations conversations=null;
                foreach(var candidate in Object.FindObjectsByType<CityAmbientConversations>(FindObjectsSortMode.None))if(candidate.gameObject.scene==pizza.scene){conversations=candidate;break;}
                if(conversations==null){var go=new GameObject("Pizza conversations");go.transform.SetParent(pizza.transform,false);Undo.RegisterCreatedObjectUndo(go,"Cafe conversations");Undo.AddComponent<CityAmbientConversations>(go);}
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(pizza.scene);AssetDatabase.SaveAssets();
                string report="[Dimari] Пиццерия: обработано поверхностей "+repaired+", новых посетителей "+added+", мест до 12. Проверьте Play, затем сохраните сцену Ctrl+S. Новые темы — субтитры; новая озвучка не включена.";
                Debug.Log(report,pizza);if(force)EditorUtility.DisplayDialog("Пиццерия обновлена",report,"OK");
            }
            finally {Undo.CollapseUndoOperations(undo);}
        }
        static int Repair(Transform pizza)
        {
            int count=0;var copies=new Dictionary<Material,Material>();
            foreach(Transform t in pizza.GetComponentsInChildren<Transform>(true))
            {
                Vector3 local=pizza.InverseTransformPoint(t.position);
                bool placeholder=local.z>4.6f&&(t.name=="Menu screen"||t.name.StartsWith("Sign МЕНЮ")||t.name.StartsWith("Sign ГОРЯЧАЯ ПИЦЦА")||t.name.StartsWith("Sign ПИЦЦА"));
                bool obsoleteDish=(t.name=="Guest pizza"||t.name=="Ceramic plate"||t.name=="Raised baked crust")&&local.z<1.2f&&Mathf.Abs(local.x)>2.8f;
                if(placeholder||obsoleteDish){Undo.RecordObject(t.gameObject,"Hide generated placeholder");t.gameObject.SetActive(false);}
            }
            foreach(Renderer r in pizza.GetComponentsInChildren<Renderer>())
            {
                if(r.GetComponentInParent<CityLifeAgent>()!=null)continue;
                Undo.RecordObject(r,"Repair local material and visibility");r.allowOcclusionWhenDynamic=false;
                var flags=GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                Undo.RecordObject(r.gameObject,"Exclude new interior from old occlusion bake");GameObjectUtility.SetStaticEditorFlags(r.gameObject,flags&~(StaticEditorFlags.OccludeeStatic|StaticEditorFlags.OccluderStatic));
                var slots=r.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    Material original=slots[i];if(original==null)continue;
                    if(!copies.TryGetValue(original,out Material material))
                    {
                        if(AssetDatabase.GetAssetPath(original).StartsWith(Folder+"/"))material=original;
                        else{material=new Material(original);AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Surface.mat"));}
                        copies[original]=material;
                    }
                    if(material.shader==null||material.shader.name=="Hidden/InternalErrorShader")
                    {Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader!=null)material.shader=shader;}
                    bool transparent=(material.HasProperty("_Surface")&&material.GetFloat("_Surface")>0.5f)||material.renderQueue>=3000;
                    if(!transparent&&material.HasProperty("_Cull"))material.SetFloat("_Cull",0);
                    if(!transparent){material.doubleSidedGI=true;if(material.HasProperty("_Metallic")&&!r.name.ToLowerInvariant().Contains("steel"))material.SetFloat("_Metallic",0);}
                    if(material.HasProperty("_BaseMap")&&material.GetTexture("_BaseMap")==null)
                    {
                        Texture texture=material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;
                        string name=original.name.ToLowerInvariant(),key=null;
                        if(r.name=="Tiled floor")key="Tiles";
                        else if(r.name=="Rounded oak table")key="OakTop";
                        else if(name.Contains("pepperoni"))key="Pepperoni";
                        else if(name.Contains("margherita"))key="Margherita";
                        else if(name.Contains("mushroom"))key="Mushroom";
                        else if(name.Contains("plaster"))key="Plaster";
                        if(texture==null&&key!=null)texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Pizza/Pizza_"+key+".png");
                        if(texture!=null)material.SetTexture("_BaseMap",texture);
                    }
                    if(r.name=="Real pizza menu")
                    {
                        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Pizza/Pizza_Menu.png");Shader unlit=Shader.Find("Universal Render Pipeline/Unlit");
                        if(texture!=null&&unlit!=null){material.shader=unlit;material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);material.SetFloat("_Cull",0);r.shadowCastingMode=ShadowCastingMode.Off;}
                    }
                    EditorUtility.SetDirty(material);slots[i]=material;
                }
                r.sharedMaterials=slots;count++;
            }
            return count;
        }
        static Material Mat(string name,string texture,Color tint)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",tint);else material.color=tint;
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",0.25f);
            if(texture!=null){var image=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Pizza/Pizza_"+texture+".png");if(image!=null)material.mainTexture=image;}
            return material;
        }
        static Transform Cylinder(Transform parent,string name,Vector3 p,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;
        }
        static Mesh Disc(bool slice)
        {
            string path=Folder+(slice?"/Slice.asset":"/Topping.asset");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            var v=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{Vector2.one*0.5f};var indices=new List<int>();int steps=slice?12:48;
            for(int i=0;i<=steps;i++)
            {float a=(float)i/steps*(slice?Mathf.PI/3:Mathf.PI*2);var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v.Add(d*0.5f);uv.Add(Vector2.one*0.5f+new Vector2(d.x,d.z)*0.46f);if(i>0)indices.AddRange(new[]{0,i+1,i});}
            mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Transform Topping(Transform parent,Vector3 p,float diameter,Material mat,bool slice)
        {
            var go=new GameObject(slice?"Conversation guest slice":"Conversation guest pizza");go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=new Vector3(diameter,1,diameter);go.AddComponent<MeshFilter>().sharedMesh=Disc(slice);go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;
        }
        static void Dining(CityLifeAgent guest,Transform root,Transform pizza,int slot)
        {
            Vector2 table=Tables[slot/4];int side=(slot%4)/2==0?-1:1;
            Vector3 plate=new Vector3(table.x+(slot%2==0?-0.4f:0.4f),0.82f,table.y+side*0.21f);
            var meal=guest.GetComponent<PizzaGuestEating>();if(meal==null)meal=Undo.AddComponent<PizzaGuestEating>(guest.gameObject);else Undo.RecordObject(meal,"Update guest food targets");
            if(meal.cup!=null){Undo.RecordObject(meal.cup.gameObject,"Hide old guest cup");meal.cup.gameObject.SetActive(false);}
            if(meal.slice!=null){Undo.RecordObject(meal.slice.gameObject,"Hide old guest slice");meal.slice.gameObject.SetActive(false);}
            var ceramic=Mat("Ceramic",null,new Color(0.94f,0.92f,0.85f));var crust=Mat("Crust",null,new Color(0.64f,0.34f,0.12f));
            var topping=Mat("Pizza"+(slot%3),new[]{"Pepperoni","Margherita","Mushroom"}[slot%3],Color.white);
            Cylinder(root,"Guest plate",plate-Vector3.up*0.01f,new Vector3(0.3f,0.005f,0.3f),ceramic);
            Cylinder(root,"Guest crust",plate,new Vector3(0.26f,0.008f,0.26f),crust);Topping(root,plate+Vector3.up*0.009f,0.25f,topping,false);
            var cup=new GameObject("Guest drink");cup.transform.SetParent(root,false);cup.transform.localPosition=plate+new Vector3(0.16f,0,0);
            Cylinder(cup.transform,"Cup",Vector3.up*0.045f,new Vector3(0.07f,0.045f,0.07f),ceramic);
            Cylinder(cup.transform,"Tea",Vector3.up*0.091f,new Vector3(0.06f,0.001f,0.06f),Mat("Tea",null,new Color(0.15f,0.06f,0.02f)));
            var slice=Topping(root,Vector3.zero,0.1f,topping,true);slice.gameObject.SetActive(false);meal.cup=cup.transform;meal.slice=slice;meal.platePosition=pizza.TransformPoint(plate);
        }
    }
}
