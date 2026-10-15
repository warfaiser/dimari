using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Dimari.Core;
using Dimari.Player;
using Dimari.Atmosphere;
using Dimari.Cinematics;

namespace Dimari.Story
{
    /// <summary>
    /// Управляет:
    /// 1. Списком текущих задач в левом верхнем углу экрана («Текущие задачи:»):
    ///    1. Найти рюкзак.
    ///    2. Найти планшет.
    ///    3. Дойти до точки перехода во 2-ю главу: «Городок Димарика, Рогово».
    /// 2. Анимацией поднятия предметов (точные векторы костей через FromToRotation):
    ///    - При поднятии рюкзака персонаж наклоняется, берёт рюкзак рукой и надевает его себе на спину!
    ///    - При поднятии планшета персонаж наклоняется, берёт планшет и ДЕРЖИТ ЕГО ПЕРЕД СОБОЙ ДВУМЯ РУКАМИ (левая и правая кисти держат края 3D-планшета перед грудью), а на экране светится загруженная фотография!
    /// 3. Субтитрами в нижней части экрана и Главным меню / Паузой.
    /// </summary>
    public class StoryQuestManager : MonoBehaviour
    {
        public static StoryQuestManager Instance { get; private set; }

        [Serializable]
        public class QuestStep
        {
            public string id;
            public string title;
            public bool isCompleted;
        }

        [Header("Текущие задачи (верхний левый угол)")]
        public string questHeader = "Текущие задачи:";
        public List<QuestStep> questSteps = new List<QuestStep>();

        public bool IsMenuOpen => _isMenuOpen;
        public bool HasGameStarted => _hasGameStarted;

        private string _currentSubtitle = "";
        private float _subtitleTimer = 0f;
        private float _subtitleAlpha = 0f;

        private string _interactionPrompt = "";
        private bool _showHUD = false;
        private bool _isMenuOpen = true;
        private bool _isSettingsOpen = false;
        private bool _hasGameStarted = false;
        private bool _isChapter2Reached = false;

        // Состояние планшета и рюкзака
        private bool _isTabletViewerOpen = false;
        private bool _hasPickedUpTablet = false;
        private bool _isHoldingTabletInHands = false;
        private float _holdTabletBlend = 0f;
        private bool _hasPickedUpBackpack = false;

        // Состояние анимации поднятия предмета (рюкзака / планшета)
        private bool _isPlayingPickupAnim = false;
        private float _pickupAnimTimer = 0f;
        private float _pickupAnimDuration = 1.25f;
        private Vector3 _pickupTargetWorldPos;
        private Transform _equippedBackpackTransform;
        private Transform _equippedTabletTransform;
        private Transform _characterChestBone;
        private Transform _characterLeftHandBone;
        private Transform _characterRightHandBone;
        private Animator _characterAnimator;

        private string _statusToast = "";
        private float _statusToastTimer = 0f;
        private int _selectedMenuIndex = 0;

        private float _menuCamOrbitTimer = 0f;

        // Текстуры для UI и экрана планшета
        [NonSerialized] private Texture2D _tabletPhotoTex;
        [NonSerialized] private Texture2D _darkPanelTex;
        [NonSerialized] private Texture2D _subtitlePanelTex;
        [NonSerialized] private Texture2D _tabletBezelTex;
        [NonSerialized] private Texture2D _menuLeftGradientTex;
        [NonSerialized] private Texture2D _buttonHoverBarTex;
        [NonSerialized] private Texture2D _accentBarTex;
        [NonSerialized] private Texture2D _goldAccentTex;
        [NonSerialized] private Texture2D _logoBoxTex;
        [NonSerialized] private Texture2D _keyCapTex;

        // Стили UI ([NonSerialized], чтобы Unity не сбрасывала их в чёрный 12px шрифт)
        [NonSerialized] private bool _stylesInitialized = false;
        [NonSerialized] private GUIStyle _logoMainStyle;
        [NonSerialized] private GUIStyle _menuBtnStyle;
        [NonSerialized] private GUIStyle _menuBtnHoverStyle;
        [NonSerialized] private GUIStyle _questHeaderStyle;
        [NonSerialized] private GUIStyle _questActiveStyle;
        [NonSerialized] private GUIStyle _questDoneStyle;
        [NonSerialized] private GUIStyle _subtitleSpeakerStyle;
        [NonSerialized] private GUIStyle _subtitleStyle;
        [NonSerialized] private GUIStyle _promptStyle;
        [NonSerialized] private GUIStyle _bottomBarTextStyle;
        [NonSerialized] private GUIStyle _keyCapStyle;
        [NonSerialized] private GUIStyle _settingsLabelStyle;
        [NonSerialized] private GUIStyle _chapter2TitleStyle;

        private ForestPlayerController _player;
        private ForestCameraController _cameraController;
        private ForestAwakeningDirector _director;
        private Camera _mainCam;

        private void Awake()
        {
            Instance = this;
            SetupDimariQuests();
        }

        private void Start()
        {
            _player = FindAnyObjectByType<ForestPlayerController>();
            _cameraController = FindAnyObjectByType<ForestCameraController>();
            _director = FindAnyObjectByType<ForestAwakeningDirector>();
            _mainCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

            CacheCharacterBones();
            LoadTabletPhotoTexture();
            SetupDimariQuests();
            RemoveStumpAndSpawnUpdatedStoryProps();
            OpenMainMenuOnStartup();
        }

        private void CacheCharacterBones()
        {
            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_player == null) return;

            _characterAnimator = _player.GetComponentInChildren<Animator>();
            if (_characterAnimator != null && _characterAnimator.isHuman)
            {
                _characterChestBone = _characterAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (_characterChestBone == null)
                    _characterChestBone = _characterAnimator.GetBoneTransform(HumanBodyBones.Chest);
                if (_characterChestBone == null)
                    _characterChestBone = _characterAnimator.GetBoneTransform(HumanBodyBones.Spine);

                _characterLeftHandBone = _characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                _characterRightHandBone = _characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }

        /// <summary>
        /// Загружает фотографию для экрана планшета:
        /// 1) Из Resources/Dimari_Tablet_Photo
        /// 2) Через AssetDatabase (в редакторе Unity)
        /// 3) Напрямую с диска из корня репозитория (77_zgLAwtLG4SHdKxcYs440fepSvHjQnqD4DSeWPWR5bn5onw9wJ-JoYUQJVeecHzUhO8YtyfQXi5WTlK02S4-D-.jpg)
        /// </summary>
        private Texture2D LoadTabletPhotoTexture()
        {
            if (_tabletPhotoTex != null) return _tabletPhotoTex;

            _tabletPhotoTex = Resources.Load<Texture2D>("Dimari_Tablet_Photo");
            if (_tabletPhotoTex != null) return _tabletPhotoTex;

#if UNITY_EDITOR
            string[] possibleAssetPaths = new string[]
            {
                "Assets/Dimari/Resources/Dimari_Tablet_Photo.jpg",
                "Assets/77_zgLAwtLG4SHdKxcYs440fepSvHjQnqD4DSeWPWR5bn5onw9wJ-JoYUQJVeecHzUhO8YtyfQXi5WTlK02S4-D-.jpg"
            };
            foreach (string ap in possibleAssetPaths)
            {
                Texture2D loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ap);
                if (loaded != null)
                {
                    _tabletPhotoTex = loaded;
                    return _tabletPhotoTex;
                }
            }

            string[] foundGuids = UnityEditor.AssetDatabase.FindAssets("77_zgLAwtLG4 t:Texture2D");
            if (foundGuids != null && foundGuids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(foundGuids[0]);
                _tabletPhotoTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (_tabletPhotoTex != null) return _tabletPhotoTex;
            }
#endif

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string[] candidateFiles = new string[]
                {
                    Path.Combine(projectRoot, "77_zgLAwtLG4SHdKxcYs440fepSvHjQnqD4DSeWPWR5bn5onw9wJ-JoYUQJVeecHzUhO8YtyfQXi5WTlK02S4-D-.jpg"),
                    Path.Combine(Application.dataPath, "Dimari", "Resources", "Dimari_Tablet_Photo.jpg"),
                    Path.Combine(Application.dataPath, "77_zgLAwtLG4SHdKxcYs440fepSvHjQnqD4DSeWPWR5bn5onw9wJ-JoYUQJVeecHzUhO8YtyfQXi5WTlK02S4-D-.jpg")
                };

                string fileToLoad = null;
                foreach (string f in candidateFiles)
                {
                    if (File.Exists(f))
                    {
                        fileToLoad = f;
                        break;
                    }
                }

                if (fileToLoad == null)
                {
                    string[] rootJpgs = Directory.GetFiles(projectRoot, "*.jpg", SearchOption.TopDirectoryOnly);
                    if (rootJpgs.Length > 0) fileToLoad = rootJpgs[0];
                }

                if (!string.IsNullOrEmpty(fileToLoad) && File.Exists(fileToLoad))
                {
                    byte[] bytes = File.ReadAllBytes(fileToLoad);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, bytes))
                    {
                        tex.name = "Dimari_Tablet_Photo_Runtime";
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                        _tabletPhotoTex = tex;
                        return _tabletPhotoTex;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dimari] Не удалось загрузить фото с диска: " + e.Message);
            }

            return _tabletPhotoTex;
        }

        public void InitializeDefaultQuests()
        {
            SetupDimariQuests();
        }

        public void SetupDimariQuests()
        {
            questHeader = "Текущие задачи:";
            questSteps.Clear();
            questSteps.Add(new QuestStep
            {
                id = "find_backpack",
                title = "1. Найти рюкзак.",
                isCompleted = false
            });
            questSteps.Add(new QuestStep
            {
                id = "find_tablet",
                title = "2. Найти планшет.",
                isCompleted = false
            });
            questSteps.Add(new QuestStep
            {
                id = "rogovo_exit",
                title = "3. Дойти до точки перехода во 2-ю главу: «Городок Димарика, Рогово».",
                isCompleted = false
            });
        }

        public void RemoveStumpAndSpawnUpdatedStoryProps()
        {
            if (_equippedBackpackTransform != null)
            {
                DestroyImmediate(_equippedBackpackTransform.gameObject);
                _equippedBackpackTransform = null;
            }
            if (_equippedTabletTransform != null)
            {
                DestroyImmediate(_equippedTabletTransform.gameObject);
                _equippedTabletTransform = null;
            }
            _hasPickedUpBackpack = false;
            _hasPickedUpTablet = false;
            _isHoldingTabletInHands = false;
            _holdTabletBlend = 0f;
            _isTabletViewerOpen = false;

            GameObject oldStoryRoot = GameObject.Find("Dimari_StoryObjects");
            if (oldStoryRoot != null)
            {
                DestroyImmediate(oldStoryRoot);
            }
            GameObject oldStoryRootV2 = GameObject.Find("Dimari_StoryObjects_V2");
            if (oldStoryRootV2 != null)
            {
                DestroyImmediate(oldStoryRootV2);
            }

            GameObject[] allObjects = FindObjectsByType<GameObject>();
            foreach (GameObject go in allObjects)
            {
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();
                if (n.Contains("storyprop_ancientstone") || n.Contains("pedestal") || n.Contains("turntable") || n.Contains("showcase_stump"))
                {
                    go.SetActive(false);
                }
            }

            if (_player == null)
            {
                _player = FindAnyObjectByType<ForestPlayerController>();
            }

            Vector3 pPos = _player != null ? _player.transform.position : Vector3.zero;
            Vector3 forward = _player != null ? _player.transform.forward : Vector3.forward;
            Vector3 right = _player != null ? _player.transform.right : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            right = Vector3.Cross(Vector3.up, forward).normalized;

            GameObject storyRoot = new GameObject("Dimari_StoryObjects_V2");

            // =========================================================================
            // 1. РЮКЗАК (Задача 1: Найти и поднять рюкзак)
            // =========================================================================
            Vector3 backpackPos = GetGroundPoint(pPos + forward * 4.5f + right * 1.4f, pPos.y);
            GameObject backpackObj = new GameObject("Quest1_Backpack");
            backpackObj.transform.SetParent(storyRoot.transform);
            backpackObj.transform.position = backpackPos;
            backpackObj.transform.rotation = Quaternion.Euler(0f, 32f, 0f);

            bool spawnedAssetBag = TrySpawnMixallBagPrefab(backpackObj.transform);
            if (!spawnedAssetBag)
            {
                CreateDetailedProceduralBackpack(backpackObj.transform);
            }

            BoxCollider bagTriggerCol = backpackObj.AddComponent<BoxCollider>();
            bagTriggerCol.center = new Vector3(0f, 0.25f, 0f);
            bagTriggerCol.size = new Vector3(0.65f, 0.55f, 0.55f);

            InteractableStoryObject bagInteract = backpackObj.AddComponent<InteractableStoryObject>();
            bagInteract.questStepId = "find_backpack";
            bagInteract.objectTitle = "Потерянный рюкзак (Поднять)";
            bagInteract.inspectMonologue = "«Отлично, мой рюкзак! Надену его на плечи. Внутри ключи и фляжка, но планшета тут нет — поищу его в траве дальше по тропе.»";

            // =========================================================================
            // 2. ПЛАНШЕТ С ФОТОГРАФИЕЙ (Задача 2: Найти и взять планшет в руки)
            // =========================================================================
            Vector3 tabletPos = GetGroundPoint(pPos + forward * 9.5f - right * 2.1f, pPos.y);
            GameObject tabletObj = new GameObject("Quest2_Tablet");
            tabletObj.transform.SetParent(storyRoot.transform);
            tabletObj.transform.position = tabletPos;
            tabletObj.transform.rotation = Quaternion.Euler(0f, -24f, 0f);

            CreateDetailedTabletModel(tabletObj.transform);

            BoxCollider tabCol = tabletObj.AddComponent<BoxCollider>();
            tabCol.center = new Vector3(0f, 0.12f, 0f);
            tabCol.size = new Vector3(0.55f, 0.35f, 0.45f);

            InteractableStoryObject tabletInteract = tabletObj.AddComponent<InteractableStoryObject>();
            tabletInteract.questStepId = "find_tablet";
            tabletInteract.objectTitle = "Планшет Димарика (Взять в руки)";
            tabletInteract.inspectMonologue = "«А вот и планшет! На экране осталась наша вчерашняя фотка... И карта работает: тропа впереди ведёт прямо в городок Димарика — Рогово!»";

            // =========================================================================
            // 3. ТОЧКА ПЕРЕХОДА ВО 2-Ю ГЛАВУ: «Городок Димарика, Рогово»
            // =========================================================================
            Vector3 rogovoPos = GetGroundPoint(pPos + forward * 16.0f, pPos.y);
            GameObject rogovoObj = new GameObject("Quest3_RogovoTransition");
            rogovoObj.transform.SetParent(storyRoot.transform);
            rogovoObj.transform.position = rogovoPos;

            CreateDetailedRogovoSignpost(rogovoObj.transform);

            InteractableStoryObject rogovoInteract = rogovoObj.AddComponent<InteractableStoryObject>();
            rogovoInteract.questStepId = "rogovo_exit";
            rogovoInteract.objectTitle = "Дорожный указатель: «Городок Димарика, Рогово» (Глава 2)";
            rogovoInteract.inspectMonologue = "«Впереди уже виднеются крыши Рогово. Держись, Димарик, я скоро разберусь, что вчера произошло.»";
            rogovoInteract.interactionDistance = 3.2f;
        }

        private bool TrySpawnMixallBagPrefab(Transform parent)
        {
#if UNITY_EDITOR
            string[] searchFilters = new string[]
            {
                "t:Prefab",
                "t:Model"
            };

            GameObject chosenBagAsset = null;
            string chosenPath = null;
            int bestScore = -1;

            foreach (string filter in searchFilters)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets(filter);
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    string lowerPath = path.ToLowerInvariant();

                    if (lowerPath.Contains("toby") ||
                        lowerPath.Contains("starterassets") ||
                        lowerPath.Contains("character controller") ||
                        lowerPath.Contains("human basic") ||
                        lowerPath.Contains("footsteps") ||
                        lowerPath.Contains("nature - essentials"))
                    {
                        continue;
                    }

                    bool isMixallBag =
                        lowerPath.Contains("mixall") ||
                        lowerPath.Contains("bags - props") ||
                        lowerPath.Contains("bags-props") ||
                        lowerPath.Contains("/bags/") ||
                        lowerPath.Contains("backpack") ||
                        lowerPath.Contains("rucksack") ||
                        Path.GetFileNameWithoutExtension(lowerPath).StartsWith("bag");

                    if (!isMixallBag) continue;

                    GameObject candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate == null) continue;
                    if (candidate.GetComponentInChildren<MeshRenderer>() == null &&
                        candidate.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                    {
                        continue;
                    }

                    int score = 10;
                    if (lowerPath.EndsWith(".prefab")) score += 50;
                    if (lowerPath.Contains("mixall") || lowerPath.Contains("bags")) score += 40;
                    if (lowerPath.Contains("bag_01") || lowerPath.Contains("bag_1") || lowerPath.Contains("backpack")) score += 20;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        chosenBagAsset = candidate;
                        chosenPath = path;
                    }
                }
            }

            if (chosenBagAsset != null)
            {
                GameObject instance = Instantiate(chosenBagAsset, parent);
                instance.name = "Mixall_Backpack_Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                UpgradeRenderersToURP(instance, new Color(0.28f, 0.35f, 0.24f, 1f));
                NormalizeModelBoundsOnGround(instance.transform, 0.56f);

                Debug.Log($"[Dimari] Загружен 3D-рюкзак из ассета: {chosenPath}");
                return true;
            }
#endif
            return false;
        }

        private void UpgradeRenderersToURP(GameObject root, Color fallbackColor)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (urpLit == null) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null || r.sharedMaterials == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material oldMat = mats[i];
                    if (oldMat == null)
                    {
                        Material fallback = new Material(urpLit);
                        SetMatColor(fallback, fallbackColor);
                        mats[i] = fallback;
                        continue;
                    }

                    string shaderName = oldMat.shader != null ? oldMat.shader.name : "";
                    if (!shaderName.Contains("Universal Render Pipeline") && !shaderName.Contains("URP") && !shaderName.Contains("Toby"))
                    {
                        Texture mainTex = oldMat.HasProperty("_BaseMap") ? oldMat.GetTexture("_BaseMap") : null;
                        if (mainTex == null && oldMat.HasProperty("_MainTex")) mainTex = oldMat.GetTexture("_MainTex");

                        Texture bumpTex = oldMat.HasProperty("_BumpMap") ? oldMat.GetTexture("_BumpMap") : null;
                        Texture metallicTex = oldMat.HasProperty("_MetallicGlossMap") ? oldMat.GetTexture("_MetallicGlossMap") : null;
                        Texture occlusionTex = oldMat.HasProperty("_OcclusionMap") ? oldMat.GetTexture("_OcclusionMap") : null;

                        Color col = Color.white;
                        if (oldMat.HasProperty("_BaseColor")) col = oldMat.GetColor("_BaseColor");
                        else if (oldMat.HasProperty("_Color")) col = oldMat.GetColor("_Color");

                        Material newMat = new Material(urpLit);
                        newMat.name = oldMat.name + "_URP";
                        SetMatColor(newMat, col);

                        if (mainTex != null)
                        {
                            if (newMat.HasProperty("_BaseMap")) newMat.SetTexture("_BaseMap", mainTex);
                            if (newMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", mainTex);
                        }
                        if (bumpTex != null && newMat.HasProperty("_BumpMap"))
                        {
                            newMat.SetTexture("_BumpMap", bumpTex);
                            newMat.EnableKeyword("_NORMALMAP");
                        }
                        if (metallicTex != null && newMat.HasProperty("_MetallicGlossMap"))
                        {
                            newMat.SetTexture("_MetallicGlossMap", metallicTex);
                        }
                        if (occlusionTex != null && newMat.HasProperty("_OcclusionMap"))
                        {
                            newMat.SetTexture("_OcclusionMap", occlusionTex);
                        }
                        if (newMat.HasProperty("_Smoothness"))
                        {
                            newMat.SetFloat("_Smoothness", 0.25f);
                        }

                        mats[i] = newMat;
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        private void NormalizeModelBoundsOnGround(Transform modelTransform, float targetSize)
        {
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim > 0.001f && (maxDim < 0.15f || maxDim > 1.2f))
            {
                float scaleFactor = targetSize / maxDim;
                modelTransform.localScale *= scaleFactor;

                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }
            }

            float groundY = modelTransform.parent != null ? modelTransform.parent.position.y : modelTransform.position.y;
            float offsetY = groundY - b.min.y + 0.02f;
            modelTransform.position += new Vector3(0f, offsetY, 0f);
        }

        private void CreateDetailedProceduralBackpack(Transform parent)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material oliveFabricMat = new Material(litShader);
            SetMatColor(oliveFabricMat, new Color(0.22f, 0.29f, 0.19f, 1f));
            if (oliveFabricMat.HasProperty("_Smoothness")) oliveFabricMat.SetFloat("_Smoothness", 0.18f);

            Material darkFlapMat = new Material(litShader);
            SetMatColor(darkFlapMat, new Color(0.15f, 0.19f, 0.13f, 1f));
            if (darkFlapMat.HasProperty("_Smoothness")) darkFlapMat.SetFloat("_Smoothness", 0.22f);

            Material strapLeatherMat = new Material(litShader);
            SetMatColor(strapLeatherMat, new Color(0.26f, 0.17f, 0.10f, 1f));
            if (strapLeatherMat.HasProperty("_Smoothness")) strapLeatherMat.SetFloat("_Smoothness", 0.35f);

            Material buckleMetalMat = new Material(litShader);
            SetMatColor(buckleMetalMat, new Color(0.75f, 0.72f, 0.62f, 1f));
            if (buckleMetalMat.HasProperty("_Smoothness")) buckleMetalMat.SetFloat("_Smoothness", 0.75f);

            GameObject root = new GameObject("DetailedBackpack_Mesh");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            root.transform.localRotation = Quaternion.Euler(18f, 0f, -8f);

            CreatePart(PrimitiveType.Capsule, "MainPackBody", root.transform,
                new Vector3(0f, 0.24f, 0f), Quaternion.identity, new Vector3(0.36f, 0.26f, 0.24f), oliveFabricMat);
            CreatePart(PrimitiveType.Cylinder, "PackBottomBase", root.transform,
                new Vector3(0f, 0.09f, 0f), Quaternion.identity, new Vector3(0.35f, 0.06f, 0.23f), darkFlapMat);
            CreatePart(PrimitiveType.Sphere, "TopHoodFlap", root.transform,
                new Vector3(0f, 0.42f, 0.01f), Quaternion.Euler(10f, 0f, 0f), new Vector3(0.37f, 0.16f, 0.26f), darkFlapMat);
            CreatePart(PrimitiveType.Capsule, "FrontPocket", root.transform,
                new Vector3(0f, 0.19f, 0.12f), Quaternion.identity, new Vector3(0.25f, 0.14f, 0.10f), darkFlapMat);
            CreatePart(PrimitiveType.Cylinder, "LeftSidePocket", root.transform,
                new Vector3(-0.18f, 0.17f, 0f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.09f, 0.10f, 0.12f), darkFlapMat);
            CreatePart(PrimitiveType.Cylinder, "RightSidePocket", root.transform,
                new Vector3(0.18f, 0.17f, 0f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.09f, 0.10f, 0.12f), darkFlapMat);
            CreatePart(PrimitiveType.Cube, "LeftFrontStrap", root.transform,
                new Vector3(-0.08f, 0.30f, 0.135f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.032f, 0.22f, 0.018f), strapLeatherMat);
            CreatePart(PrimitiveType.Cube, "RightFrontStrap", root.transform,
                new Vector3(0.08f, 0.30f, 0.135f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.032f, 0.22f, 0.018f), strapLeatherMat);
            CreatePart(PrimitiveType.Cube, "LeftBuckle", root.transform,
                new Vector3(-0.08f, 0.25f, 0.145f), Quaternion.identity, new Vector3(0.045f, 0.035f, 0.02f), buckleMetalMat);
            CreatePart(PrimitiveType.Cube, "RightBuckle", root.transform,
                new Vector3(0.08f, 0.25f, 0.145f), Quaternion.identity, new Vector3(0.045f, 0.035f, 0.02f), buckleMetalMat);
            CreatePart(PrimitiveType.Capsule, "ShoulderStrapLeft", root.transform,
                new Vector3(-0.09f, 0.25f, -0.12f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.055f, 0.18f, 0.04f), strapLeatherMat);
            CreatePart(PrimitiveType.Capsule, "ShoulderStrapRight", root.transform,
                new Vector3(0.09f, 0.25f, -0.12f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.055f, 0.18f, 0.04f), strapLeatherMat);
            CreatePart(PrimitiveType.Cylinder, "TopCarryLoop", root.transform,
                new Vector3(0f, 0.49f, -0.04f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.03f, 0.06f, 0.03f), strapLeatherMat);
        }

        private void CreateDetailedTabletModel(Transform parent)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material bodyMat = new Material(litShader);
            SetMatColor(bodyMat, new Color(0.14f, 0.15f, 0.18f, 1f));
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.72f);

            Material bezelMat = new Material(litShader);
            SetMatColor(bezelMat, new Color(0.05f, 0.05f, 0.06f, 1f));
            if (bezelMat.HasProperty("_Smoothness")) bezelMat.SetFloat("_Smoothness", 0.85f);

            Texture2D photoTex = LoadTabletPhotoTexture();

            Material screenMat = new Material(litShader);
            if (photoTex != null)
            {
                SetMatColor(screenMat, Color.white);
                if (screenMat.HasProperty("_BaseMap")) screenMat.SetTexture("_BaseMap", photoTex);
                if (screenMat.HasProperty("_MainTex")) screenMat.SetTexture("_MainTex", photoTex);
                if (screenMat.HasProperty("_EmissionColor"))
                {
                    screenMat.EnableKeyword("_EMISSION");
                    if (screenMat.HasProperty("_EmissionMap")) screenMat.SetTexture("_EmissionMap", photoTex);
                    screenMat.SetColor("_EmissionColor", new Color(0.90f, 0.90f, 0.90f, 1f));
                }
            }
            else
            {
                Color screenGlow = new Color(0.18f, 0.78f, 0.96f, 1f);
                SetMatColor(screenMat, screenGlow);
                if (screenMat.HasProperty("_EmissionColor"))
                {
                    screenMat.EnableKeyword("_EMISSION");
                    screenMat.SetColor("_EmissionColor", screenGlow * 1.4f);
                }
            }

            CreatePart(PrimitiveType.Cube, "TabletChassis", parent,
                new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.36f, 0.020f, 0.25f), bodyMat);

            CreatePart(PrimitiveType.Cube, "TabletBezel", parent,
                new Vector3(0f, 0.051f, 0f), Quaternion.identity, new Vector3(0.34f, 0.008f, 0.23f), bezelMat);

            CreatePart(PrimitiveType.Cube, "TabletGlowScreen", parent,
                new Vector3(0f, 0.055f, 0f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.31f, 0.006f, 0.20f), screenMat);
        }

        private void CreateDetailedRogovoSignpost(Transform parent)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material woodPostMat = new Material(litShader);
            SetMatColor(woodPostMat, new Color(0.38f, 0.25f, 0.14f, 1f));
            if (woodPostMat.HasProperty("_Smoothness")) woodPostMat.SetFloat("_Smoothness", 0.2f);

            Material signBoardMat = new Material(litShader);
            SetMatColor(signBoardMat, new Color(0.48f, 0.33f, 0.18f, 1f));

            CreatePart(PrimitiveType.Cylinder, "SignPost", parent,
                new Vector3(0f, 0.90f, 0f), Quaternion.identity, new Vector3(0.11f, 0.90f, 0.11f), woodPostMat);

            CreatePart(PrimitiveType.Cube, "SignBoard", parent,
                new Vector3(0.18f, 1.50f, 0f), Quaternion.identity, new Vector3(0.85f, 0.24f, 0.06f), signBoardMat);

            GameObject textObj = new GameObject("SignText_Rogovo");
            textObj.transform.SetParent(parent, false);
            textObj.transform.localPosition = new Vector3(0.18f, 1.52f, -0.036f);
            textObj.transform.localRotation = Quaternion.identity;
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = "РОГОВО  →";
            tm.characterSize = 0.045f;
            tm.fontSize = 36;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.96f, 0.92f, 0.78f, 1f);

            BoxCollider signCol = parent.gameObject.AddComponent<BoxCollider>();
            signCol.center = new Vector3(0.1f, 1.0f, 0f);
            signCol.size = new Vector3(0.95f, 1.8f, 0.5f);
        }

        private GameObject CreatePart(PrimitiveType type, string partName, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = localRot;
            part.transform.localScale = localScale;
            Collider c = part.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
            Renderer r = part.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return part;
        }

        private static void SetMatColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private Vector3 GetGroundPoint(Vector3 pos, float fallbackY)
        {
            RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 20f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = fallbackY;
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (h.transform.CompareTag("Player")) continue;
                if (h.transform.GetComponentInParent<InteractableStoryObject>() != null) continue;
                if (!found || h.point.y > bestY)
                {
                    bestY = h.point.y;
                    found = true;
                }
            }
            return new Vector3(pos.x, bestY, pos.z);
        }

        private void Update()
        {
            if (_statusToastTimer > 0f)
            {
                _statusToastTimer -= Time.unscaledDeltaTime;
            }

            // Плавный вес позы удержания планшета двумя руками перед грудью
            float targetHoldBlend = (_hasPickedUpTablet && _isHoldingTabletInHands && !_isPlayingPickupAnim && !_isTabletViewerOpen) ? 1.0f : 0.0f;
            _holdTabletBlend = Mathf.MoveTowards(_holdTabletBlend, targetHoldBlend, Time.deltaTime * 5.0f);

            if (_isMenuOpen)
            {
                Time.timeScale = 1.0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (_director == null) _director = FindAnyObjectByType<ForestAwakeningDirector>();
                if (_director != null && _director.enabled)
                {
                    _director.StopAllCoroutines();
                    _director.enabled = false;
                }
            }
            else if (_isTabletViewerOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // По [E], [TAB] или [Пробел] — убираем планшет в рюкзак!
                if (DimariInput.IsInteractPressed() || DimariInput.IsSkipOrContinuePressed() || IsTabKeyPressed())
                {
                    PutTabletInBackpack();
                }
                else if (IsToggleHoldHandsKeyPressed())
                {
                    KeepTabletInHands();
                }
            }
            else if (_hasPickedUpTablet)
            {
                // [TAB] — открыть крупный экран планшета с фото
                if (IsTabKeyPressed())
                {
                    OpenTabletViewer();
                }
                // [T] или Правая кнопка мыши (когда планшет в руках) — убрать планшет в рюкзак / достать в руки
                else if (IsToggleHoldHandsKeyPressed() || (_isHoldingTabletInHands && IsRightMousePressed()))
                {
                    if (_isHoldingTabletInHands)
                    {
                        PutTabletInBackpack();
                    }
                    else
                    {
                        KeepTabletInHands();
                    }
                }
            }

            if (DimariInput.IsPauseMenuPressed())
            {
                if (_isTabletViewerOpen)
                {
                    CloseTabletViewer();
                }
                else if (_isSettingsOpen)
                {
                    _isSettingsOpen = false;
                }
                else if (_isMenuOpen && _hasGameStarted)
                {
                    ResumeFromPause();
                }
                else if (!_isMenuOpen)
                {
                    OpenPauseMenu();
                }
            }

            if (DimariInput.IsQuickSavePressed() && _hasGameStarted)
            {
                SaveGame();
            }

            if (_isMenuOpen && DimariInput.IsSettingsShortcutPressed())
            {
                _isSettingsOpen = !_isSettingsOpen;
            }

            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= Time.deltaTime;
                _subtitleAlpha = Mathf.MoveTowards(_subtitleAlpha, 1f, Time.deltaTime * 5f);
            }
            else
            {
                _subtitleAlpha = Mathf.MoveTowards(_subtitleAlpha, 0f, Time.deltaTime * 2.5f);
            }
        }

        private bool IsTabKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                return true;
            }
#endif
            return false;
        }

        private bool IsToggleHoldHandsKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                (UnityEngine.InputSystem.Keyboard.current.tKey.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.R))
            {
                return true;
            }
#endif
            return false;
        }

        private bool IsRightMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(1))
            {
                return true;
            }
#endif
            return false;
        }

        public void OpenTabletViewer()
        {
            _isTabletViewerOpen = true;
            if (_player != null) _player.IsControlEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseTabletViewer()
        {
            PutTabletInBackpack();
        }

        public void PutTabletInBackpack()
        {
            _isTabletViewerOpen = false;
            _isHoldingTabletInHands = false;
            _holdTabletBlend = 0f;

            if (_equippedTabletTransform != null)
            {
                _equippedTabletTransform.gameObject.SetActive(false);
            }

            if (!_isMenuOpen)
            {
                if (_player != null) _player.IsControlEnabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ShowStatusToast("Планшет убран в рюкзак ([TAB] — фото, [T] — в руки)");
        }

        public void KeepTabletInHands()
        {
            _isTabletViewerOpen = false;
            _isHoldingTabletInHands = true;
            _holdTabletBlend = 1.0f;

            if (_equippedTabletTransform != null)
            {
                _equippedTabletTransform.gameObject.SetActive(true);
            }

            if (!_isMenuOpen)
            {
                if (_player != null) _player.IsControlEnabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ShowStatusToast("Планшет в руках ([T] или ПКМ — убрать в рюкзак)");
        }

        private void LateUpdate()
        {
            if (!_isMenuOpen)
            {
                if (_isPlayingPickupAnim)
                {
                    ApplyHumanoidPickupPose();
                }
                else if (_holdTabletBlend > 0.001f)
                {
                    // ДЕРЖИТ ПЛАНШЕТ ПЕРЕД СОБОЙ ДВУМЯ РУКАМИ!
                    ApplyHumanoidHoldTabletInBothHandsPose(_holdTabletBlend);
                }

                UpdateEquippedItemsOnCharacter();
                return;
            }

            // В Главном Меню / Паузе — персонаж стоит СПРАВА экрана, а за ним качаются деревья леса
            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_mainCam == null) _mainCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (_player == null || _mainCam == null) return;

            if (_player.visualModelRoot != null)
            {
                _player.visualModelRoot.localPosition = Vector3.zero;
                _player.visualModelRoot.localRotation = Quaternion.identity;
            }

            if (_cameraController == null) _cameraController = FindAnyObjectByType<ForestCameraController>();
            if (_cameraController != null)
            {
                _cameraController.UpdateCharacterMeshVisibility(false);
            }

            _menuCamOrbitTimer += Time.unscaledDeltaTime;
            float swayX = Mathf.Sin(_menuCamOrbitTimer * 0.45f) * 0.06f;
            float swayY = Mathf.Cos(_menuCamOrbitTimer * 0.35f) * 0.03f;

            Vector3 pPos = _player.transform.position;
            Vector3 pForward = _player.transform.forward;
            pForward.y = 0f;
            if (pForward.sqrMagnitude < 0.01f) pForward = Vector3.forward;
            pForward.Normalize();

            Vector3 camPos = pPos + pForward * 2.65f + Vector3.up * (1.28f + swayY) + _player.transform.right * swayX;
            Vector3 characterChest = pPos + Vector3.up * 1.18f;

            Quaternion lookDirect = Quaternion.LookRotation(characterChest - camPos, Vector3.up);
            Quaternion lookWithCharacterOnRight = lookDirect * Quaternion.Euler(0f, -20f, 0f);

            _mainCam.transform.position = camPos;
            _mainCam.transform.rotation = lookWithCharacterOnRight;

            Vector3 vp = _mainCam.WorldToViewportPoint(characterChest);
            if (vp.x < 0.5f)
            {
                _mainCam.transform.rotation = lookDirect * Quaternion.Euler(0f, +20f, 0f);
            }
        }

        /// <summary>
        /// Точный двухсуставный IK-солвер для руки: направляет плечо к точке локтя, а предплечье — точно к точке захвата предмета!
        /// Работает независимо от локальных осей костей любого FBX-персонажа.
        /// </summary>
        private void AimArmAtWorldTargets(Transform upperArm, Transform lowerArm, Transform hand, Vector3 elbowTarget, Vector3 handTarget, float weight)
        {
            if (upperArm == null || lowerArm == null || hand == null || weight <= 0.001f) return;

            Vector3 curUpperDir = lowerArm.position - upperArm.position;
            Vector3 desUpperDir = elbowTarget - upperArm.position;
            if (curUpperDir.sqrMagnitude > 0.0001f && desUpperDir.sqrMagnitude > 0.0001f)
            {
                Quaternion upperRotDelta = Quaternion.FromToRotation(curUpperDir.normalized, desUpperDir.normalized);
                upperArm.rotation = Quaternion.Slerp(Quaternion.identity, upperRotDelta, weight) * upperArm.rotation;
            }

            Vector3 curLowerDir = hand.position - lowerArm.position;
            Vector3 desLowerDir = handTarget - lowerArm.position;
            if (curLowerDir.sqrMagnitude > 0.0001f && desLowerDir.sqrMagnitude > 0.0001f)
            {
                Quaternion lowerRotDelta = Quaternion.FromToRotation(curLowerDir.normalized, desLowerDir.normalized);
                lowerArm.rotation = Quaternion.Slerp(Quaternion.identity, lowerRotDelta, weight) * lowerArm.rotation;
            }
        }

        /// <summary>
        /// Поза удержания планшета ПЕРЕД СОБОЙ ДВУМЯ РУКАМИ:
        /// Обе руки согнуты в локтях перед грудью, левая кисть держит левый край планшета, правая кисть — правый край,
        /// голова слегка наклонена на экран планшета.
        /// </summary>
        private void ApplyHumanoidHoldTabletInBothHandsPose(float weight)
        {
            if (_player == null) return;
            if (_characterAnimator == null) CacheCharacterBones();
            if (_characterAnimator == null || !_characterAnimator.isHuman) return;

            Vector3 charForward = _player.transform.forward;
            Vector3 charRight = _player.transform.right;
            Vector3 charUp = Vector3.up;

            Transform chest = _characterChestBone != null
                ? _characterChestBone
                : _characterAnimator.GetBoneTransform(HumanBodyBones.Chest);

            Vector3 chestPos = chest != null ? chest.position : (_player.transform.position + charUp * 1.24f);

            // Точка центра планшета повыше перед грудью персонажа (на удобном уровне чтения глазами)
            Vector3 tabletCenter = chestPos + charForward * 0.36f + charUp * 0.06f;

            // Точки для локтей и кистей обеих рук (левая и правая грани планшета)
            Vector3 leftElbowTarget = chestPos + charForward * 0.16f - charRight * 0.24f - charUp * 0.12f;
            Vector3 rightElbowTarget = chestPos + charForward * 0.16f + charRight * 0.24f - charUp * 0.12f;

            Vector3 leftHandGrip = tabletCenter - charRight * 0.16f;
            Vector3 rightHandGrip = tabletCenter + charRight * 0.16f;

            Transform lUpperArm = _characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lLowerArm = _characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform lHand = _characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

            Transform rUpperArm = _characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rLowerArm = _characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand = _characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);

            AimArmAtWorldTargets(lUpperArm, lLowerArm, lHand, leftElbowTarget, leftHandGrip, weight);
            AimArmAtWorldTargets(rUpperArm, rLowerArm, rHand, rightElbowTarget, rightHandGrip, weight);

            // Лёгкий наклон шеи/головы вниз на экран планшета
            Transform neck = _characterAnimator.GetBoneTransform(HumanBodyBones.Neck);
            if (neck != null)
            {
                neck.rotation = Quaternion.AngleAxis(15f * weight, charRight) * neck.rotation;
            }
        }

        /// <summary>
        /// Плавная 3D-анимация наклона персонажа и захвата предмета правой рукой с земли.
        /// </summary>
        private void ApplyHumanoidPickupPose()
        {
            if (_player == null) return;
            if (_characterAnimator == null) CacheCharacterBones();
            if (_characterAnimator == null || !_characterAnimator.isHuman) return;

            float norm = Mathf.Clamp01(_pickupAnimTimer / _pickupAnimDuration);
            float bendWeight = Mathf.Sin(norm * Mathf.PI);
            bendWeight = Mathf.SmoothStep(0f, 1f, bendWeight);

            Vector3 charRight = _player.transform.right;
            Vector3 charForward = _player.transform.forward;

            Transform hips = _characterAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = _characterAnimator.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = _characterAnimator.GetBoneTransform(HumanBodyBones.Chest);
            Transform upperChest = _characterAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            Transform neck = _characterAnimator.GetBoneTransform(HumanBodyBones.Neck);
            Transform rUpperArm = _characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rLowerArm = _characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand = _characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform lUpperLeg = _characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform lLowerLeg = _characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform rUpperLeg = _characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rLowerLeg = _characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

            if (hips != null)
            {
                hips.position += Vector3.down * (0.26f * bendWeight);
            }
            if (lUpperLeg != null) lUpperLeg.rotation = Quaternion.AngleAxis(-34f * bendWeight, charRight) * lUpperLeg.rotation;
            if (rUpperLeg != null) rUpperLeg.rotation = Quaternion.AngleAxis(-38f * bendWeight, charRight) * rUpperLeg.rotation;
            if (lLowerLeg != null) lLowerLeg.rotation = Quaternion.AngleAxis(62f * bendWeight, charRight) * lLowerLeg.rotation;
            if (rLowerLeg != null) rLowerLeg.rotation = Quaternion.AngleAxis(66f * bendWeight, charRight) * rLowerLeg.rotation;

            if (spine != null) spine.rotation = Quaternion.AngleAxis(34f * bendWeight, charRight) * spine.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(26f * bendWeight, charRight) * chest.rotation;
            if (upperChest != null) upperChest.rotation = Quaternion.AngleAxis(16f * bendWeight, charRight) * upperChest.rotation;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(18f * bendWeight, charRight) * neck.rotation;

            // Направляем правую руку прямо к поднимаемому предмету на земле!
            Vector3 chestPos = chest != null ? chest.position : (_player.transform.position + Vector3.up * 1.0f);
            Vector3 elbowHint = chestPos + charForward * 0.20f + charRight * 0.22f - Vector3.up * 0.25f;
            Vector3 reachPoint = _pickupTargetWorldPos + Vector3.up * 0.12f;
            AimArmAtWorldTargets(rUpperArm, rLowerArm, rHand, elbowHint, reachPoint, bendWeight);
        }

        /// <summary>
        /// Держит поднятый рюкзак на спине персонажа, а планшет — перед грудью МЕЖДУ ДВУМЯ РУКАМИ!
        /// </summary>
        private void UpdateEquippedItemsOnCharacter()
        {
            if (_player == null) return;
            if (_characterChestBone == null) CacheCharacterBones();

            // 1. РЮКЗАК НА СПИНЕ ПЕРСОНАЖА
            if (_equippedBackpackTransform != null)
            {
                Transform anchor = _characterChestBone != null ? _characterChestBone : _player.transform;
                Vector3 backForward = anchor.forward;
                backForward.y = 0f;
                if (backForward.sqrMagnitude < 0.01f) backForward = _player.transform.forward;
                backForward.Normalize();

                Vector3 chestPos = _characterChestBone != null
                    ? _characterChestBone.position
                    : (_player.transform.position + Vector3.up * 1.22f);

                _equippedBackpackTransform.position = chestPos - backForward * 0.22f - Vector3.up * 0.16f;
                _equippedBackpackTransform.rotation = Quaternion.LookRotation(-backForward, Vector3.up);
            }

            // 2. ПЛАНШЕТ В РУКАХ ПЕРЕД СОБОЙ
            if (_equippedTabletTransform != null)
            {
                if (_isTabletViewerOpen)
                {
                    // Пока открыт крупный 2D-экран планшета по центру, прячем 3D-дубликат снизу, чтобы не было двух планшетов одновременно
                    _equippedTabletTransform.gameObject.SetActive(false);
                }
                else if (_isPlayingPickupAnim && _characterRightHandBone != null)
                {
                    _equippedTabletTransform.gameObject.SetActive(true);
                    _equippedTabletTransform.position = _characterRightHandBone.position + _player.transform.forward * 0.08f;
                    _equippedTabletTransform.rotation = Quaternion.LookRotation(_player.transform.forward, Vector3.up);
                }
                else if (_isHoldingTabletInHands)
                {
                    _equippedTabletTransform.gameObject.SetActive(true);

                    Vector3 charForward = _player.transform.forward;
                    Vector3 charUp = Vector3.up;

                    // Размещаем 3D-планшет ровно посередине между левой и правой кистями персонажа на уровне груди!
                    Vector3 handsCenter;
                    if (_characterLeftHandBone != null && _characterRightHandBone != null)
                    {
                        handsCenter = (_characterLeftHandBone.position + _characterRightHandBone.position) * 0.5f + charForward * 0.04f + charUp * 0.04f;
                    }
                    else
                    {
                        Vector3 chestPos = _characterChestBone != null
                            ? _characterChestBone.position
                            : (_player.transform.position + charUp * 1.28f);
                        handsCenter = chestPos + charForward * 0.36f + charUp * 0.06f;
                    }

                    _equippedTabletTransform.position = handsCenter;
                    // Экран планшета (+Y) повёрнут к лицу персонажа и строго головой вверх (без переворота вверх ногами!)
                    Vector3 tabletTopEdge = (charForward + charUp * 0.90f).normalized;
                    Vector3 tabletScreenNormal = (-charForward * 0.67f + charUp * 0.74f).normalized;
                    _equippedTabletTransform.rotation = Quaternion.LookRotation(tabletTopEdge, tabletScreenNormal);
                }
                else
                {
                    _equippedTabletTransform.gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator PlayPickupItemRoutine(string stepId, InteractableStoryObject targetInteractable)
        {
            _isPlayingPickupAnim = true;
            _pickupAnimTimer = 0f;
            _interactionPrompt = "";

            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_cameraController == null) _cameraController = FindAnyObjectByType<ForestCameraController>();

            bool wasFirstPerson = _cameraController != null && _cameraController.isFirstPerson;
            if (_cameraController != null)
            {
                _cameraController.UpdateCharacterMeshVisibility(false);
            }

            Transform itemTransform = targetInteractable != null ? targetInteractable.transform : null;
            if (itemTransform != null)
            {
                _pickupTargetWorldPos = itemTransform.position;

                Collider[] cols = itemTransform.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in cols) c.enabled = false;
                targetInteractable.enabled = false;
            }

            Vector3 startForward = _player != null ? _player.transform.forward : Vector3.forward;
            Vector3 toItem = itemTransform != null && _player != null
                ? (itemTransform.position - _player.transform.position)
                : startForward;
            toItem.y = 0f;
            Quaternion targetRot = toItem.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toItem.normalized, Vector3.up)
                : (_player != null ? _player.transform.rotation : Quaternion.identity);

            bool attachedToHandOrBack = false;

            while (_pickupAnimTimer < _pickupAnimDuration)
            {
                _pickupAnimTimer += Time.deltaTime;
                float norm = Mathf.Clamp01(_pickupAnimTimer / _pickupAnimDuration);

                if (_player != null && norm < 0.45f)
                {
                    _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, targetRot, Time.deltaTime * 10f);
                }

                if (!attachedToHandOrBack && norm >= 0.45f && itemTransform != null)
                {
                    attachedToHandOrBack = true;
                    CacheCharacterBones();

                    if (stepId == "find_backpack")
                    {
                        _hasPickedUpBackpack = true;
                        _equippedBackpackTransform = itemTransform;
                        _equippedBackpackTransform.localScale *= 0.78f;
                        ShowStatusToast("Рюкзак поднят и надет на спину!");
                    }
                    else if (stepId == "find_tablet")
                    {
                        _hasPickedUpTablet = true;
                        _isHoldingTabletInHands = true;
                        _holdTabletBlend = 1.0f;
                        _equippedTabletTransform = itemTransform;
                        _equippedTabletTransform.localScale *= 0.85f;
                        ShowStatusToast("Планшет в руках! ([TAB] — экран, [T] — убрать/достать)");
                    }
                }

                yield return null;
            }

            _isPlayingPickupAnim = false;

            if (_cameraController != null && wasFirstPerson)
            {
                _cameraController.UpdateCharacterMeshVisibility(true);
            }

            if (stepId == "find_tablet")
            {
                OpenTabletViewer();
            }
        }

        public void OpenMainMenuOnStartup()
        {
            _isMenuOpen = true;
            _isSettingsOpen = false;
            _hasGameStarted = false;
            _showHUD = false;
            _isTabletViewerOpen = false;
            Time.timeScale = 1.0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_director == null) _director = FindAnyObjectByType<ForestAwakeningDirector>();
            if (_director != null)
            {
                _director.StopAllCoroutines();
                _director.enabled = false;
            }

            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_player != null)
            {
                _player.IsControlEnabled = false;
                if (_player.visualModelRoot != null)
                {
                    _player.visualModelRoot.localPosition = Vector3.zero;
                    _player.visualModelRoot.localRotation = Quaternion.identity;
                }
            }

            if (_cameraController == null) _cameraController = FindAnyObjectByType<ForestCameraController>();
            if (_cameraController != null)
            {
                _cameraController.IsMenuCameraActive = true;
                _cameraController.IsCutsceneControlled = false;
                _cameraController.UpdateCharacterMeshVisibility(false);
            }
        }

        public void OpenPauseMenu()
        {
            _isMenuOpen = true;
            _isSettingsOpen = false;
            Time.timeScale = 1.0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_player != null)
            {
                _player.IsControlEnabled = false;
            }

            if (_cameraController != null)
            {
                _cameraController.IsMenuCameraActive = true;
                _cameraController.UpdateCharacterMeshVisibility(false);
            }
        }

        public void ResumeFromPause()
        {
            _isMenuOpen = false;
            _isSettingsOpen = false;
            Time.timeScale = 1.0f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_cameraController != null)
            {
                _cameraController.IsMenuCameraActive = false;
                _cameraController.UpdateCharacterMeshVisibility(_cameraController.isFirstPerson);
            }

            if (_director != null && _director.IsPlayingCutscene)
            {
                _player.IsControlEnabled = false;
            }
            else if (_player != null)
            {
                _player.IsControlEnabled = true;
                _showHUD = true;
            }
        }

        public void StartNewGame()
        {
            _isMenuOpen = false;
            _isSettingsOpen = false;
            _isTabletViewerOpen = false;
            _hasGameStarted = true;
            _isChapter2Reached = false;
            Time.timeScale = 1.0f;

            SetupDimariQuests();
            RemoveStumpAndSpawnUpdatedStoryProps();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_cameraController != null)
            {
                _cameraController.IsMenuCameraActive = false;
            }

            if (_director == null) _director = FindAnyObjectByType<ForestAwakeningDirector>();
            if (_director != null)
            {
                _director.enabled = true;
                _director.StartAwakeningCutscene();
            }
            else
            {
                if (_player != null) _player.IsControlEnabled = true;
                _showHUD = true;
            }
        }

        public void SaveGame()
        {
            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_player != null)
            {
                Vector3 pos = _player.transform.position;
                PlayerPrefs.SetFloat("Dimari_Save_PosX", pos.x);
                PlayerPrefs.SetFloat("Dimari_Save_PosY", pos.y);
                PlayerPrefs.SetFloat("Dimari_Save_PosZ", pos.z);
                PlayerPrefs.SetFloat("Dimari_Save_RotY", _player.transform.eulerAngles.y);
            }

            for (int i = 0; i < questSteps.Count; i++)
            {
                PlayerPrefs.SetInt("Dimari_Save_Quest_" + questSteps[i].id, questSteps[i].isCompleted ? 1 : 0);
            }

            PlayerPrefs.SetInt("Dimari_HasSave", 1);
            PlayerPrefs.Save();

            ShowStatusToast("Игра успешно сохранена!");
        }

        public void LoadSavedGame()
        {
            if (PlayerPrefs.GetInt("Dimari_HasSave", 0) == 0)
            {
                ShowStatusToast("Нет сохранённой игры — начинаем новую!");
                StartNewGame();
                return;
            }

            _isMenuOpen = false;
            _isSettingsOpen = false;
            _hasGameStarted = true;
            Time.timeScale = 1.0f;

            if (_director != null)
            {
                _director.StopAllCoroutines();
                _director.FinishAwakeningImmediately();
                _director.enabled = false;
            }

            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_player != null && PlayerPrefs.HasKey("Dimari_Save_PosX"))
            {
                CharacterController cc = _player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                Vector3 savedPos = new Vector3(
                    PlayerPrefs.GetFloat("Dimari_Save_PosX"),
                    PlayerPrefs.GetFloat("Dimari_Save_PosY"),
                    PlayerPrefs.GetFloat("Dimari_Save_PosZ")
                );
                _player.transform.position = savedPos;
                _player.transform.rotation = Quaternion.Euler(0f, PlayerPrefs.GetFloat("Dimari_Save_RotY", 0f), 0f);

                if (cc != null) cc.enabled = true;
                _player.IsControlEnabled = true;
            }

            for (int i = 0; i < questSteps.Count; i++)
            {
                questSteps[i].isCompleted = PlayerPrefs.GetInt("Dimari_Save_Quest_" + questSteps[i].id, 0) == 1;
            }

            if (_cameraController != null)
            {
                _cameraController.IsMenuCameraActive = false;
                _cameraController.IsCutsceneControlled = false;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _showHUD = true;

            ShowStatusToast("Сохранение загружено!");
        }

        public void ShowStatusToast(string msg)
        {
            _statusToast = msg;
            _statusToastTimer = 3.0f;
        }

        public void SetHUDVisible(bool visible)
        {
            _showHUD = visible;
        }

        public void ShowMonologue(string text, float duration = 5.5f)
        {
            _currentSubtitle = text;
            _subtitleTimer = duration;
            _subtitleAlpha = 1.0f;
        }

        public void SetInteractionPrompt(string prompt)
        {
            _interactionPrompt = prompt;
        }

        public void CompleteQuestStep(string stepId, string monologueText)
        {
            foreach (QuestStep step in questSteps)
            {
                if (step.id == stepId && !step.isCompleted)
                {
                    step.isCompleted = true;
                    break;
                }
            }

            if (stepId == "find_backpack" || stepId == "find_tablet")
            {
                InteractableStoryObject targetObj = null;
                InteractableStoryObject[] allInteractables = FindObjectsByType<InteractableStoryObject>();
                foreach (InteractableStoryObject iso in allInteractables)
                {
                    if (iso != null && iso.questStepId == stepId)
                    {
                        targetObj = iso;
                        break;
                    }
                }
                StartCoroutine(PlayPickupItemRoutine(stepId, targetObj));
            }

            if (!string.IsNullOrEmpty(monologueText))
            {
                ShowMonologue(monologueText, 7.5f);
            }

            if (ProceduralForestAudio.Instance != null)
            {
                string voiceKey = stepId;
                if (stepId == "find_backpack") voiceKey = "backpack";
                else if (stepId == "find_tablet") voiceKey = "tablet";
                ProceduralForestAudio.Instance.PlayQuestVoiceClip(voiceKey);
            }

            if (stepId == "rogovo_exit")
            {
                _isChapter2Reached = true;
                SaveGame();
            }
        }

        private static void ConfigureStyle(ref GUIStyle style, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color textColor, bool wordWrap = false)
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle());
            }
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.alignment = alignment;
            style.wordWrap = wordWrap;
            style.richText = true;
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
        }

        private void EnsureStyles()
        {
            if (_darkPanelTex == null)
            {
                _darkPanelTex = new Texture2D(1, 1);
                _darkPanelTex.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.08f, 0.84f));
                _darkPanelTex.Apply();
            }

            if (_subtitlePanelTex == null)
            {
                _subtitlePanelTex = new Texture2D(1, 1);
                _subtitlePanelTex.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.05f, 0.90f));
                _subtitlePanelTex.Apply();
            }

            if (_tabletBezelTex == null)
            {
                _tabletBezelTex = new Texture2D(1, 1);
                _tabletBezelTex.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.11f, 0.98f));
                _tabletBezelTex.Apply();
            }

            if (_menuLeftGradientTex == null)
            {
                int w = 128;
                _menuLeftGradientTex = new Texture2D(w, 1);
                _menuLeftGradientTex.wrapMode = TextureWrapMode.Clamp;
                for (int x = 0; x < w; x++)
                {
                    float norm = (float)x / (w - 1);
                    float alpha = Mathf.Pow(1f - norm, 2.0f) * 0.68f;
                    _menuLeftGradientTex.SetPixel(x, 0, new Color(0.04f, 0.05f, 0.06f, alpha));
                }
                _menuLeftGradientTex.Apply();
            }

            if (_buttonHoverBarTex == null)
            {
                int w = 64;
                _buttonHoverBarTex = new Texture2D(w, 1);
                _buttonHoverBarTex.wrapMode = TextureWrapMode.Clamp;
                for (int x = 0; x < w; x++)
                {
                    float norm = (float)x / (w - 1);
                    float alpha = Mathf.Lerp(0.45f, 0.0f, norm);
                    _buttonHoverBarTex.SetPixel(x, 0, new Color(0.92f, 0.95f, 0.92f, alpha));
                }
                _buttonHoverBarTex.Apply();
            }

            if (_accentBarTex == null)
            {
                _accentBarTex = new Texture2D(1, 1);
                _accentBarTex.SetPixel(0, 0, new Color(0.96f, 0.97f, 0.95f, 1.0f));
                _accentBarTex.Apply();
            }

            if (_goldAccentTex == null)
            {
                _goldAccentTex = new Texture2D(1, 1);
                _goldAccentTex.SetPixel(0, 0, new Color(1.0f, 0.84f, 0.38f, 1.0f));
                _goldAccentTex.Apply();
            }

            if (_logoBoxTex == null)
            {
                _logoBoxTex = new Texture2D(1, 1);
                _logoBoxTex.SetPixel(0, 0, new Color(0.94f, 0.94f, 0.92f, 0.97f));
                _logoBoxTex.Apply();
            }

            if (_keyCapTex == null)
            {
                _keyCapTex = new Texture2D(1, 1);
                _keyCapTex.SetPixel(0, 0, new Color(0.92f, 0.92f, 0.92f, 0.96f));
                _keyCapTex.Apply();
            }

            if (!_stylesInitialized || _logoMainStyle == null || _logoMainStyle.fontSize != 26)
            {
                _stylesInitialized = true;

                ConfigureStyle(ref _logoMainStyle, 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.06f, 0.07f, 0.07f, 1f), false);
                ConfigureStyle(ref _menuBtnStyle, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.94f, 0.92f, 0.98f), false);
                ConfigureStyle(ref _menuBtnHoverStyle, 20, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, false);
                ConfigureStyle(ref _questHeaderStyle, 17, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1.0f, 0.86f, 0.42f, 1f), false);
                ConfigureStyle(ref _questActiveStyle, 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, true);
                ConfigureStyle(ref _questDoneStyle, 15, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.55f, 0.95f, 0.62f, 0.95f), true);
                ConfigureStyle(ref _subtitleSpeakerStyle, 14, FontStyle.Bold, TextAnchor.UpperCenter, new Color(1.0f, 0.84f, 0.38f, 1f), false);
                ConfigureStyle(ref _subtitleStyle, 19, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.99f, 0.98f, 0.94f, 1f), true);
                ConfigureStyle(ref _promptStyle, 17, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1.0f, 0.92f, 0.55f, 1f), false);
                ConfigureStyle(ref _bottomBarTextStyle, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.94f, 0.96f, 0.94f, 0.98f), false);
                ConfigureStyle(ref _keyCapStyle, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.06f, 0.06f, 0.06f, 1f), false);
                ConfigureStyle(ref _settingsLabelStyle, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.97f, 0.96f, 1f), false);
                ConfigureStyle(ref _chapter2TitleStyle, 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1.0f, 0.88f, 0.45f, 1f), false);
            }
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;

            EnsureStyles();

            if (_isMenuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                DrawDyingLightStyleMenu();
                DrawToastIfActive();
                return;
            }

            if (_isChapter2Reached)
            {
                DrawChapter2TransitionOverlay();
            }

            if (_isTabletViewerOpen)
            {
                DrawTabletPhotoViewerOverlay();
            }

            DrawSubtitlesBar();
            DrawToastIfActive();

            if (!_showHUD) return;

            float panelX = 24f;
            float panelY = 24f;
            float panelW = 460f;
            float panelH = 54f + questSteps.Count * 38f;

            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _darkPanelTex);
            GUI.DrawTexture(new Rect(panelX, panelY, 4f, panelH), _goldAccentTex);
            GUI.Label(
                new Rect(panelX + 16f, panelY + 10f, panelW - 28f, 28f),
                $"<color=#FFDB6B><b>{questHeader}</b></color>",
                _questHeaderStyle
            );

            float curY = panelY + 42f;
            foreach (QuestStep step in questSteps)
            {
                if (step.isCompleted)
                {
                    GUI.Label(
                        new Rect(panelX + 16f, curY, panelW - 28f, 36f),
                        $"<color=#8CEB9E>✓ {step.title}</color>",
                        _questDoneStyle
                    );
                }
                else
                {
                    GUI.Label(
                        new Rect(panelX + 16f, curY, panelW - 28f, 36f),
                        $"<color=#FFFFFF><b>{step.title}</b></color>",
                        _questActiveStyle
                    );
                }
                curY += 36f;
            }

            if (!_isTabletViewerOpen)
            {
                float centerX = Screen.width * 0.5f;
                float centerY = Screen.height * 0.5f;
                GUI.DrawTexture(new Rect(centerX - 2f, centerY - 2f, 4f, 4f), _accentBarTex);

                if (!string.IsNullOrEmpty(_interactionPrompt))
                {
                    float pW = 500f;
                    float pH = 44f;
                    Rect pRect = new Rect(centerX - pW * 0.5f, centerY + 32f, pW, pH);
                    GUI.DrawTexture(pRect, _subtitlePanelTex);
                    GUI.DrawTexture(new Rect(pRect.x, pRect.y, pRect.width, 2f), _goldAccentTex);
                    GUI.Label(
                        pRect,
                        $"<color=#FFE875><b>{_interactionPrompt}</b></color>",
                        _promptStyle
                    );
                }
                else if (_isHoldingTabletInHands)
                {
                    // Подсказка прямо под 3D-планшетом в руках, как убрать его в рюкзак
                    float pW = 480f;
                    float pH = 38f;
                    Rect pRect = new Rect(centerX - pW * 0.5f, Screen.height - 86f, pW, pH);
                    GUI.DrawTexture(pRect, _subtitlePanelTex);
                    GUI.DrawTexture(new Rect(pRect.x, pRect.y, pRect.width, 2f), _goldAccentTex);
                    GUI.Label(
                        pRect,
                        "<color=#FFE875><b>[T] или [ПКМ] Убрать в рюкзак   •   [TAB] На весь экран</b></color>",
                        _promptStyle
                    );
                }
            }

            string tabHint = _hasPickedUpTablet ? "   [TAB] Фото   [T / ПКМ] Убрать в рюкзак / В руки" : "";
            float bottomBarW = _hasPickedUpTablet ? 860f : 530f;
            GUI.DrawTexture(new Rect(24f, Screen.height - 42f, bottomBarW, 30f), _darkPanelTex);
            GUI.Label(
                new Rect(36f, Screen.height - 40f, bottomBarW - 20f, 26f),
                $"<color=#F0F4F0>[ESC] Меню / Пауза   [F5] Сохранить   [E] Поднять   [V] 1/3 лицо{tabHint}</color>",
                _bottomBarTextStyle
            );
        }

        private void DrawTabletPhotoViewerOverlay()
        {
            float w = Screen.width;
            float h = Screen.height;

            float tabW = Mathf.Min(820f, w - 100f);
            float tabH = Mathf.Min(560f, h - 150f);
            float tabX = (w - tabW) * 0.5f;
            float tabY = (h - tabH) * 0.45f;

            GUI.DrawTexture(new Rect(tabX - 6f, tabY - 6f, tabW + 12f, tabH + 12f), _goldAccentTex);
            GUI.DrawTexture(new Rect(tabX, tabY, tabW, tabH), _tabletBezelTex);

            GUI.Label(
                new Rect(tabX + 24f, tabY + 12f, tabW - 48f, 26f),
                "<color=#FFDB6B><b>ПЛАНШЕТ ДИМАРИКА  •  ГАЛЕРЕЯ: ПОСЛЕДНЕЕ ФОТО  •  GPS: РОГОВО (16 М →)</b></color>",
                _questHeaderStyle
            );

            Rect screenRect = new Rect(tabX + 24f, tabY + 44f, tabW - 48f, tabH - 104f);
            GUI.DrawTexture(screenRect, _darkPanelTex);

            Texture2D photo = LoadTabletPhotoTexture();
            if (photo != null)
            {
                GUI.DrawTexture(screenRect, photo, ScaleMode.ScaleToFit, false);
            }
            else
            {
                GUI.Label(
                    screenRect,
                    "<color=#FFFFFF><b>Фотография загружается...</b></color>",
                    _subtitleStyle
                );
            }

            // Две удобные кнопки внизу планшета: 1) Убрать в рюкзак, 2) Держать перед собой в руках
            float btnW = 360f;
            float btnH = 38f;
            float gap = 20f;
            float startBtnsX = tabX + (tabW - (btnW * 2f + gap)) * 0.5f;
            float btnsY = tabY + tabH - 50f;

            Rect putAwayBtnRect = new Rect(startBtnsX, btnsY, btnW, btnH);
            GUI.DrawTexture(putAwayBtnRect, _buttonHoverBarTex);
            GUI.DrawTexture(new Rect(putAwayBtnRect.x, putAwayBtnRect.y, 4f, putAwayBtnRect.height), _goldAccentTex);
            GUI.Label(
                putAwayBtnRect,
                "<color=#FFE875><b>[E] / [TAB] УБРАТЬ В РЮКЗАК</b></color>",
                _promptStyle
            );

            Rect holdInHandsBtnRect = new Rect(startBtnsX + btnW + gap, btnsY, btnW, btnH);
            GUI.DrawTexture(holdInHandsBtnRect, _buttonHoverBarTex);
            GUI.DrawTexture(new Rect(holdInHandsBtnRect.x, holdInHandsBtnRect.y, 4f, holdInHandsBtnRect.height), _accentBarTex);
            GUI.Label(
                holdInHandsBtnRect,
                "<color=#FFFFFF><b>[T] ДЕРЖАТЬ В РУКАХ</b></color>",
                _promptStyle
            );

            if (Event.current.type == EventType.MouseDown)
            {
                if (putAwayBtnRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    PutTabletInBackpack();
                }
                else if (holdInHandsBtnRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    KeepTabletInHands();
                }
            }
        }

        private void DrawSubtitlesBar()
        {
            if (_subtitleAlpha <= 0.01f || string.IsNullOrEmpty(_currentSubtitle))
            {
                return;
            }

            float boxW = Mathf.Min(880f, Screen.width - 80f);
            float boxH = 92f;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = Screen.height - 142f;

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _subtitleAlpha);

            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), _subtitlePanelTex);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 3f), _goldAccentTex);

            GUI.Label(
                new Rect(boxX + 20f, boxY + 8f, boxW - 40f, 22f),
                "<color=#FFD86B><b>ГЛАВНЫЙ ГЕРОЙ</b></color>",
                _subtitleSpeakerStyle
            );

            GUI.Label(
                new Rect(boxX + 24f, boxY + 28f, boxW - 48f, boxH - 34f),
                $"<color=#FFFDF5><b>{_currentSubtitle}</b></color>",
                _subtitleStyle
            );

            GUI.color = prevColor;
        }

        private void DrawDyingLightStyleMenu()
        {
            float w = Screen.width;
            float h = Screen.height;

            GUI.DrawTexture(new Rect(0f, 0f, w * 0.38f, h), _menuLeftGradientTex);

            float logoX = 64f;
            float logoY = 72f;
            float logoW = 395f;
            float logoH = 58f;

            GUI.DrawTexture(new Rect(logoX, logoY, logoW, logoH), _logoBoxTex);
            GUI.Label(
                new Rect(logoX + 8f, logoY + 4f, logoW - 16f, logoH - 8f),
                "<color=#101212><size=25><b>ПОИСКИ ДИМАРИКА</b></size></color>",
                _logoMainStyle
            );

            if (!_isSettingsOpen)
            {
                float btnX = 64f;
                float btnY = 205f;
                float btnW = 390f;
                float btnH = 46f;
                float spacing = 56f;

                int row = 0;

                if (_hasGameStarted)
                {
                    if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "ВЕРНУТЬСЯ В ИГРУ", row))
                    {
                        ResumeFromPause();
                    }
                    row++;
                }

                if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "НАЧАТЬ НОВУЮ ИГРУ", row))
                {
                    StartNewGame();
                }
                row++;

                if (_hasGameStarted)
                {
                    if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "СОХРАНИТЬ ИГРУ", row))
                    {
                        SaveGame();
                    }
                    row++;
                }
                else if (PlayerPrefs.GetInt("Dimari_HasSave", 0) == 1)
                {
                    if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "ЗАГРУЗИТЬ СОХРАНЕНИЕ", row))
                    {
                        LoadSavedGame();
                    }
                    row++;
                }
                else
                {
                    if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "СОХРАНЕНИЕ", row))
                    {
                        SaveGame();
                    }
                    row++;
                }

                if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "НАСТРОЙКИ", row))
                {
                    _isSettingsOpen = true;
                }
                row++;

                if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "ВЫХОД", row))
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
            }
            else
            {
                DrawSettingsPanel(64f, 185f);
            }

            float bottomY = h - 52f;
            DrawKeyHint(64f, bottomY, "ESC", _isSettingsOpen ? "Назад" : (_hasGameStarted ? "В игру" : "Выход"));
            DrawKeyHint(195f, bottomY, "F5", "Быстрое сохранение");
            DrawKeyHint(405f, bottomY, "O", "Настройки");

            GUI.Label(
                new Rect(w - 140f, bottomY, 125f, 26f),
                "<color=#E8ECE8><b>ВЕР. 1.5.0</b></color>",
                _bottomBarTextStyle
            );
        }

        private bool DrawMenuButton(Rect rect, string label, int index)
        {
            Vector2 mousePos = Event.current.mousePosition;
            bool isHovered = rect.Contains(mousePos);
            if (isHovered)
            {
                _selectedMenuIndex = index;
            }

            bool isHighlighted = isHovered || (_selectedMenuIndex == index);

            if (isHighlighted)
            {
                GUI.DrawTexture(rect, _buttonHoverBarTex);
                GUI.DrawTexture(new Rect(rect.x, rect.y + 4f, 4f, rect.height - 8f), _goldAccentTex);
            }

            GUIStyle style = isHighlighted ? _menuBtnHoverStyle : _menuBtnStyle;
            string coloredLabel = isHighlighted
                ? $"<color=#FFFFFF><size=19><b>{label}</b></size></color>"
                : $"<color=#E6EAE6><size=17><b>{label}</b></size></color>";

            GUI.Label(new Rect(rect.x + 18f, rect.y, rect.width - 18f, rect.height), coloredLabel, style);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                if (ProceduralForestAudio.Instance != null)
                {
                    ProceduralForestAudio.Instance.PlayInteractSound();
                }
                return true;
            }

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void DrawSettingsPanel(float startX, float startY)
        {
            float panelW = 440f;
            float panelH = 355f;
            GUI.DrawTexture(new Rect(startX - 12f, startY - 12f, panelW, panelH), _subtitlePanelTex);
            GUI.DrawTexture(new Rect(startX - 12f, startY - 12f, 4f, panelH), _goldAccentTex);

            GUI.Label(
                new Rect(startX + 8f, startY, 400f, 28f),
                "<color=#FFDB6B><size=16><b>НАСТРОЙКИ ГРОМКОСТИ И КАМЕРЫ</b></size></color>",
                _questHeaderStyle
            );

            float master = ProceduralForestAudio.Instance != null ? ProceduralForestAudio.Instance.masterVolume : 1.0f;
            float voice = ProceduralForestAudio.Instance != null ? ProceduralForestAudio.Instance.voiceVolume : 1.0f;
            float env = PlayerPrefs.GetFloat("Dimari_EnvVol", 0.7f);
            float sens = _cameraController != null ? _cameraController.mouseSensitivity : 2.2f;

            float y = startY + 44f;

            GUI.Label(
                new Rect(startX + 8f, y, 235f, 24f),
                $"<color=#FFFFFF><b>Общая громкость: {Mathf.RoundToInt(master * 100f)}%</b></color>",
                _settingsLabelStyle
            );
            float newMaster = GUI.HorizontalSlider(new Rect(startX + 245f, y + 6f, 160f, 20f), master, 0f, 1f);
            y += 44f;

            GUI.Label(
                new Rect(startX + 8f, y, 235f, 24f),
                $"<color=#FFFFFF><b>Громкость озвучки: {Mathf.RoundToInt(voice * 100f)}%</b></color>",
                _settingsLabelStyle
            );
            float newVoice = GUI.HorizontalSlider(new Rect(startX + 245f, y + 6f, 160f, 20f), voice, 0f, 1f);
            y += 44f;

            GUI.Label(
                new Rect(startX + 8f, y, 235f, 24f),
                $"<color=#FFFFFF><b>Звуки леса и шагов: {Mathf.RoundToInt(env * 100f)}%</b></color>",
                _settingsLabelStyle
            );
            float newEnv = GUI.HorizontalSlider(new Rect(startX + 245f, y + 6f, 160f, 20f), env, 0f, 1f);
            y += 44f;

            if (ProceduralForestAudio.Instance != null &&
                (Mathf.Abs(newMaster - master) > 0.005f || Mathf.Abs(newVoice - voice) > 0.005f || Mathf.Abs(newEnv - env) > 0.005f))
            {
                ProceduralForestAudio.Instance.ApplyVolumeSettings(newMaster, newVoice, newEnv);
            }

            GUI.Label(
                new Rect(startX + 8f, y, 235f, 24f),
                $"<color=#FFFFFF><b>Чувствительность мыши: {sens:F1}</b></color>",
                _settingsLabelStyle
            );
            float newSens = GUI.HorizontalSlider(new Rect(startX + 245f, y + 6f, 160f, 20f), sens, 0.4f, 5.0f);
            if (_cameraController != null && Mathf.Abs(newSens - sens) > 0.01f)
            {
                _cameraController.mouseSensitivity = newSens;
                PlayerPrefs.SetFloat("Dimari_MouseSens", newSens);
            }
            y += 48f;

            bool isFp = _cameraController != null && _cameraController.isFirstPerson;
            string camModeLabel = isFp ? "Режим камеры: От 1-го лица (Сменить)" : "Режим камеры: От 3-го лица (Сменить)";
            if (DrawMenuButton(new Rect(startX + 8f, y, 400f, 38f), camModeLabel, 10))
            {
                if (_cameraController != null)
                {
                    _cameraController.isFirstPerson = !_cameraController.isFirstPerson;
                    PlayerPrefs.SetInt("Dimari_FirstPerson", _cameraController.isFirstPerson ? 1 : 0);
                }
            }
            y += 50f;

            if (DrawMenuButton(new Rect(startX + 8f, y, 400f, 42f), "← НАЗАД В МЕНЮ", 11))
            {
                PlayerPrefs.Save();
                _isSettingsOpen = false;
            }
        }

        private void DrawKeyHint(float x, float y, string keyText, string actionText)
        {
            float keyW = Mathf.Max(30f, keyText.Length * 11f + 14f);
            GUI.DrawTexture(new Rect(x, y, keyW, 24f), _keyCapTex);
            GUI.Label(new Rect(x, y, keyW, 24f), $"<color=#111111><b>{keyText}</b></color>", _keyCapStyle);
            GUI.Label(new Rect(x + keyW + 8f, y, 165f, 24f), $"<color=#F2F5F2><b>{actionText}</b></color>", _bottomBarTextStyle);
        }

        private void DrawToastIfActive()
        {
            if (_statusToastTimer <= 0f || string.IsNullOrEmpty(_statusToast)) return;

            float w = 420f;
            float h = 42f;
            Rect r = new Rect(Screen.width - w - 32f, 32f, w, h);
            GUI.DrawTexture(r, _subtitlePanelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, 4f, r.height), _goldAccentTex);
            GUI.Label(
                new Rect(r.x + 14f, r.y + 8f, r.width - 20f, 26f),
                $"<color=#FFFFFF><b>{_statusToast}</b></color>",
                _questActiveStyle
            );
        }

        private void DrawChapter2TransitionOverlay()
        {
            float boxW = 660f;
            float boxH = 165f;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = Screen.height * 0.22f;

            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), _subtitlePanelTex);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 4f), _goldAccentTex);
            GUI.Label(
                new Rect(boxX, boxY + 24f, boxW, 44f),
                "<color=#FFE066><size=26><b>ГЛАВА 1 ЗАВЕРШЕНА</b></size></color>",
                _chapter2TitleStyle
            );
            GUI.Label(
                new Rect(boxX + 24f, boxY + 74f, boxW - 48f, 68f),
                "<color=#FFFFFF><b>Переход во 2-ю главу: «Городок Димарика, Рогово»\nВсе задачи в лесу выполнены!</b></color>",
                _subtitleStyle
            );
        }
    }
}
