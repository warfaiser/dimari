using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Dimari.Core;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.Cinematics
{
    [DefaultExecutionOrder(300)]
    public class PizzaSellerCutscene : MonoBehaviour
    {
        public Transform seller;
        public static bool IsRunning { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession(){IsRunning=false;}
        readonly string[] lines={
            "Так, а ты что здесь делаешь?",
            "Я здесь подрабатываю в свободное время. Когда мы разговаривали в администрации, я ела рыбу и забыла тебе об этом сказать.",
            "Похоже, от твоей просроченной еды у тебя уже всё в голове перепуталось. Ладно, давай к делу. Где Димарик?",
            "Он закончил работу в пять вечера и уехал в спортивный зал «Донспорт». Езжай туда — уверена, там тебе подскажут, где его найти.",
            "Надеюсь, больше с тобой не встретимся. И лучше выброси эту просроченную еду."
        };
        ForestPlayerController player;
        StoryQuestManager story;
        ForestCameraController view;
        Camera cam;
        AudioSource speech;
        Transform head,jaw,arm;
        Quaternion jawRest,headRest,armRest,playerRotation,camRotation;
        Vector3 camPosition,shotPosition,lookAt;
        float originalFov,envelope;
        bool running,prompted,sellerSpeaking,paused,previousControl,previousCutscene;
        string subtitle="";
        readonly float[] samples=new float[256];
        readonly List<Renderer> renderers=new List<Renderer>();
        readonly List<ShadowCastingMode> shadows=new List<ShadowCastingMode>();
        readonly List<bool> visible=new List<bool>();
        readonly List<SkinnedMeshRenderer> mouths=new List<SkinnedMeshRenderer>();
        readonly List<int> shapes=new List<int>();
        readonly List<float> weights=new List<float>();
        public static bool HasGymQuest(StoryQuestManager manager)
        {if(manager==null)return false;foreach(var step in manager.questSteps)if(step.id=="city_donsport")return true;return false;}
        bool HasPizzaQuest()
        {if(story==null)return false;foreach(var step in story.questSteps)if(step.id=="city_pizza_express"&&!step.isCompleted)return true;return false;}
        bool NearCounter()
        {
            if(player==null||seller==null)return false;
            Vector3 local=transform.InverseTransformPoint(player.transform.position),s=transform.InverseTransformPoint(seller.position);
            Vector3 delta=player.transform.position-seller.position;delta.y=0;
            return local.z>0.2f&&local.z<2.2f&&Mathf.Abs(local.x-s.x)<2.4f&&delta.magnitude<3.4f;
        }
        void Update()
        {
            if(running)
            {
                if(player==null||seller==null||cam==null||story==null){StopAllCoroutines();Release();return;}
                bool menu=story.IsMenuOpen;
                if(speech!=null&&menu!=paused){if(menu)speech.Pause();else speech.UnPause();paused=menu;}
                return;
            }
            story=StoryQuestManager.Instance;
            if(player==null)player=FindAnyObjectByType<ForestPlayerController>();
            if(seller==null)seller=transform.Find("Pizza seller Obzhorka");
            bool eligible=!IsRunning&&story!=null&&!story.IsMenuOpen&&player!=null&&player.IsControlEnabled&&HasPizzaQuest()&&!HasGymQuest(story)&&NearCounter();
            if(!eligible){if(prompted&&story!=null){story.SetInteractionPrompt("");prompted=false;}return;}
            story.SetInteractionPrompt("[E] Поговорить с Обжоркой Просрочки");prompted=true;
            if(DimariInput.IsInteractPressed())StartCoroutine(Play());
        }
        public void ReplayForPreview()
        {
            story=StoryQuestManager.Instance;if(player==null)player=FindAnyObjectByType<ForestPlayerController>();
            if(seller==null)seller=transform.Find("Pizza seller Obzhorka");
            if(!Application.isPlaying||IsRunning||story==null||story.IsMenuOpen||player==null||!player.IsControlEnabled||!NearCounter())
            {Debug.LogWarning("[Dimari] Для повтора разговора подойдите к стойке в Play и закройте меню.",this);return;}
            StartCoroutine(Play());
        }
        IEnumerator Play()
        {
            cam=Camera.main;if(cam==null||seller==null||player==null)yield break;
            var animator=seller.GetComponentInChildren<Animator>();
            head=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
            jaw=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Jaw):null;
            arm=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.RightUpperArm):null;
            if(head!=null)headRest=head.localRotation;if(jaw!=null)jawRest=jaw.localRotation;if(arm!=null)armRest=arm.localRotation;
            mouths.Clear();shapes.Clear();weights.Clear();
            foreach(var mesh in seller.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(mesh.sharedMesh==null)continue;
                for(int i=0;i<mesh.sharedMesh.blendShapeCount;i++)
                {
                    string name=mesh.sharedMesh.GetBlendShapeName(i).ToLowerInvariant();
                    if(!name.Contains("jawopen")&&!name.Contains("mouthopen")&&!name.Contains("mouth_open"))continue;
                    mouths.Add(mesh);shapes.Add(i);weights.Add(mesh.GetBlendShapeWeight(i));break;
                }
            }
            view=cam.GetComponent<ForestCameraController>();previousControl=player.IsControlEnabled;previousCutscene=view!=null&&view.IsCutsceneControlled;
            camPosition=cam.transform.position;camRotation=cam.transform.rotation;originalFov=cam.fieldOfView;playerRotation=player.transform.rotation;
            renderers.Clear();shadows.Clear();visible.Clear();
            foreach(var r in player.GetComponentsInChildren<Renderer>(true))
            {renderers.Add(r);shadows.Add(r.shadowCastingMode);visible.Add(r.enabled);r.shadowCastingMode=ShadowCastingMode.On;}
            if(view!=null){view.IsCutsceneControlled=true;view.UpdateCharacterMeshVisibility(false);}
            player.StopLocomotionAnimation();player.IsControlEnabled=false;
            Vector3 direction=seller.position-player.transform.position;direction.y=0;if(direction.sqrMagnitude>0.01f)player.transform.rotation=Quaternion.LookRotation(direction);
            story.SetInteractionPrompt("");prompted=false;running=true;IsRunning=true;
            if(speech==null){speech=gameObject.AddComponent<AudioSource>();speech.spatialBlend=0;speech.volume=1;speech.playOnAwake=false;}
            for(int i=0;i<lines.Length;i++)
            {
                sellerSpeaking=i%2==1;subtitle=(sellerSpeaking?"Обжорка Просрочки":"Главный герой")+":\n"+lines[i];
                AudioClip clip=Resources.Load<AudioClip>("Voice_PizzaClear_"+i);speech.clip=clip;if(clip!=null)speech.Play();
                SetShot(sellerSpeaking);
                float duration=clip!=null?clip.length+0.35f:Mathf.Clamp(lines[i].Length/12f,4.5f,16f);
                for(float t=0;t<duration;)
                {
                    if(player==null||seller==null||cam==null||story==null){Release();yield break;}
                    if(!story.IsMenuOpen)t+=Time.deltaTime;
                    yield return null;
                }
                speech.Stop();envelope=0;
            }
            if(story!=null)
            {
                foreach(var step in story.questSteps)if(step.id=="city_pizza_express")step.isCompleted=true;
                if(!HasGymQuest(story))story.questSteps.Add(new StoryQuestManager.QuestStep{id="city_donsport",title="Поехать в спортивный зал «Донспорт» и расспросить о Димарике."});
                story.ShowStatusToast("Новая зацепка: Димарик уехал в «Донспорт» после 17:00.");
            }
            Release();
        }
        void SetShot(bool woman)
        {
            Vector3 face;
            if(woman)
            {face=head!=null?head.position:seller.position+Vector3.up*1.5f;shotPosition=face+seller.forward*1.9f+seller.right*0.3f+Vector3.up*0.05f;}
            else
            {
                var animator=player.characterAnimator;Transform heroHead=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
                face=heroHead!=null?heroHead.position:player.transform.position+Vector3.up*1.55f;
                shotPosition=face+player.transform.forward*1.7f+player.transform.right*0.6f+Vector3.up*0.04f;
            }
            lookAt=face+Vector3.up*0.03f;
        }
        void LateUpdate()
        {
            if(!running||cam==null||story==null||story.IsMenuOpen)return;
            cam.fieldOfView=46;
            cam.transform.position=Vector3.Lerp(cam.transform.position,shotPosition,1-Mathf.Exp(-8*Time.deltaTime));
            Vector3 delta=lookAt-cam.transform.position;if(delta.sqrMagnitude>0.001f)cam.transform.rotation=Quaternion.Slerp(cam.transform.rotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-10*Time.deltaTime));
            envelope=0;
            if(sellerSpeaking&&speech!=null&&speech.isPlaying)
            {speech.GetOutputData(samples,0);float sum=0;foreach(float value in samples)sum+=value*value;envelope=Mathf.Clamp01(Mathf.Sqrt(sum/samples.Length)*10);}
            if(head!=null)head.localRotation=headRest*Quaternion.Euler(Mathf.Sin(Time.time*1.6f)*1.5f,Mathf.Sin(Time.time)*2,0);
            if(jaw!=null)jaw.localRotation=jawRest*Quaternion.Euler(envelope*14,0,0);
            if(arm!=null)arm.localRotation=armRest*Quaternion.Euler(sellerSpeaking?Mathf.Sin(Time.time*2.7f)*6:0,0,0);
            for(int i=0;i<mouths.Count;i++)if(mouths[i]!=null)mouths[i].SetBlendShapeWeight(shapes[i],Mathf.Max(weights[i],envelope*65));
        }
        void Release()
        {
            if(!running)return;
            running=false;IsRunning=false;subtitle="";sellerSpeaking=false;paused=false;if(speech!=null)speech.Stop();
            if(head!=null)head.localRotation=headRest;if(jaw!=null)jaw.localRotation=jawRest;if(arm!=null)arm.localRotation=armRest;
            for(int i=0;i<mouths.Count;i++)if(mouths[i]!=null)mouths[i].SetBlendShapeWeight(shapes[i],weights[i]);
            if(player!=null){player.transform.rotation=playerRotation;player.StopLocomotionAnimation();player.IsControlEnabled=previousControl;}
            if(cam!=null){cam.transform.SetPositionAndRotation(camPosition,camRotation);cam.fieldOfView=originalFov;}
            if(view!=null){view.BeginGameplayTransitionFromCutscene();view.IsCutsceneControlled=previousCutscene;}
            for(int i=0;i<renderers.Count;i++)if(renderers[i]!=null){renderers[i].shadowCastingMode=shadows[i];renderers[i].enabled=visible[i];}
            if(story!=null)story.SetInteractionPrompt("");
        }
        void OnDisable(){StopAllCoroutines();Release();if(prompted&&story!=null)story.SetInteractionPrompt("");prompted=false;}
        void OnGUI()
        {
            if(!running||story==null||story.IsMenuOpen||string.IsNullOrEmpty(subtitle))return;
            int depth=GUI.depth;Color color=GUI.color;GUI.depth=-250;
            GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*0.06f),Texture2D.whiteTexture);
            Rect box=new Rect(Screen.width*0.08f,Screen.height*0.77f,Screen.width*0.84f,Screen.height*0.18f);
            GUI.color=new Color(0.01f,0.015f,0.02f,0.94f);GUI.DrawTexture(box,Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(box.x+18,box.y+8,box.width-36,box.height-16),subtitle,new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Screen.height/39,18,29),alignment=TextAnchor.MiddleCenter,wordWrap=true});
            GUI.color=color;GUI.depth=depth;
        }
    }
}
