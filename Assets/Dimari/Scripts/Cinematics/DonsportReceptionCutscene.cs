using System.Collections;
using UnityEngine;
using Dimari.Core;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.Cinematics
{
    [DefaultExecutionOrder(310)]
    public class DonsportReceptionCutscene : MonoBehaviour
    {
        public Transform elizaveta, maybeBaby;
        public static bool IsRunning { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { IsRunning = false; }
        public static readonly string[] Lines = {
            "Здравствуйте, подскажите, пожалуйста, вы тут мальчика вприпрыжку не видели? Он катается на BMW 3 серии 320d, но всё ещё хочет купить Changan Uni V.",
            "Здравствуйте. Да, прибегал вприпрыжку один: вены были вздутые, красный весь. Подумала — наркоман. Был одет как Зеленский.",
            "Да, это похоже на него. А сколько он тут был и с кем-то общался?",
            "Да, разговаривал. Была девушка, певица, думаю, вы её знаете, зовут Мэйби Бэйби. Она к нам тоже начала ходить. Он к ней бежит и пытается запеть с ней.",
            "А сегодня она тут? Я хотел бы с ней поговорить насчёт Димарика, никак не могу его найти со вчерашнего дня.",
            "Да, она тут, проходите в спортзал."
        };
        ForestPlayerController player;
        StoryQuestManager story;
        ForestCameraController view;
        Camera cam;
        AudioSource voice;
        bool running, prompted, paused, oldControl, oldCutscene, womanSpeaking;
        Vector3 oldCameraPosition;
        Quaternion oldCameraRotation;
        float oldFov;
        string subtitle = "";
        Renderer[] heroRenderers;
        bool[] heroVisibility;
        Transform head,jaw;
        Quaternion headRest,jawRest;
        readonly float[] samples=new float[128];
        static StoryQuestManager.QuestStep Step(StoryQuestManager manager, string id)
        { if (manager != null) foreach (var step in manager.questSteps) if (step.id == id) return step; return null; }
        void Update()
        {
            story = StoryQuestManager.Instance;
            if (player == null) player = FindAnyObjectByType<ForestPlayerController>();
            if (running)
            {
                if (story == null || player == null || cam == null || elizaveta == null) { StopAllCoroutines(); Release(); return; }
                if (story.IsMenuOpen != paused)
                { paused = story.IsMenuOpen; if (voice != null) { if (paused) voice.Pause(); else voice.UnPause(); } }
                return;
            }
            if (story == null || player == null || story.IsMenuOpen || !player.IsControlEnabled) { ClearPrompt(); return; }
            var gym = Step(story, "city_donsport");
            if (gym != null && !gym.isCompleted && elizaveta != null && Vector3.Distance(player.transform.position, elizaveta.position) < 3.6f)
            {
                story.SetInteractionPrompt("[E] Поговорить с Елизаветой"); prompted = true;
                if (DimariInput.IsInteractPressed() && !IsRunning) StartCoroutine(Play());
                return;
            }
            ClearPrompt();
            var find = Step(story, "city_maybe_baby");
            if (find != null && !find.isCompleted && maybeBaby != null)
            {
                Vector3 p = transform.InverseTransformPoint(player.transform.position);
                Vector3 target = maybeBaby.position + Vector3.up * 1.5f;
                Vector3 delta = target - (player.transform.position + Vector3.up * 1.5f);
                if (p.z > 2.4f && delta.magnitude < 3f)
                {
                    bool blocked = Physics.Linecast(player.transform.position + Vector3.up * 1.5f, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore);
                    if (!blocked || hit.transform.IsChildOf(maybeBaby)) Found(find);
                }
            }
        }
        void Found(StoryQuestManager.QuestStep find)
        { find.isCompleted = true; story.ShowStatusToast("Вы нашли Мэйби Бэйби. Её разговор будет добавлен следующим этапом."); }
        void ClearPrompt() { if (prompted && story != null) story.SetInteractionPrompt(""); prompted = false; }
        public void ReplayForPreview()
        {
            story = StoryQuestManager.Instance; player = FindAnyObjectByType<ForestPlayerController>();
            if (!Application.isPlaying || IsRunning || story == null || story.IsMenuOpen || player == null || !player.IsControlEnabled || elizaveta == null
                || Vector3.Distance(player.transform.position, elizaveta.position) > 3.6f) return;
            StartCoroutine(Play());
        }
        IEnumerator Play()
        {
            cam = Camera.main;
            if (cam == null || elizaveta == null || player == null) yield break;
            view = cam.GetComponent<ForestCameraController>(); oldCutscene = view != null && view.IsCutsceneControlled;
            oldControl = player.IsControlEnabled; oldCameraPosition = cam.transform.position; oldCameraRotation = cam.transform.rotation; oldFov = cam.fieldOfView;
            heroRenderers = player.GetComponentsInChildren<Renderer>(true); heroVisibility = new bool[heroRenderers.Length];
            for (int i = 0; i < heroRenderers.Length; i++) heroVisibility[i] = heroRenderers[i].enabled;
            if (view != null) { view.IsCutsceneControlled = true; view.UpdateCharacterMeshVisibility(false); }
            player.StopLocomotionAnimation(); player.IsControlEnabled = false; ClearPrompt();
            var animator = elizaveta.GetComponentInChildren<Animator>();
            head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null) headRest = head.localRotation;
            jaw = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Jaw) : null;
            if(jaw!=null)jawRest=jaw.localRotation;
            if (voice == null) { voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0; }
            running = true; IsRunning = true;
            for (int i = 0; i < Lines.Length; i++)
            {
                womanSpeaking = i % 2 == 1;
                subtitle = (womanSpeaking ? "Елизавета" : "Главный герой") + ":\n" + Lines[i];
                var clip = Resources.Load<AudioClip>("Voice_Donsport_" + i); voice.clip = clip;
                if (clip != null) { voice.Play(); if (story.IsMenuOpen) { voice.Pause(); paused = true; } }
                float duration = clip != null ? clip.length + 0.35f : Mathf.Clamp(Lines[i].Length / 12f, 3.5f, 22f);
                for (float t = 0; t < duration;)
                {
                    if (story == null || player == null || cam == null || elizaveta == null) { Release(); yield break; }
                    if (!story.IsMenuOpen) t += Time.deltaTime;
                    yield return null;
                }
                voice.Stop();
            }
            var gym = Step(story, "city_donsport"); if (gym != null) gym.isCompleted = true;
            if (Step(story, "city_maybe_baby") == null) story.questSteps.Add(new StoryQuestManager.QuestStep { id = "city_maybe_baby", title = "Найти Мэйби Бэйби среди посетителей тренажёрного зала Донспорта." });
            story.ShowStatusToast("Пройдите в тренажёрный зал через проход справа от ресепшена.");
            Release();
        }
        void LateUpdate()
        {
            if (!running || cam == null || story == null || story.IsMenuOpen) return;
            Transform actor = womanSpeaking ? elizaveta : player.transform;
            var animator = actor.GetComponentInChildren<Animator>();
            Transform faceBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 face = faceBone != null ? faceBone.position : actor.position + Vector3.up * 1.55f;
            Vector3 direction = womanSpeaking ? player.transform.position - elizaveta.position : elizaveta.position - player.transform.position;
            direction.y = 0; if (direction.sqrMagnitude < 0.01f) direction = actor.forward;
            direction.Normalize();
            Vector3 desired = face + direction * 1.35f + Vector3.Cross(Vector3.up, direction) * 0.35f + Vector3.up * 0.05f;
            // Keep the camera on the actor's side of the desk; do not fly through a pillar.
            foreach (var hit in Physics.SphereCastAll(face, 0.1f, (desired-face).normalized, Vector3.Distance(face, desired), ~0, QueryTriggerInteraction.Ignore))
            { if (hit.transform.IsChildOf(actor) || hit.transform.IsChildOf(player.transform)) continue; desired = face + (desired-face).normalized * Mathf.Max(0.25f, hit.distance-0.12f); }
            cam.fieldOfView = 48; cam.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(face-desired));
            if (head != null) head.localRotation = headRest * Quaternion.Euler(womanSpeaking ? Mathf.Sin(Time.time*2)*1.5f : 0, 0, 0);
            if(jaw!=null)
            {
                float envelope=0;
                if(womanSpeaking&&voice!=null&&voice.isPlaying){voice.GetOutputData(samples,0);foreach(float sample in samples)envelope+=sample*sample;envelope=Mathf.Clamp01(Mathf.Sqrt(envelope/samples.Length)*12);}
                jaw.localRotation=jawRest*Quaternion.Euler(envelope*14,0,0);
            }
        }
        void Release()
        {
            if (!running) return; running = false; IsRunning = false; subtitle = ""; paused = false;
            if (voice != null) voice.Stop(); if (head != null) head.localRotation = headRest; if(jaw!=null)jaw.localRotation=jawRest;
            if (player != null) player.IsControlEnabled = oldControl;
            if (view != null) { view.IsCutsceneControlled = oldCutscene; if (!oldCutscene) view.BeginGameplayTransitionFromCutscene(); }
            if (cam != null) { cam.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation); cam.fieldOfView = oldFov; }
            if (heroRenderers != null) for (int i = 0; i < heroRenderers.Length; i++) if (heroRenderers[i] != null) heroRenderers[i].enabled = heroVisibility[i];
        }
        void OnDisable() { StopAllCoroutines(); Release(); ClearPrompt(); }
        void OnGUI()
        {
            if (story == null || story.IsMenuOpen) return;
            if (running)
            {
                GUI.depth = -260;
                GUI.Box(new Rect(Screen.width*.06f, Screen.height*.75f, Screen.width*.88f, Screen.height*.2f), subtitle,
                    new GUIStyle(GUI.skin.box) { fontSize = Mathf.Clamp(Screen.height/40,18,28), wordWrap = true, alignment = TextAnchor.MiddleCenter });
                return;
            }
            var find = Step(story, "city_maybe_baby");
            if (find == null || find.isCompleted || maybeBaby == null || player == null || !player.IsControlEnabled || Camera.main == null) return;
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            if (local.z < 2.4f) return;
            Vector3 target = maybeBaby.position + Vector3.up*1.8f, from = Camera.main.transform.position;
            if (Physics.Linecast(from,target,out RaycastHit hit,~0,QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(maybeBaby)) return;
            Vector3 p = Camera.main.WorldToScreenPoint(target); if (p.z <= 0) return;
            GUI.Box(new Rect(p.x-100,Screen.height-p.y-35,200,35),"МЭЙБИ БЭЙБИ");
        }
    }
}
