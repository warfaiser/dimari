using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.Cinematics
{
    public class OfficeFishCutscene : MonoBehaviour
    {
        public static bool Completed;
        ForestPlayerController player;
        ForestCameraController view;
        Camera cam;
        StoryQuestManager story;
        string cityPath;
        Vector3 returnPosition;
        Quaternion returnRotation;
        Animator actor;
        Transform head, jaw, hand, upperArm, lowerArm;
        Quaternion jawRest, headRest, armRest, forearmRest;
        readonly List<SkinnedMeshRenderer> mouths = new List<SkinnedMeshRenderer>();
        readonly List<int> mouthIndices = new List<int>();
        AudioSource speech;
        float envelope;
        bool speaking, eating, running, exiting;
        readonly float[] audioSamples = new float[256];
        string subtitle;
        Transform fish;
        Vector3 fishRest;
        string[] lines = {
            "Так-так-так, во и моя рыбка тухлая на тарелке, наконец-то я её съем.",
            "Здравствуйте, ой, что же у вас так воняет, рыба что ли тухлая?",
            "Ты что? Это моё фирменное блюдо — рыбка под блевотным соусом от бомжа, угощайся.",
            "Нет, спасибо, откажусь. У меня к вам вопрос: вы не видели Димарика, вашего коллегу? Со вчерашнего дня не могу найти его.",
            "Да, видела его вчера. Сидел, бегал вприпрыжку, но потом ему кто-то позвонил, и он вышел. После звонка он вернулся в кабинет будто дёрганный: вены вздулись, покраснел.",
            "А куда ушёл, не сказал?",
            "Услышала, что он бормотал под нос, говорит, что нужно срочно купить пиццу. Возможно, он поехал в любимое место — «Пицца Экспресс 24», говорит, что там очень вкусная пицца. Поищи его там.",
            "Спасибо большое. Удавись в переваривании блядского поноса."
        };

        public static void Begin(ForestPlayerController player, Camera camera, StoryQuestManager story,
            string city, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Dimari_OfficeFishCutscene");
            var scene = go.AddComponent<OfficeFishCutscene>();
            scene.player = player; scene.cam = camera; scene.story = story;
            scene.cityPath = city; scene.returnPosition = position; scene.returnRotation = rotation;
            scene.StartCoroutine(scene.Play());
        }

        Transform Find(string name)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        IEnumerator Play()
        {
            Transform woman = Find("Dimari_SeatedAdministrationWoman");
            if (woman == null || cam == null || player == null) { Debug.LogError("[Dimari] Для кат-сцены нужна посаженная женщина и камера героя."); yield break; }
            actor = woman.GetComponentInChildren<Animator>();
            if (actor == null || !actor.isHuman) yield break;
            head = actor.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) yield break;
            jaw = actor.GetBoneTransform(HumanBodyBones.Jaw);
            if (jaw != null) jawRest = jaw.localRotation;
            headRest = head.localRotation;
            hand = actor.GetBoneTransform(HumanBodyBones.RightHand);
            upperArm = actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lowerArm = actor.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (upperArm != null) armRest = upperArm.localRotation;
            if (lowerArm != null) forearmRest = lowerArm.localRotation;
            foreach (SkinnedMeshRenderer mesh in woman.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (mesh.sharedMesh == null) continue;
                for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                {
                    string n = mesh.sharedMesh.GetBlendShapeName(i).ToLowerInvariant();
                    if (n.Contains("jawopen") || n.Contains("mouthopen") || n.Contains("mouth_open"))
                    { mouths.Add(mesh); mouthIndices.Add(i); break; }
                }
            }
            if (jaw == null && mouths.Count == 0) Debug.LogWarning("[Dimari] Нет Jaw / mouthOpen: движение рта недоступно для этой модели.");
            NeutralLighting();
            AddPhoto(woman);
            CreateMeal(woman);
            speech = gameObject.AddComponent<AudioSource>(); speech.spatialBlend = 0; speech.volume = 1;
            view = cam.GetComponent<ForestCameraController>();
            player.StopLocomotionAnimation(); player.IsControlEnabled = false;
            if (view != null) view.IsCutsceneControlled = true;
            running = true;
            story.SetInteractionPrompt("");
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < lines.Length; i++)
            {
                speaking = i % 2 == 0;
                eating = i == 0 || i == 1 || i == 5;
                subtitle = (speaking ? "Обжорка Просрочки" : "Главный герой") + ":\n" + lines[i];
                AudioClip clip = Resources.Load<AudioClip>("Voice_Office_" + i);
                speech.clip = clip;
                if (clip != null) speech.Play();
                float duration = clip != null ? clip.length : Mathf.Max(3, lines[i].Length / 12f);
                Vector3 forward = woman.forward;
                // Close, elevated three-quarter shots stay on the actor's side of the monitor.
                // Do not cross behind the screen when the speaker changes.
                Vector3 destination = head.position + forward * (speaking ? 0.72f : 0.85f)
                    + woman.right * (speaking ? 0.72f : 0.82f) + Vector3.up * 0.32f;
                cam.fieldOfView = 48f;
                Vector3 start = cam.transform.position;
                for (float t = 0; t < duration + 0.35f; t += Time.deltaTime)
                {
                    cam.transform.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.7f)));
                    cam.transform.rotation = Quaternion.LookRotation(head.position + Vector3.up * 0.07f - cam.transform.position);
                    envelope = 0;
                    if (clip != null && speech.isPlaying)
                    {
                        speech.GetOutputData(audioSamples, 0);
                        float sum = 0;
                        foreach (float sample in audioSamples) sum += sample * sample;
                        envelope = Mathf.Clamp01(Mathf.Sqrt(sum / audioSamples.Length) * 9);
                    }
                    yield return null;
                }
            }
            speaking = eating = false; envelope = 0; subtitle = "";
            yield return ReturnToCity();
        }

        void LateUpdate()
        {
            if (!running || head == null) return;
            float open = speaking ? envelope : eating ? 0.25f + Mathf.Abs(Mathf.Sin(Time.time * 7)) * 0.4f : 0;
            if (jaw != null) jaw.localRotation = jawRest * Quaternion.Euler(open * 16, 0, 0);
            foreach (var mesh in mouths) if (mesh != null) mesh.SetBlendShapeWeight(mouthIndices[mouths.IndexOf(mesh)], open * 65);
            head.localRotation = headRest * Quaternion.Euler(Mathf.Sin(Time.time * 2) * 2, Mathf.Sin(Time.time * 0.9f) * 3, 0);
            if (upperArm != null && lowerArm != null && hand != null)
            {
                upperArm.localRotation = armRest; lowerArm.localRotation = forearmRest;
                float lift = eating ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(Time.time * 1.5f)), 2) : 0;
                Vector3 target = Vector3.Lerp(fishRest + Vector3.up * 0.12f, head.position + actor.transform.forward * 0.16f - Vector3.up * 0.1f, lift);
                if (speaking && !eating) target += Vector3.up * (0.04f + 0.035f * Mathf.Sin(Time.time * 3)) + actor.transform.right * (0.025f * Mathf.Sin(Time.time * 2));
                SolveArm(upperArm, lowerArm, hand, target, actor.transform.right + actor.transform.forward * 0.3f);
                if (fish != null) fish.position = lift > 0.1f ? hand.position : fishRest;
            }
        }

        static void SolveArm(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            float l1 = Vector3.Distance(a.position, b.position), l2 = Vector3.Distance(b.position, c.position);
            if (l1 < 0.001f || l2 < 0.001f) return;
            Vector3 delta = target - a.position;
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            float x = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            Vector3 elbow = a.position + direction * x + bend * Mathf.Sqrt(Mathf.Max(0, l1 * l1 - x * x));
            a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
        }

        void NeutralLighting()
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.5f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = 0;
            LightmapSettings.lightmaps = new LightmapData[0];
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>(true)) probe.enabled = false;
                foreach (Volume volume in root.GetComponentsInChildren<Volume>(true)) volume.enabled = false;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.lightmapIndex = -1;
                    foreach (Material material in renderer.materials)
                    {
                        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                        if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", new Color(0.12f, 0.12f, 0.12f));
                        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
                    }
                }
            }
            var lamp = new GameObject("Neutral office key").AddComponent<Light>();
            lamp.transform.SetParent(transform); lamp.type = LightType.Point;
            lamp.color = new Color(1, 0.98f, 0.93f); lamp.intensity = 1.4f; lamp.range = 9;
            lamp.transform.position = head.position + actor.transform.forward * 1.8f + Vector3.up * 0.8f;
            lamp.shadows = LightShadows.None;
        }

        Material Mat(Color color, bool unlit = false)
        {
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader); mat.color = color; return mat;
        }
        GameObject Prop(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(transform);
            go.transform.position = position; go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().material = Mat(color); return go;
        }
        void CreateMeal(Transform woman)
        {
            Transform desk = Find("OfficeDesk_1");
            float y = head.position.y - 0.55f;
            if (desk != null && desk.TryGetComponent<Renderer>(out var renderer)) y = renderer.bounds.max.y + 0.025f;
            Vector3 plate = woman.position + woman.forward * 0.52f + woman.right * 0.3f;
            plate.y = y;
            Prop(PrimitiveType.Cylinder, "Dirty fish plate", plate, new Vector3(0.32f, 0.009f, 0.32f), new Color(0.5f, 0.48f, 0.3f));
            Prop(PrimitiveType.Sphere, "Spoiled sauce", plate + Vector3.up * 0.015f, new Vector3(0.24f, 0.016f, 0.18f), new Color(0.28f, 0.32f, 0.06f));
            fishRest = plate + Vector3.up * 0.045f;
            fish = Prop(PrimitiveType.Sphere, "Rotten fish", fishRest, new Vector3(0.23f, 0.065f, 0.08f), new Color(0.32f, 0.38f, 0.18f)).transform;
            var eye = Prop(PrimitiveType.Sphere, "Clouded fish eye", fishRest + new Vector3(0.075f, 0.025f, -0.03f), Vector3.one * 0.015f, Color.gray);
            eye.transform.SetParent(fish, true);
            var tail = Prop(PrimitiveType.Cube, "Fish tail", fishRest + Vector3.left * 0.12f, new Vector3(0.05f, 0.016f, 0.09f), new Color(0.24f, 0.28f, 0.12f));
            tail.transform.SetParent(fish, true);
            var smell = new GameObject("Green stink"); smell.transform.SetParent(transform); smell.transform.position = plate + Vector3.up * 0.1f;
            var ps = smell.AddComponent<ParticleSystem>(); var main = ps.main;
            main.startColor = new Color(0.34f, 0.48f, 0.09f, 0.18f); main.startLifetime = 1.8f; main.startSpeed = 0.16f; main.startSize = 0.055f; main.maxParticles = 30;
            var emission = ps.emission; emission.rateOverTime = 8;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.08f;
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader != null)
            {
                Material mat = new Material(particleShader);
                mat.SetFloat("_Surface", 1); mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0); mat.renderQueue = 3000; mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                ps.GetComponent<ParticleSystemRenderer>().material = mat;
            }
        }
        void AddPhoto(Transform woman)
        {
            Transform frame = Find("Frame 1");
            if (frame == null) { Debug.LogWarning("[Dimari] Не найдена рамка Frame 1 для фото."); return; }
            Renderer original = frame.GetComponentInChildren<Renderer>();
            if (original == null) return;
            Bounds b = original.bounds;
            Texture photo = Resources.Load<Texture2D>("OfficeWallPhoto");
            if (photo == null) return;
            // The thinnest horizontal axis of the old frame is the wall normal.
            // A vector toward the actor is NOT the wall normal and makes the image turn edge-on.
            Vector3 normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            if (Vector3.Dot(normal, head.position - b.center) < 0) normal = -normal;
            foreach (Renderer old in frame.GetComponentsInChildren<Renderer>()) old.enabled = false;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Office wall photograph";
            Destroy(quad.GetComponent<Collider>()); quad.transform.SetParent(transform);
            quad.transform.position = b.center + normal * (Mathf.Min(b.size.x, b.size.z) * 0.5f + 0.035f);
            quad.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            float width = 1.15f;
            float height = width * photo.height / photo.width;
            quad.transform.localScale = new Vector3(width, height, 1);
            var material = Mat(Color.white, true); material.mainTexture = photo; quad.GetComponent<Renderer>().material = material;
        }

        IEnumerator ReturnToCity()
        {
            subtitle = "Вы выходите из администрации…";
            exiting = true;
            if (!Application.CanStreamedLevelBeLoaded(cityPath))
            {
                Debug.LogError("[Dimari] Город не добавлен в Scene List: " + cityPath);
                Unlock(); yield break;
            }
            yield return SceneManager.LoadSceneAsync(cityPath, LoadSceneMode.Additive);
            Scene city = SceneManager.GetSceneByPath(cityPath);
            Scene office = gameObject.scene;
            foreach (GameObject root in city.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
            foreach (Transform t in new[] { story.transform, player.transform, cam.transform })
            { t.SetParent(null, true); SceneManager.MoveGameObjectToScene(t.gameObject, city); }
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(returnPosition, returnRotation); player.ResetWaterMotion();
            if (controller != null) controller.enabled = true;
            SceneManager.SetActiveScene(city);
            story.questSteps.Clear();
            story.questSteps.Add(new StoryQuestManager.QuestStep { id = "city_pizza_express", title = "Найти «Пицца Экспресс 24» и разыскать Димарика." });
            Completed = true;
            cam.transform.SetPositionAndRotation(returnPosition + Vector3.up * 1.6f, returnRotation);
            Unlock();
            SceneManager.UnloadSceneAsync(office);
        }
        void Unlock()
        {
            running = false; exiting = false; subtitle = "";
            if (player != null) player.IsControlEnabled = true;
            if (view != null) view.BeginGameplayTransitionFromCutscene();
        }
        void OnGUI()
        {
            if (!running || string.IsNullOrEmpty(subtitle)) return;
            GUI.depth = -250;
            if (exiting)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * 0.075f), Texture2D.whiteTexture);
            Rect box = new Rect(Screen.width * 0.1f, Screen.height * 0.76f, Screen.width * 0.8f, Screen.height * 0.19f);
            GUI.color = new Color(0, 0, 0, 0.88f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(box, subtitle, new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(Screen.height / 37, 18, 30), alignment = TextAnchor.MiddleCenter, wordWrap = true });
        }
        void OnDisable() { if (running) Unlock(); }
    }
}
