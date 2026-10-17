using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Dimari.City;
using Dimari.Player;
using Dimari.Story;
using Dimari.Vehicles;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class CityStreetLifeRepair
    {
        const string Stamp="Street population repair v3";
        static CityStreetLifeRepair()
        {EditorApplication.delayCall+=Auto;EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Auto;}
        static void Auto()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}
            Apply(false);
        }
        [MenuItem("Dimari/Город — исправить прохожих, трафик и разговоры")]
        public static void Install(){Apply(true);}
        [MenuItem("Dimari/Город — открыть главу 2 и применить исправление")]
        public static void OpenAndRepair()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorUtility.DisplayDialog("Город","Сначала остановите Play.","OK");return;}
            if(SceneManager.GetActiveScene().name!="Chapter2_Rogovo")
            {
                string path=null;foreach(string guid in AssetDatabase.FindAssets("Chapter2_Rogovo t:Scene"))
                {string candidate=AssetDatabase.GUIDToAssetPath(guid);if(System.IO.Path.GetFileNameWithoutExtension(candidate)=="Chapter2_Rogovo"){path=candidate;break;}}
                if(path==null){EditorUtility.DisplayDialog("Город","Не найдена сцена Chapter2_Rogovo в Assets.","OK");return;}
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            }
            Apply(true);
        }
        static List<int> Connected(CityLifeGraph graph,int minimum)
        {
            var result=new List<int>();if(graph==null||graph.nodes==null)return result;
            var seen=new bool[graph.nodes.Length];
            for(int i=0;i<seen.Length;i++)
            {
                if(seen[i]||graph.nodes[i]==null)continue;
                var queue=new Queue<int>();var component=new List<int>();queue.Enqueue(i);seen[i]=true;
                while(queue.Count>0)
                {
                    int n=queue.Dequeue();component.Add(n);var links=graph.nodes[n].next;if(links==null)continue;
                    foreach(int next in links)if(next>=0&&next<seen.Length&&!seen[next]&&graph.nodes[next]!=null){seen[next]=true;queue.Enqueue(next);}
                }
                if(component.Count<minimum)continue;
                foreach(int n in component)if(graph.nodes[n].next!=null&&graph.nodes[n].next.Length>=(minimum>=12?2:1))result.Add(n);
            }
            return result;
        }
        static void Apply(bool report)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")return;
            GameObject city=GameObject.Find("Dimari_CityLife");
            if(city!=null&&city.transform.Find(Stamp)!=null&&!report)return;
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Repair street life without changing cafe guests");
            var disabled=new Dictionary<Collider,bool>();
            try
            {
                if(city==null){city=new GameObject("Dimari_CityLife");Undo.RegisterCreatedObjectUndo(city,"Street population root");}
                var oldGraphs=new List<CityLifeGraph>(city.GetComponentsInChildren<CityLifeGraph>());
                foreach(var agent in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))if(agent.gameObject.scene==city.scene)
                    foreach(var collider in agent.GetComponentsInChildren<Collider>()){disabled[collider]=collider.enabled;collider.enabled=false;}
                Physics.SyncTransforms();
                CityLifeGraph road=CityLifeSetup.Graph(city.transform,false);Undo.RegisterCreatedObjectUndo(road.gameObject,"Rebuild road routes");string roadsReport=CityLifeSetup.LastGraphReport;
                CityLifeGraph walk=CityLifeSetup.Graph(city.transform,true);Undo.RegisterCreatedObjectUndo(walk.gameObject,"Rebuild pavement routes");string walksReport=CityLifeSetup.LastGraphReport;
                var roadNodes=Connected(road,12);var walkNodes=Connected(walk,4);
                // Keep previous graphs when new surface recognition fails.
                if(roadNodes.Count==0){Undo.DestroyObjectImmediate(road.gameObject);road=null;foreach(var old in oldGraphs)if(old.name=="Road graph"){road=old;roadNodes=Connected(road,12);break;}}
                if(walkNodes.Count==0){Undo.DestroyObjectImmediate(walk.gameObject);walk=null;foreach(var old in oldGraphs)if(old.name=="Sidewalk graph"){walk=old;walkNodes=Connected(walk,4);break;}}
                var people=new List<GameObject>();
                if(AssetDatabase.IsValidFolder("Assets/Dimari/NPCs"))foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dimari/NPCs"}))
                {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var animator=prefab.GetComponentInChildren<Animator>();if(animator!=null&&animator.isHuman&&animator.avatar!=null&&animator.avatar.isValid)people.Add(prefab);}
                var walkers=new List<CityLifeAgent>();var cars=new List<CityLifeAgent>();
                foreach(var agent in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))
                {
                    if(agent.gameObject.scene!=city.scene||agent.seated||agent.graph==null)continue;
                    if(agent.vehicle)cars.Add(agent);else walkers.Add(agent);
                }
                var anchors=new List<Vector3>();
                Transform arrival=null;
                foreach(var candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(candidate.gameObject.scene==city.scene&&candidate.name=="CityArrivalPoint"){arrival=candidate;break;}
                if(arrival!=null)anchors.Add(arrival.position);
                var player=Object.FindAnyObjectByType<ForestPlayerController>();if(player!=null&&player.gameObject.scene==city.scene)anchors.Add(player.transform.position);
                var admin=Object.FindAnyObjectByType<AdministrationEntrance>();if(admin!=null)anchors.Add(admin.transform.position);
                var pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza!=null)anchors.Add(pizza.transform.position);
                var gym=GameObject.Find("Dimari_Donsport");if(gym!=null)anchors.Add(gym.transform.position);
                if(anchors.Count==0)anchors.Add(city.transform.position);
                var source=Object.FindAnyObjectByType<CityDriveableCar>();
                int pedestrianCount=Populate(city.transform,walk,walkNodes,anchors,walkers,people,null,24,false);
                int trafficLimit=10;
                if(source!=null)
                {
                    int vertices=0;foreach(var filter in source.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null)vertices+=filter.sharedMesh.vertexCount;
                    foreach(var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>())if(renderer.sharedMesh!=null)vertices+=renderer.sharedMesh.vertexCount;
                    trafficLimit=vertices>200000?4:vertices>80000?6:10;
                }
                int carCount=Populate(city.transform,road,roadNodes,anchors,cars,people,source,trafficLimit,true);
                foreach(var old in oldGraphs)
                {
                    if(old==null||old==road||old==walk)continue;bool used=false;
                    foreach(var agent in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))if(agent.graph==old){used=true;break;}
                    if(!used)Undo.DestroyObjectImmediate(old.gameObject);
                }
                if(city.transform.Find(Stamp)==null&&pedestrianCount>0&&carCount>0)
                {var stamp=new GameObject(Stamp);stamp.transform.SetParent(city.transform,false);Undo.RegisterCreatedObjectUndo(stamp,"Street repair stamp");}
                bool conversationFound=false;
                foreach(var conversation in Object.FindObjectsByType<CityAmbientConversations>(FindObjectsSortMode.None))if(conversation.gameObject.scene==city.scene)
                {Undo.RecordObject(conversation,"One ambient conversation scheduler");conversation.enabled=!conversationFound;conversationFound=true;}
                if(!conversationFound){var go=new GameObject("City conversations");go.transform.SetParent(city.transform,false);Undo.RegisterCreatedObjectUndo(go,"Street conversations");Undo.AddComponent<CityAmbientConversations>(go);}
                EditorSceneManager.MarkSceneDirty(city.scene);AssetDatabase.SaveAssets();
                string message="Настроено прохожих: "+pedestrianCount+" / 24; машин: "+carCount+" / "+trafficLimit+". Гости пиццерии и продавцы не заменены.\n"+"Пригодных узлов: дороги "+roadNodes.Count+", тротуары "+walkNodes.Count+".\n"+roadsReport+"\n"+walksReport+"\nЕсли счётчик нулевой: нужны распознаваемые поверхности/связные маршруты, импортированные Humanoid-префабы в Assets/Dimari/NPCs и исходная CityDriveableCar. Ничего не пускается наугад через дома. После Play сохраните сцену Ctrl+S вне Play.";
                Debug.Log("[Dimari] "+message,city);
                if(report)
                {
                    string walkSummary=walksReport.Split(new[]{" Материалы:"},System.StringSplitOptions.None)[0];
                    string roadSummary=roadsReport.Split(new[]{" Материалы:"},System.StringSplitOptions.None)[0];
                    EditorUtility.DisplayDialog("Городская жизнь",
                        "Прохожие: "+pedestrianCount+" / 24; машины: "+carCount+" / "+trafficLimit+".\n"+
                        "Пригодные узлы: тротуары "+walkNodes.Count+", дороги "+roadNodes.Count+".\n"+
                        "Готовых моделей людей: "+people.Count+".\n"+walkSummary+"\n"+roadSummary+
                        "\nПолный отчёт с материалами — в Console. Сохраните сцену Ctrl+S вне Play.","OK");
                }
            }
            catch {Undo.RevertAllDownToGroup(undo);throw;}
            finally
            {foreach(var pair in disabled)if(pair.Key!=null)pair.Key.enabled=pair.Value;Physics.SyncTransforms();Undo.CollapseUndoOperations(undo);}
        }
        static int Populate(Transform root,CityLifeGraph graph,List<int> nodes,List<Vector3> anchors,List<CityLifeAgent> existing,List<GameObject> people,CityDriveableCar source,int maximum,bool vehicle)
        {
            if(graph==null||nodes.Count==0)return 0;
            var used=new List<Vector3>();int count=0,lastGroupSlot=-2;
            for(int slot=0;slot<maximum;slot++)
            {
                bool pair=!vehicle&&slot%2==1&&lastGroupSlot==slot-1&&used.Count>0;
                Vector3 anchor=pair?used[used.Count-1]:anchors[(vehicle?slot:slot/2)%anchors.Count];int best=-1;float nearest=float.PositiveInfinity;
                foreach(int index in nodes)
                {
                    Vector3 p=graph.nodes[index].position;float distance=(p-anchor).sqrMagnitude;if(distance>100*100||distance>=nearest)continue;
                    bool close=false;
                    for(int u=0;u<used.Count;u++)if(Vector3.Distance(p,used[u])<(vehicle?18:pair&&u==used.Count-1?1.3f:7)){close=true;break;}
                    if(close||(pair&&distance>3.1f*3.1f))continue;
                    int next=graph.nodes[index].next[0];Vector3 direction=graph.nodes[next].position-p;direction.y=0;if(direction.sqrMagnitude<0.1f)continue;
                    if(Physics.OverlapBox(p+Vector3.up*0.9f,vehicle?new Vector3(1.05f,0.65f,2.5f):new Vector3(0.3f,0.65f,0.3f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore).Length>0)continue;
                    best=index;nearest=distance;
                }
                if(best<0)continue;
                Vector3 point=graph.nodes[best].position;int target=graph.nodes[best].next[0];Vector3 forward=graph.nodes[target].position-point;forward.y=0;Quaternion rotation=Quaternion.LookRotation(forward.normalized);
                CityLifeAgent agent=count<existing.Count?existing[count]:null;
                if(agent==null)
                {
                    if(vehicle)
                    {
                        if(source==null)continue;
                        var clone=Object.Instantiate(source.gameObject,root);clone.name="Traffic car "+(count+1);CityLifeSetup.Strip(clone);
                        foreach(var animator in clone.GetComponentsInChildren<Animator>())animator.enabled=false;
                        var body=clone.AddComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
                        agent=clone.AddComponent<CityLifeAgent>();agent.vehicle=true;
                        Undo.RegisterCreatedObjectUndo(clone,"Add road traffic");
                    }
                    else
                    {
                        if(people.Count==0)continue;
                        agent=CityLifeSetup.Human(people[count%people.Count],root,point,rotation,false);Undo.RegisterCreatedObjectUndo(agent.gameObject,"Add pavement pedestrian");
                    }
                }
                Undo.RecordObject(agent,"Assign connected street route");Undo.RecordObject(agent.transform,"Move street actor near city activity");
                agent.transform.SetPositionAndRotation(point,rotation);
                bool first=true;Bounds bounds=new Bounds();
                foreach(var renderer in agent.GetComponentsInChildren<Renderer>())
                {
                    if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                    Undo.RecordObject(renderer,"Keep street model visible");renderer.allowOcclusionWhenDynamic=false;renderer.forceRenderingOff=false;renderer.shadowCastingMode=ShadowCastingMode.On;
                    Undo.RecordObject(renderer.gameObject,"Remove static flag from moving actor");GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);
                    if(renderer is SkinnedMeshRenderer skinned)skinned.updateWhenOffscreen=true;
                }
                if(!first)agent.transform.position+=Vector3.up*(point.y-bounds.min.y);
                if(vehicle)
                {
                    // Bounds measured in the root's local frame, respecting imported scale and pivot.
                    bool empty=true;Bounds local=new Bounds();
                    foreach(var renderer in agent.GetComponentsInChildren<Renderer>())
                    {
                        Bounds b=renderer.localBounds;
                        for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                        {Vector3 p=agent.transform.InverseTransformPoint(renderer.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));if(empty){local=new Bounds(p,Vector3.zero);empty=false;}else local.Encapsulate(p);}
                    }
                    if(!empty){var box=agent.GetComponent<BoxCollider>();if(box==null)box=Undo.AddComponent<BoxCollider>(agent.gameObject);else Undo.RecordObject(box,"Fit traffic collider to imported model");box.center=local.center;box.size=local.size;}
                }
                agent.graph=graph;agent.previous=best;agent.node=target;agent.groundOffset=agent.transform.position.y-graph.nodes[best].position.y;agent.speed=vehicle?3.5f+(count%4)*0.6f:0.9f+(count%4)*0.12f;
                agent.talkingUntil=0;agent.speakingUntil=0;agent.conversationPartner=null;used.Add(point);if(!pair)lastGroupSlot=slot;count++;
            }
            return count;
        }
        [MenuItem("Dimari/Пицца — повторить обновлённый разговор (Play, у стойки)")]
        static void Replay()
        {
            if(!EditorApplication.isPlaying){EditorUtility.DisplayDialog("Разговор","Запустите Play и подойдите к стойке пиццерии.","OK");return;}
            var cutscene=Object.FindAnyObjectByType<Dimari.Cinematics.PizzaSellerCutscene>();if(cutscene!=null)cutscene.ReplayForPreview();
        }
    }
}
