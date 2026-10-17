using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.City;
using Dimari.Vehicles;

namespace Dimari.Editor
{
    public static class CityLifeSetup
    {
        [MenuItem("Dimari/Город — добавить жизнь и меню пиццерии")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")
            {EditorUtility.DisplayDialog("Город","Остановите Play и откройте Chapter2_Rogovo.","OK");return;}
            GameObject pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza!=null)Menu(pizza.transform);
            if(GameObject.Find("Dimari_CityLife")!=null)
            {EditorUtility.DisplayDialog("Город","Меню обновлено. Население уже существует; дубликаты не добавлены.","OK");EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return;}
            GameObject root=new GameObject("Dimari_CityLife");Undo.RegisterCreatedObjectUndo(root,"Add city life");
            CityLifeGraph road=Graph(root.transform,false),walk=Graph(root.transform,true);
            GameObject human=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AdministrationWoman/Generated/AdministrationWoman.prefab");
            var people=new List<GameObject>();if(human!=null)people.Add(human);
            if(AssetDatabase.IsValidFolder("Assets/Dimari/NPCs"))
                foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dimari/NPCs"}))
                {GameObject p=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var a=p.GetComponentInChildren<Animator>();if(a!=null&&a.avatar!=null&&a.avatar.isHuman)people.Add(p);}
            int walkers=0,visitors=0,cars=0;
            if(people.Count>0)
            {
                var used=new List<Vector3>();
                for(int i=0;i<walk.nodes.Length&&walkers<12;i++)
                {
                    if(walk.nodes[i].next.Length==0)continue;
                    Vector3 p=walk.nodes[i].position;bool close=false;foreach(Vector3 q in used)if(Vector3.Distance(p,q)<22)close=true;
                    if(close)continue;used.Add(p);
                    foreach(int id in new[]{i,walk.nodes[i].next[0]})
                    {
                        var agent=Human(people[walkers%people.Count],root.transform,walk.nodes[id].position,Quaternion.identity,false);
                        agent.graph=walk;agent.node=id;agent.speed=0.8f+(walkers%4)*0.12f;walkers++;
                    }
                }
                if(pizza!=null)for(int i=0;i<4;i++)
                {
                    float x=i<2?-3.9f:3.9f;int side=i%2==0?-1:1;
                    Vector3 local=new Vector3(x,0.58f,-2.5f+side*0.8f);
                    Quaternion rotation=pizza.transform.rotation*Quaternion.Euler(0,side<0?0:180,0);
                    Human(people[i%people.Count],root.transform,pizza.transform.TransformPoint(local),rotation,true);visitors++;
                }
            }
            var source=Object.FindAnyObjectByType<CityDriveableCar>();
            if(source!=null)
            {
                var used=new List<Vector3>();
                for(int i=0;i<road.nodes.Length&&cars<6;i++)
                {
                    if(road.nodes[i].next.Length==0)continue;Vector3 p=road.nodes[i].position;
                    bool close=false;foreach(Vector3 q in used)if(Vector3.Distance(p,q)<35)close=true;if(close)continue;
                    Vector3 direction=road.nodes[road.nodes[i].next[0]].position-p;
                    Quaternion rotation=Quaternion.LookRotation(direction.normalized);
                    if(Physics.OverlapBox(p+Vector3.up*0.9f,new Vector3(1,0.6f,2.4f),rotation,~0,QueryTriggerInteraction.Ignore).Length>0)continue;
                    GameObject car=Object.Instantiate(source.gameObject,p,rotation,root.transform);car.name="Traffic car "+(cars+1);
                    Strip(car);var body=car.AddComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
                    var collider=car.AddComponent<BoxCollider>();collider.center=new Vector3(0,0.7f,0);collider.size=new Vector3(1.9f,1.3f,4.3f);
                    var agent=car.AddComponent<CityLifeAgent>();agent.vehicle=true;agent.graph=road;agent.node=road.nodes[i].next[0];agent.previous=i;agent.speed=3+(cars%3)*0.4f;
                    used.Add(p);cars++;
                }
            }
            var conversations=new GameObject("City conversations");conversations.transform.SetParent(root.transform);conversations.AddComponent<CityAmbientConversations>();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Город","Добавлено: машин "+cars+", прохожих "+walkers+", посетителей "+visitors+".\nУзлов дороги "+road.nodes.Length+", тротуара "+walk.nodes.Length+". Если узлов нет, покрытие не распознано по материалам — ничего не пущено через дома.\nИспользовано моделей людей: "+people.Count+". При единственной модели персонажи будут похожи. Проверьте город в Play, затем сохраните Ctrl+S.","OK");
        }

        static CityLifeGraph Graph(Transform root,bool pedestrians)
        {
            float step=pedestrians?3:6;
            var cells=new Dictionary<Vector2Int,Vector3>();
            int scanned=0;
            foreach(MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if(filter.gameObject.scene!=root.gameObject.scene||filter.transform.IsChildOf(root)||filter.name.Contains("Pizza"))continue;
                Renderer renderer=filter.GetComponent<Renderer>();if(renderer==null||filter.sharedMesh==null)continue;
                Material[] materials=renderer.sharedMaterials;
                using(var data=Mesh.AcquireReadOnlyMeshData(filter.sharedMesh))
                {
                    var positions=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);data[0].GetVertices(positions);
                    for(int sub=0;sub<data[0].subMeshCount&&sub<materials.Length;sub++)
                    {
                        if(materials[sub]==null)continue;string name=materials[sub].name.ToLowerInvariant();
                        bool sidewalk=name.Contains("sidewalk")||name.Contains("pavement")||name.Contains("walkway");
                        bool asphalt=!sidewalk&&(name.Contains("asphalt")||name.Contains("road")||name.Contains("street"));
                        if(pedestrians?!sidewalk:!asphalt)continue;
                        var desc=data[0].GetSubMesh(sub);if(desc.topology!=MeshTopology.Triangles)continue;
                        var indices=new Unity.Collections.NativeArray<int>(desc.indexCount,Unity.Collections.Allocator.Temp);data[0].GetIndices(indices,sub);
                        for(int i=0;i<indices.Length;i+=3)
                        {
                            Vector3 a=filter.transform.TransformPoint(positions[indices[i]]),b=filter.transform.TransformPoint(positions[indices[i+1]]),c=filter.transform.TransformPoint(positions[indices[i+2]]);
                            if(Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)<0.98f)continue;
                            float minX=Mathf.Min(a.x,b.x,c.x),maxX=Mathf.Max(a.x,b.x,c.x),minZ=Mathf.Min(a.z,b.z,c.z),maxZ=Mathf.Max(a.z,b.z,c.z);
                            if(maxX-minX>1500||maxZ-minZ>1500)continue;
                            for(int x=Mathf.CeilToInt(minX/step);x<=Mathf.FloorToInt(maxX/step);x++)for(int z=Mathf.CeilToInt(minZ/step);z<=Mathf.FloorToInt(maxZ/step);z++)
                            {
                                if(++scanned>2000000)break;
                                Vector3 p=new Vector3(x*step,0,z*step);
                                if(!Height(p,a,b,c,out float y))continue;p.y=y+0.06f;
                                if(!Physics.Raycast(p+Vector3.up*0.5f,Vector3.down,out RaycastHit hit,1.2f,~0,QueryTriggerInteraction.Ignore)||Mathf.Abs(hit.point.y-y)>0.15f)continue;
                                Vector3 ext=pedestrians?new Vector3(0.3f,0.65f,0.3f):new Vector3(1,0.65f,1);
                                if(Physics.OverlapBox(p+Vector3.up*0.9f,ext,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Length>0)continue;
                                cells[new Vector2Int(x,z)]=p;
                            }
                        }
                        indices.Dispose();
                    }
                    positions.Dispose();
                }
            }
            var keys=new List<Vector2Int>(cells.Keys);keys.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
            var ids=new Dictionary<Vector2Int,int>();for(int i=0;i<keys.Count;i++)ids[keys[i]]=i;
            var graph=new GameObject(pedestrians?"Sidewalk graph":"Road graph").AddComponent<CityLifeGraph>();graph.transform.SetParent(root);
            graph.nodes=new CityLifeGraph.Node[keys.Count];
            for(int i=0;i<keys.Count;i++)
            {
                var next=new List<int>();
                foreach(Vector2Int offset in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                {
                    if(!ids.TryGetValue(keys[i]+offset,out int n)||Mathf.Abs(cells[keys[n]].y-cells[keys[i]].y)>0.18f)continue;
                    Vector3 start=cells[keys[i]]+Vector3.up*0.9f,end=cells[keys[n]]+Vector3.up*0.9f;
                    if(Physics.Linecast(start,end,~0,QueryTriggerInteraction.Ignore))continue;
                    next.Add(n);
                }
                graph.nodes[i]=new CityLifeGraph.Node{position=cells[keys[i]],next=next.ToArray()};
            }
            return graph;
        }
        static bool Height(Vector3 p,Vector3 a,Vector3 b,Vector3 c,out float y)
        {
            y=0;float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<0.00001f)return false;
            float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/d,v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/d,w=1-u-v;
            if(u< -0.0001f||v< -0.0001f||w< -0.0001f)return false;y=u*a.y+v*b.y+w*c.y;return true;
        }
        static void Strip(GameObject go)
        {
            foreach(MonoBehaviour b in go.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);
            foreach(Collider c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            foreach(Rigidbody b in go.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(b);
            foreach(Camera c in go.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(c);
            foreach(AudioListener c in go.GetComponentsInChildren<AudioListener>(true))Object.DestroyImmediate(c);
            foreach(AudioSource c in go.GetComponentsInChildren<AudioSource>(true))Object.DestroyImmediate(c);
        }
        static CityLifeAgent Human(GameObject prefab,Transform parent,Vector3 position,Quaternion rotation,bool seated)
        {
            GameObject go=Object.Instantiate(prefab,parent);go.name=seated?"Pizza visitor":"Pedestrian";Strip(go);
            go.transform.SetPositionAndRotation(position,rotation);
            Animator animator=go.GetComponentInChildren<Animator>();animator.enabled=false;
            Bounds bounds=new Bounds();bool first=true;foreach(Renderer r in go.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            if(bounds.size.y>0.01f)go.transform.localScale*=1.65f/bounds.size.y;
            Aim(animator,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,Vector3.down-go.transform.right*0.12f);
            Aim(animator,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,Vector3.down+go.transform.right*0.12f);
            if(seated)
            {
                Aim(animator,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,go.transform.forward);
                Aim(animator,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,go.transform.forward);
                Aim(animator,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,Vector3.down);
                Aim(animator,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,Vector3.down);
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);go.transform.position+=position+Vector3.up*0.12f-hips.position;
            }
            else
            {
                Renderer[] rs=go.GetComponentsInChildren<Renderer>();float bottom=float.PositiveInfinity;foreach(Renderer r in rs)bottom=Mathf.Min(bottom,r.bounds.min.y);
                go.transform.position+=Vector3.up*(position.y-bottom);
                var collider=go.AddComponent<CapsuleCollider>();collider.height=1.65f/go.transform.localScale.y;collider.radius=0.25f/go.transform.localScale.x;collider.center=Vector3.up*collider.height*0.5f;
                var body=go.AddComponent<Rigidbody>();body.isKinematic=true;
            }
            var agent=go.AddComponent<CityLifeAgent>();agent.seated=seated;return agent;
        }
        static void Aim(Animator animator,HumanBodyBones bone,HumanBodyBones child,Vector3 direction)
        {Transform a=animator.GetBoneTransform(bone),b=animator.GetBoneTransform(child);if(a!=null&&b!=null)a.rotation=Quaternion.FromToRotation(b.position-a.position,direction)*a.rotation;}
        static void Menu(Transform pizza)
        {
            Transform existing=pizza.Find("Real pizza menu");if(existing!=null)Undo.DestroyObjectImmediate(existing.gameObject);
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Pizza/Pizza_Menu.png");if(texture==null)return;
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="Real pizza menu";go.transform.SetParent(pizza,false);go.transform.localPosition=new Vector3(0,2.28f,4.76f);
            float width=2.65f;go.transform.localScale=new Vector3(width,width*texture.height/texture.width,1);Object.DestroyImmediate(go.GetComponent<Collider>());
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.mainTexture=texture;
            System.IO.Directory.CreateDirectory("Assets/Dimari/Generated/PizzaExpress");AssetDatabase.Refresh();
            string path="Assets/Dimari/Generated/PizzaExpress/RealMenu.mat";
            Material old=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(old!=null){old.mainTexture=texture;Object.DestroyImmediate(material);material=old;}else AssetDatabase.CreateAsset(material,path);
            go.GetComponent<Renderer>().sharedMaterial=material;
            // Clear placeholder signs that would overlap the real reference menu.
            foreach(Transform child in pizza.GetComponentsInChildren<Transform>())if(child.name.StartsWith("Sign МЕНЮ")||child.name.StartsWith("Sign ГОРЯЧАЯ ПИЦЦА"))child.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(go,"Add reference pizza menu");EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
        }
    }
}
