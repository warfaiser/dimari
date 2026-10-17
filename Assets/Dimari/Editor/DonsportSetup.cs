using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Dimari.City;
using Dimari.Story;
using Dimari.Vehicles;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class DonsportSetup
    {
        const string Folder="Assets/Dimari/Generated/Donsport";
        static Transform root;
        static Material ivory,red,bronze,glass,darkGlass,stone,asphalt,black,steel,white,orange,rubber,lightMat;
        static int meshIndex;
        struct Site {public Vector3 point;public Quaternion rotation;public float score;}
        static DonsportSetup()
        {EditorApplication.delayCall+=Auto;EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Auto;}
        static void Auto()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}
            Build(false);
        }
        [MenuItem("Dimari/Донспорт — построить в 150 метрах от пиццерии")]
        public static void Install(){Build(true);}
        static void Build(bool report)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")return;
            if(GameObject.Find("Dimari_Donsport")!=null){if(report)EditorUtility.DisplayDialog("Донспорт","Клуб уже существует. Дубликат не создан.","OK");return;}
            var pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza==null)return;
            Transform pizzaDoor=pizza.transform.Find("PizzaExpressEntrance");Vector3 origin=pizzaDoor!=null?pizzaDoor.position:pizza.transform.position;
            Physics.SyncTransforms();
            if(!FindSite(origin,out Site site))
            {
                string message="Не найдена свободная ровная площадка примерно в 150 м от пиццерии, рядом с распознанной дорогой. Требуется участок около 30×32 м. Ничего не удалено и не перестроено. Пришлите общий вид карты сверху/выделенное здание под замену; вручную двигать Transform не нужно. Команду можно повторить после открытия полного города.";
                Debug.LogWarning("[Dimari] Донспорт: "+message);if(report)EditorUtility.DisplayDialog("Площадка Донспорта",message,"OK");return;
            }
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();PrepareTextures();meshIndex=0;
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Build Donsport fitness club");
            var building=new GameObject("Dimari_Donsport");root=building.transform;root.SetPositionAndRotation(site.point,site.rotation);Undo.RegisterCreatedObjectUndo(building,"Donsport club and forecourt");
            try
            {
                Materials();Exterior();Lobby();Gym();Parking();int people=People();
                var portal=new GameObject("DonsportEntrance");portal.transform.SetParent(root,false);portal.transform.localPosition=new Vector3(-1.6f,0.06f,-9.5f);
                var entry=portal.AddComponent<DonsportEntrance>();entry.building=root;entry.door=root.Find("Donsport door hinge");
                foreach(var renderer in building.GetComponentsInChildren<Renderer>())renderer.allowOcclusionWhenDynamic=false;
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(building.scene);Undo.CollapseUndoOperations(undo);Physics.SyncTransforms();
                float distance=Vector3.Distance(origin,portal.transform.position);
                string message="Донспорт построен: "+distance.ToString("0")+" м от входа пиццерии по прямой. Добавлено людей: "+people+". Внутри ресепшен, зона ожидания и небольшой тренажёрный зал. Метка включится при задаче city_donsport после разговора с Просрочкой. Проверьте подъезд, дверь E и коллизии в Play; затем Ctrl+S вне Play. Второй этаж — внешний объём, без доступной лестницы.";
                Debug.Log("[Dimari] "+message,building);if(report){Selection.activeGameObject=building;SceneView.lastActiveSceneView?.FrameSelected();EditorUtility.DisplayDialog("Донспорт",message,"OK");}
            }
            catch {Undo.RevertAllDownToGroup(undo);throw;}
        }
        static bool Ground(Vector3 p,float baseY,out Vector3 point,out Collider support)
        {
            point=default;support=null;
            if(!Physics.Raycast(new Vector3(p.x,baseY+2,p.z),Vector3.down,out RaycastHit hit,4.5f,~0,QueryTriggerInteraction.Ignore)||hit.normal.y<0.97f||Mathf.Abs(hit.point.y-baseY)>1.2f)return false;
            point=hit.point;support=hit.collider;return true;
        }
        static void Candidate(List<Site> sites,Vector3 road,Vector3 away,Vector3 origin)
        {
            away.y=0;if(away.sqrMagnitude<0.1f)return;away.Normalize();
            foreach(float offset in new[]{25f,27f})
            {
                Quaternion q=Quaternion.LookRotation(away);Vector3 p=road+away*offset;
                Vector3 entry=p+q*new Vector3(-1.6f,0,-9.5f);Vector3 d=entry-origin;d.y=0;float distance=d.magnitude;
                if(distance<145||distance>155)continue;
                // Prefer an exact 150 m horizontal entrance distance by sliding at most
                // 3.5 m along the same street; all geometry/support tests still apply.
                Vector3 tangent=Vector3.Cross(Vector3.up,away);float projection=Vector3.Dot(d,tangent);
                float discriminant=projection*projection-(d.sqrMagnitude-150f*150f);
                if(discriminant>=0)
                {
                    float first=-projection+Mathf.Sqrt(discriminant),second=-projection-Mathf.Sqrt(discriminant);
                    float shift=Mathf.Abs(first)<Mathf.Abs(second)?first:second;
                    if(Mathf.Abs(shift)<=3.5f)sites.Add(new Site{point=p+tangent*shift,rotation=q,score=0});
                }
                sites.Add(new Site{point=p,rotation=q,score=Mathf.Abs(distance-150)+0.01f});
            }
        }
        static bool FindSite(Vector3 origin,out Site chosen)
        {
            chosen=default;var candidates=new List<Site>();
            foreach(var graph in Object.FindObjectsByType<CityLifeGraph>(FindObjectsSortMode.None))
            {
                if(graph.name!="Road graph"||graph.nodes==null||graph.gameObject.scene!=SceneManager.GetActiveScene())continue;
                for(int i=0;i<graph.nodes.Length;i++)
                {
                    var node=graph.nodes[i];if(node==null||node.next==null||node.next.Length==0)continue;
                    int n=node.next[0];if(n<0||n>=graph.nodes.Length||graph.nodes[n]==null)continue;
                    Vector3 tangent=graph.nodes[n].position-node.position;tangent.y=0;if(tangent.sqrMagnitude<0.1f)continue;
                    Vector3 away=Vector3.Cross(Vector3.up,tangent.normalized);
                    Candidate(candidates,node.position,away,origin);Candidate(candidates,node.position,-away,origin);
                }
            }
            // Fallback for a scene without CityLife graphs: require an explicitly named road support.
            if(candidates.Count==0)for(int i=0;i<96;i++)
            {
                float angle=i*Mathf.PI/48;Vector3 away=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                Vector3 road=origin+away*134;
                if(!Ground(road,origin.y,out Vector3 point,out Collider support))continue;
                string name=support.name.ToLowerInvariant();
                if(!name.Contains("road")&&!name.Contains("asphalt")&&!name.Contains("street"))continue;
                Candidate(candidates,point,away,origin);
            }
            candidates.Sort((a,b)=>a.score.CompareTo(b.score));var checkedSites=new HashSet<string>();
            foreach(var site in candidates)
            {
                string key=Mathf.RoundToInt(site.point.x)+":"+Mathf.RoundToInt(site.point.z)+":"+Mathf.RoundToInt(site.rotation.eulerAngles.y);
                if(!checkedSites.Add(key))continue;
                if(!Ground(site.point,origin.y,out Vector3 floor,out Collider support))continue;
                // Entire building + parking + approach must fit; no collider carving or guessed deletion.
                Vector3 center=floor+site.rotation*new Vector3(0,5,-6);
                if(Physics.OverlapBox(center,new Vector3(15.2f,4.7f,16.2f),site.rotation,~0,QueryTriggerInteraction.Ignore).Length>0)continue;
                if(Physics.OverlapBox(floor+site.rotation*new Vector3(-1.6f,1.2f,-23.5f),new Vector3(1.5f,0.95f,2.6f),site.rotation,~0,QueryTriggerInteraction.Ignore).Length>0)continue;
                bool flat=true;
                for(int x=-14;x<=14&&flat;x+=7)for(int z=-21;z<=9;z+=5)
                {
                    Vector3 p=floor+site.rotation*new Vector3(x,0,z);
                    if(!Ground(p,floor.y,out Vector3 g,out Collider surface)||Mathf.Abs(g.y-floor.y)>0.12f){flat=false;break;}
                }
                // Continuous supported pedestrian approach reaches the detected street, not an empty gap.
                for(int z=-25;z<=-9&&flat;z+=2)
                    if(!Ground(floor+site.rotation*new Vector3(-1.6f,0,z),floor.y,out Vector3 g,out Collider surface)||Mathf.Abs(g.y-floor.y)>0.16f)flat=false;
                foreach(Vector3 corner in new[]{new Vector3(-15,0,-22),new Vector3(15,0,-22),new Vector3(-15,0,10),new Vector3(15,0,10)})
                    if(!Ground(floor+site.rotation*corner,floor.y,out Vector3 edge,out Collider edgeSupport)||Mathf.Abs(edge.y-floor.y)>0.12f)flat=false;
                if(!flat)continue;
                chosen=new Site{point=floor+Vector3.up*0.06f,rotation=site.rotation,score=site.score};return true;
            }
            return false;
        }
        static void PrepareTextures()
        {
            foreach(string name in new[]{"Cladding","Cladding_Normal","LobbyStone","Asphalt"})
            {
                string path="Assets/Dimari/Art/Donsport/"+name+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.maxTextureSize=1024;
                if(name.EndsWith("_Normal")){importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;}
                importer.SaveAndReimport();
            }
        }
        static Material Mat(string name,Color color,string texture=null,float smooth=0.3f,float metallic=0)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);else material.color=color;
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smooth);if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(texture!=null)material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Donsport/"+texture+".png");
            EditorUtility.SetDirty(material);return material;
        }
        static void Materials()
        {
            ivory=Mat("Ivory facade ceramic",Color.white,"Cladding",0.32f);
            Texture normal=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Donsport/Cladding_Normal.png");
            if(normal!=null&&ivory.HasProperty("_BumpMap")){ivory.SetTexture("_BumpMap",normal);ivory.EnableKeyword("_NORMALMAP");ivory.SetFloat("_BumpScale",0.4f);}
            red=Mat("Donsport deep red",new Color(0.43f,0.022f,0.035f),null,0.4f);
            bronze=Mat("Bronze window frames",new Color(0.17f,0.08f,0.052f),null,0.62f,0.65f);
            darkGlass=Mat("Upper floor smoked glazing",new Color(0.065f,0.105f,0.12f),null,0.9f,0.65f);
            glass=Mat("Lobby transparent glazing",new Color(0.18f,0.23f,0.25f,0.2f),null,0.9f,0.05f);
            if(glass.HasProperty("_Surface"))
            {glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetOverrideTag("RenderType","Transparent");glass.renderQueue=3000;glass.SetFloat("_Cull",0);}
            else if(glass.HasProperty("_Mode"))
            {glass.SetFloat("_Mode",3);glass.SetInt("_SrcBlend",(int)BlendMode.One);glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);glass.SetInt("_ZWrite",0);glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");glass.renderQueue=3000;}
            stone=Mat("Lobby travertine",Color.white,"LobbyStone",0.48f);asphalt=Mat("Forecourt asphalt",Color.white,"Asphalt",0.12f);
            black=Mat("Charcoal",new Color(0.025f,0.03f,0.035f));steel=Mat("Brushed steel",new Color(0.4f,0.43f,0.44f),null,0.65f,0.75f);
            white=Mat("Warm white",new Color(0.91f,0.9f,0.85f),null,0.3f);orange=Mat("Lounge orange vinyl",new Color(0.56f,0.21f,0.07f),null,0.3f);
            rubber=Mat("Gym rubber",new Color(0.065f,0.07f,0.075f),null,0.12f);lightMat=Mat("Luminaire",Color.white);
            if(lightMat.HasProperty("_EmissionColor")){lightMat.EnableKeyword("_EMISSION");lightMat.SetColor("_EmissionColor",Color.white*1.8f);}
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,bool collider=true,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent!=null?parent:root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void SaveMesh(Mesh mesh)
        {mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Mesh_"+(meshIndex++)+".asset"));}
        static void TileBox(string name,Vector3 p,Vector3 size,Material mat)
        {
            var go=Box(name,p,size,mat);go.transform.localScale=Vector3.one;go.GetComponent<BoxCollider>().size=size;
            var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            foreach(Vector3 n in new[]{Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                Vector3 u=Vector3.Cross(n,Mathf.Abs(n.y)>0.5f?Vector3.forward:Vector3.up).normalized,w=Vector3.Cross(n,u);int start=v.Count;
                foreach(Vector2 corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(-1,1),new Vector2(1,1)})
                {Vector3 point=Vector3.Scale(n+u*corner.x+w*corner.y,size)*0.5f;v.Add(point);uv.Add(new Vector2(Vector3.Dot(point,u),Vector3.Dot(point,w))/4f);}
                triangles.AddRange(new[]{start,start+1,start+2,start+2,start+1,start+3});
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();SaveMesh(mesh);go.GetComponent<MeshFilter>().sharedMesh=mesh;
        }
        static GameObject SoftBox(string name,Vector3 p,Vector3 size,Material material,Transform parent)
        {
            var go=Box(name,p,size,material,true,parent);go.transform.localScale=Vector3.one;go.GetComponent<BoxCollider>().size=size;
            float radius=Mathf.Min(0.075f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*0.45f);Vector3 half=size*0.5f,core=half-Vector3.one*radius;
            var v=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int steps=5;
            foreach(Vector3 n in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                Vector3 u=Vector3.Cross(n,Mathf.Abs(n.y)>0.5f?Vector3.forward:Vector3.up).normalized,w=Vector3.Cross(n,u);int start=v.Count;
                for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {
                    Vector3 point=Vector3.Scale(n+u*(2f*x/steps-1)+w*(2f*y/steps-1),half);
                    Vector3 nearest=new Vector3(Mathf.Clamp(point.x,-core.x,core.x),Mathf.Clamp(point.y,-core.y,core.y),Mathf.Clamp(point.z,-core.z,core.z));Vector3 normal=(point-nearest).normalized;
                    v.Add(nearest+normal*radius);normals.Add(normal);uv.Add(new Vector2((float)x/steps,(float)y/steps));
                    if(x<steps&&y<steps){int a=start+y*(steps+1)+x;triangles.AddRange(new[]{a,a+1,a+steps+1,a+1,a+steps+2,a+steps+1});}
                }
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);SaveMesh(mesh);go.GetComponent<MeshFilter>().sharedMesh=mesh;return go;
        }
        static GameObject Cylinder(string name,Vector3 p,Vector3 scale,Material mat,bool collider=true,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent!=null?parent:root,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Text(string words,Vector3 p,float size,Color color)
        {
            var go=new GameObject("Sign "+words);go.transform.SetParent(root,false);go.transform.localPosition=p;var text=go.AddComponent<TextMesh>();text.text=words;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=96;text.fontStyle=FontStyle.Bold;text.characterSize=size/7f;text.color=color;
            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");if(font!=null){text.font=font;go.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
            float height=go.GetComponent<MeshRenderer>().bounds.size.y;if(height>0.0001f)go.transform.localScale=Vector3.one*(size/height);
        }
        static void Exterior()
        {
            TileBox("Foundation",new Vector3(0,-0.13f,0),new Vector3(28.4f,0.26f,18.4f),ivory);
            // Smaller floor renderers receive the nearest URP local lights instead of only four at the building centre.
            foreach(float x in new[]{-10.5f,-3.5f,3.5f,10.5f})foreach(float z in new[]{-6f,0f,6f})TileBox("Lobby stone floor",new Vector3(x,0.02f,z),new Vector3(7,0.04f,6),stone);
            TileBox("Left tiled wall",new Vector3(-14,4.2f,0),new Vector3(0.24f,8.4f,18),ivory);
            // Side glass volume follows the large right-hand glazing in the reference.
            TileBox("Right base wall",new Vector3(14,0.55f,0),new Vector3(0.24f,1.1f,18),ivory);
            Box("Right lobby glass",new Vector3(14,2.15f,-0.3f),new Vector3(0.05f,2.1f,16.8f),glass);
            TileBox("Right floor band",new Vector3(14,4.15f,0),new Vector3(0.24f,1.9f,18),ivory);
            Box("Right upper glass",new Vector3(14,6.05f,0),new Vector3(0.06f,2,16.8f),darkGlass);
            TileBox("Right parapet",new Vector3(14,7.8f,0),new Vector3(0.24f,1.5f,18),ivory);
            for(float z=-8.4f;z<9;z+=1.4f){Box("Side bronze mullion",new Vector3(14.04f,4.25f,z),new Vector3(0.08f,6.5f,0.055f),bronze);}
            TileBox("Back tiled wall",new Vector3(0,4.2f,9),new Vector3(28,8.4f,0.24f),ivory);
            TileBox("Upper floor slab",new Vector3(0,4.1f,0),new Vector3(28,0.2f,18),white);
            TileBox("Roof",new Vector3(0,8.45f,0),new Vector3(28.5f,0.2f,18.5f),ivory);
            Box("Red roof coping",new Vector3(0,8.6f,0),new Vector3(28.6f,0.1f,18.6f),red);
            foreach(Vector2 section in new[]{new Vector2(-8.25f,11.5f),new Vector2(6.55f,14.5f)})
            {
                TileBox("Front ground plinth",new Vector3(section.x,0.47f,-9),new Vector3(section.y,0.94f,0.24f),ivory);
                Box("Front lobby window",new Vector3(section.x,1.99f,-9.02f),new Vector3(section.y,2.1f,0.045f),glass);
            }
            TileBox("Front inter-floor band",new Vector3(0,3.65f,-9),new Vector3(28,1.25f,0.24f),ivory);
            TileBox("Upper facade lower band",new Vector3(0,4.55f,-9),new Vector3(28,0.55f,0.24f),ivory);
            foreach(Vector2 pier in new[]{new Vector2(-13.45f,1.1f),new Vector2(-8.9f,2.6f),new Vector2(13.45f,1.1f)})TileBox("Upper ceramic pier",new Vector3(pier.x,5.95f,-9),new Vector3(pier.y,2.3f,0.24f),ivory);
            Box("Small upper window",new Vector3(-11.5f,5.95f,-9.03f),new Vector3(2.8f,2.3f,0.055f),darkGlass);
            Box("Panoramic upper window",new Vector3(2.6f,5.95f,-9.03f),new Vector3(20.4f,2.3f,0.055f),darkGlass);
            TileBox("Signage ceramic fascia",new Vector3(0,7.75f,-9),new Vector3(28,1.4f,0.24f),ivory);
            for(float x=-13.8f;x<14;x+=1.15f)
            {
                if(x< -2.5f||x> -0.7f)Box("Ground bronze mullion",new Vector3(x,1.95f,-9.07f),new Vector3(0.06f,2.12f,0.1f),bronze);
                Box("Upper bronze mullion",new Vector3(x,5.95f,-9.08f),new Vector3(0.055f,2.34f,0.1f),bronze);
            }
            foreach(float y in new[]{1.16f,2.35f,4.82f,5.72f,7.1f})
            {
                if(y<3)foreach(Vector2 section in new[]{new Vector2(-8.25f,11.5f),new Vector2(6.55f,14.5f)})Box("Glazing transom",new Vector3(section.x,y,-9.08f),new Vector3(section.y,0.045f,0.1f),bronze);
                else Box("Upper transom",new Vector3(0,y,-9.08f),new Vector3(28,0.045f,0.1f),bronze);
            }
            Text("ФИТНЕС КЛУБ  ДОНСПОРТ",new Vector3(0,7.83f,-9.15f),0.62f,new Color(0.65f,0.025f,0.04f));
            foreach(float x in new[]{-11.8f,11.8f}){Box("Roof logo board",new Vector3(x,8.95f,-7.8f),new Vector3(1.5f,1.1f,0.08f),white,false);Text("ДОН\nСПОРТ",new Vector3(x,8.95f,-7.86f),0.7f,new Color(0.6f,0.02f,0.04f));}
            var hinge=new GameObject("Donsport door hinge");hinge.transform.SetParent(root,false);hinge.transform.localPosition=new Vector3(-2.5f,0,-9.02f);
            Box("Entrance glass door",new Vector3(0.9f,1.5f,0),new Vector3(1.76f,2.96f,0.05f),glass,true,hinge.transform);
            foreach(float x in new[]{0.025f,1.775f})Box("Door steel stile",new Vector3(x,1.5f,0),new Vector3(0.05f,3,0.09f),bronze,true,hinge.transform);
            Box("Door top rail",new Vector3(0.9f,2.98f,0),new Vector3(1.8f,0.05f,0.09f),bronze,true,hinge.transform);
            Box("Door pull handle",new Vector3(1.54f,1.3f,-0.12f),new Vector3(0.035f,0.55f,0.04f),steel,false,hinge.transform);
            Box("Entrance canopy",new Vector3(-1.6f,3.28f,-9.7f),new Vector3(4.5f,0.12f,1.7f),steel);
            Lamp(new Vector3(-1.6f,3.16f,-9.65f));
            Text("ВХОД · РЕСЕПШЕН",new Vector3(-1.6f,3.45f,-9.15f),0.15f,new Color(0.16f,0.17f,0.17f));
        }
        static void Arc(string name,Vector3 center,float inner,float outer,float bottom,float top,Material material,bool collision=false)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=center;
            const int steps=36;
            for(int i=0;i<steps;i++)
            {
                float a=(200+i*140f/steps)*Mathf.Deg2Rad,b=(200+(i+1)*140f/steps)*Mathf.Deg2Rad;
                Vector3 u=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),w=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                Vector3[] p={u*outer+Vector3.up*bottom,w*outer+Vector3.up*bottom,u*outer+Vector3.up*top,w*outer+Vector3.up*top,u*inner+Vector3.up*bottom,w*inner+Vector3.up*bottom,u*inner+Vector3.up*top,w*inner+Vector3.up*top};
                foreach(int[] face in new[]{new[]{0,1,2,3},new[]{5,4,7,6},new[]{2,3,6,7},new[]{4,5,0,1}})
                {int start=v.Count;for(int j=0;j<4;j++){v.Add(p[face[j]]);uv.Add(new Vector2(j%2,j/2));}triangles.AddRange(new[]{start,start+2,start+1,start+2,start+3,start+1});}
                if(collision)
                {var box=go.AddComponent<BoxCollider>();float angle=(a+b)*0.5f;box.center=new Vector3(Mathf.Cos(angle)*(inner+outer)/2,(bottom+top)/2,Mathf.Sin(angle)*(inner+outer)/2);box.size=new Vector3(0.36f,top-bottom,0.36f);}
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();SaveMesh(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        static void Lamp(Vector3 p,bool globe=false)
        {
            if(globe)
            {
                Cylinder("Globe lamp post",p+Vector3.up*0.8f,new Vector3(0.06f,0.8f,0.06f),bronze);
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="White globe";sphere.transform.SetParent(root,false);sphere.transform.localPosition=p+Vector3.up*1.68f;sphere.transform.localScale=Vector3.one*0.3f;sphere.GetComponent<Renderer>().sharedMaterial=lightMat;Object.DestroyImmediate(sphere.GetComponent<Collider>());
            }
            else Box("Recessed ceiling panel",p,new Vector3(1.15f,0.035f,0.55f),lightMat,false);
            var go=new GameObject("Local club light");go.transform.SetParent(root,false);go.transform.localPosition=p+(globe?Vector3.up*1.65f:Vector3.down*0.18f);var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=globe?6:7;light.intensity=globe?1.1f:1.15f;light.color=new Color(1,0.96f,0.88f);light.shadows=LightShadows.None;
        }
        static void Lobby()
        {
            Box("Reception central pillar",new Vector3(0,1.95f,-0.5f),new Vector3(1.5f,3.9f,1.2f),white);
            Arc("Curved white reception",new Vector3(0,0,-0.5f),2.65f,3,0.12f,1.08f,white,true);
            Arc("Curved red reception plinth",new Vector3(0,0,-0.5f),2.63f,3.02f,0.08f,0.24f,red);
            Arc("Red reception countertop",new Vector3(0,0,-0.5f),2.5f,3.16f,1.08f,1.17f,red);
            var glow=Mat("Reception lavender trim",new Color(0.58f,0.5f,0.8f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(0.28f,0.13f,0.65f)*2);
            Arc("Under-counter light ribbon",new Vector3(0,0,-0.5f),2.98f,3.025f,1.02f,1.07f,glow);
            Arc("Reception circular canopy",new Vector3(0,0,-0.5f),2.3f,3.5f,3.24f,3.6f,white);
            Text("ДОНСПОРТ · АТМОСФЕРА",new Vector3(0,3.43f,-3.96f),0.24f,new Color(0.1f,0.11f,0.11f));
            for(int i=0;i<2;i++)
            {
                float x=i==0?-1.1f:1.1f;Box("Reception monitor",new Vector3(x,1.45f,-2.94f),new Vector3(0.52f,0.32f,0.05f),black,false);Box("Monitor support",new Vector3(x,1.25f,-2.95f),new Vector3(0.04f,0.22f,0.06f),steel,false);
                Box("Keyboard",new Vector3(x,1.2f,-2.72f),new Vector3(0.42f,0.025f,0.14f),black,false);
            }
            Box("Leaflet holder",new Vector3(-0.3f,1.32f,-3.5f),new Vector3(0.3f,0.3f,0.04f),white,false);Text("ДОНСПОРТ\nАБОНЕМЕНТЫ",new Vector3(-0.3f,1.34f,-3.53f),0.045f,new Color(0.5f,0.02f,0.03f));
            for(int i=0;i<6;i++)
            {
                float x=-12.5f+i*1.4f;Box("Red locker door",new Vector3(x,1.12f,1.78f),new Vector3(1.32f,2.1f,0.22f),i%3==0?red:white);Box("Locker handle",new Vector3(x+0.45f,1.1f,1.62f),new Vector3(0.03f,0.25f,0.04f),steel,false);
            }
            Box("Towel rack backing",new Vector3(-3.6f,1.5f,1.5f),new Vector3(1.7f,2.9f,0.3f),red);
            for(int i=0;i<4;i++)
            {
                Box("Towel shelf",new Vector3(-3.6f,0.4f+i*0.6f,1.1f),new Vector3(1.75f,0.05f,0.8f),steel);
                for(int j=0;j<3;j++)Box("Folded towel",new Vector3(-4.1f+j*0.5f,0.5f+i*0.6f,1.1f),new Vector3(0.42f,0.14f,0.55f),j==1?red:white,false);
            }
            Box("Gym partition left",new Vector3(-3.9f,1.98f,2.05f),new Vector3(20.2f,3.88f,0.16f),white);
            Box("Gym partition right",new Vector3(11.1f,1.98f,2.05f),new Vector3(5.8f,3.88f,0.16f),white);
            Box("Gym doorway lintel",new Vector3(7.1f,3.65f,2.05f),new Vector3(2.2f,0.6f,0.16f),red);
            Text("ТРЕНАЖЁРНЫЙ ЗАЛ",new Vector3(7.1f,3.65f,1.95f),0.12f,Color.white);
            foreach(Vector2 p in new[]{new Vector2(8.8f,-5.8f),new Vector2(11.8f,-5.8f),new Vector2(8.8f,-2.8f),new Vector2(11.8f,-2.8f)})Chair(p.x,p.y,p.y< -4?0:180);
            Cylinder("Lounge round table",new Vector3(10.3f,0.67f,-4.3f),new Vector3(1.2f,0.035f,1.2f),white);Cylinder("Lounge table pedestal",new Vector3(10.3f,0.33f,-4.3f),new Vector3(0.08f,0.33f,0.08f),steel);
            Box("Dark lounge curtain",new Vector3(13.78f,2.1f,-3.9f),new Vector3(0.05f,3.5f,5.3f),black,false);
            for(int x=-10;x<=10;x+=5)for(int z=-6;z<=6;z+=6)Lamp(new Vector3(x,3.96f,z));
        }
        static void Chair(float x,float z,float yaw)
        {
            var holder=new GameObject("Orange lounge chair");holder.transform.SetParent(root,false);holder.transform.localPosition=new Vector3(x,0,z);holder.transform.localRotation=Quaternion.Euler(0,yaw,0);
            SoftBox("Padded lounge seat",new Vector3(0,0.48f,0),new Vector3(0.9f,0.18f,0.8f),orange,holder.transform);
            var back=SoftBox("Lounge back cushion",new Vector3(0,0.9f,-0.35f),new Vector3(0.95f,0.8f,0.18f),orange,holder.transform);back.transform.localRotation=Quaternion.Euler(-8,0,0);
            foreach(float side in new[]{-1f,1f}){SoftBox("Chair upholstered arm",new Vector3(side*0.48f,0.67f,0),new Vector3(0.13f,0.3f,0.8f),orange,holder.transform);foreach(float front in new[]{-1f,1f})Box("Chair steel leg",new Vector3(side*0.35f,0.22f,front*0.28f),new Vector3(0.035f,0.44f,0.035f),steel,true,holder.transform);}
        }
        static void Gym()
        {
            foreach(float x in new[]{-9.2f,0f,9.2f})Box("Gym rubber floor",new Vector3(x,0.052f,5.55f),new Vector3(9.2f,0.024f,6.75f),rubber);
            for(int i=0;i<3;i++)
            {
                float x=-10+i*4;Box("Treadmill chassis",new Vector3(x,0.21f,5.6f),new Vector3(1.35f,0.3f,2.6f),steel);
                Box("Treadmill belt",new Vector3(x,0.38f,5.6f),new Vector3(1,0.035f,2.3f),black);
                foreach(float side in new[]{-1f,1f}){Box("Treadmill upright",new Vector3(x+side*0.57f,0.94f,6.45f),new Vector3(0.055f,1.25f,0.07f),steel);Box("Treadmill handrail",new Vector3(x+side*0.57f,1.45f,5.95f),new Vector3(0.06f,0.06f,1.1f),black);}
                Box("Treadmill console",new Vector3(x,1.52f,6.48f),new Vector3(1.2f,0.23f,0.14f),black);Box("Treadmill display",new Vector3(x,1.54f,6.39f),new Vector3(0.32f,0.14f,0.01f),Mat("Console LCD",new Color(0.12f,0.42f,0.44f)),false);
            }
            for(int i=0;i<3;i++)
            {
                float x=3+i*4;Box("Training bench cushion",new Vector3(x,0.55f,6.1f),new Vector3(0.6f,0.16f,1.8f),red);
                foreach(float end in new[]{-1f,1f}){Box("Bench steel stand",new Vector3(x,0.28f,6.1f+end*0.6f),new Vector3(0.08f,0.55f,0.08f),steel);Box("Bench ground support",new Vector3(x,0.1f,6.1f+end*0.6f),new Vector3(0.95f,0.08f,0.3f),steel);}
            }
            Box("Dumbbell rack",new Vector3(6,0.7f,8.3f),new Vector3(9.5f,0.1f,0.65f),steel);
            foreach(float x in new[]{2f,3.2f,4.4f,5.6f,6.8f,8f,9.2f,10.4f})
            {
                var bar=Cylinder("Dumbbell handle",new Vector3(x,0.88f,8.25f),new Vector3(0.04f,0.2f,0.04f),steel,false);bar.transform.localRotation=Quaternion.Euler(0,0,90);
                foreach(float s in new[]{-1f,1f}){var weight=Cylinder("Dumbbell weight",new Vector3(x+s*0.18f,0.88f,8.25f),new Vector3(0.2f,0.07f,0.2f),black,false);weight.transform.localRotation=Quaternion.Euler(0,0,90);}
            }
            foreach(float x in new[]{2f,10f})Box("Dumbbell rack foot",new Vector3(x,0.35f,8.3f),new Vector3(0.08f,0.7f,0.6f),steel);
            Text("ДОНСПОРТ · ТРЕНИРУЙСЯ В СВОЁМ РИТМЕ",new Vector3(0,2.7f,8.84f),0.2f,new Color(0.55f,0.03f,0.04f));
        }
        static void Fence(float x1,float x2,float z)
        {
            if(x2<=x1)return;
            for(float x=x1;x<=x2;x+=1.2f)
            {
                Box("Fence post",new Vector3(x,0.65f,z),new Vector3(0.055f,1.3f,0.055f),black);
                if(x+1.2f>x2)continue;
                foreach(float y in new[]{0.25f,1.13f})Box("Fence rail",new Vector3(x+0.6f,y,z),new Vector3(1.2f,0.04f,0.04f),black);
                foreach(float offset in new[]{0.28f,0.6f,0.92f})Box("Fence square inset",new Vector3(x+offset,0.69f,z),new Vector3(0.025f,0.86f,0.025f),black);
            }
        }
        static void Parking()
        {
            TileBox("Forecourt asphalt",new Vector3(0,0.012f,-15.25f),new Vector3(29.5f,0.025f,12.5f),asphalt);
            TileBox("Entrance pavement",new Vector3(-1.6f,0.045f,-15.3f),new Vector3(2.8f,0.06f,12.6f),stone);
            // Supported pedestrian connection to the road; no raised platform across the carriageway.
            TileBox("Street approach",new Vector3(-1.6f,0,-23.3f),new Vector3(2.8f,0.04f,3.6f),stone);
            Fence(-14.7f,-3.2f,-21.25f);Fence(0.1f,5.9f,-21.25f);Fence(12.1f,14.7f,-21.25f);
            foreach(float x in new[]{-14.7f,14.7f})for(float z=-20.9f;z< -9.5f;z+=1.2f)
            {Box("Side fence post",new Vector3(x,0.65f,z),new Vector3(0.055f,1.3f,0.055f),black);Box("Side fence top",new Vector3(x,1.13f,z+0.55f),new Vector3(0.04f,0.04f,1.1f),black);}
            foreach(float x in new[]{-12.5f,-9.6f,-6.7f,2.2f,5.1f,8f,10.9f})
            {foreach(float side in new[]{-1f,1f})Box("Parking bay stripe",new Vector3(x+side*1.25f,0.031f,-15.9f),new Vector3(0.07f,0.008f,4.8f),white,false);Box("Parking end stripe",new Vector3(x,0.031f,-13.5f),new Vector3(2.5f,0.008f,0.07f),white,false);}
            foreach(float x in new[]{-13f,-6f,3f,13f})Lamp(new Vector3(x,0.025f,-20.4f),true);
            var source=Object.FindAnyObjectByType<CityDriveableCar>();
            if(source!=null)foreach(float x in new[]{-12.5f,-9.6f,5.1f})
            {
                var car=Object.Instantiate(source.gameObject,root);car.name="Donsport parked car";
                foreach(var script in car.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
                foreach(var c in car.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
                foreach(var b in car.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(b);
                foreach(var camera in car.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(camera);
                foreach(var audio in car.GetComponentsInChildren<AudioSource>(true))Object.DestroyImmediate(audio);
                foreach(var listener in car.GetComponentsInChildren<AudioListener>(true))Object.DestroyImmediate(listener);
                foreach(var animator in car.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                car.transform.SetPositionAndRotation(root.TransformPoint(new Vector3(x,0.08f,-15.9f)),root.rotation);
                bool first=true;Bounds bounds=new Bounds();foreach(var r in car.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                if(!first)car.transform.position+=Vector3.up*(root.position.y+0.027f-bounds.min.y);
                // A simple parked body collider, no copied driving / wheel physics.
                var localBounds=new Bounds();bool empty=true;
                foreach(var r in car.GetComponentsInChildren<Renderer>())
                {
                    Bounds b=r.localBounds;
                    for(int a=0;a<2;a++)for(int c=0;c<2;c++)for(int d=0;d<2;d++)
                    {Vector3 point=car.transform.InverseTransformPoint(r.transform.TransformPoint(new Vector3(a==0?b.min.x:b.max.x,c==0?b.min.y:b.max.y,d==0?b.min.z:b.max.z)));if(empty){localBounds=new Bounds(point,Vector3.zero);empty=false;}else localBounds.Encapsulate(point);}
                }
                if(!empty){var body=car.AddComponent<BoxCollider>();body.center=localBounds.center;body.size=localBounds.size;}
            }
        }
        static int People()
        {
            var pool=new List<GameObject>();if(!AssetDatabase.IsValidFolder("Assets/Dimari/NPCs"))return 0;
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dimari/NPCs"}))
            {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var a=prefab.GetComponentInChildren<Animator>();if(a!=null&&a.isHuman&&a.avatar!=null&&a.avatar.isValid)pool.Add(prefab);}
            if(pool.Count==0)return 0;
            var receptionist=CityLifeSetup.Human(pool[0],root,root.TransformPoint(new Vector3(1.2f,0.06f,-1.8f)),root.rotation*Quaternion.Euler(0,180,0),false);receptionist.name="Donsport receptionist";receptionist.graph=null;
            for(int i=0;i<2;i++){var guest=CityLifeSetup.Human(pool[(i+1)%pool.Count],root,root.TransformPoint(new Vector3(8.8f+i*3,0.48f,-5.8f)),root.rotation,true);guest.name="Donsport lounge visitor";}
            return 3;
        }
    }
}
