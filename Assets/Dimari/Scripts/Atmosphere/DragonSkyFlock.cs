using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Dimari.Atmosphere
{
    /// <summary>Decorative sky flock. No attacks, physics, or player input controllers.</summary>
    public sealed class DragonSkyFlock : MonoBehaviour
    {
        public GameObject dragonPrefab;
        public AnimationClip flightClip;
        [Range(1, 40)] public int dragonCount = 18;
        public float flightHeight = 85f;
        public float minimumRadius = 100f;
        public float maximumRadius = 310f;
        public float wingspan = 14f;
        [Tooltip("Adjust if the imported model faces sideways or backwards.")]
        public Vector3 modelRotationOffset;

        private sealed class Flyer
        {
            public Transform root;
            public float phase, radius, height, speed;
        }
        private readonly List<Flyer> _flyers = new List<Flyer>();
        private readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>();
        private sealed class FlightAnimation
        {
            public PlayableGraph graph;
            public AnimationClipPlayable playable;
            public float length, time, speed;
        }
        private readonly List<FlightAnimation> _animations = new List<FlightAnimation>();
        private Vector3 _center;
        private float _skyFloor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureFlock()
        {
            if (FindAnyObjectByType<DragonSkyFlock>() == null)
                new GameObject("Dimari_DragonSkyFlock").AddComponent<DragonSkyFlock>();
        }

        private void Start()
        {
            if (dragonPrefab == null) dragonPrefab = Resources.Load<GameObject>("Dimari_Dragon");
#if UNITY_EDITOR
            if (dragonPrefab == null) dragonPrefab = FindImportedDragon();
#endif
            if (dragonPrefab == null)
            {
                Debug.LogWarning("[Dimari] Dragon PBR не найден. Назначьте Dragon Prefab у DragonSkyFlock или используйте Dimari/Драконы — настроить стаю в сцене.");
                enabled = false;
                return;
            }
            var player = FindAnyObjectByType<Dimari.Player.ForestPlayerController>();
            _center = player != null ? player.transform.position : Vector3.zero;
            _skyFloor = _center.y;
            foreach (Terrain terrain in Terrain.activeTerrains)
                _skyFloor = Mathf.Max(_skyFloor, terrain.transform.position.y + terrain.terrainData.bounds.max.y);
            // Static cliffs can rise well above the terrain. Use collider bounds conservatively.
            foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.isTrigger || collider.GetComponentInParent<Dimari.Player.ForestPlayerController>() != null) continue;
                if (Vector3.Distance(new Vector3(collider.bounds.center.x, _center.y, collider.bounds.center.z), _center) < maximumRadius + 100f)
                    _skyFloor = Mathf.Max(_skyFloor, collider.bounds.max.y);
            }
            for (int i = 0; i < Mathf.Clamp(dragonCount, 1, 40); i++) Spawn(i);
        }

        private void Spawn(int index)
        {
            Transform pivot = new GameObject("SkyDragon_" + (index + 1)).transform;
            pivot.SetParent(transform, false);
            GameObject model = Instantiate(dragonPrefab, pivot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(modelRotationOffset);
            foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) script.enabled = false;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            foreach (Camera camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (AudioListener listener in model.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (AudioSource audio in model.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (size > 0.001f) model.transform.localScale *= wingspan / size * Random.Range(0.8f, 1.25f);
                foreach (Renderer renderer in renderers)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Material[] materials = renderer.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++) materials[m] = CompatibleMaterial(materials[m]);
                    renderer.sharedMaterials = materials;
                }
            }
            bool hasFlight = false;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = Random.Range(0.8f, 1.2f);
                AnimationClip flight = flightClip;
                if (flight == null && animator.runtimeAnimatorController != null)
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    {
                        string name = clip.name.ToLowerInvariant();
                        if (name.Contains("fly") || name.Contains("flight") || name.Contains("glide"))
                        { flight = clip; break; }
                    }
#if UNITY_EDITOR
                if (flight == null)
                {
                    foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AnimationClip"))
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        if (!path.ToLowerInvariant().Contains("dragon")) continue;
                        foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                        {
                            AnimationClip clip = asset as AnimationClip;
                            if (clip == null) continue;
                            string name = clip.name.ToLowerInvariant();
                            if (name.Contains("fly") || name.Contains("flight") || name.Contains("glide"))
                            { flight = clip; break; }
                        }
                        if (flight != null) break;
                    }
                }
#endif
                if (flight == null || flight.length <= 0f) continue;
                PlayableGraph graph = PlayableGraph.Create("DragonFlight");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, flight);
                playable.SetApplyFootIK(false);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Wings", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                _animations.Add(new FlightAnimation { graph = graph, playable = playable,
                    length = flight.length, time = Random.value * flight.length, speed = animator.speed });
                hasFlight = true;
            }
            if (!hasFlight && index == 0)
                Debug.LogWarning("[Dimari] У Dragon PBR не найдена подключённая анимация полёта. Драконы двигаются по небу, но взмахи крыльев не гарантированы.");
            Flyer flyer = new Flyer
            {
                root = pivot, phase = index * Mathf.PI * 2f / Mathf.Max(1, dragonCount),
                radius = Random.Range(minimumRadius, Mathf.Max(minimumRadius + 1f, maximumRadius)),
                height = Random.Range(0f, 45f), speed = Random.Range(0.025f, 0.055f)
            };
            _flyers.Add(flyer);
            UpdateFlyer(flyer);
        }

        private Material CompatibleMaterial(Material original)
        {
            if (original == null) return null;
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null || original.shader == null || original.shader.name.Contains("Universal Render Pipeline")) return original;
            if (_materials.TryGetValue(original, out Material cached)) return cached;
            Material material = new Material(urp) { name = original.name + "_SkyURP" };
            if (original.HasProperty("_Color")) material.SetColor("_BaseColor", original.GetColor("_Color"));
            if (original.HasProperty("_MainTex"))
            {
                material.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
                material.SetTextureScale("_BaseMap", original.GetTextureScale("_MainTex"));
                material.SetTextureOffset("_BaseMap", original.GetTextureOffset("_MainTex"));
            }
            if (original.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", original.GetTexture("_BumpMap"));
                if (original.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
            }
            _materials.Add(original, material);
            return material;
        }

        private void Update()
        {
            foreach (FlightAnimation animation in _animations)
            {
                animation.time = Mathf.Repeat(animation.time + Time.deltaTime * animation.speed, animation.length);
                animation.playable.SetTime(animation.time);
                animation.graph.Evaluate(0f);
            }
            foreach (Flyer flyer in _flyers)
            {
                flyer.phase += Time.deltaTime * flyer.speed;
                UpdateFlyer(flyer);
            }
        }

        private void UpdateFlyer(Flyer flyer)
        {
            float a = flyer.phase;
            Vector3 position = _center + new Vector3(Mathf.Cos(a) * flyer.radius,
                _skyFloor - _center.y + Mathf.Max(35f, flightHeight) + flyer.height + Mathf.Sin(a * 3f) * 4f,
                Mathf.Sin(a) * flyer.radius * 0.8f);
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0.03f * Mathf.Cos(a * 3f), Mathf.Cos(a) * 0.8f);
            flyer.root.SetPositionAndRotation(position, Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, -8f));
        }

        private void OnDestroy()
        {
            foreach (FlightAnimation animation in _animations)
                if (animation.graph.IsValid()) animation.graph.Destroy();
            foreach (Material material in _materials.Values) if (material != null) Destroy(material);
        }

#if UNITY_EDITOR
        public static GameObject FindImportedDragon()
        {
            GameObject best = null;
            int score = -1;
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                if (!lower.Contains("dragon") || lower.Contains("head") || lower.Contains("skyflock")) continue;
                GameObject candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate == null || candidate.GetComponentInChildren<Renderer>() == null) continue;
                int candidateScore = lower.Contains("pbr") ? 50 : 0;
                if (candidate.GetComponentInChildren<SkinnedMeshRenderer>() != null) candidateScore += 30;
                if (candidate.GetComponentInChildren<Animator>() != null) candidateScore += 10;
                if (candidateScore > score) { score = candidateScore; best = candidate; }
            }
            return best;
        }
#endif
    }
}
