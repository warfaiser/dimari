using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.City;

namespace Dimari.Editor
{
    public static class PizzaInteriorPolish
    {
        const string Folder="Assets/Dimari/Generated/PizzaPolish";
        static Transform root;
        static Material oak,leather,steel,ceramic,crust,black;
        static int meshIndex;
        [MenuItem("Dimari/Пицца — улучшить интерьер, еду и вход")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")
            {EditorUtility.DisplayDialog("Пиццерия","Остановите Play и откройте Chapter2_Rogovo.","OK");return;}
            GameObject pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza==null)return;
            if(pizza.transform.Find("Dimari_PizzaPolish")!=null)
            {EditorUtility.DisplayDialog("Пиццерия","Улучшение уже применено. Повторная команда не создаёт дубликаты. Для отмены используйте Ctrl+Z сразу после первой установки.","OK");return;}
            if(Shader.Find("Universal Render Pipeline/Lit")==null)return;
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Polish pizza interior");
            var group=new GameObject("Dimari_PizzaPolish");root=group.transform;root.SetParent(pizza.transform,false);Undo.RegisterCreatedObjectUndo(group,"Pizza polish");meshIndex=0;
            oak=Mat("Satin oak",Color.white,"Pizza_OakTop",0.28f);
            leather=Mat("Red padded vinyl",new Color(0.38f,0.018f,0.027f),null,0.28f);
            steel=Mat("Brushed dark steel",new Color(0.13f,0.14f,0.15f),null,0.48f);steel.SetFloat("_Metallic",0.7f);
            ceramic=Mat("Ivory ceramic",new Color(0.93f,0.92f,0.87f),null,0.48f);
            crust=Mat("Baked crust",new Color(0.65f,0.33f,0.1f),null,0.2f);
            black=Mat("Rubber trim",new Color(0.02f,0.023f,0.025f),null,0.1f);
            Material[] pizzas={Mat("Pepperoni",Color.white,"Pizza_Pepperoni",0.32f),Mat("Margherita",Color.white,"Pizza_Margherita",0.3f),Mat("Mushroom",Color.white,"Pizza_Mushroom",0.3f)};
            // Hide only known generated placeholders, not arbitrary city geometry or player objects.
            foreach(Transform t in pizza.GetComponentsInChildren<Transform>())
            {
                if(t.IsChildOf(root))continue;
                string n=t.name;
                if(n=="Cafe tabletop"||n=="Table pedestal"||n=="Red booth base"||n=="Red seat cushion"||n=="Red booth back"||n=="Upholstery seam"||n=="Pizza in display"||n=="Pepperoni"||n=="Outdoor bar"||n=="Bar stool seat"||n=="Stool legs"||n=="Extractor hood"||n=="Oven door"||n=="Oven")
                {Undo.RecordObject(t.gameObject,"Hide placeholder");t.gameObject.SetActive(false);}
            }
            // Keep the kitchen hood out of the guest sightline; the broad old slab was the 'board'.
            Rounded("Compact pizza oven",new Vector3(-1.5f,1.45f,4.5f),new Vector3(1.8f,0.65f,0.65f),0.06f,steel);
            Rounded("Oven dark opening",new Vector3(-1.5f,1.4f,4.16f),new Vector3(1.4f,0.3f,0.03f),0.01f,black,false);
            Rounded("Compact kitchen hood",new Vector3(-1.5f,2.95f,4.45f),new Vector3(2.4f,0.18f,0.65f),0.045f,steel,false);
            foreach(Vector2 table in new[]{new Vector2(-3.9f,-2.5f),new Vector2(3.9f,-2.5f),new Vector2(3.9f,0.45f)})Booth(table.x,table.y);
            for(int i=0;i<6;i++)Food("Display pizza",new Vector3(-2.6f+i*1.35f,1.12f,2.6f),0.42f,pizzas[i%3]);
            Rounded("Exterior oak counter",new Vector3(1,1.05f,-5.7f),new Vector3(5,0.075f,0.48f),0.025f,oak);
            for(int i=0;i<4;i++)
            {
                float x=-0.8f+i*1.2f;Rounded("Outdoor stool",new Vector3(x,0.72f,-6.1f),new Vector3(0.43f,0.08f,0.43f),0.035f,oak);
                foreach(int side in new[]{-1,1})Rounded("Stool steel frame",new Vector3(x+side*0.15f,0.35f,-6.1f),new Vector3(0.035f,0.7f,0.3f),0.012f,steel);
            }
            Rounded("Entrance canopy",new Vector3(-4.1f,2.86f,-5.45f),new Vector3(2.25f,0.13f,0.95f),0.045f,steel);
            Rounded("Entrance rubber mat",new Vector3(-4.1f,0.051f,-5.85f),new Vector3(1.8f,0.012f,0.9f),0.005f,black,false);
            foreach(float x in new[]{-4.93f,-3.27f})Rounded("Brushed door jamb",new Vector3(x,1.38f,-5.09f),new Vector3(0.075f,2.76f,0.08f),0.02f,steel);
            var entryLamp=new GameObject("Warm entrance downlight");entryLamp.transform.SetParent(root,false);entryLamp.transform.localPosition=new Vector3(-4.1f,2.72f,-5.65f);entryLamp.transform.localRotation=Quaternion.Euler(90,0,0);
            var entryLight=entryLamp.AddComponent<Light>();entryLight.type=LightType.Spot;entryLight.spotAngle=100;entryLight.range=4;entryLight.intensity=2;entryLight.color=new Color(1,0.93f,0.8f);entryLight.shadows=LightShadows.None;
            // Warm local lighting does not overwrite city ambience or shared assets.
            foreach(Light old in pizza.GetComponentsInChildren<Light>())if(old.name=="Neutral interior lamp")
            {Undo.RecordObject(old,"Brighten interior");old.intensity=1.1f;old.range=4.8f;old.color=new Color(1,0.95f,0.86f);}
            foreach(Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var person=r.GetComponentInParent<CityLifeAgent>();
                if(person==null||person.vehicle||r.gameObject.scene!=pizza.scene)continue;
                // Preserve the palette texture. Facial detail cannot be invented for a low-poly mesh.
                var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++)
                {
                    if(slots[i]==null)continue;
                    Material m=new Material(slots[i]);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",Color.white);
                    if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",0.18f);
                    AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Person.mat"));slots[i]=m;
                }
                Undo.RecordObject(r,"Person material");r.sharedMaterials=slots;
            }
            int guests=0;
            foreach(CityLifeAgent guest in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))
            {
                if(!guest.seated||guest.gameObject.scene!=pizza.scene||guest.GetComponent<PizzaGuestEating>()!=null)continue;
                Vector3 local=pizza.transform.InverseTransformPoint(guest.transform.position);if(Mathf.Abs(local.x)>6||Mathf.Abs(local.z)>5)continue;
                Vector3 table=new Vector3(local.x<0?-3.9f:3.9f,0.82f,local.z> -0.8f?0.45f:-2.5f);
                Vector3 forward=pizza.transform.InverseTransformDirection(guest.transform.forward);
                Vector3 plate=table-forward*0.22f+Vector3.right*0.1f;
                Food("Guest pizza",plate,0.29f,pizzas[guests%3]);
                Vector3 cupPoint=plate+Vector3.right*0.3f;cupPoint.y=0.83f;
                var cup=Cup(cupPoint);
                var slice=Disc("Hand pizza slice",Vector3.zero,0.09f,pizzas[guests%3],3,0.12f);
                slice.gameObject.SetActive(false);
                var animation=Undo.AddComponent<PizzaGuestEating>(guest.gameObject);animation.platePosition=pizza.transform.TransformPoint(plate);
                animation.cup=cup;animation.slice=slice;guests++;
            }
            // Reuse the facade/door update without rebuilding the restaurant or population.
            PizzaNeonEntranceSetup.Apply();
            Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(pizza.scene);
            EditorUtility.DisplayDialog("Пиццерия обновлена","Заменены столы и диваны, добавлены три вида пиццы и посуда. Посетителей с едой/питьём: "+guests+". Старая вытяжка-доска скрыта.\nПроверьте в Play руки, чашки, вход и освещение, затем Ctrl+S. Мыши и тараканы требуют импорта реальных моделей — в этом архиве их нет.","OK");
        }
        static void Booth(float x,float z)
        {
            Rounded("Rounded oak table",new Vector3(x,0.77f,z),new Vector3(1.5f,0.07f,0.8f),0.03f,oak);
            Rounded("Table steel column",new Vector3(x,0.37f,z),new Vector3(0.065f,0.74f,0.065f),0.018f,steel);
            Rounded("Weighted table foot",new Vector3(x,0.045f,z),new Vector3(0.7f,0.07f,0.5f),0.035f,steel);
            foreach(int s in new[]{-1,1})
            {
                Rounded("Booth plinth",new Vector3(x,0.22f,z+s*0.8f),new Vector3(1.68f,0.4f,0.58f),0.04f,black);
                Rounded("Padded red seat",new Vector3(x,0.5f,z+s*0.8f),new Vector3(1.68f,0.16f,0.62f),0.065f,leather);
                for(int i=0;i<3;i++)Rounded("Upholstered back panel",new Vector3(x+(i-1)*0.55f,0.9f,z+s*1.08f),new Vector3(0.54f,0.76f,0.18f),0.075f,leather);
            }
            Rounded("Napkin holder",new Vector3(x+0.48f,0.845f,z),new Vector3(0.14f,0.12f,0.1f),0.015f,steel,false);
        }
        static Transform Cup(Vector3 position)
        {
            var go=new GameObject("Guest cup");go.transform.SetParent(root,false);go.transform.localPosition=position;
            Transform parent=root;root=go.transform;
            // Hollow-looking rim and dark tea surface rather than a solid cylinder top.
            var cup=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cup.name="Ceramic cup";cup.transform.SetParent(root,false);cup.transform.localPosition=Vector3.up*0.045f;cup.transform.localScale=new Vector3(0.075f,0.045f,0.075f);cup.GetComponent<Renderer>().sharedMaterial=ceramic;Object.DestroyImmediate(cup.GetComponent<Collider>());
            Disc("Tea",new Vector3(0,0.091f,0),0.064f,Mat("Tea",new Color(0.15f,0.065f,0.02f),null,0.5f));
            Rounded("Cup handle",new Vector3(0.048f,0.05f,0),new Vector3(0.025f,0.048f,0.022f),0.008f,ceramic,false);
            root=parent;return go.transform;
        }
        static void Food(string name,Vector3 p,float diameter,Material topping)
        {
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cylinder);plate.name="Ceramic plate";plate.transform.SetParent(root,false);plate.transform.localPosition=p-Vector3.up*0.009f;plate.transform.localScale=new Vector3(diameter+0.05f,0.006f,diameter+0.05f);plate.GetComponent<Renderer>().sharedMaterial=ceramic;Object.DestroyImmediate(plate.GetComponent<Collider>());
            var baseGo=GameObject.CreatePrimitive(PrimitiveType.Cylinder);baseGo.name="Raised baked crust";baseGo.transform.SetParent(root,false);baseGo.transform.localPosition=p;baseGo.transform.localScale=new Vector3(diameter,0.012f,diameter);baseGo.GetComponent<Renderer>().sharedMaterial=crust;Object.DestroyImmediate(baseGo.GetComponent<Collider>());
            Disc(name,p+Vector3.up*0.013f,diameter,topping);
        }
        static Transform Disc(string name,Vector3 position,float diameter,Material mat,int segments=64,float start=0)
        {
            var vertices=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{new Vector2(0.5f,0.5f)};var indices=new List<int>();
            float arc=segments==3?Mathf.PI/3:Mathf.PI*2;
            for(int i=0;i<=segments;i++){float a=start+(float)i/segments*arc;Vector3 v=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));vertices.Add(v*diameter*0.5f);uv.Add(new Vector2(0.5f+v.x*0.46f,0.5f+v.z*0.46f));if(i>0){indices.Add(0);indices.Add(i+1);indices.Add(i);}}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return MeshObject(name,position,mesh,mat,false).transform;
        }
        static GameObject Rounded(string name,Vector3 position,Vector3 size,float radius,Material material,bool collider=true)
        {
            radius=Mathf.Min(radius,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*0.48f);Vector3 half=size*0.5f,core=half-Vector3.one*radius;
            var v=new List<Vector3>();var uv=new List<Vector2>();var normals=new List<Vector3>();var triangles=new List<int>();
            foreach(Vector3 n in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                Vector3 u=Vector3.Cross(n,Mathf.Abs(n.y)>0.5f?Vector3.forward:Vector3.up).normalized,w=Vector3.Cross(n,u);int offset=v.Count;const int steps=8;
                for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {
                    Vector3 p=Vector3.Scale(n+u*(2f*x/steps-1)+w*(2f*y/steps-1),half);
                    Vector3 closest=new Vector3(Mathf.Clamp(p.x,-core.x,core.x),Mathf.Clamp(p.y,-core.y,core.y),Mathf.Clamp(p.z,-core.z,core.z));Vector3 normal=(p-closest).normalized;
                    v.Add(closest+normal*radius);normals.Add(normal);uv.Add(new Vector2((float)x/steps,(float)y/steps));
                    if(x<steps&&y<steps){int a=offset+y*(steps+1)+x;triangles.AddRange(new[]{a,a+1,a+steps+1,a+1,a+steps+2,a+steps+1});}
                }
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return MeshObject(name,position,mesh,material,collider);
        }
        static GameObject MeshObject(string name,Vector3 position,Mesh mesh,Material mat,bool collide)
        {
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Mesh_"+(meshIndex++)+".asset"));
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            if(collide){var box=go.AddComponent<BoxCollider>();box.center=mesh.bounds.center;box.size=mesh.bounds.size;}return go;
        }
        static Material Mat(string name,Color color,string texture,float smooth)
        {
            string path=Folder+"/"+name+".mat";Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smooth);mat.SetFloat("_Metallic",0);
            if(texture!=null)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Pizza/"+texture+".png"));EditorUtility.SetDirty(mat);return mat;
        }
    }
}
