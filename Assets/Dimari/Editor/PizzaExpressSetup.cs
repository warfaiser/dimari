using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Story;

namespace Dimari.Editor
{
    public static class PizzaExpressSetup
    {
        const string Generated = "Assets/Dimari/Generated/PizzaExpress";
        static Transform root;
        static Material white, red, plaster, metal, wood, upholstery, floor, black, glass, green;

        [MenuItem("Dimari/Пицца Экспресс 24 — построить в городе")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().name != "Chapter2_Rogovo")
            { EditorUtility.DisplayDialog("Пиццерия", "Откройте сцену Chapter2_Rogovo вне Play Mode.", "OK"); return; }
            AdministrationEntrance entrance = null;
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Dimari_PizzaExpress24") { EditorUtility.DisplayDialog("Пиццерия", "Здание уже установлено. Повторная установка остановлена.", "OK"); return; }
                if (entrance == null) entrance = go.GetComponentInChildren<AdministrationEntrance>(true);
            }
            if (entrance == null) { EditorUtility.DisplayDialog("Пиццерия", "Не найден вход в администрацию.", "OK"); return; }
            Physics.SyncTransforms();
            if (!FindSite(entrance.transform.position, out Vector3 site, out Quaternion rotation))
            { EditorUtility.DisplayDialog("Пиццерия", "В радиусе 250–300 м не найдена достаточно ровная свободная площадка 15×13 м с коллайдером земли. Ничего не изменено. Пришлите вид карты сверху — выберем площадку без удаления города.", "OK"); return; }
            if (Shader.Find("Universal Render Pipeline/Lit") == null) return;
            Directory.CreateDirectory(Generated); AssetDatabase.Refresh();
            white = Mat("White ceramic", Color.white, null, 0.32f);
            red = Mat("Red ceramic", new Color(0.7f,0.025f,0.035f), null, 0.35f);
            plaster = Mat("Warm plaster", Color.white, "Plaster", 0.1f);
            metal = Mat("Brushed steel", Color.white, "Metal", 0.6f, 0.75f);
            wood = Mat("Table wood", Color.white, "Wood", 0.25f);
            upholstery = Mat("Red upholstery", Color.white, "Upholstery", 0.3f);
            floor = Mat("Floor tiles", Color.white, "Tiles", 0.2f);
            floor.SetTextureScale("_BaseMap", new Vector2(6,5));
            black = Mat("Dark trim", new Color(0.035f,0.04f,0.04f), null, 0.28f);
            green = Mat("Sign green", new Color(0.025f,0.4f,0.07f), null, 0.25f);
            glass = Mat("Glass", new Color(0.7f,0.85f,0.9f,0.16f), null, 0.8f);
            glass.SetFloat("_Surface",1); glass.SetFloat("_SrcBlend",5); glass.SetFloat("_DstBlend",10);
            glass.SetFloat("_ZWrite",0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue=3000;
            GameObject building = new GameObject("Dimari_PizzaExpress24"); root=building.transform;
            root.SetPositionAndRotation(site,rotation); Undo.RegisterCreatedObjectUndo(building,"Build Pizza Express");
            Box("Foundation",new Vector3(0,-0.08f,0),new Vector3(12.4f,0.16f,10.4f),plaster);
            Box("Tiled floor",new Vector3(0,0.015f,0),new Vector3(12,0.03f,10),floor);
            Box("Back wall",new Vector3(0,1.7f,5),new Vector3(12,3.4f,0.18f),plaster);
            Box("Left wall",new Vector3(-6,1.7f,0),new Vector3(0.18f,3.4f,10),plaster);
            Box("Right wall",new Vector3(6,1.7f,0),new Vector3(0.18f,3.4f,10),plaster);
            // Full physical roof and ceiling grid, with no cutaway in the playable building.
            Box("Roof",new Vector3(0,3.48f,0),new Vector3(12.4f,0.16f,10.4f),metal);
            Box("Ceiling",new Vector3(0,3.36f,0),new Vector3(12,0.06f,10),plaster);
            for(int x=-5;x<=5;x++) Box("Ceiling grid",new Vector3(x,3.31f,0),new Vector3(0.018f,0.018f,10),metal,false);
            for(int z=-4;z<=4;z++) Box("Ceiling grid",new Vector3(0,3.31f,z),new Vector3(12,0.018f,0.018f),metal,false);
            // Front door is a real opening. Remaining facade is checkerboard below the glazing.
            Box("Front left pier",new Vector3(-5.45f,1.5f,-5),new Vector3(1.1f,3,0.16f),plaster);
            Box("Front right pier",new Vector3(5.65f,1.5f,-5),new Vector3(0.7f,3,0.16f),plaster);
            Box("Front lintel",new Vector3(0,2.85f,-5),new Vector3(12,0.3f,0.18f),white);
            Box("Window sill wall",new Vector3(1,0.48f,-5),new Vector3(8.6f,0.96f,0.16f),white);
            Box("Shop window",new Vector3(1,1.8f,-5),new Vector3(8.6f,1.65f,0.025f),glass);
            for(int x=-3;x<=5;x+=2) Box("Window mullion",new Vector3(x,1.8f,-5.04f),new Vector3(0.04f,1.7f,0.06f),metal);
            for(int x=0;x<24;x++) for(int y=0;y<2;y++)
            {
                float px=-5.75f+x*0.5f;
                if(px>-4.9f && px<-3.3f) continue;
                Box("Facade tile",new Vector3(px,0.25f+y*0.5f,-5.10f),new Vector3(0.488f,0.488f,0.025f),(x+y)%2==0?red:white,false);
            }
            for(int z=0;z<20;z++) for(int y=0;y<2;y++)
                Box("Interior checker tile",new Vector3(5.9f,0.75f+y*0.5f,-4.75f+z*0.5f),new Vector3(0.025f,0.488f,0.488f),(z+y)%2==0?red:white,false);
            var hinge=new GameObject("Door hinge");hinge.transform.SetParent(root,false);hinge.transform.localPosition=new Vector3(-4.9f,0,-5);
            Transform old=root;root=hinge.transform;
            Box("Door glass",new Vector3(0.8f,1.32f,0),new Vector3(1.5f,2.6f,0.04f),glass);
            Box("Door lower panel",new Vector3(0.8f,0.4f,-0.04f),new Vector3(1.5f,0.8f,0.06f),red);
            Box("Door left frame",new Vector3(0.025f,1.35f,0),new Vector3(0.05f,2.7f,0.08f),metal);
            Box("Door right frame",new Vector3(1.575f,1.35f,0),new Vector3(0.05f,2.7f,0.08f),metal);
            Box("Door top frame",new Vector3(0.8f,2.68f,0),new Vector3(1.6f,0.05f,0.08f),metal);
            Box("Door handle",new Vector3(1.4f,1.2f,-0.1f),new Vector3(0.04f,0.35f,0.06f),metal);root=old;
            Box("White sign fascia",new Vector3(0,3.15f,-5.18f),new Vector3(12.3f,0.8f,0.16f),white);
            Text("ПИЦЦА",new Vector3(-4,3.15f,-5.28f),0.62f,new Color(0.75f,0.02f,0.03f));
            Text("ЭКСПРЕСС",new Vector3(0.7f,3.15f,-5.28f),0.58f,new Color(0.02f,0.4f,0.06f));
            Box("24 badge",new Vector3(5.2f,3.15f,-5.3f),new Vector3(1,0.7f,0.08f),red,false);
            Text("24\nЧАСА",new Vector3(5.2f,3.15f,-5.36f),0.25f,Color.white);
            // Counter spans the back, leaving staff access at the left end.
            Box("Counter base",new Vector3(0.8f,0.5f,2.6f),new Vector3(8.4f,1,1.1f),white);
            for(int x=0;x<17;x++) for(int y=0;y<2;y++) Box("Counter tile",new Vector3(-3.2f+x*0.5f,0.25f+y*0.5f,2.03f),new Vector3(0.488f,0.488f,0.025f),(x+y)%2==0?red:white,false);
            Box("Serving worktop",new Vector3(0.8f,1.04f,2.6f),new Vector3(8.6f,0.08f,1.3f),metal);
            Box("Display glass",new Vector3(0.8f,1.4f,2.05f),new Vector3(8.4f,0.65f,0.02f),glass,false);
            Box("Display canopy",new Vector3(0.8f,1.75f,2.45f),new Vector3(8.6f,0.06f,1),metal,false);
            for(int x=-3;x<=5;x+=2) Box("Display post",new Vector3(x,1.4f,2.05f),new Vector3(0.025f,0.7f,0.025f),metal,false);
            for(int i=0;i<6;i++) Pizza(new Vector3(-2.6f+i*1.35f,1.12f,2.6f));
            Box("Kitchen steel table",new Vector3(0.5f,0.95f,4.35f),new Vector3(7,0.12f,0.85f),metal);
            Box("Oven",new Vector3(-1.5f,1.4f,4.35f),new Vector3(2.2f,0.8f,0.8f),metal);
            Box("Oven door",new Vector3(-1.5f,1.35f,3.93f),new Vector3(1.9f,0.4f,0.03f),black,false);
            Box("Extractor hood",new Vector3(-1.5f,2.5f,4.3f),new Vector3(2.8f,0.4f,1),metal,false);
            Box("Red refrigerator",new Vector3(4.8f,1.25f,4.2f),new Vector3(1.2f,2.5f,1),red);
            Box("Fridge handle",new Vector3(4.35f,1.3f,3.65f),new Vector3(0.04f,0.7f,0.04f),metal,false);
            for(int i=0;i<4;i++)
            {
                float x=-3.1f+i*2.2f;
                Box("Menu screen",new Vector3(x,2.75f,4.85f),new Vector3(2,0.75f,0.06f),black,false);
                Text(i==0?"ПИЦЦА\nМАРГАРИТА  •  ПЕППЕРОНИ":i==1?"ГОРЯЧАЯ ПИЦЦА\n30 см  •  40 см":i==2?"МЕНЮ\nНАПИТКИ  •  ЗАКУСКИ":"ПИЦЦА ЭКСПРЕСС\nОТКРЫТО 24 ЧАСА",new Vector3(x,2.75f,4.8f),0.1f,Color.white);
            }
            Booth(-3.9f,-2.5f); Booth(3.9f,-2.5f); Booth(3.9f,0.45f);
            // Outdoor bar and bin as in the exterior reference.
            Box("Outdoor bar",new Vector3(1,1.05f,-5.7f),new Vector3(5,0.08f,0.48f),wood);
            for(int i=0;i<4;i++)
            {
                float x=-0.8f+i*1.2f;
                Box("Bar stool seat",new Vector3(x,0.72f,-6.1f),new Vector3(0.4f,0.07f,0.4f),wood);
                for(int side=-1;side<=1;side+=2) Box("Stool legs",new Vector3(x+side*0.14f,0.34f,-6.1f),new Vector3(0.025f,0.68f,0.3f),black);
            }
            Box("Bin",new Vector3(5.6f,0.45f,-5.7f),new Vector3(0.4f,0.9f,0.4f),metal);
            for(int x=-3;x<=3;x+=3) for(int z=-3;z<=3;z+=3)
            {
                Box("Ceiling LED panel",new Vector3(x,3.29f,z),new Vector3(1.15f,0.025f,0.6f),white,false);
                var go=new GameObject("Neutral interior lamp");go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(x,2.95f,z);
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=4.2f;light.intensity=0.65f;light.color=new Color(1,0.97f,0.91f);light.shadows=LightShadows.None;
            }
            var portal=new GameObject("PizzaExpressEntrance");portal.transform.SetParent(root,false);portal.transform.localPosition=new Vector3(-4.1f,0.1f,-5.25f);
            var entry=portal.AddComponent<PizzaExpressEntrance>();entry.door=hinge.transform;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Selection.activeGameObject=building;SceneView.lastActiveSceneView?.FrameSelected();
            EditorUtility.DisplayDialog("Пиццерия построена", "Расстояние от администрации: "+Vector3.Distance(site,entrance.transform.position).ToString("0")+" м по прямой.\nПроверьте проход к зданию, свет и дверь E в Play. Сохраните город Ctrl+S. Пешеходный маршрут не рассчитан автоматически; сцену программа не сохраняет.", "OK");
        }

        static bool FindSite(Vector3 origin,out Vector3 point,out Quaternion rotation)
        {
            point=default;rotation=Quaternion.identity;
            foreach(float radius in new[]{275f,260f,290f}) for(int i=0;i<48;i++)
            {
                float angle=i*Mathf.PI/24;
                Vector3 p=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!Ground(p,origin.y,out Vector3 ground))continue;
                Vector3 toward=Vector3.ProjectOnPlane(origin-ground,Vector3.up).normalized;
                Quaternion q=Quaternion.LookRotation(-toward,Vector3.up);
                bool flat=true;
                foreach(Vector3 corner in new[]{new Vector3(-7,0,-7),new Vector3(7,0,-7),new Vector3(-7,0,6),new Vector3(7,0,6)})
                    if(!Ground(ground+q*corner,ground.y,out Vector3 g)||Mathf.Abs(g.y-ground.y)>0.16f)flat=false;
                if(!flat)continue;
                if(Physics.OverlapBox(ground+Vector3.up*2,new Vector3(7,1.8f,6.7f),q,~0,QueryTriggerInteraction.Ignore).Length>0)continue;
                point=ground+Vector3.up*0.03f;rotation=q;return true;
            }
            return false;
        }
        static bool Ground(Vector3 p,float height,out Vector3 ground)
        {
            ground=default;
            if(!Physics.Raycast(new Vector3(p.x,height+8,p.z),Vector3.down,out RaycastHit hit,16,~0,QueryTriggerInteraction.Ignore))return false;
            if(hit.normal.y<0.97f||Mathf.Abs(hit.point.y-height)>5)return false;
            ground=hit.point;return true;
        }
        static Material Mat(string name,Color color,string texture,float smooth,float metallic=0)
        {
            string path=Generated+"/"+name+".mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);
            if(texture!=null)m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Pizza/Pizza_"+texture+".png"));return m;
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,bool collide=true)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collide)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Text(string text,Vector3 p,float height,Color color)
        {
            var go=new GameObject("Sign "+text.Split('\n')[0]);go.transform.SetParent(root,false);go.transform.localPosition=p;
            var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.fontSize=72;mesh.characterSize=height/7f;mesh.color=color;
            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");if(font!=null){mesh.font=font;go.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
            // Fit world-space letter height rather than relying on font-specific character metrics.
            float actualHeight=go.GetComponent<MeshRenderer>().bounds.size.y;
            if(actualHeight>0.0001f)go.transform.localScale=Vector3.one*(height/actualHeight);
        }
        static void Booth(float x,float z)
        {
            Box("Cafe tabletop",new Vector3(x,0.77f,z),new Vector3(1.5f,0.07f,0.8f),wood);
            Box("Table pedestal",new Vector3(x,0.37f,z),new Vector3(0.08f,0.74f,0.35f),black);
            for(int s=-1;s<=1;s+=2)
            {
                Box("Red booth base",new Vector3(x,0.25f,z+s*0.8f),new Vector3(1.7f,0.5f,0.62f),upholstery);
                Box("Red seat cushion",new Vector3(x,0.52f,z+s*0.8f),new Vector3(1.65f,0.12f,0.6f),upholstery);
                Box("Red booth back",new Vector3(x,0.83f,z+s*1.08f),new Vector3(1.7f,0.7f,0.14f),upholstery);
                for(int i=-1;i<=1;i++)Box("Upholstery seam",new Vector3(x+i*0.5f,0.83f,z+s*0.997f),new Vector3(0.012f,0.55f,0.012f),red,false);
            }
        }
        static void Pizza(Vector3 p)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Pizza in display";go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=new Vector3(0.42f,0.014f,0.42f);go.GetComponent<Renderer>().sharedMaterial=wood;Object.DestroyImmediate(go.GetComponent<Collider>());
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Box("Pepperoni",p+new Vector3(Mathf.Cos(a)*0.12f,0.02f,Mathf.Sin(a)*0.12f),new Vector3(0.06f,0.008f,0.06f),red,false);}
        }
    }
}
