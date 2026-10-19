using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Core;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.Cinematics
{
    /// <summary>
    /// Автоматическая кат-сцена у Мэйби Бэйби: планшет с фотографией Димарика,
    /// шесть реплик и передача трусов. Начинается, когда герой подходит.
    /// </summary>
    [DefaultExecutionOrder(320)]
    public class DonsportMaybeBabyCutscene : MonoBehaviour
    {
        public Transform maybeBaby;
        public static bool IsRunning { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { IsRunning = false; }

        public static readonly string[] Lines =
        {
            "Привет, подскажи, к тебе такой парень подходил?",
            "Да, видела такого. Всё время прыгал, скакал. Как Зеленский был одет.",
            "Отлично. Что он хотел от тебя и не говорил, куда он мог отправиться?",
            "Сказал, что ему нужно срочно поехать купить в «Авторусь» Changan Uni V. Уж больно он мечтал о ней.",
            "Да, все уши прожужжал этим «Чанганом». Спасибо, поеду туда.",
            "Передай ему мои трусы, он их очень хотел."
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            EnsureRuntime();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { EnsureRuntime(); }
        static void EnsureRuntime()
        {
            var story = Object.FindAnyObjectByType<DonsportReceptionCutscene>();
            if (story == null || story.maybeBaby == null) return;
            var scene = story.GetComponent<DonsportMaybeBabyCutscene>();
            if (scene == null) scene = story.gameObject.AddComponent<DonsportMaybeBabyCutscene>();
            scene.maybeBaby = story.maybeBaby;
        }

        ForestPlayerController player;
        StoryQuestManager story;
        ForestCameraController view;
        Camera cam;
        AudioSource voice;
        Renderer[] heroRenderers;
        bool[] heroVisibility;
        Vector3 oldCameraPosition, oldPlayerPosition;
        Quaternion oldCameraRotation, oldPlayerRotation, oldWomanRotation;
        float oldFov;
        int cameraShot;
        bool cameraReady;
        bool running, paused, oldControl, oldCutscene, womanSpeaking;
        string subtitle = "";
        readonly float[] samples = new float[128];
        float poseWeight, womanLookDown, heroArmWeight, womanArmWeight;

        Transform heroHead, heroJaw, womanHead, womanJaw;
        Transform heroUpperArm, heroLowerArm, heroHand;
        Transform womanUpperArm, womanLowerArm, womanHand;
        Quaternion heroJawRest, womanJawRest, womanHeadRest;
        Quaternion heroUpperRest, heroLowerRest, womanUpperRest, womanLowerRest;

        GameObject tablet, panties;
        Material tabletScreenMaterial, tabletBodyMaterial, pantyFabricMaterial, pantyBandMaterial;
        float tabletBlend, pantyBlend;
        bool tabletActive, pantyActive;
        Vector3 tabletPosition, pantyPosition;

        static StoryQuestManager.QuestStep Step(StoryQuestManager manager, string id)
        {
            if (manager != null) foreach (var step in manager.questSteps) if (step.id == id) return step;
            return null;
        }

        void Update()
        {
            story = StoryQuestManager.Instance;
            if (player == null) player = FindAnyObjectByType<ForestPlayerController>();
            if (running)
            {
                if (story == null || player == null || cam == null || maybeBaby == null) { StopAllCoroutines(); Release(); return; }
                if (story.IsMenuOpen != paused)
                {
                    paused = story.IsMenuOpen;
                    if (voice != null) { if (paused) voice.Pause(); else voice.UnPause(); }
                }
                return;
            }
            if (story == null || player == null || maybeBaby == null || story.IsMenuOpen || !player.IsControlEnabled || IsRunning) return;
            var find = Step(story, "city_maybe_baby");
            if (find == null || find.isCompleted) return;
            Vector3 from = player.transform.position + Vector3.up * 1.35f;
            Vector3 target = maybeBaby.position + Vector3.up * 1.35f;
            if (Vector3.Distance(player.transform.position, maybeBaby.position) > 3.2f) return;
            if (Physics.Linecast(from, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(maybeBaby) && !hit.transform.IsChildOf(player.transform)) return;
            StartCoroutine(Play(find));
        }

        IEnumerator Play(StoryQuestManager.QuestStep find)
        {
            cam = Camera.main;
            if (cam == null) yield break;
            view = cam.GetComponent<ForestCameraController>();
            oldCutscene = view != null && view.IsCutsceneControlled;
            oldControl = player.IsControlEnabled;
            oldCameraPosition = cam.transform.position; oldCameraRotation = cam.transform.rotation; oldFov = cam.fieldOfView;
            oldPlayerPosition = player.transform.position; oldPlayerRotation = player.transform.rotation;
            oldWomanRotation = maybeBaby.rotation;
            heroRenderers = player.GetComponentsInChildren<Renderer>(true);
            heroVisibility = new bool[heroRenderers.Length];
            for (int i = 0; i < heroRenderers.Length; i++) heroVisibility[i] = heroRenderers[i].enabled;
            if (view != null) { view.IsCutsceneControlled = true; view.UpdateCharacterMeshVisibility(false); }
            player.StopLocomotionAnimation(); player.IsControlEnabled = false;
            FaceEachOther();
            CacheBones();
            BuildProps();
            tabletBlend = 0f; pantyBlend = 0f; tabletActive = false; pantyActive = false;
            poseWeight = 0f; heroArmWeight = 0f; womanArmWeight = 0f; womanLookDown = 0f;
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false; voice.spatialBlend = 0;
            }
            running = true; IsRunning = true; cameraReady = false;
            yield return null;

            for (int i = 0; i < Lines.Length; i++)
            {
                womanSpeaking = i % 2 == 1;
                if (i == 5) { pantyActive = true; pantyBlend = 0f; }
                subtitle = (womanSpeaking ? "Мэйби Бэйби" : "Главный герой") + ":\n" + Lines[i];
                var clip = Resources.Load<AudioClip>("Voice_MaybeBaby_" + i);
                voice.clip = clip;
                if (clip != null) { voice.Play(); if (story.IsMenuOpen) { voice.Pause(); paused = true; } }
                float duration = clip != null ? clip.length + 0.3f : Mathf.Clamp(Lines[i].Length / 12f, 2.8f, 18f);
                cameraShot = i == 1 ? 1 : (i == 5 ? 2 : 0);
                for (float t = 0; t < duration;)
                {
                    if (story == null || player == null || cam == null || maybeBaby == null) { Release(); yield break; }
                    if (!story.IsMenuOpen) t += Time.deltaTime;
                    yield return null;
                }
                voice.Stop();
                if (i == 0) yield return HandOverTablet();
                if (i == 5) yield return HandOverPanties();
            }

            find.isCompleted = true;
            if (Step(story, "city_avtorusal") == null)
                story.questSteps.Add(new StoryQuestManager.QuestStep
                {
                    id = "city_avtorusal",
                    title = "Поехать в «Авторусь»: Димарик хотел купить Changan UNI-V."
                });
            story.ShowStatusToast("Мэйби Бэйби передала трусы. Следующая остановка — «Авторусь».");
            Release();
        }

        IEnumerator HandOverTablet()
        {
            subtitle = "(Главный герой передаёт Мэйби Бэйби планшет с фотографией.)";
            cameraShot = 1;
            tabletActive = true;
            for (float t = 0; t < 1.25f; t += Time.deltaTime)
            {
                tabletBlend = Mathf.SmoothStep(0f, 1f, t / 1.25f);
                yield return null;
            }
            tabletBlend = 1f;
            yield return new WaitForSeconds(0.25f);
            subtitle = "";
        }

        IEnumerator HandOverPanties()
        {
            pantyActive = true;
            yield return new WaitForSeconds(0.15f);
            subtitle = "(Мэйби Бэйби отдаёт трусы.)";
            cameraShot = 2;
            for (float t = 0; t < 1.15f; t += Time.deltaTime)
            {
                pantyBlend = Mathf.SmoothStep(0f, 1f, t / 1.15f);
                yield return null;
            }
            pantyBlend = 1f;
            yield return new WaitForSeconds(0.35f);
            subtitle = "";
        }

        void FaceEachOther()
        {
            Vector3 look = maybeBaby.position - player.transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(look);
            look = player.transform.position - maybeBaby.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) maybeBaby.rotation = Quaternion.LookRotation(look);
            Physics.SyncTransforms();
        }

        void CacheBones()
        {
            var hero = player.characterAnimator != null ? player.characterAnimator : player.GetComponentInChildren<Animator>();
            var woman = maybeBaby.GetComponentInChildren<Animator>();
            if (hero != null && hero.isHuman)
            {
                heroHead = hero.GetBoneTransform(HumanBodyBones.Head);
                heroJaw = hero.GetBoneTransform(HumanBodyBones.Jaw);
                heroHand = hero.GetBoneTransform(HumanBodyBones.RightHand);
                heroLowerArm = hero.GetBoneTransform(HumanBodyBones.RightLowerArm);
                heroUpperArm = hero.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (heroJaw != null) heroJawRest = heroJaw.localRotation;
                if (heroUpperArm != null) heroUpperRest = heroUpperArm.localRotation;
                if (heroLowerArm != null) heroLowerRest = heroLowerArm.localRotation;
            }
            if (woman != null && woman.isHuman)
            {
                womanHead = woman.GetBoneTransform(HumanBodyBones.Head);
                womanJaw = woman.GetBoneTransform(HumanBodyBones.Jaw);
                womanHand = woman.GetBoneTransform(HumanBodyBones.RightHand);
                womanLowerArm = woman.GetBoneTransform(HumanBodyBones.RightLowerArm);
                womanUpperArm = woman.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (womanJaw != null) womanJawRest = womanJaw.localRotation;
                if (womanHead != null) womanHeadRest = womanHead.localRotation;
                if (womanUpperArm != null) womanUpperRest = womanUpperArm.localRotation;
                if (womanLowerArm != null) womanLowerRest = womanLowerArm.localRotation;
            }
        }

        void BuildProps()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            Shader unlit = Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? lit;
            if (lit == null) { Debug.LogError("[Dimari] Не найден шейдер для реквизита кат-сцены.", this); return; }
            tabletBodyMaterial = new Material(lit); SetColor(tabletBodyMaterial, new Color(.05f, .06f, .08f));
            tabletScreenMaterial = new Material(unlit);
            var photo = Resources.Load<Texture2D>("Dimari_Tablet_Photo");
            if (photo != null) { tabletScreenMaterial.mainTexture = photo; SetColor(tabletScreenMaterial, Color.white); }
            else SetColor(tabletScreenMaterial, new Color(.32f, .55f, .78f));
            pantyFabricMaterial = new Material(lit); SetColor(pantyFabricMaterial, new Color(.68f, .04f, .12f));
            pantyBandMaterial = new Material(lit); SetColor(pantyBandMaterial, new Color(.18f, .03f, .06f));

            tablet = new GameObject("Cutscene tablet — Dimari photo");
            Part(PrimitiveType.Cube, "Tablet body", tablet.transform, Vector3.zero, Quaternion.identity,
                new Vector3(.28f, .018f, .20f), tabletBodyMaterial);
            Part(PrimitiveType.Cube, "Tablet photo screen", tablet.transform, new Vector3(0, 0, .011f), Quaternion.identity,
                new Vector3(.245f, .006f, .17f), tabletScreenMaterial);

            panties = new GameObject("Cutscene panties prop");
            Part(PrimitiveType.Cube, "Folded fabric", panties.transform, Vector3.zero, Quaternion.Euler(8, 0, 4),
                new Vector3(.18f, .035f, .13f), pantyFabricMaterial);
            Part(PrimitiveType.Cube, "Waistband", panties.transform, new Vector3(0, .025f, -.055f), Quaternion.identity,
                new Vector3(.19f, .022f, .025f), pantyBandMaterial);
            Part(PrimitiveType.Sphere, "Left leg opening", panties.transform, new Vector3(-.045f, -.022f, .045f), Quaternion.identity,
                new Vector3(.055f, .018f, .05f), pantyBandMaterial);
            Part(PrimitiveType.Sphere, "Right leg opening", panties.transform, new Vector3(.045f, -.022f, .045f), Quaternion.identity,
                new Vector3(.055f, .018f, .05f), pantyBandMaterial);
            tablet.SetActive(false); panties.SetActive(false);
        }

        static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition; part.transform.localRotation = localRotation; part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.Destroy(collider);
            var renderer = part.GetComponent<Renderer>(); if (renderer != null) renderer.sharedMaterial = material;
            return part;
        }

        void LateUpdate()
        {
            if (!running || cam == null || story == null || story.IsMenuOpen || maybeBaby == null || player == null) return;
            float dt = Time.deltaTime;
            Vector3 heroPos = player.transform.position, womanPos = maybeBaby.position;
            Vector3 axis = womanPos - heroPos; axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) axis = player.transform.forward;
            axis.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            Vector3 midpoint = (heroPos + womanPos) * .5f;
            Vector3 heroFace = heroHead != null ? heroHead.position : heroPos + Vector3.up * 1.58f;
            Vector3 womanFace = womanHead != null ? womanHead.position + Vector3.up * .08f : womanPos + Vector3.up * 1.62f;

            poseWeight = Mathf.MoveTowards(poseWeight, 1f, dt * 2.2f);
            bool tabletInHand = tabletActive && tabletBlend < .98f;
            heroArmWeight = Mathf.MoveTowards(heroArmWeight, tabletInHand || (pantyActive && pantyBlend > .85f) ? 1f : 0f, dt * 3f);
            womanArmWeight = Mathf.MoveTowards(womanArmWeight, tabletActive || pantyActive ? 1f : 0f, dt * 3f);
            womanLookDown = Mathf.MoveTowards(womanLookDown, cameraShot == 1 ? 1f : 0f, dt * 2.5f);

            tabletPosition = Vector3.Lerp(
                heroPos + axis * .34f + side * .02f + Vector3.up * 1.24f,
                womanPos - axis * .34f + side * .02f + Vector3.up * 1.24f,
                tabletBlend);
            pantyPosition = Vector3.Lerp(
                womanPos - axis * .28f + side * .16f + Vector3.up * 1.02f,
                heroPos + axis * .30f - side * .10f + Vector3.up * 1.10f,
                pantyBlend);
            if (tablet != null)
            {
                tablet.SetActive(tabletActive);
                tablet.transform.position = tabletPosition;
                Vector3 facing = cam.transform.position - tabletPosition;
                if (facing.sqrMagnitude > 0.001f) tablet.transform.rotation = Quaternion.LookRotation(facing);
            }
            if (panties != null)
            {
                panties.SetActive(pantyActive);
                panties.transform.position = pantyPosition;
                panties.transform.rotation = Quaternion.LookRotation(-axis) * Quaternion.Euler(8f, 0f, Time.time * 25f);
            }

            if (womanHead != null) womanHead.localRotation = womanHeadRest * Quaternion.Euler(womanLookDown * 11f, 0f, 0f);
            float envelope = 0f;
            if (voice != null && voice.isPlaying)
            {
                voice.GetOutputData(samples, 0);
                foreach (float sample in samples) envelope += sample * sample;
                envelope = Mathf.Clamp01(Mathf.Sqrt(envelope / samples.Length) * 12f);
            }
            if (womanJaw != null) womanJaw.localRotation = womanJawRest * Quaternion.Euler(envelope * (womanSpeaking ? 15f : 2f), 0f, 0f);
            if (heroJaw != null) heroJaw.localRotation = heroJawRest * Quaternion.Euler(envelope * (!womanSpeaking ? 10f : 1f), 0f, 0f);

            if (heroUpperArm != null && heroArmWeight > 0.001f)
            {
                heroUpperArm.localRotation = heroUpperRest;
                if (heroLowerArm != null) heroLowerArm.localRotation = heroLowerRest;
                AimArm(heroUpperArm, heroLowerArm, heroHand, tabletInHand ? tabletPosition - axis * .05f : (pantyActive ? pantyPosition - axis * .05f : heroFace + player.transform.forward * .35f), heroArmWeight * poseWeight);
            }
            if (womanUpperArm != null && womanArmWeight > 0.001f)
            {
                womanUpperArm.localRotation = womanUpperRest;
                if (womanLowerArm != null) womanLowerArm.localRotation = womanLowerRest;
                AimArm(womanUpperArm, womanLowerArm, womanHand, pantyActive ? pantyPosition + axis * .05f : tabletPosition + axis * .05f, womanArmWeight * poseWeight);
            }

            UpdateCamera(heroPos, womanPos, axis, side, midpoint, heroFace, womanFace);
        }

        static void AimArm(Transform upper, Transform lower, Transform hand, Vector3 target, float weight)
        {
            if (weight <= 0.001f || upper == null || lower == null || hand == null) return;
            AimBone(upper, lower, target, weight);
            AimBone(lower, hand, target, weight);
        }
        static void AimBone(Transform bone, Transform end, Vector3 target, float weight)
        {
            Vector3 from = end.position - bone.position, to = target - bone.position;
            if (from.sqrMagnitude < .0001f || to.sqrMagnitude < .0001f) return;
            bone.rotation = Quaternion.Slerp(bone.rotation, Quaternion.FromToRotation(from, to) * bone.rotation, weight);
        }

        void UpdateCamera(Vector3 heroPos, Vector3 womanPos, Vector3 axis, Vector3 side, Vector3 midpoint, Vector3 heroFace, Vector3 womanFace)
        {
            Vector3 focus, desired;
            float fov = 46f;
            if (cameraShot == 1 && tablet != null)
            {
                focus = tabletPosition; desired = midpoint + side * 1.05f + Vector3.up * .08f; fov = 41f;
            }
            else if (cameraShot == 2 && panties != null)
            {
                focus = pantyPosition - Vector3.up * .04f; desired = midpoint + side * .95f - Vector3.up * .02f; fov = 38f;
            }
            else if (womanSpeaking)
            {
                focus = womanFace;
                desired = womanPos - axis * 1.45f + side * .72f + Vector3.up * 1.55f;
            }
            else
            {
                focus = heroFace;
                desired = heroPos + axis * 1.45f + side * .72f + Vector3.up * 1.55f;
            }
            Vector3 direction = desired - focus;
            foreach (var hit in Physics.SphereCastAll(focus, .09f, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(player.transform) || hit.transform.IsChildOf(maybeBaby) || hit.transform.IsChildOf(transform)) continue;
                desired = focus + direction.normalized * Mathf.Max(.3f, hit.distance - .12f);
            }
            if (!cameraReady) { cam.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(focus - desired)); cameraReady = true; }
            else
            {
                float blend = 1f - Mathf.Exp(-7f * Time.deltaTime);
                cam.transform.position = Vector3.Lerp(cam.transform.position, desired, blend);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(focus - cam.transform.position), blend);
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        void Release()
        {
            if (!running) return;
            running = false; IsRunning = false; subtitle = ""; paused = false;
            if (voice != null) voice.Stop();
            if (player != null)
            {
                player.IsControlEnabled = oldControl;
                player.transform.position = oldPlayerPosition;
                player.transform.rotation = oldPlayerRotation;
            }
            if (maybeBaby != null) maybeBaby.rotation = oldWomanRotation;
            if (view != null)
            {
                view.IsCutsceneControlled = oldCutscene;
                if (!oldCutscene) view.BeginGameplayTransitionFromCutscene();
            }
            if (cam != null) { cam.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation); cam.fieldOfView = oldFov; }
            if (heroRenderers != null) for (int i = 0; i < heroRenderers.Length; i++) if (heroRenderers[i] != null) heroRenderers[i].enabled = heroVisibility[i];
            if (tablet != null) Destroy(tablet);
            if (panties != null) Destroy(panties);
            DestroyMaterial(tabletScreenMaterial); DestroyMaterial(tabletBodyMaterial);
            DestroyMaterial(pantyFabricMaterial); DestroyMaterial(pantyBandMaterial);
            tablet = null; panties = null; tabletActive = false; pantyActive = false;
            poseWeight = 0f; heroArmWeight = 0f; womanArmWeight = 0f; womanLookDown = 0f; cameraReady = false;
            if (womanJaw != null) womanJaw.localRotation = womanJawRest;
            if (heroJaw != null) heroJaw.localRotation = heroJawRest;
            if (womanHead != null) womanHead.localRotation = womanHeadRest;
            if (heroUpperArm != null) { heroUpperArm.localRotation = heroUpperRest; if (heroLowerArm != null) heroLowerArm.localRotation = heroLowerRest; }
            if (womanUpperArm != null) { womanUpperArm.localRotation = womanUpperRest; if (womanLowerArm != null) womanLowerArm.localRotation = womanLowerRest; }
        }
        static void DestroyMaterial(Material m) { if (m != null) UnityEngine.Object.Destroy(m); }

        void OnDisable() { StopAllCoroutines(); Release(); }

        void OnGUI()
        {
            if (story == null || story.IsMenuOpen || !running) return;
            GUI.depth = -260;
            GUI.Box(new Rect(Screen.width * .06f, Screen.height * .75f, Screen.width * .88f, Screen.height * .2f), subtitle,
                new GUIStyle(GUI.skin.box) { fontSize = Mathf.Clamp(Screen.height / 40, 18, 28), wordWrap = true, alignment = TextAnchor.MiddleCenter });
        }
    }
}
