using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Core;
using Dimari.Player;
using Dimari.Story;
using Dimari.Vehicles;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Dimari.City
{
    // Read-only diagnostic. Never unlocks a cinematic, disables a map collider or teleports the player.
    public class CityGameplayDiagnostics : MonoBehaviour
    {
        ForestPlayerController player;
        Vector3 last;
        float blockedFor,nextLog;
        string lastReport="";
        string lastCollision="none";
        float lastCollisionTime=-100;
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.collider==null||hit.transform.IsChildOf(transform)||Mathf.Abs(hit.normal.y)>0.7f)return;
            lastCollision=Path(hit.transform)+" type="+hit.collider.GetType().Name+" point="+hit.point+" normal="+hit.normal;
            lastCollisionTime=Time.unscaledTime;
        }
        void Start(){player=GetComponent<ForestPlayerController>();last=transform.position;}
        static bool CopyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame)return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if(Input.GetKeyDown(KeyCode.F8))return true;
#endif
            return false;
        }
        bool InCity()
        {return SceneManager.GetActiveScene().name=="Chapter2_Rogovo"&&StoryQuestManager.Instance!=null&&StoryQuestManager.Instance.HasGameStarted&&!StoryQuestManager.Instance.IsMenuOpen;}
        void Update()
        {
            if(!InCity()){last=transform.position;blockedFor=0;return;}
            if(CopyPressed())Copy();
            Vector3 delta=transform.position-last;delta.y=0;last=transform.position;
            var camera=Camera.main;var view=camera!=null?camera.GetComponent<ForestCameraController>():null;
            bool trying=player!=null&&player.IsControlEnabled&&CityDriveableCar.ActiveDriver==null&&(view==null||!view.IsCutsceneControlled)&&DimariInput.GetMoveInput().sqrMagnitude>0.1f;
            if(trying&&delta.magnitude<0.02f*Mathf.Max(Time.unscaledDeltaTime,0.001f))blockedFor+=Time.unscaledDeltaTime;else blockedFor=0;
            if(blockedFor>1.25f&&Time.unscaledTime>=nextLog)
            {lastReport=Report();Debug.LogWarning(lastReport,this);nextLog=Time.unscaledTime+15;}
        }
        void Copy()
        {lastReport=Report();GUIUtility.systemCopyBuffer=lastReport;Debug.Log(lastReport,this);}
        static string Path(Transform t)
        {string p=t.name;for(int i=0;i<7&&t.parent!=null;i++){t=t.parent;p=t.name+"/"+p;}return p;}
        string Report()
        {
            var scene=SceneManager.GetActiveScene();var story=StoryQuestManager.Instance;var cc=GetComponent<CharacterController>();
            var b=new StringBuilder("[Dimari CityFix2 report]\n");
            b.AppendLine("Scene="+scene.name+" position="+transform.position.ToString("F3")+" input="+DimariInput.GetMoveInput()+" timeScale="+Time.timeScale);
            b.AppendLine("Application focused="+Application.isFocused);
            b.AppendLine("Control="+(player!=null&&player.IsControlEnabled)+" gameStarted="+(story!=null&&story.HasGameStarted)+" menu="+(story!=null&&story.IsMenuOpen)+" car="+(CityDriveableCar.ActiveDriver!=null));
            if(cc!=null)
            {
                b.AppendLine("CharacterController enabled="+cc.enabled+" minMoveDistance="+cc.minMoveDistance+" grounded="+cc.isGrounded+" velocity="+cc.velocity);
                Vector3 center=transform.TransformPoint(cc.center);float scale=Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));float radius=cc.radius*scale;
                float half=Mathf.Max(0,cc.height*Mathf.Abs(transform.lossyScale.y)*0.5f-radius);
                var overlaps=Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,Mathf.Max(0.01f,radius-0.01f),~0,QueryTriggerInteraction.Ignore);
                b.AppendLine("Overlapping capsule colliders="+overlaps.Length);
                int shown=0;foreach(var hit in overlaps)
                {if(hit==cc)continue;b.AppendLine("  "+Path(hit.transform)+" type="+hit.GetType().Name+" selfChild="+hit.transform.IsChildOf(transform)+" bounds="+hit.bounds);if(++shown>=12)break;}
            }
            b.AppendLine("Last side collision="+lastCollision+" age="+(Time.unscaledTime-lastCollisionTime).ToString("F1")+"s");
            foreach(var capsule in GetComponentsInChildren<CharacterController>(true))
                b.AppendLine("Player capsule="+Path(capsule.transform)+" enabled="+capsule.enabled);
            foreach(var script in GetComponents<MonoBehaviour>())if(script!=null)b.AppendLine("Player component: "+script.GetType().Name+" enabled="+script.enabled);
            int walkers=0,cars=0,nearWalk=0,nearCars=0;
            foreach(var agent in FindObjectsByType<CityLifeAgent>())
            {
                if(agent.gameObject.scene!=scene||agent.seated||agent.graph==null)continue;
                bool near=Vector3.Distance(transform.position,agent.transform.position)<80;
                if(agent.vehicle){cars++;if(near)nearCars++;}else{walkers++;if(near)nearWalk++;}
            }
            b.AppendLine("Street pedestrians="+walkers+" near80m="+nearWalk+" traffic="+cars+" near80m="+nearCars);
            foreach(var graph in FindObjectsByType<CityLifeGraph>())
            {if(graph.gameObject.scene!=scene)continue;int linked=0;if(graph.nodes!=null)foreach(var n in graph.nodes)if(n!=null&&n.next!=null&&n.next.Length>0)linked++;b.AppendLine("Graph "+graph.name+" nodes="+(graph.nodes!=null?graph.nodes.Length:0)+" linked="+linked);}
            foreach(var hit in Physics.RaycastAll(transform.position+Vector3.up*1.8f,Vector3.down,6,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform))continue;b.AppendLine("Ground ray: "+Path(hit.transform)+" y="+hit.point.y+" normal="+hit.normal);
                var renderer=hit.collider.GetComponent<Renderer>();if(renderer!=null)foreach(var mat in renderer.sharedMaterials)if(mat!=null)
                {Texture texture=mat.HasProperty("_BaseMap")?mat.GetTexture("_BaseMap"):(mat.HasProperty("_MainTex")?mat.GetTexture("_MainTex"):null);b.AppendLine("  material="+mat.name+" texture="+(texture!=null?texture.name:"none"));}
            }
            return b.ToString();
        }
        void OnGUI()
        {
            if(!InCity())return;
            var evt=Event.current;if(evt!=null&&evt.type==EventType.KeyDown&&evt.keyCode==KeyCode.F8){Copy();evt.Use();}
            if(GUI.Button(new Rect(Screen.width-245,82,230,30),"F8 — скопировать отчёт CityFix2"))Copy();
            if(blockedFor>1.25f)GUI.Box(new Rect(Screen.width-390,120,375,70),"Движение заблокировано.\nНажмите F8 и пришлите скопированный отчёт.\nКоллайдеры автоматически не отключаются.");
        }
    }
}
