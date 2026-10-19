using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    ///    3. Выйти к воде и найти корабль (Глава 2: «Городок Димарика, Рогово»).
    /// 2. Анимацией поднятия предметов (рюкзак на спину, планшет в руки с фотографией).
    /// 3. Финальной кинематографичной КАТ-СЦЕНОЙ С АНИМАЦИЕЙ ХОДЬБЫ:
    ///    Герой произносит: «Нужно идти к воде — возможно, там будет корабль»,
    ///    сам идёт шагом (с 3D-анимацией ходьбы) к берегу/причалу у воды и видит качающийся на воде КОРАБЛЬ и стоящего у корабля ПЕРСОНАЖА!
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

        [Header("Корабль и капитан за штурвалом")]
        [Tooltip("Назначьте основной префаб Stylized Pirate Ship (также для сборки игры).")]
        public GameObject shipPrefab;
        [Header("Большой корабль и разговор с капитаном")]
        [Min(8.5f)] public float shipModelSize = 28f;
        [Tooltip("Старое поле, оставлено для совместимости со сценой. Больше ни на что не влияет: импортированный корабль используется всегда, если он найден и не является заглушкой.")]
        public bool useProceduralShip = false;
        [Tooltip("Принудительно строить встроенный корабль, даже если в проекте есть настоящая модель. Обычно выключено.")]
        public bool forceBuiltInShip = false;
        public bool humanSizedCaptain = true;
        public AudioClip heroCaptainQuestion;
        public AudioClip pirateCaptainAnswer;
        [Header("Плавание к Рогово")]
        [Tooltip("Путь к сцене Demo City, добавленной в Build Settings. Настройка: Dimari/Город — привязать выбранную сцену.")]
        public string cityScenePath = "";
        private float _cityChapterTitleTimer;
        public bool playSailingCutscene = true;
        [Min(10f)] public float sailingDistance = 120f;
        [Tooltip("Необязательно: конечная точка плавания в мировых координатах. Маршрут должен проходить по свободной воде.")]
        public Transform sailingDestination;
        private Collider _cinematicStairCollider;
        private bool _isSailing;
        private Vector3 _sailingStart;
        private Vector3 _sailingEnd;
        private float _sailingElapsed;
        private const float SailingDuration = 78f;
        [Header("Финал у обрыва")]
        [Tooltip("Поставьте на безопасный край обрыва, Z направьте в море. Без маркера остаётся прежний финал у причала.")]
        public Transform cliffArrivalPoint;
        public float oceanLevel = -55f;
        public float oceanSize = 2000f;
        [Header("Вода только перед обрывом")]
        [Min(0f)] public float bayStartFromCliff = 12f;
        [Min(80f)] public float bayWidth = 350f;
        [Min(100f)] public float bayLength = 650f;
        public Material oceanMaterial;
        [Tooltip("Используется только без метки обрыва. С меткой корабль ставится по Ship Distance From Cliff.")]
        public Vector3 shipOffsetFromCliff = new Vector3(8f, 0f, 40f);
        [Header("Спуск к кораблю у обрыва")]
        [Tooltip("Расстояние от метки обрыва до центра корабля в метрах.")]
        public float shipDistanceFromCliff = 38f;
        [Tooltip("Насколько вода ниже метки обрыва. Здесь строятся лестница, причал и корабль.")]
        public float shipWaterDropBelowCliff = 7f;
        [Tooltip("Размер водной глади в проёме у обрыва, метры.")]
        public float cutsceneWaterSize = 240f;
        public AudioClip shoutForHelpVoice;
        private InteractableStoryObject _cliffInteraction;
        private float _cutsceneWaterLevel = -55f;
        private float _cliffGapDistance = 38f;
        private float _cliffShipDistance = 44f;
        private bool _cliffGapFound;
        private readonly List<Vector3> _descentPath = new List<Vector3>();
        private Transform _endingApproachMarker;
        private float _endingFade;
        [Header("Метка перехода к обрыву")]
        [Range(10f, 20f)] public float endingMarkerDistance = 15f;
        [Min(0.5f)] public float endingMarkerRadius = 2f;
        [Tooltip("Назначьте полный префаб Sky Protective Suit (также для сборки игры).")]
        public GameObject captainPrefab;
        [Tooltip("Необязательно: путь к пустому CaptainHelmAnchor внутри префаба корабля. Ось Z — направление взгляда, начало — под ногами.")]
        public string captainHelmAnchorPath = "";
        [Tooltip("Смещение ног от центра найденного штурвала в локальных метрах корня корабля.")]
        public Vector3 captainOffsetFromWheel = new Vector3(0f, -1f, -0.65f);
        [Tooltip("Запасная позиция ног на кормовой палубе, если отдельного штурвала в модели нет. Подстройте под модель.")]
        public Vector3 captainFallbackPosition = new Vector3(0f, 1.8f, -2.2f);
        public Vector3 captainFacingEuler = Vector3.zero;
        [Header("Подогнанный капитан Sky Protective Suit / Stylized Pirate Ship")]
        [Tooltip("Использовать проверенную расстановку капитана вместо поиска штурвала. Отключите для другого корабля или CaptainHelmAnchor.")]
        public bool useFittedCaptainPlacement = true;
        public Vector3 fittedCaptainPosition = new Vector3(0.182f, 2.124f, -1.563f);
        [Min(0.01f)]
        [Tooltip("Масштаб корня NPC относительно нормализованной модели, подобранный на корабле.")]
        public float fittedCaptainScale = 0.3f;
        [Header("Автопроверка корабля и капитана")]
        [Tooltip("Измерять реальные размеры корабля и ставить капитана/героя на настоящую палубу, а не на расчётную высоту. Если корабль подходит, ничего не меняется.")]
        public bool autoFitCrewToMeasuredDeck = true;
        [Tooltip("Камера кат-сцены не должна оказываться внутри корпуса, скал или паруса.")]
        public bool keepCutsceneCameraOutOfGeometry = true;

        public Transform ShipTransform => _shipTransform;
        public Transform CaptainTransform => _npcTransform;
        public bool ShipDeckMeasured => _shipDeckMeasured;
        public float MeasuredDeckLocalY => _measuredDeckLocalY;
        public Bounds MeasuredShipLocalBounds => _measuredShipLocalBounds;
        public Transform CliffArrivalPoint => cliffArrivalPoint;

        public bool IsMenuOpen => _isMenuOpen;
        public bool HasGameStarted => _hasGameStarted;
        public bool IsTabletViewerOpen => _isTabletViewerOpen;
        public bool IsHudVisible => _showHUD;
        /// <summary>Frame in which the normal ESC handler ran; used by the fallback handler to avoid double toggling.</summary>
        public static int LastMenuHotkeyFrame { get; private set; } = -1;

        private string _currentSubtitle = "";
        private float _subtitleTimer = 0f;
        private float _subtitleAlpha = 0f;

        private string _interactionPrompt = "";
        private bool _showHUD = false;
        private bool _isMenuOpen = true;
        private bool _isSettingsOpen = false;
        private bool _hasGameStarted = false;
        private bool _isChapter2Reached = false;
        private bool _isPlayingEndingCutscene = false;

        // Состояние планшета и рюкзака
        private bool _isTabletViewerOpen = false;
        private bool _hasPickedUpTablet = false;
        private bool _isHoldingTabletInHands = false;
        private float _holdTabletBlend = 0f;

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

        // Объекты финала у воды: берег/причал, 3D-корабль на воде и NPC-капитан
        private Transform _shoreStartPoint;
        private Transform _pierWalkEndPoint;
        private Transform _shipTransform;
        private bool _isBoardingFromWater;
        private bool[,] _seaCells;
        private float _seaCellSize = 16f;
        private float _seaWidth;
        private float _seaLength;
        private Vector3 _boardingDeckLocal;
        public Transform ShipRoot => _shipTransform;
        private Transform _npcTransform;
        private Animator _captainAnimator;
        private Transform _captainWheel;
        private Transform _wheelLeftGrip;
        private Transform _wheelRightGrip;
        private bool _captainConversationActive;
        private bool _hasReachedShip;
        private bool _waitingForCaptainInteraction;
        private Vector3 _shipBasePos;
        private Quaternion _shipBaseRot;

        private string _statusToast = "";
        private float _statusToastTimer = 0f;
        private int _selectedMenuIndex = 0;

        private bool _newGameRequested;

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
                title = "3. Выйти к воде и найти корабль (Глава 2).",
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
            _hasPickedUpTablet = false;
            _isHoldingTabletInHands = false;
            _holdTabletBlend = 0f;
            _isTabletViewerOpen = false;
            _isPlayingEndingCutscene = false;

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
            bagInteract.inspectMonologue = "«Отлично, мой рюкзак! Надену его на плечи. Внутри ключи и фляжка, но планшета тут нет — поищу его в траве дальше.»";

            // =========================================================================
            // 2. ПЛАНШЕТ С ФОТОГРАФИЕЙ (Задача 2: Найти и взять планшет)
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
            tabletInteract.inspectMonologue = "«А вот и планшет! На экране осталась наша вчерашняя фотка... Нужно идти к воде — возможно, там будет корабль.»";

            // =========================================================================
            // 3. ВЫХОД К ВОДЕ, ПРИЧАЛ, КОРАБЛЬ И NPC-ПЕРСОНАЖ (Задача 3 + Кат-сцена)
            // =========================================================================
            Vector3 shorePos = cliffArrivalPoint != null ? cliffArrivalPoint.position : GetGroundPoint(pPos + forward * 15.5f, pPos.y);
            if (cliffArrivalPoint != null)
            {
                // The marker keeps the rotation it has in the scene: the user placed it at the
                // cliff edge and the cutscene must happen exactly there.
                forward = Vector3.ProjectOnPlane(cliffArrivalPoint.forward, Vector3.up).normalized;
            }
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            GameObject shoreEventRoot = new GameObject("Quest3_ShoreAndShip");
            shoreEventRoot.transform.SetParent(storyRoot.transform);
            shoreEventRoot.transform.position = shorePos;
            shoreEventRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            CreateShorelinePierShipAndNpc(shoreEventRoot.transform, shorePos, forward, right);

            InteractableStoryObject shoreInteract = shoreEventRoot.AddComponent<InteractableStoryObject>();
            shoreInteract.questStepId = "rogovo_exit";
            shoreInteract.objectTitle = "Выход к воде (Подойти к кораблю)";
            shoreInteract.inspectMonologue = "«Нужно идти к воде — возможно, там будет корабль. Постой-ка… Там у берега и правда стоит корабль!»";
            shoreInteract.interactionDistance = 3.8f;
            _cliffInteraction = cliffArrivalPoint != null ? shoreInteract : null;
            if (cliffArrivalPoint != null)
            {
                GameObject marker = new GameObject("Dimari_CliffCutsceneApproachMarker");
                marker.transform.SetParent(storyRoot.transform, true);
                Vector3 approachForward = _player != null ? _player.transform.forward : Vector3.forward;
                approachForward = Vector3.ProjectOnPlane(approachForward, Vector3.up).normalized;
                // Measure from the tablet's original ground position, not the player spawn.
                marker.transform.position = GetGroundPoint(
                    tabletPos + approachForward * endingMarkerDistance, tabletPos.y);
                _endingApproachMarker = marker.transform;
                // The cliff is a cinematic destination, not an interactable across the map.
                shoreInteract.enabled = false;
            }
        }

        /// <summary>
        /// Создаёт береговой деревянный пирс у воды, водную гладь залива,
        /// большой 3D-корабль (из Asset Store или сборный 3D-корабль) и NPC-персонажа на причале у корабля!
        /// </summary>
        private void CreateShorelinePierShipAndNpc(Transform root, Vector3 shorePos, Vector3 forward, Vector3 right)
        {
            // A placeholder saved into the scene in an earlier session would sit next to the
            // real ship forever. Remove it before the pier is built.
            PurgeShipPlaceholderObjects();
            _descentPath.Clear();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material woodMat = new Material(litShader);
            SetMatColor(woodMat, new Color(0.36f, 0.24f, 0.14f, 1f));
            if (woodMat.HasProperty("_Smoothness")) woodMat.SetFloat("_Smoothness", 0.2f);

            Material darkWoodMat = new Material(litShader);
            SetMatColor(darkWoodMat, new Color(0.24f, 0.15f, 0.09f, 1f));

            Material waterMat = new Material(litShader);
            SetMatColor(waterMat, new Color(0.08f, 0.32f, 0.42f, 0.94f));
            if (waterMat.HasProperty("_Smoothness")) waterMat.SetFloat("_Smoothness", 0.92f);
            if (waterMat.HasProperty("_Metallic")) waterMat.SetFloat("_Metallic", 0.15f);

            // Точка начала и конца прохода героя в кат-сцене
            GameObject startPt = new GameObject("CutsceneWalkStart");
            startPt.transform.SetParent(root, false);
            startPt.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            _shoreStartPoint = startPt.transform;

            GameObject endPt = new GameObject("CutsceneWalkEnd");
            endPt.transform.SetParent(root, false);
            endPt.transform.localPosition = new Vector3(0f, 0.18f, 5.2f);
            _pierWalkEndPoint = endPt.transform;
            if (cliffArrivalPoint != null)
            {
                startPt.transform.localPosition = new Vector3(0f, 0f, -4f);
                endPt.transform.localPosition = Vector3.zero;
                startPt.transform.position = GetGroundPoint(startPt.transform.position, shorePos.y);
                BuildCliffDescent(root, waterMat, woodMat, darkWoodMat);
                CreateSurroundingOcean(waterMat);
            }

            // 1. Деревянный причал (мостик к воде, по которому идёт герой в кат-сцене)
            GameObject pierRoot = new GameObject("WoodenShorePier");
            pierRoot.transform.SetParent(root, false);

            // Настил причала (длина 7 метров) с коллайдером, чтобы персонаж шёл по нему!
            GameObject pierDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pierDeck.name = "PierWalkwayDeck";
            pierDeck.transform.SetParent(pierRoot.transform, false);
            pierDeck.transform.localPosition = new Vector3(0f, 0.12f, 3.2f);
            pierDeck.transform.localScale = new Vector3(2.2f, 0.16f, 7.0f);
            pierDeck.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Сваи причала
            for (int i = 0; i < 4; i++)
            {
                float z = 0.6f + i * 1.8f;
                CreatePart(PrimitiveType.Cylinder, $"PierPost_L_{i}", pierRoot.transform,
                    new Vector3(-1.05f, 0.25f, z), Quaternion.identity, new Vector3(0.16f, 0.85f, 0.16f), darkWoodMat);
                CreatePart(PrimitiveType.Cylinder, $"PierPost_R_{i}", pierRoot.transform,
                    new Vector3(1.05f, 0.25f, z), Quaternion.identity, new Vector3(0.16f, 0.85f, 0.16f), darkWoodMat);
            }

            // Фонарный столб у входа на причал
            CreatePart(PrimitiveType.Cylinder, "LanternPost", pierRoot.transform,
                new Vector3(-1.15f, 1.15f, 0.4f), Quaternion.identity, new Vector3(0.10f, 1.15f, 0.10f), darkWoodMat);

            Material lanternGlowMat = new Material(litShader);
            Color warmLight = new Color(1.0f, 0.82f, 0.38f, 1f);
            SetMatColor(lanternGlowMat, warmLight);
            if (lanternGlowMat.HasProperty("_EmissionColor"))
            {
                lanternGlowMat.EnableKeyword("_EMISSION");
                lanternGlowMat.SetColor("_EmissionColor", warmLight * 2.5f);
            }
            CreatePart(PrimitiveType.Sphere, "PierLanternBulb", pierRoot.transform,
                new Vector3(-1.15f, 2.35f, 0.4f), Quaternion.identity, new Vector3(0.28f, 0.28f, 0.28f), lanternGlowMat);

            // 2. Зеркальная водная гладь перед причалом (под кораблём)
            CreatePart(PrimitiveType.Cube, "ShoreWaterBay", root,
                new Vector3(0f, -0.22f, 19.0f), Quaternion.identity, new Vector3(46f, 0.15f, 30f), waterMat);

            if (cliffArrivalPoint != null)
            {
                pierRoot.SetActive(false);
                Transform bay = root.Find("ShoreWaterBay");
                if (bay != null) bay.gameObject.SetActive(false);
            }

            // 3. 3D-КОРАБЛЬ НА ВОДЕ У ПРИЧАЛА
            GameObject shipObj = new GameObject("Dimari_WaterShip");
            shipObj.transform.SetParent(root, false);
            shipObj.transform.localPosition = new Vector3(1.2f, -0.05f, 12.2f);
            shipObj.transform.localRotation = Quaternion.Euler(0f, -68f, 0f);

            if (cliffArrivalPoint != null)
            {
                // The ship stands in the water of the gap right below the cliff, where the hero
                // comes down and sees it. No searching for distant ocean cells.
                Vector3 seaPosition = root.TransformPoint(new Vector3(0f, 0f, Mathf.Max(12f, _cliffShipDistance)));
                seaPosition.y = _cutsceneWaterLevel;
                shipObj.transform.position = seaPosition;
                shipObj.transform.rotation = root.rotation * Quaternion.Euler(0f, 15f, 0f);
                Debug.Log("[Dimari] Корабль поставлен у обрыва: " + seaPosition.ToString("F1")
                    + ", вода на " + _cutsceneWaterLevel.ToString("F1") + " м.", this);
            }

            bool spawnedAssetShip = TrySpawnAssetStoreShipPrefab(shipObj.transform);
            if (!spawnedAssetShip)
            {
                Debug.LogWarning("[Dimari] Корабль из ассетов не найден. Строю встроенный корабль. "
                    + "Чтобы использовать модель, назначьте её в поле Ship Prefab.", this);
                CreateDetailedProceduralShip(shipObj.transform);
            }

            _shipTransform = shipObj.transform;
            ConfigureBoardingDeck();
            if (cliffArrivalPoint != null && _shipDeckMeasured)
            {
                // Посадить корпус в воду, чтобы корабль не стоял на самой поверхности.
                float sink = Mathf.Clamp(_measuredShipLocalBounds.size.y * 0.12f, 0.3f, 2f);
                _shipTransform.position -= Vector3.up * sink;
                Debug.Log("[Dimari] Корабль посажен в воду на " + sink.ToString("F2") + " м.", this);
            }
            _shipBasePos = _shipTransform.localPosition;
            _shipBaseRot = _shipTransform.localRotation;

            // 4. NPC-ПЕРСОНАЖ (Капитан / Проводник), ожидающий у трапа корабля на конце причала
            GameObject npcObj = new GameObject("Dimari_ShipCaptain_NPC");
            npcObj.transform.SetParent(shipObj.transform, false);
            // The helm must be known first: the captain belongs at the wheel, not at a
            // serialized fitted position that can be inside the cabin of another ship model.
            foreach (Transform part in shipObj.GetComponentsInChildren<Transform>(true))
            {
                string partName = part.name.ToLowerInvariant();
                if (partName == "helmleftgrip") _wheelLeftGrip = part;
                if (partName == "helmrightgrip") _wheelRightGrip = part;
            }
            _captainWheel = ChooseHelm(shipObj.transform);
            PositionCaptainAtHelm(npcObj.transform, shipObj.transform);

            SpawnNpcCharacterAtShip(npcObj.transform);
            _npcTransform = npcObj.transform;
            _captainAnimator = npcObj.GetComponentInChildren<Animator>();

            BoxCollider triggerCol = root.gameObject.AddComponent<BoxCollider>();
            triggerCol.center = new Vector3(0f, 1.0f, 0.5f);
            triggerCol.size = new Vector3(2.8f, 2.2f, 2.8f);
        }

        /// <summary>
        /// Вода в проёме у обрыва, деревянная лестница по скале и нижний причал.
        /// Строится в локальных координатах Quest3_ShoreAndShip: local +Z — от обрыва в море.
        /// </summary>
        private void BuildCliffDescent(Transform root, Material waterMat, Material woodMat, Material darkWoodMat)
        {
            _descentPath.Clear();
            float drop = Mathf.Clamp(shipWaterDropBelowCliff, 2.5f, 40f);
            _cutsceneWaterLevel = cliffArrivalPoint.position.y - drop;
            float waterSize = Mathf.Clamp(cutsceneWaterSize, 60f, 600f);

            // 1. Вода прямо в проёме между скалами, а не на уровне oceanLevel.
            CreatePart(PrimitiveType.Cube, "CutsceneGapWater", root,
                new Vector3(0f, -drop, waterSize * 0.4f), Quaternion.identity,
                new Vector3(waterSize, 0.12f, waterSize), waterMat);

            // 2. Настоящий проём: идём вперёд от метки, пока впереди не останется только пустота.
            Vector3 flatForward = Vector3.ProjectOnPlane(cliffArrivalPoint.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
            flatForward.Normalize();
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;
            Vector3 gapOrigin = cliffArrivalPoint.position;
            gapOrigin.y = _cutsceneWaterLevel;
            _cliffGapDistance = FindCliffGapDistance(gapOrigin, flatForward, flatRight, _cutsceneWaterLevel,
                Mathf.Max(12f, shipDistanceFromCliff));
            _cliffGapFound = _cliffGapDistance < Mathf.Max(12f, shipDistanceFromCliff) - 0.1f;
            // Центр корабля чуть дальше начала проёма, чтобы корпус целиком стоял на воде.
            _cliffShipDistance = _cliffGapDistance + Mathf.Max(4f, shipModelSize * 0.3f);
            Debug.Log("[Dimari] Проём у обрыва: корабль в " + _cliffGapDistance.ToString("F1")
                + " м впереди метки, вода на " + _cutsceneWaterLevel.ToString("F1") + " м"
                + (_cliffGapFound ? " (найдено пустое место между скалами)." : " (пустое место не найдено, используется заданное расстояние)."), this);

            // 3. Лестница вниз по скале: каждая ступень проходима (BoxCollider).
            int steps = Mathf.Clamp(Mathf.CeilToInt(drop / 0.26f), 6, 64);
            float rise = drop / steps;
            float run = 0.38f;
            float width = 2.6f;
            float firstStepZ = 0.9f;
            GameObject stairs = new GameObject("CliffDescentStairs");
            stairs.transform.SetParent(root, false);
            for (int i = 0; i < steps; i++)
            {
                GameObject step = CreatePart(PrimitiveType.Cube, "CliffStep_" + i, stairs.transform,
                    new Vector3(0f, -rise * (i + 1) - 0.11f, firstStepZ + run * (i + 0.5f)),
                    Quaternion.identity, new Vector3(width, 0.22f, run + 0.05f),
                    i % 2 == 0 ? woodMat : darkWoodMat);
                step.AddComponent<BoxCollider>();
                if (i % 4 == 0 && i > 0)
                {
                    CreatePart(PrimitiveType.Cylinder, "CliffRailing_" + i, stairs.transform,
                        new Vector3(width * 0.5f, -rise * i + 0.35f, firstStepZ + run * i),
                        Quaternion.identity, new Vector3(0.09f, 0.45f, 0.09f), darkWoodMat);
                }
            }
            float stairEndZ = firstStepZ + run * steps;
            float landingZ = stairEndZ + 1.6f;

            // 3. Нижний причал на воде.
            GameObject landing = CreatePart(PrimitiveType.Cube, "CliffLanding", root,
                new Vector3(0f, -drop + 0.02f, landingZ + 2.4f), Quaternion.identity,
                new Vector3(4.6f, 0.22f, 7.0f), woodMat);
            landing.AddComponent<BoxCollider>();
            for (int i = 0; i < 3; i++)
            {
                float z = landingZ + i * 2.2f;
                CreatePart(PrimitiveType.Cylinder, "LandingPost_L_" + i, root,
                    new Vector3(-2.0f, -drop - 0.55f, z), Quaternion.identity,
                    new Vector3(0.12f, 0.75f, 0.12f), darkWoodMat);
                CreatePart(PrimitiveType.Cylinder, "LandingPost_R_" + i, root,
                    new Vector3(2.0f, -drop - 0.55f, z), Quaternion.identity,
                    new Vector3(0.12f, 0.75f, 0.12f), darkWoodMat);
            }

            // 4. Дорожка от причала к кораблю: герой выходит по ней к борту.
            float walkwayStart = landingZ + 5.9f;
            float hullFront = _cliffShipDistance - Mathf.Max(8.5f, shipModelSize) * 0.5f - 2.5f;
            float walkwayEnd = Mathf.Max(walkwayStart + 1f, hullFront);
            if (walkwayEnd - walkwayStart > 1.5f)
            {
                GameObject walkway = CreatePart(PrimitiveType.Cube, "CliffShipWalkway", root,
                    new Vector3(0f, -drop + 0.02f, (walkwayStart + walkwayEnd) * 0.5f), Quaternion.identity,
                    new Vector3(2.2f, 0.2f, walkwayEnd - walkwayStart), woodMat);
                walkway.AddComponent<BoxCollider>();
                for (float z = walkwayStart + 2f; z < walkwayEnd; z += 4f)
                {
                    CreatePart(PrimitiveType.Cylinder, "WalkwayPost_L", root,
                        new Vector3(-0.95f, -drop - 0.5f, z), Quaternion.identity,
                        new Vector3(0.1f, 0.7f, 0.1f), darkWoodMat);
                    CreatePart(PrimitiveType.Cylinder, "WalkwayPost_R", root,
                        new Vector3(0.95f, -drop - 0.5f, z), Quaternion.identity,
                        new Vector3(0.1f, 0.7f, 0.1f), darkWoodMat);
                }
            }
            else walkwayEnd = landingZ + 1.2f;

            // 5. Маршрут героя: сверху по скале, вниз по ступеням, по причалу и дорожке к кораблю.
            Vector3 topStart = root.TransformPoint(new Vector3(0f, 0f, -4.5f));
            topStart.y = GetGroundPoint(topStart, topStart.y).y + 0.05f;
            _descentPath.Add(topStart);
            _descentPath.Add(root.TransformPoint(new Vector3(0f, 0.05f, 0.35f)));
            for (int i = 1; i <= steps; i += 2)
                _descentPath.Add(root.TransformPoint(new Vector3(0f, -rise * i + 0.12f, firstStepZ + run * (i - 0.5f))));
            _descentPath.Add(root.TransformPoint(new Vector3(0f, -drop + 0.2f, landingZ + 1.2f)));
            _descentPath.Add(root.TransformPoint(new Vector3(0f, -drop + 0.2f, walkwayEnd)));

            if (_shoreStartPoint != null) _shoreStartPoint.position = _descentPath[0];
            if (_pierWalkEndPoint != null) _pierWalkEndPoint.position = _descentPath[_descentPath.Count - 1];

            // Диагностика: не окажется ли корабль внутри скалы.
            Vector3 shipProbe = root.TransformPoint(new Vector3(0f, 0f, Mathf.Max(12f, _cliffShipDistance)));
            float rockTop = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(shipProbe + Vector3.up * 120f, Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.point.y > rockTop) rockTop = hit.point.y;
            if (rockTop > _cutsceneWaterLevel + 0.5f)
                Debug.LogWarning("[Dimari] Под точкой корабля есть рельеф на " + rockTop.ToString("F1")
                    + " м, а вода на " + _cutsceneWaterLevel.ToString("F1")
                    + " м. Увеличьте Ship Distance From Cliff или Ship Water Drop Below Cliff.", this);
            else
                Debug.Log("[Dimari] Место корабля свободно: рельеф ниже воды (" + rockTop.ToString("F1") + " м).", this);

            Debug.Log("[Dimari] Спуск к кораблю: дорожка до " + walkwayEnd.ToString("F1")
                + " м, вода на " + _cutsceneWaterLevel.ToString("F1") + " м (обрыв "
                + cliffArrivalPoint.position.y.ToString("F1") + " м), ступеней " + steps
                + ", корабль в " + Mathf.Max(12f, _cliffShipDistance).ToString("F0") + " м от метки, дорожка до "
                + walkwayEnd.ToString("F0") + " м.", this);
        }

        /// <summary>
        /// Идёт от обрыва вперёд и возвращает расстояние, на котором впереди уже только пустота:
        /// именно там, в проёме между скалами, должен стоять корабль.
        /// </summary>
        private float FindCliffGapDistance(Vector3 origin, Vector3 forward, Vector3 right, float waterLevel, float fallback)
        {
            for (float distance = 12f; distance <= 90f; distance += 2f)
            {
                bool empty = true;
                foreach (float lateral in new[] { 0f, -6f, 6f })
                {
                    Vector3 probe = origin + forward * distance + right * lateral;
                    foreach (RaycastHit hit in Physics.RaycastAll(probe + Vector3.up * 150f, Vector3.down,
                        320f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (_player != null && hit.transform.IsChildOf(_player.transform)) continue;
                        if (hit.collider is TerrainCollider || hit.point.y > waterLevel + 0.4f) { empty = false; break; }
                    }
                    if (!empty) break;
                }
                if (empty) return distance;
            }
            return fallback;
        }

        /// <summary>Точка на ломаной маршрута по параметру 0..1 и направление движения.</summary>
        private static Vector3 SamplePath(List<Vector3> path, float t, out Vector3 direction)
        {
            direction = Vector3.forward;
            if (path == null || path.Count == 0) return Vector3.zero;
            if (path.Count == 1) return path[0];
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float target = Mathf.Clamp01(t) * total;
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (target > segment && i < path.Count - 1) { target -= segment; continue; }
                Vector3 delta = path[i] - path[i - 1];
                if (delta.sqrMagnitude > 0.0001f) direction = delta.normalized;
                return Vector3.Lerp(path[i - 1], path[i], segment > 0.0001f ? Mathf.Clamp01(target / segment) : 0f);
            }
            return path[path.Count - 1];
        }

        private void CreateSurroundingOcean(Material fallback)
        {
            // Extend seaward, not backwards into the starting forest. Build holes over land.
            _seaWidth = Mathf.Max(bayWidth, Mathf.Max(100f, oceanSize));
            _seaLength = Mathf.Max(bayLength, Mathf.Max(100f, oceanSize));
            int columns = Mathf.Clamp(Mathf.CeilToInt(_seaWidth / _seaCellSize), 8, 160);
            int rows = Mathf.Clamp(Mathf.CeilToInt(_seaLength / _seaCellSize), 8, 160);
            float dx = _seaWidth / columns, dz = _seaLength / rows;
            _seaCells = new bool[columns, rows];
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> triangles = new List<int>();
            Quaternion rotation = BayRotation();
            Vector3 origin = cliffArrivalPoint.position;
            origin.y = oceanLevel;
            Physics.SyncTransforms();
            for (int x = 0; x < columns; x++)
                for (int z = 0; z < rows; z++)
                {
                    float left = -_seaWidth * 0.5f + x * dx;
                    float near = bayStartFromCliff + z * dz;
                    Vector3 center = origin + rotation * new Vector3(left + dx * 0.5f, 0f, near + dz * 0.5f);
                    if (IsDryLandBelowSea(center) ||
                        IsDryLandBelowSea(origin + rotation * new Vector3(left, 0f, near)) ||
                        IsDryLandBelowSea(origin + rotation * new Vector3(left + dx, 0f, near)) ||
                        IsDryLandBelowSea(origin + rotation * new Vector3(left, 0f, near + dz)) ||
                        IsDryLandBelowSea(origin + rotation * new Vector3(left + dx, 0f, near + dz))) continue;
                    _seaCells[x, z] = true;
                    int i = vertices.Count;
                    vertices.Add(new Vector3(left, 0f, near));
                    vertices.Add(new Vector3(left, 0f, near + dz));
                    vertices.Add(new Vector3(left + dx, 0f, near + dz));
                    vertices.Add(new Vector3(left + dx, 0f, near));
                    uv.Add(new Vector2((float)x / columns, (float)z / rows));
                    uv.Add(new Vector2((float)x / columns, (float)(z + 1) / rows));
                    uv.Add(new Vector2((float)(x + 1) / columns, (float)(z + 1) / rows));
                    uv.Add(new Vector2((float)(x + 1) / columns, (float)z / rows));
                    triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                    triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
                }
            GameObject ocean = new GameObject("Dimari_SurroundingOcean");
            ocean.transform.SetParent(_shoreStartPoint.parent, true);
            ocean.transform.SetPositionAndRotation(origin, rotation);
            Mesh mesh = new Mesh { name = "Dimari_SeaWithLandCutouts", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ocean.AddComponent<MeshFilter>().sharedMesh = mesh;
            ocean.AddComponent<MeshRenderer>().sharedMaterial = oceanMaterial != null ? oceanMaterial : fallback;
        }

        private bool IsDryLandBelowSea(Vector3 point)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 500f, Vector3.down,
                1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                if ((_player != null && hit.transform.IsChildOf(_player.transform)) ||
                    (_shipTransform != null && hit.transform.IsChildOf(_shipTransform))) continue;
                if (hit.transform.GetComponentInParent<InteractableStoryObject>() != null) continue;
                // Terrain is the playable island, even in depressions below sea level.
                if (hit.collider is TerrainCollider || hit.point.y >= oceanLevel - 0.5f) return true;
            }
            return false;
        }

        public bool TryGetWaterHeight(Vector3 position, out float height)
        {
            height = oceanLevel;
            if (cliffArrivalPoint == null || _shipTransform == null || !IsInsideOcean(position)) return false;
            // Never apply buoyancy on dry ground just because its Y is below sea level.
            foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 2f, Vector3.down,
                4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if ((_player != null && hit.transform.IsChildOf(_player.transform)) ||
                    hit.transform.IsChildOf(_shipTransform)) continue;
                if (hit.normal.y > 0.45f && hit.point.y >= position.y - 0.25f &&
                    hit.point.y <= position.y + 0.4f) return false;
            }
            return true;
        }

        private bool _shipDeckMeasured;
        private float _measuredDeckLocalY;
        private Bounds _measuredShipLocalBounds;
        private bool _shipGeometryReported;

        /// <summary>Sails, masts and rigging are above the crew, so they must not define the deck.</summary>
        private static bool IsRigging(Transform part)
        {
            if (part == null) return false;
            string name = part.name.ToLowerInvariant();
            return name.Contains("sail") || name.Contains("canvas") || name.Contains("rope") ||
                   name.Contains("rig") || name.Contains("mast") || name.Contains("yard") ||
                   name.Contains("flag") || name.Contains("shroud") || name.Contains("spar");
        }

        /// <summary>Correct local bounds: every corner of the world bounds is transformed separately.</summary>
        private void MeasureShipBounds(out Bounds local)
        {
            local = new Bounds();
            Renderer[] renderers = _shipTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;
            bool has = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Bounds world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? world.min.x : world.max.x,
                        (corner & 2) == 0 ? world.min.y : world.max.y,
                        (corner & 4) == 0 ? world.min.z : world.max.z);
                    Vector3 localPoint = _shipTransform.InverseTransformPoint(point);
                    if (!has) { local = new Bounds(localPoint, Vector3.zero); has = true; }
                    else local.Encapsulate(localPoint);
                }
            }
        }

        /// <summary>
        /// Measures the real deck. A mast or sail must not be mistaken for a deck:
        /// only upward faces are used, rigging is skipped, and the largest flat area wins.
        /// </summary>
        private void MeasureShipGeometry()
        {
            _shipDeckMeasured = false;
            if (_shipTransform == null) return;
            MeasureShipBounds(out _measuredShipLocalBounds);
            if (_measuredShipLocalBounds.size.y < 0.1f) return;

            const int columns = 13;
            const float binSize = 0.25f;
            var area = new System.Collections.Generic.Dictionary<int, int>();
            for (int x = 0; x < columns; x++)
                for (int z = 0; z < columns; z++)
                {
                    float lx = Mathf.Lerp(-_measuredShipLocalBounds.extents.x * 0.72f,
                        _measuredShipLocalBounds.extents.x * 0.72f, x / (columns - 1f));
                    float lz = Mathf.Lerp(-_measuredShipLocalBounds.extents.z * 0.72f,
                        _measuredShipLocalBounds.extents.z * 0.72f, z / (columns - 1f));
                    Vector3 origin = _shipTransform.TransformPoint(new Vector3(lx,
                        _measuredShipLocalBounds.max.y + 2f, lz));
                    RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down,
                        _measuredShipLocalBounds.size.y + 6f, ~0, QueryTriggerInteraction.Ignore);
                    var binsInColumn = new System.Collections.Generic.HashSet<int>();
                    foreach (RaycastHit hit in hits)
                    {
                        if (_player != null && hit.transform.IsChildOf(_player.transform)) continue;
                        if (!hit.transform.IsChildOf(_shipTransform)) continue;
                        // Horizontal, walkable surfaces only: sails are vertical or slanted.
                        if (hit.normal.y < 0.72f) continue;
                        if (IsRigging(hit.transform)) continue;
                        float y = _shipTransform.InverseTransformPoint(hit.point).y;
                        if (y < _measuredShipLocalBounds.min.y + 0.2f) continue;
                        binsInColumn.Add(Mathf.RoundToInt(y / binSize));
                    }
                    foreach (int bin in binsInColumn)
                    {
                        area.TryGetValue(bin, out int count);
                        area[bin] = count + 1;
                    }
                }
            if (area.Count == 0) return;
            int bestBin = 0, bestCount = -1;
            foreach (var pair in area)
                if (pair.Value > bestCount) { bestCount = pair.Value; bestBin = pair.Key; }
            _measuredDeckLocalY = bestBin * binSize;
            // A "deck" in the top 15% of the hull is a roof or a lid, not the floor the crew stands on.
            if (_measuredDeckLocalY > _measuredShipLocalBounds.min.y + _measuredShipLocalBounds.size.y * 0.85f)
            {
                Debug.LogWarning("[Dimari] Найденная поверхность " + _measuredDeckLocalY.ToString("F2")
                    + " м слишком высоко для палубы (верх корпуса " + _measuredShipLocalBounds.max.y.ToString("F2")
                    + " м). Модель корабля похожа на заглушку/коробку.");
                return;
            }
            _shipDeckMeasured = bestCount >= Mathf.Max(3, columns);
        }

        /// <summary>
        /// Top walkable surface at a local XZ, never the sail above it.
        /// Uses the measured deck as an upper limit so the cabin roof is not mistaken for it.
        /// </summary>
        private float DeckLocalYAt(Vector3 localPoint)
        {
            if (_shipTransform == null) return 0f;
            // Stay at deck level: a cabin roof or cargo hatch is above the crew, not the floor.
            float ceiling = _measuredDeckLocalY + 0.6f;
            float top = float.NegativeInfinity;
            Vector3 origin = _shipTransform.TransformPoint(new Vector3(localPoint.x,
                _measuredShipLocalBounds.max.y + 2f, localPoint.z));
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down,
                _measuredShipLocalBounds.size.y + 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_player != null && hit.transform.IsChildOf(_player.transform)) continue;
                if (!hit.transform.IsChildOf(_shipTransform)) continue;
                if (hit.normal.y < 0.62f) continue;
                if (IsRigging(hit.transform)) continue;
                float y = _shipTransform.InverseTransformPoint(hit.point).y;
                if (y > ceiling || y < _measuredDeckLocalY - 1.2f) continue;
                if (y > top) top = y;
            }
            return top > float.NegativeInfinity ? top : _measuredDeckLocalY;
        }

        /// <summary>Wheel on the open deck wins over a decorative wheel inside the cabin.</summary>
        private Transform ChooseHelm(Transform ship)
        {
            Transform best = null;
            float bestScore = float.MinValue;
            foreach (Transform part in ship.GetComponentsInChildren<Transform>(true))
            {
                string name = part.name.ToLowerInvariant().Replace("_", "").Replace(" ", "");
                bool named = name.Contains("wheel") || name.Contains("helm") ||
                             name.Contains("steering") || name.Contains("штурвал");
                if (!named) continue;
                Vector3 point = part.position;
                Renderer renderer = part.GetComponentInChildren<Renderer>();
                if (renderer != null) point = renderer.bounds.center;
                float clearance = 8f;
                foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 0.5f, Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(ship)) continue;
                    clearance = Mathf.Min(clearance, hit.distance);
                }
                float deckDistance = _shipDeckMeasured
                    ? Mathf.Abs(ship.InverseTransformPoint(point).y - _measuredDeckLocalY) : 0f;
                float score = clearance * 4f - deckDistance * 4f;
                if (name.Contains("helm")) score += 6f;
                if (name.Contains("steering")) score += 3f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = part;
            }
            if (best != null)
                Debug.Log("[Dimari] Штурвал выбран: " + best.name + " (открытая палуба важнее декоративного).", best);
            return best;
        }

        /// <summary>Nearest point on the deck with open sky — used when the fitted point is indoors.</summary>
        private Vector3 FindOpenDeckPoint(Transform ship, Vector3 nearLocal)
        {
            if (!_shipDeckMeasured) return nearLocal;
            Vector3 best = nearLocal;
            float bestScore = float.MinValue;
            for (int x = -4; x <= 4; x++)
                for (int z = -4; z <= 4; z++)
                {
                    Vector3 local = new Vector3(
                        nearLocal.x + x * _measuredShipLocalBounds.extents.x * 0.12f,
                        _measuredDeckLocalY + 0.02f,
                        nearLocal.z + z * _measuredShipLocalBounds.extents.z * 0.12f);
                    Vector3 world = ship.TransformPoint(local);
                    float clearance = 12f;
                    foreach (RaycastHit hit in Physics.RaycastAll(world + Vector3.up * 0.2f, Vector3.up, 12f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.IsChildOf(ship)) continue;
                        clearance = Mathf.Min(clearance, hit.distance);
                    }
                    float score = Mathf.Min(clearance, 12f) - Vector3.Distance(local, nearLocal) * 0.35f;
                    if (score > bestScore) { bestScore = score; best = local; }
                }
            return best;
        }

        private void ConfigureBoardingDeck()
        {
            _boardingDeckLocal = new Vector3(0f, 1f, 0f) * (shipModelSize / 8.5f);
            // An authored deck marker (or a mesh literally called Floor) is authoritative:
            // no raycast guessing for models without colliders or with a closed lid.
            bool explicitDeck = false;
            foreach (MeshFilter mesh in _shipTransform.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null) continue;
                // Sails and rigging need no collision: they only blocked the camera and the deck probe.
                bool rigging = IsRigging(mesh.transform) || IsRigging(mesh.transform.parent);
                if (mesh.GetComponent<Collider>() == null && !rigging)
                {
                    MeshCollider collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                }
                string name = mesh.name.ToLowerInvariant();
                if (name.Contains("floor") && !name.Contains("back") && !name.Contains("bottom"))
                {
                    Bounds bounds = mesh.sharedMesh.bounds;
                    _boardingDeckLocal = _shipTransform.InverseTransformPoint(mesh.transform.TransformPoint(
                        new Vector3(bounds.center.x, bounds.max.y, bounds.center.z))) + Vector3.up * 0.08f;
                    explicitDeck = true;
                }
            }
            foreach (Transform point in _shipTransform.GetComponentsInChildren<Transform>())
                if (point.name == "ShipBoardingPoint")
                {
                    _boardingDeckLocal = _shipTransform.InverseTransformPoint(point.position);
                    explicitDeck = true;
                    break;
                }
            Physics.SyncTransforms();
            MeasureShipBounds(out _measuredShipLocalBounds);
            if (explicitDeck)
            {
                _measuredDeckLocalY = _boardingDeckLocal.y;
                _shipDeckMeasured = true;
                Debug.Log("[Dimari] Палуба задана явно: " + _boardingDeckLocal.y.ToString("F2")
                    + " м (локально), габариты " + _measuredShipLocalBounds.size.ToString("F1") + ".", this);
            }
            else MeasureShipGeometry();
            if (_shipDeckMeasured)
            {
                Debug.Log("[Dimari] Корабль измерен: палуба на " + _measuredDeckLocalY.ToString("F2")
                    + " м (локально), габариты " + _measuredShipLocalBounds.size.ToString("F1")
                    + ". Капитан и герой ставятся на эту палубу.");
                _boardingDeckLocal.y = _measuredDeckLocalY + 0.06f;
                _boardingDeckLocal.x = Mathf.Clamp(_boardingDeckLocal.x, -_measuredShipLocalBounds.extents.x * 0.3f, _measuredShipLocalBounds.extents.x * 0.3f);
                _boardingDeckLocal.z = Mathf.Clamp(_boardingDeckLocal.z, -_measuredShipLocalBounds.extents.z * 0.3f, _measuredShipLocalBounds.extents.z * 0.3f);
            }
            else Debug.LogWarning("[Dimari] Палуба корабля не измерена: у модели нет горизонтальных поверхностей с коллайдерами. Капитан останется на расчётной высоте.", this);
        }

        public bool CanBoardFromWater(Vector3 position)
        {
            if (_shipTransform == null || _isPlayingEndingCutscene || _isBoardingFromWater || _isMenuOpen ||
                _player == null || !_player.IsSwimming || !TryGetWaterHeight(position, out float waterHeight)) return false;
            if (position.y > waterHeight + 0.25f) return false;
            Vector3 horizontalDistance = Vector3.ProjectOnPlane(position - _shipTransform.position, Vector3.up);
            if (horizontalDistance.magnitude > Mathf.Max(12f, shipModelSize * 0.75f)) return false;
            foreach (Collider collider in _shipTransform.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                if ((collider.ClosestPoint(position + Vector3.up) - (position + Vector3.up)).sqrMagnitude < 6.25f)
                    return true;
            }
            return false;
        }

        public void BoardShipFromWater()
        {
            if (_player == null || !CanBoardFromWater(_player.transform.position)) return;
            StartCoroutine(BoardShipFromWaterRoutine());
        }

        private IEnumerator BoardShipFromWaterRoutine()
        {
            _isBoardingFromWater = true;
            _interactionPrompt = "";
            _player.IsControlEnabled = false;
            CharacterController controller = _player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            Vector3 startLocal = _shipTransform.InverseTransformPoint(_player.transform.position);
            Vector3 raised = startLocal;
            raised.y = _boardingDeckLocal.y + 0.5f;
            // Rise outside the hull before moving over its railing.
            for (int phase = 0; phase < 2; phase++)
            {
                float elapsed = 0f;
                Vector3 from = phase == 0 ? startLocal : raised;
                Vector3 to = phase == 0 ? raised : _boardingDeckLocal;
                while (elapsed < 0.9f)
                {
                    elapsed += Time.deltaTime;
                    _player.transform.position = _shipTransform.TransformPoint(Vector3.Lerp(from, to,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.9f))));
                    yield return null;
                }
            }
            _player.ResetWaterMotion();
            if (controller != null) controller.enabled = true;
            _player.IsControlEnabled = true;
            _isBoardingFromWater = false;
        }

        private void PrepareShipModel(GameObject instance)
        {
            instance.name = "AssetStore_Ship_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            UpgradeRenderersToURP(instance, new Color(0.35f, 0.24f, 0.16f, 1f));
            float target = Mathf.Max(8.5f, shipModelSize);
            // A Sketchfab ship can be hundreds of metres long. Such a model swallows the
            // camera and puts the captain outside it, so only clearly oversized ships are fitted.
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (maxDim > target * 2f)
                {
                    float factor = target / maxDim;
                    instance.transform.localScale *= factor;
                    Debug.LogWarning("[Dimari] Корабль был " + maxDim.ToString("F0") + " м и закрывал камеру; уменьшен до "
                        + (maxDim * factor).ToString("F0") + " м. Исходный префаб не изменён.", instance);
                }
            }
            NormalizeModelBoundsOnGround(instance.transform, target);
        }

        private void PositionCaptainAtHelm(Transform captain, Transform ship)
        {
            // A real helm always wins over the fitted placeholder: the placeholder was measured
            // on the Sky Protective Suit / pirate-ship pair and lands inside other models.
            if (_captainWheel != null && _shipDeckMeasured)
            {
                Vector3 wheelLocal = ship.InverseTransformPoint(_captainWheel.position);
                Renderer wheelRenderer = _captainWheel.GetComponentInChildren<Renderer>();
                if (wheelRenderer != null) wheelLocal = ship.InverseTransformPoint(wheelRenderer.bounds.center);
                Vector3 fitted = fittedCaptainPosition * (Mathf.Max(8.5f, shipModelSize) / 8.5f);
                bool fittedIsFarFromWheel = (ship.TransformPoint(fitted) - _captainWheel.position).sqrMagnitude > 6.25f;
                if (!useFittedCaptainPlacement || fittedIsFarFromWheel)
                {
                    // Stand just aft of the wheel, where a helmsman stands.
                    Vector3 aft = wheelLocal + ship.InverseTransformDirection(-ship.forward) * 0.45f;
                    if (Mathf.Abs(DeckLocalYAt(aft) - DeckLocalYAt(wheelLocal)) > 0.6f) aft = wheelLocal;
                    aft.y = DeckLocalYAt(aft) + 0.02f;
                    aft = FindOpenDeckPoint(ship, aft);
                    captain.localPosition = aft;
                    captain.localRotation = Quaternion.Euler(captainFacingEuler);
                    captain.localScale = Vector3.one * (humanSizedCaptain ? 1f : Mathf.Max(0.01f, fittedCaptainScale));
                    Debug.Log("[Dimari] Капитан поставлен к штурвалу: локально " + aft.ToString("F2")
                        + " (прежняя расчётная точка " + fitted.ToString("F2") + " отклонена).", this);
                    return;
                }
            }
            if (useFittedCaptainPlacement)
            {
                captain.localPosition = fittedCaptainPosition * (Mathf.Max(8.5f, shipModelSize) / 8.5f);
                captain.localRotation = Quaternion.Euler(captainFacingEuler);
                captain.localScale = Vector3.one * (humanSizedCaptain ? 1f : Mathf.Max(0.01f, fittedCaptainScale));
                if (autoFitCrewToMeasuredDeck && _shipDeckMeasured)
                {
                    Vector3 fitted = captain.localPosition;
                    float halfX = _measuredShipLocalBounds.extents.x, halfZ = _measuredShipLocalBounds.extents.z;
                    if (halfX > 0.02f) fitted.x = Mathf.Clamp(fitted.x, -halfX * 0.55f, halfX * 0.55f);
                    if (halfZ > 0.02f) fitted.z = Mathf.Clamp(fitted.z, -halfZ * 0.55f, halfZ * 0.55f);
                    float deck = DeckLocalYAt(fitted);
                    bool wrongHeight = Mathf.Abs(fitted.y - deck) > 0.35f;
                    bool outsideHull = Mathf.Abs(fitted.x) > halfX || Mathf.Abs(fitted.z) > halfZ;
                    if (wrongHeight || outsideHull)
                    {
                        fitted.y = deck + 0.02f;
                        fitted = FindOpenDeckPoint(ship, fitted);
                        captain.localPosition = fitted;
                        if (!_shipGeometryReported)
                        {
                            _shipGeometryReported = true;
                            Debug.LogWarning("[Dimari] Капитан переставлен на измеренную палубу: было "
                                + (fittedCaptainPosition * (Mathf.Max(8.5f, shipModelSize) / 8.5f)).ToString("F2")
                                + ", стало " + fitted.ToString("F2") + " | размеры корабля " + _measuredShipLocalBounds.size.ToString("F1")
                                + ". Причина — расчётная высота не совпала с реальной моделью корабля.", this);
                        }
                    }
                    else if (!_shipGeometryReported)
                    {
                        _shipGeometryReported = true;
                        Debug.Log("[Dimari] Капитан на палубе: высота " + fitted.y.ToString("F2") + " при палубе " + deck.ToString("F2")
                            + " | размеры корабля " + _measuredShipLocalBounds.size.ToString("F1"), this);
                    }
                }
                return;
            }
            captain.localScale = Vector3.one;
            Transform model = ship.Find("AssetStore_Ship_Model");
            Transform anchor = null;
            if (model != null && !string.IsNullOrWhiteSpace(captainHelmAnchorPath))
                anchor = model.Find(captainHelmAnchorPath);
            if (anchor == null)
            {
                foreach (Transform part in ship.GetComponentsInChildren<Transform>(true))
                    if (part.name == "CaptainHelmAnchor") { anchor = part; break; }
            }
            if (anchor != null)
            {
                captain.position = anchor.position;
                captain.rotation = anchor.rotation;
                return;
            }

            Transform wheel = null;
            foreach (Transform part in ship.GetComponentsInChildren<Transform>(true))
            {
                string name = part.name.ToLowerInvariant().Replace("_", "").Replace(" ", "");
                if (name.Contains("shipwheel") || name.Contains("steeringwheel") ||
                    name.Contains("helm") || name == "wheel" || name.Contains("штурвал"))
                { wheel = part; break; }
            }
            captain.localPosition = wheel != null
                ? ship.InverseTransformPoint(wheel.position) + captainOffsetFromWheel
                : captainFallbackPosition;
            captain.localRotation = Quaternion.Euler(captainFacingEuler);
            if (wheel == null)
                Debug.LogWarning("[Dimari] Штурвал не выделен в модели: подстройте Captain Fallback Position или добавьте CaptainHelmAnchor на палубу за штурвалом.");
        }

        /// <summary>
        /// Автоматически ищет импортированный пользователем 3D-корабль или лодку из Unity Asset Store
        /// (например: Boats Pack, Stylized Pirate Ship, Sailing Ships, Simple Boats and Tankers).
        /// </summary>
        private bool TrySpawnAssetStoreShipPrefab(Transform parent)
        {
            if (forceBuiltInShip) return false;
            if (shipPrefab != null)
            {
                // A serialized prefab bypassed the asset search, and StylShip_Box was assigned
                // here by an earlier version. The box must never become the ship again.
                GameObject assigned = Instantiate(shipPrefab, parent);
                if (ShipModelIsUsable(assigned, out string assignedReason))
                {
                    PrepareShipModel(assigned);
                    Debug.Log("[Dimari] Корабль: префаб из поля Ship Prefab — " + assigned.name, this);
                    return true;
                }
                Debug.LogWarning("[Dimari] Префаб корабля из поля Ship Prefab отклонён: " + assignedReason
                    + " Ищу настоящий корабль среди ассетов проекта.", this);
                Destroy(assigned);
            }
#if UNITY_EDITOR
            string[] filters = new string[] { "t:Prefab", "t:Model" };
            GameObject bestShip = null;
            string bestPath = null;
            int bestScore = -1;

            foreach (string filter in filters)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets(filter);
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    string lower = path.ToLowerInvariant();

                    if (lower.Contains("toby") || lower.Contains("starterassets") ||
                        lower.Contains("character controller") || lower.Contains("human basic") ||
                        lower.Contains("mixall") || lower.Contains("bags"))
                    {
                        continue;
                    }

                    bool isShipOrBoat =
                        lower.Contains("ship") ||
                        lower.Contains("boat") ||
                        lower.Contains("vessel") ||
                        lower.Contains("sailboat") ||
                        lower.Contains("pirate") ||
                        lower.Contains("cruiser") ||
                        lower.Contains("tanker") ||
                        lower.Contains("yacht") ||
                        lower.Contains("galleon");

                    if (!isShipOrBoat) continue;
                    string assetName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    if (assetName.Contains("barrel") || assetName.Contains("cannon") ||
                        assetName.Contains("crate") || assetName.Contains("wheel") ||
                        assetName.Contains("sail") && !assetName.Contains("ship") ||
                        assetName.Contains("mast") || assetName.Contains("rope")) continue;
                    // A package folder named Ship does not make every prop a ship.
                    if (!assetName.Contains("ship") && !assetName.Contains("boat") &&
                        !assetName.Contains("galleon") && !assetName.Contains("vessel")) continue;
                    // Stylized Pirate Ship ships a StylShip_Box prefab: a rectangular placeholder
                    // used for collisions. It is not a ship and used to be selected as one.
                    if (assetName.Contains("box") || assetName.Contains("collider") ||
                        assetName.Contains("collision") || assetName.Contains("proxy") ||
                        assetName.Contains("trigger") || assetName.Contains("volume") ||
                        assetName.Contains("bounds") || assetName.Contains("outline") ||
                        assetName.Contains("lod") || assetName.Contains("pivot") ||
                        assetName.Contains("anchor") || assetName.Contains("socket") ||
                        assetName.Contains("empty") || assetName.Contains("dummy")) continue;

                    GameObject candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate == null) continue;
                    if (candidate.GetComponentInChildren<MeshRenderer>() == null) continue;
                    if (!LooksLikeShip(candidate, out string reason))
                    {
                        Debug.Log("[Dimari] Корабль пропущен: " + path + " — " + reason);
                        continue;
                    }

                    int score = 10;
                    if (lower.EndsWith(".prefab")) score += 40;
                    if (lower.Contains("ship")) score += 30;
                    if (lower.Contains("boat")) score += 20;
                    if (lower.Contains("pirate")) score += 150;
                    if (lower.Contains("stylized") && lower.Contains("pirate")) score += 100;
                    if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Contains("ship")) score += 80;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestShip = candidate;
                        bestPath = path;
                    }
                }
            }

            if (bestShip != null)
            {
                GameObject instance = Instantiate(bestShip, parent);
                if (!ShipModelIsUsable(instance, out string instanceReason))
                {
                    Debug.LogWarning("[Dimari] Загруженный корабль отклонён: " + instanceReason
                        + " Путь: " + bestPath + ". Строю встроенный корабль.", this);
                    Destroy(instance);
                    return false;
                }
                PrepareShipModel(instance);

                Debug.Log($"[Dimari] Загружен 3D-корабль из ассета: {bestPath}");
                return true;
            }
#endif
            return false;
        }

        /// <summary>Destroys StylShip_Box-style leftovers that were saved into the scene.</summary>
        private static void PurgeShipPlaceholderObjects()
        {
            // Превью из редактора (Dimari → Корабль — превью сцены обрыва) не должно
            // остаться рядом с настоящей сценой в Play.
            foreach (GameObject preview in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (preview == null || preview.name != "Dimari_CliffPreview") continue;
                Debug.Log("[Dimari] Превью обрыва из редактора удалено перед запуском сцены.", preview);
                DestroyImmediate(preview);
            }

            foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (filter == null || filter.sharedMesh == null) continue;
                string meshName = filter.sharedMesh.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
                if (!meshName.Contains("stylshipbox") && !meshName.Contains("shipbox") && !meshName.Contains("shipcollider")) continue;
                Transform victim = filter.transform;
                while (victim.parent != null && !victim.name.StartsWith("Quest3_") && !victim.name.StartsWith("Dimari_StoryObjects"))
                    victim = victim.parent;
                Debug.LogWarning("[Dimari] Найдена сохранённая в сцене заглушка корабля «" + victim.name
                    + "» (меш " + filter.sharedMesh.name + ") — удалена.", victim);
                Destroy(victim.gameObject);
            }
        }

        /// <summary>
        /// Runtime-safe shape check for an instantiated model. StylShip_Box is a 19.8 m cube
        /// with a BoxCollider: square in plan, so it can never pass as a hull.
        /// </summary>
        private static bool ShipModelIsUsable(GameObject instance, out string reason)
        {
            reason = "";
            if (instance == null) { reason = "объект не создан."; return false; }
            if (!instance.activeSelf) instance.SetActive(true);
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                string meshName = filter.sharedMesh.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
                if (!meshName.Contains("stylshipbox") && !meshName.Contains("shipbox") && !meshName.Contains("shipcollider")) continue;
                reason = "меш «" + filter.sharedMesh.name + "» — заглушка из набора."; return false;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { reason = "в модели нет видимых деталей."; return false; }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 size = bounds.size;
            float length = Mathf.Max(size.x, size.z);
            float width = Mathf.Min(size.x, size.z);
            if (length < 2f || width < 0.3f) { reason = "модель слишком маленькая (" + size.ToString("F1") + ")."; return false; }
            if (size.y > length * 0.95f) { reason = "высота больше длины — это коробка, а не корпус (" + size.ToString("F1") + ")."; return false; }
            if (length / Mathf.Max(0.01f, width) < 1.3f)
            { reason = "в плане почти квадрат (" + size.ToString("F1") + "), похоже на коллайдер-заглушку."; return false; }
            // A low-poly ship is still a ship. Only a primitive-like one-mesh box is rejected;
            // a cube is already caught by the square footprint above.
            if (renderers.Length == 1)
            {
                MeshFilter filter = renderers[0].GetComponent<MeshFilter>();
                int triangles = filter != null && filter.sharedMesh != null ? filter.sharedMesh.triangles.Length / 3 : 0;
                if (triangles > 0 && triangles < 80)
                { reason = "одна примитивная деталь (" + triangles + " треугольников) — это ящик."; return false; }
            }
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Rejects placeholders. A ship is longer than it is wide and is not a cube:
        /// StylShip_Box (39.6 × 19.8 × 39.6) is exactly the box that used to replace the ship.
        /// </summary>
        private static bool LooksLikeShip(GameObject prefab, out string reason)
        {
            reason = "";
            GameObject probe = Instantiate(prefab);
            try
            {
                Renderer[] renderers = probe.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { reason = "нет рендереров"; return false; }
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                Vector3 size = bounds.size;
                float length = Mathf.Max(size.x, size.z);
                float width = Mathf.Min(size.x, size.z);
                if (length < 2f || width < 0.3f) { reason = "слишком маленький"; return false; }
                if (size.y > length * 0.95f) { reason = "высота больше длины — это не корпус"; return false; }
                if (length / Mathf.Max(0.01f, width) < 1.3f) { reason = "в плане почти квадрат (" + size.ToString("F1") + "), похоже на заглушку"; return false; }
                int triangles = 0;
                foreach (MeshFilter filter in probe.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;
                foreach (SkinnedMeshRenderer skin in probe.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (skin.sharedMesh != null) triangles += skin.sharedMesh.triangles.Length / 3;
                if (renderers.Length < 2 && triangles < 80)
                { reason = "одна примитивная деталь (" + triangles + " треугольников), похоже на коллайдер"; return false; }
                return true;
            }
            finally
            {
                DestroyImmediate(probe);
            }
        }
#endif

        /// <summary>
        /// Строит детализированный 3D-корабль на воде (корпус, палуба, борта, рубка капитана со светящимися иллюминаторами,
        /// мачта с парусом, труба и спасательные круги), если внешний ассет корабля ещё не импортирован.
        /// </summary>
        private void CreateDetailedProceduralShip(Transform parent)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material hullDarkMat = new Material(litShader);
            SetMatColor(hullDarkMat, new Color(0.18f, 0.12f, 0.08f, 1f));
            if (hullDarkMat.HasProperty("_Smoothness")) hullDarkMat.SetFloat("_Smoothness", 0.30f);

            Material hullRedBottomMat = new Material(litShader);
            SetMatColor(hullRedBottomMat, new Color(0.45f, 0.14f, 0.11f, 1f));

            Material deckWoodMat = new Material(litShader);
            SetMatColor(deckWoodMat, new Color(0.46f, 0.32f, 0.19f, 1f));

            Material cabinWhiteMat = new Material(litShader);
            SetMatColor(cabinWhiteMat, new Color(0.86f, 0.84f, 0.78f, 1f));

            Material sailCanvasMat = new Material(litShader);
            SetMatColor(sailCanvasMat, new Color(0.92f, 0.89f, 0.81f, 1f));

            Material windowGlowMat = new Material(litShader);
            Color warmGlow = new Color(1.0f, 0.84f, 0.42f, 1f);
            SetMatColor(windowGlowMat, warmGlow);
            if (windowGlowMat.HasProperty("_EmissionColor"))
            {
                windowGlowMat.EnableKeyword("_EMISSION");
                windowGlowMat.SetColor("_EmissionColor", warmGlow * 2.2f);
            }

            // 1. Нижняя подводная часть корпуса (киль)
            CreatePart(PrimitiveType.Capsule, "ShipKeelHull", parent,
                new Vector3(0f, 0.15f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(2.6f, 4.4f, 0.9f), hullRedBottomMat);

            // 2. Основной надводный корпус корабля
            CreatePart(PrimitiveType.Cube, "ShipMainHull", parent,
                new Vector3(0f, 0.65f, 0f), Quaternion.identity, new Vector3(2.7f, 0.85f, 6.8f), hullDarkMat);

            // 3. Заострённый нос корабля (форштевень) и корма
            CreatePart(PrimitiveType.Cube, "ShipBowFront", parent,
                new Vector3(0f, 0.72f, 3.6f), Quaternion.Euler(0f, 45f, 0f), new Vector3(1.9f, 0.85f, 1.9f), hullDarkMat);

            // 4. Деревянная палуба
            CreatePart(PrimitiveType.Cube, "ShipWoodDeck", parent,
                new Vector3(0f, 1.10f, 0.1f), Quaternion.identity, new Vector3(2.55f, 0.08f, 6.6f), deckWoodMat);

            // 5. Бортики / фальшборт по краям палубы
            CreatePart(PrimitiveType.Cube, "PortRail", parent,
                new Vector3(-1.28f, 1.32f, 0f), Quaternion.identity, new Vector3(0.08f, 0.38f, 6.4f), hullDarkMat);
            CreatePart(PrimitiveType.Cube, "StarboardRail", parent,
                new Vector3(1.28f, 1.32f, 0f), Quaternion.identity, new Vector3(0.08f, 0.38f, 6.4f), hullDarkMat);

            // 6. Капитанская рубка / каюта
            CreatePart(PrimitiveType.Cube, "CaptainCabin", parent,
                new Vector3(0f, 1.75f, -0.9f), Quaternion.identity, new Vector3(1.85f, 1.25f, 2.4f), cabinWhiteMat);
            CreatePart(PrimitiveType.Cube, "CabinRoof", parent,
                new Vector3(0f, 2.42f, -0.9f), Quaternion.identity, new Vector3(2.05f, 0.12f, 2.6f), hullDarkMat);

            // Светящиеся окна капитанской рубки
            CreatePart(PrimitiveType.Cube, "CabinFrontWindow", parent,
                new Vector3(0f, 1.95f, 0.31f), Quaternion.identity, new Vector3(1.35f, 0.42f, 0.04f), windowGlowMat);
            CreatePart(PrimitiveType.Cube, "CabinSideWindowL", parent,
                new Vector3(-0.93f, 1.92f, -0.8f), Quaternion.identity, new Vector3(0.04f, 0.38f, 1.4f), windowGlowMat);
            CreatePart(PrimitiveType.Cube, "CabinSideWindowR", parent,
                new Vector3(0.93f, 1.92f, -0.8f), Quaternion.identity, new Vector3(0.04f, 0.38f, 1.4f), windowGlowMat);

            // 7. Высокая мачта, рея и парус
            CreatePart(PrimitiveType.Cylinder, "MainMast", parent,
                new Vector3(0f, 3.4f, 1.35f), Quaternion.identity, new Vector3(0.14f, 2.3f, 0.14f), deckWoodMat);
            CreatePart(PrimitiveType.Cylinder, "CrossYardTop", parent,
                new Vector3(0f, 4.8f, 1.35f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.09f, 1.6f, 0.09f), deckWoodMat);
            CreatePart(PrimitiveType.Cube, "MainSailCanvas", parent,
                new Vector3(0f, 3.65f, 1.45f), Quaternion.Euler(6f, 0f, 0f), new Vector3(2.8f, 2.1f, 0.06f), sailCanvasMat);

            // 8. Пароходная труба позади рубки
            CreatePart(PrimitiveType.Cylinder, "SmokeFunnel", parent,
                new Vector3(0f, 2.95f, -1.55f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.36f, 0.65f, 0.36f), hullRedBottomMat);

            // 9. Точка палубы: по ней ставятся капитан, герой и переход с воды.
            GameObject deckPoint = new GameObject("ShipBoardingPoint");
            deckPoint.transform.SetParent(parent, false);
            deckPoint.transform.localPosition = new Vector3(0f, 1.16f, 0f);
        }

        /// <summary>
        /// Спавнит 3D-персонажа (NPC-капитана / выжившего) у корабля:
        /// Сначала ищет любого нового персонажа, скачанного с Asset Store, иначе использует модель персонажа с анимацией дыхания Idle.
        /// </summary>
        private void SpawnNpcCharacterAtShip(Transform parent)
        {
            GameObject bestCharPrefab = captainPrefab;
#if UNITY_EDITOR
            int bestScore = -1;

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string lower = path.ToLowerInvariant();

                if (lower.Contains("toby") || lower.Contains("mixall") || lower.Contains("bags")) continue;

                // Ищем сторонних 3D-персонажей или модели людей
                bool isExternalChar =
                    lower.Contains("modular character") ||
                    lower.Contains("human_male") ||
                    lower.Contains("rpg character") ||
                    lower.Contains("chuby") ||
                    lower.Contains("protective_suit") ||
                    lower.Contains("protective suit") ||
                    lower.Contains("protectivesuit") ||
                    lower.Contains("didimo") ||
                    lower.Contains("villager") ||
                    lower.Contains("npc") ||
                    lower.Contains("playerarmature");

                if (!isExternalChar) continue;

                GameObject candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate == null) continue;
                if (candidate.GetComponentInChildren<SkinnedMeshRenderer>() == null) continue;

                int score = 10;
                // Приоритет отдаём новому скачанному человеческому персонажу (не дефолтному PlayerArmature)
                if (!lower.Contains("playerarmature") && !lower.Contains("starterassets"))
                {
                    score += 80;
                }

                if (lower.Contains("protective")) score += 250;
                if (captainPrefab == null && score > bestScore)
                {
                    bestScore = score;
                    bestCharPrefab = candidate;
                }
            }

#endif
            if (bestCharPrefab != null)
            {
                GameObject npcInstance = Instantiate(bestCharPrefab, parent);
                npcInstance.name = "ShipCaptain_Character";
                npcInstance.transform.localPosition = Vector3.zero;
                npcInstance.transform.localRotation = Quaternion.identity;
                npcInstance.transform.localScale = Vector3.one;

                NormalizeModelBoundsOnGround(npcInstance.transform, 1.8f);
                foreach (Rigidbody body in npcInstance.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                foreach (Collider collider in npcInstance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (Camera camera in npcInstance.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (AudioListener listener in npcInstance.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;

                // Отключаем на NPC любые контроллеры управления игроком, чтобы он спокойно стоял на пирсе
                MonoBehaviour[] scripts = npcInstance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (MonoBehaviour mb in scripts)
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName.Contains("Player") || typeName.Contains("Controller") || typeName.Contains("Input"))
                    {
                        mb.enabled = false;
                    }
                }

                UpgradeRenderersToURP(npcInstance, new Color(0.22f, 0.30f, 0.38f, 1f));

                // Если у игрока есть AnimatorController — назначаем его и NPC, чтобы он дышал в стойке Idle
                Animator npcAnim = npcInstance.GetComponentInChildren<Animator>();
                if (npcAnim != null && _characterAnimator != null && _characterAnimator.runtimeAnimatorController != null)
                {
                    npcAnim.runtimeAnimatorController = _characterAnimator.runtimeAnimatorController;
                    npcAnim.applyRootMotion = false;
                }
                return;
            }
            // Запасной силуэт капитана в плаще у фонаря, если префаб не найден
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material coatMat = new Material(litShader);
            SetMatColor(coatMat, new Color(0.16f, 0.22f, 0.28f, 1f));
            CreatePart(PrimitiveType.Capsule, "NpcBody", parent,
                new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(0.45f, 0.85f, 0.35f), coatMat);
            CreatePart(PrimitiveType.Sphere, "NpcHead", parent,
                new Vector3(0f, 1.65f, 0f), Quaternion.identity, new Vector3(0.28f, 0.28f, 0.28f), coatMat);
        }

        private bool TrySpawnMixallBagPrefab(Transform parent)
        {
#if UNITY_EDITOR
            string[] searchFilters = new string[] { "t:Prefab", "t:Model" };
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

        private bool _chapterSelectionOpen;
        private int _requestedChapter;
        private static int _pendingChapter;
        // Temporary playtest shortcut: set false before release to hide the menu entry.
        private const bool EnablePizzaChapterPreview = true;
        private const string ChapterForestScene = "Assets/Dimari/Scenes/ForestAwakening_TTFEL.unity";

        private void LaunchChapter(int chapter)
        {
            if (!Application.CanStreamedLevelBeLoaded(ChapterForestScene))
            { ShowStatusToast("Лесная сцена не найдена в Scene List."); return; }
            string cityPath = string.IsNullOrWhiteSpace(cityScenePath)
                ? "Assets/Dimari/Scenes/Chapter2_Rogovo.unity" : cityScenePath;
            if (chapter >= 2 && !Application.CanStreamedLevelBeLoaded(cityPath))
            { ShowStatusToast("Добавьте Chapter2_Rogovo в Scene List."); return; }
            // A fresh forest bootstrap supplies the player, camera and story systems.
            // Do not call StartNewGame on a city manager: its forest dependencies were unloaded.
            _pendingChapter = chapter;
            Time.timeScale = 1f;
            SceneManager.LoadScene(ChapterForestScene, LoadSceneMode.Single);
        }

        private IEnumerator LaunchCityChapter(bool pizzaPreview = false)
        {
            if (_director == null) _director = FindAnyObjectByType<ForestAwakeningDirector>();
            if (_director != null) { _director.StopAllCoroutines(); _director.enabled = false; }
            _hasGameStarted = true;
            _hasReachedShip = true;
            _waitingForCaptainInteraction = false;
            _hasPickedUpTablet = true;
            _isMenuOpen = false;
            SyncStoryVoiceMenuState();
            _isSettingsOpen = false;
            _chapterSelectionOpen = false;
            _isTabletViewerOpen = false;
            _showHUD = true;
            _cameraController.IsMenuCameraActive = false;
            _cameraController.UpdateCharacterMeshVisibility(_cameraController.isFirstPerson);
            _player.ResetWaterMotion();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (string.IsNullOrWhiteSpace(cityScenePath)) cityScenePath = "Assets/Dimari/Scenes/Chapter2_Rogovo.unity";
            yield return ArriveInCity(pizzaPreview);
        }

        private void Update()
        {
            if (_requestedChapter != 0)
            {
                int chapter = _requestedChapter;
                _requestedChapter = 0;
                LaunchChapter(chapter);
                return;
            }
            if (_pendingChapter != 0 && _player != null && _cameraController != null && _mainCam != null)
            {
                int chapter = _pendingChapter;
                _pendingChapter = 0;
                if (chapter == 1) StartNewGame();
                else StartCoroutine(LaunchCityChapter(chapter == 3 && EnablePizzaChapterPreview));
                return;
            }
            if (_newGameRequested)
            {
                _newGameRequested = false;
                StartNewGame();
                return;
            }
            if (!_hasReachedShip && _cliffInteraction != null && _endingApproachMarker != null && _player != null && _hasGameStarted &&
                !_isMenuOpen && !_isPlayingEndingCutscene && !_isChapter2Reached && _hasPickedUpTablet &&
                Vector3.Distance(_player.transform.position, _endingApproachMarker.position) <= endingMarkerRadius)
            {
                CompleteQuestStep("rogovo_exit", "");
            }
            if (_cityChapterTitleTimer > 0f) _cityChapterTitleTimer -= Time.unscaledDeltaTime;
            if (_statusToastTimer > 0f)
            {
                _statusToastTimer -= Time.unscaledDeltaTime;
            }

            if (_isSailing && _shipTransform != null)
            {
                _sailingElapsed += Time.deltaTime;
                _shipBasePos = Vector3.Lerp(_sailingStart, _sailingEnd,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_sailingElapsed / SailingDuration)));
            }

            // Плавное покачивание корабля на волнах у причала
            if (_shipTransform != null)
            {
                float t = Time.time;
                float bobY = Mathf.Sin(t * 1.4f) * 0.08f;
                float rollZ = Mathf.Sin(t * 1.1f) * 2.2f;
                float pitchX = Mathf.Cos(t * 0.9f) * 1.4f;
                _shipTransform.localPosition = _shipBasePos + new Vector3(0f, bobY, 0f);
                _shipTransform.localRotation = _shipBaseRot * Quaternion.Euler(pitchX, 0f, rollZ);
            }

            float targetHoldBlend = (_hasPickedUpTablet && _isHoldingTabletInHands && !_isPlayingPickupAnim && !_isTabletViewerOpen && !_isPlayingEndingCutscene) ? 1.0f : 0.0f;
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

                if (DimariInput.IsInteractPressed() || DimariInput.IsSkipOrContinuePressed() || IsTabKeyPressed())
                {
                    PutTabletInBackpack();
                }
                else if (IsToggleHoldHandsKeyPressed())
                {
                    KeepTabletInHands();
                }
            }
            else if (_hasPickedUpTablet && !_isPlayingEndingCutscene)
            {
                if (IsTabKeyPressed())
                {
                    OpenTabletViewer();
                }
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
                LastMenuHotkeyFrame = Time.frameCount;
                if (_isTabletViewerOpen)
                {
                    PutTabletInBackpack();
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

            if (!_isMenuOpen && !_isPlayingEndingCutscene)
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

            if (!_isMenuOpen && !_isPlayingEndingCutscene)
            {
                if (_player != null) _player.IsControlEnabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ShowStatusToast("Планшет в руках ([T] или ПКМ — убрать в рюкзак)");
        }

        private void LateUpdate()
        {
            SyncStoryVoiceMenuState();
            ApplyCaptainHelmPose();
            if (!_isMenuOpen)
            {
                if (_isPlayingPickupAnim)
                {
                    ApplyHumanoidPickupPose();
                }
                else if (_holdTabletBlend > 0.001f)
                {
                    ApplyHumanoidHoldTabletInBothHandsPose(_holdTabletBlend);
                }

                UpdateEquippedItemsOnCharacter();
                return;
            }

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

            // ForestCameraController is the sole camera writer when available.
            if (_cameraController != null) return;
            float swayX = 0f;
            float swayY = 0f;

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

        private void ApplyCaptainHelmPose()
        {
            if (_captainAnimator == null || !_captainAnimator.isHuman || _npcTransform == null ||
                _captainWheel == null) return;
            Vector3 wheelCenter = _captainWheel.position;
            Renderer wheelRenderer = _captainWheel.GetComponent<Renderer>();
            if (wheelRenderer != null) wheelCenter = wheelRenderer.bounds.center;
            Vector3 forward = Vector3.ProjectOnPlane(wheelCenter - _npcTransform.position, _shipTransform.up);
            if (forward.sqrMagnitude > 0.01f)
                _npcTransform.rotation = Quaternion.LookRotation(forward, _shipTransform.up);
            Vector3 right = _npcTransform.right;
            Vector3 leftGrip = _wheelLeftGrip != null ? _wheelLeftGrip.position : wheelCenter - right * 0.27f;
            Vector3 rightGrip = _wheelRightGrip != null ? _wheelRightGrip.position : wheelCenter + right * 0.27f;
            // A wheel on another deck would stretch the arms over the head: do not pose then.
            float armSpan = 0f;
            Transform upperProbe = _captainAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform handProbe = _captainAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            if (upperProbe != null && handProbe != null) armSpan = Vector3.Distance(upperProbe.position, handProbe.position) * 1.9f;
            if (armSpan > 0.1f && Vector3.Distance(_npcTransform.position + Vector3.up * 1.2f, wheelCenter) > armSpan)
                return;
            PoseCaptainArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, leftGrip, -right);
            PoseCaptainArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rightGrip, right);
            if (_captainConversationActive && _player != null)
            {
                Transform head = _captainAnimator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    Vector3 direction = _player.transform.position + Vector3.up * 1.5f - head.position;
                    if (direction.sqrMagnitude > 0.001f)
                    {
                        Quaternion desired = Quaternion.LookRotation(direction, _shipTransform.up);
                        head.rotation = Quaternion.RotateTowards(head.rotation, desired, 55f);
                    }
                }
            }
        }

        private void PoseCaptainArm(HumanBodyBones upperBone, HumanBodyBones lowerBone,
            HumanBodyBones handBone, Vector3 grip, Vector3 outward)
        {
            Transform upper = _captainAnimator.GetBoneTransform(upperBone);
            Transform lower = _captainAnimator.GetBoneTransform(lowerBone);
            Transform hand = _captainAnimator.GetBoneTransform(handBone);
            if (upper == null || lower == null || hand == null) return;
            // Analytic two-bone reach, preserving bone lengths.
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            Vector3 delta = grip - upper.position;
            if (a < 0.001f || b < 0.001f || delta.sqrMagnitude < 0.001f) return;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(outward - _shipTransform.up * 0.4f, direction).normalized;
            float x = (a * a - b * b + distance * distance) / (2f * distance);
            Vector3 elbow = upper.position + direction * x + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position,
                upper.position + direction * distance - lower.position) * lower.rotation;
        }

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

            Vector3 tabletCenter = chestPos + charForward * 0.36f + charUp * 0.06f;

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

            Transform neck = _characterAnimator.GetBoneTransform(HumanBodyBones.Neck);
            if (neck != null)
            {
                neck.rotation = Quaternion.AngleAxis(15f * weight, charRight) * neck.rotation;
            }
        }

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

            Vector3 chestPos = chest != null ? chest.position : (_player.transform.position + Vector3.up * 1.0f);
            Vector3 elbowHint = chestPos + charForward * 0.20f + charRight * 0.22f - Vector3.up * 0.25f;
            Vector3 reachPoint = _pickupTargetWorldPos + Vector3.up * 0.12f;
            AimArmAtWorldTargets(rUpperArm, rLowerArm, rHand, elbowHint, reachPoint, bendWeight);
        }

        private void UpdateEquippedItemsOnCharacter()
        {
            if (_player == null) return;
            if (_characterChestBone == null) CacheCharacterBones();

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

            if (_equippedTabletTransform != null)
            {
                if (_isTabletViewerOpen || _isPlayingEndingCutscene)
                {
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
                        ShowStatusToast("Планшет в руках! ([TAB] — экран, [T] — убрать в рюкзак)");
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

        /// <summary>
        /// КИНЕМАТОГРАФИЧНАЯ КАТ-СЦЕНА С АНИМАЦИЕЙ ХОДЬБЫ К ВОДЕ И КОРАБЛЮ:
        /// 1) Герой говорит: «Нужно идти к воде — возможно, там будет корабль.»
        /// 2) Камера показывает героя со стороны, включается анимация ходьбы (Walk_N), и герой шагает по деревянному пирсу к воде.
        /// 3) Герой останавливается на краю причала, а камера плавно поднимается из-за его плеча, открывая вид на качающийся на воде КОРАБЛЬ и стоящего у трапа ПЕРСОНАЖА!
        /// </summary>
        private IEnumerator PlayWalkToWaterAndShipCutsceneRoutine(InteractableStoryObject shoreInteractable)
        {
            if (_isPlayingEndingCutscene) yield break;
            _isPlayingEndingCutscene = true;
            _interactionPrompt = "";
            _isTabletViewerOpen = false;
            _isHoldingTabletInHands = false;

            if (shoreInteractable != null)
            {
                shoreInteractable.enabled = false;
            }

            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_cameraController == null) _cameraController = FindAnyObjectByType<ForestCameraController>();
            if (_mainCam == null) _mainCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

            if (_player != null)
            {
                _player.IsControlEnabled = false;
            }

            if (_cameraController != null)
            {
                _cameraController.IsCutsceneControlled = true;
                // Обязательно делаем 3D-модель главного героя видимой, чтобы в кат-сцене было видно, как он идёт к воде!
                _cameraController.UpdateCharacterMeshVisibility(false);
            }

            // Conceal the relocation from the nearby marker to the authored cliff.
            if (_endingApproachMarker != null)
            {
                float fadeTime = 0f;
                while (fadeTime < 0.45f)
                {
                    fadeTime += Time.deltaTime;
                    _endingFade = Mathf.Clamp01(fadeTime / 0.45f);
                    yield return null;
                }
            }
            CacheCharacterBones();

            // Маршрут спуска: сверху по скале, вниз по лестнице, на нижний причал.
            List<Vector3> route = _descentPath.Count >= 2 ? new List<Vector3>(_descentPath) : null;
            if (route == null)
            {
                Vector3 plainStart = _shoreStartPoint != null
                    ? _shoreStartPoint.position
                    : (_player != null ? _player.transform.position : Vector3.zero);
                Vector3 plainEnd = _pierWalkEndPoint != null
                    ? _pierWalkEndPoint.position
                    : (plainStart + (_player != null ? _player.transform.forward * 5.2f : Vector3.forward * 5.2f));
                route = new List<Vector3> { plainStart, plainEnd };
            }
            Vector3 walkStart = route[0];
            Vector3 walkEnd = route[route.Count - 1];
            float routeLength = 0f;
            for (int i = 1; i < route.Count; i++) routeLength += Vector3.Distance(route[i - 1], route[i]);

            Vector3 walkDir = (walkEnd - walkStart);
            walkDir.y = 0f;
            if (walkDir.sqrMagnitude < 0.01f) walkDir = Vector3.forward;
            walkDir.Normalize();
            Vector3 walkRight = Vector3.Cross(Vector3.up, walkDir).normalized;
            Quaternion walkRot = Quaternion.LookRotation(walkDir, Vector3.up);

            CharacterController cc = _player != null ? _player.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;
            if (_player != null)
            {
                _player.transform.position = walkStart;
                _player.transform.rotation = walkRot;
            }

            if (_endingApproachMarker != null && _mainCam != null)
            {
                Vector3 cameraPosition = walkStart - walkDir * 2.6f + walkRight * 1.35f + Vector3.up * 1.45f;
                _mainCam.transform.position = cameraPosition;
                _mainCam.transform.rotation = Quaternion.LookRotation(walkStart + walkDir * 3.5f + Vector3.up * 1.15f - cameraPosition);
                float fadeTime = 0f;
                while (fadeTime < 0.45f)
                {
                    fadeTime += Time.deltaTime;
                    _endingFade = 1f - Mathf.Clamp01(fadeTime / 0.45f);
                    yield return null;
                }
                _endingFade = 0f;
            }

            // Фраза 1: «Нужно идти к воде — возможно, там будет корабль.»
            ShowMonologue("«Нужно идти к воде — возможно, там будет корабль.»", 4.5f);

            // Фаза 1 (0..4.2 сек): Персонаж идёт шагом по пирсу к воде с полноценной 3D-анимацией ходьбы!
            // Скорость спокойного шага: по лестнице герой идёт медленнее, чем по ровному.
            float walkDuration = Mathf.Clamp(routeLength / 1.05f, 4f, 16f);
            float t = 0f;
            float stepSoundTimer = 0.2f;

            while (t < walkDuration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / walkDuration);

                if (_player != null)
                {
                    Vector3 stepPosition = SamplePath(route, norm, out Vector3 stepDir);
                    _player.transform.position = stepPosition;
                    _player.transform.rotation = Quaternion.LookRotation(stepDir, Vector3.up);
                }

                // Включаем 3D-анимацию ходьбы в Animator (Speed = 2.0f соответствует Walk_N в StarterAssets/BasicMotions)
                if (_characterAnimator != null)
                {
                    _characterAnimator.SetFloat("Speed", 2.0f);
                    _characterAnimator.SetFloat("MotionSpeed", 1.0f);
                    _characterAnimator.SetBool("Grounded", true);
                }

                // Звуки шагов по причалу/берегу
                stepSoundTimer -= Time.deltaTime;
                if (stepSoundTimer <= 0f)
                {
                    stepSoundTimer = 0.48f;
                    if (ProceduralForestAudio.Instance != null)
                    {
                        ProceduralForestAudio.Instance.PlayFootstep(false);
                    }
                }

                // Кинематографичная камера плавно сопровождает идущего героя сбоку-сзади, открывая вид на воду и корабль
                if (_mainCam != null && _player != null)
                {
                    SamplePath(route, norm, out Vector3 camDir);
                    Vector3 pPos = _player.transform.position;
                    Vector3 camRight = Vector3.Cross(Vector3.up, camDir).normalized;
                    // Сбоку-сзади и чуть выше: видно и героя, и ступени, и воду впереди.
                    Vector3 camPos = pPos - camDir * 2.9f + camRight * 1.5f + Vector3.up * 1.55f;
                    Vector3 lookAt = pPos + camDir * 3.6f + Vector3.up * 0.9f;
                    if (Physics.Linecast(lookAt, camPos, out RaycastHit camHit, ~0, QueryTriggerInteraction.Ignore)
                        && !camHit.transform.IsChildOf(_player.transform))
                    {
                        camPos = Vector3.Lerp(lookAt, camPos, 0.55f) + Vector3.up * 0.35f;
                    }
                    _mainCam.transform.position = camPos;
                    _mainCam.transform.rotation = Quaternion.LookRotation(lookAt - camPos, Vector3.up);
                }

                yield return null;
            }

            if (_player != null)
            {
                // Спуск закончен на нижнем причале: ставим героя точно в конец маршрута.
                _player.transform.position = walkEnd;
            }
            else if (cliffArrivalPoint != null)
            {
                PlaceHeroOnCliffSurface(walkEnd);
            }

            // Персонаж останавливается у края причала перед кораблём (переход в стойку Idle)
            if (_characterAnimator != null)
            {
                _characterAnimator.SetFloat("Speed", 0f);
                _characterAnimator.SetFloat("MotionSpeed", 1f);
            }

            // Фраза 2: Герой видит корабль и стоящего на причале человека
            ShowMonologue(
                "«Эй-й-й! На борту! Я тут! Помоги!»",
                6.0f
            );

            if (shoutForHelpVoice != null && _mainCam != null)
                AudioSource.PlayClipAtPoint(shoutForHelpVoice, _mainCam.transform.position);

            // Фаза 2 (4.2..9.0 сек): Панорамный наезд камеры из-за плеча героя на корабль и NPC у трапа
            float revealDuration = 4.6f;
            t = 0f;
            // С нижнего причала камера поднимается над водой и показывает корабль целиком.
            Vector3 revealCamStart = walkEnd - walkDir * 1.4f + walkRight * 2.6f + Vector3.up * 1.7f;
            Vector3 revealCamEnd = walkEnd + walkDir * 2.2f + walkRight * 1.9f + Vector3.up * 2.6f;
            Vector3 shipLookTarget = _shipTransform != null
                ? (_shipTransform.position + Vector3.up * 1.6f)
                : (walkEnd + walkDir * 9.0f + Vector3.up * 1.5f);

            while (t < revealDuration)
            {
                if (_player != null) _player.transform.position = walkEnd;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / revealDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                if (_mainCam != null)
                {
                    Vector3 camPos = Vector3.Lerp(revealCamStart, revealCamEnd, smooth);
                    Vector3 lookPos = Vector3.Lerp(walkEnd + walkDir * 3.5f + Vector3.up * 1.15f, shipLookTarget, smooth);
                    _mainCam.transform.position = camPos;
                    _mainCam.transform.rotation = Quaternion.LookRotation(lookPos - camPos, Vector3.up);
                }

                yield return null;
            }

            yield return PlayCaptainConversation();

            if (playSailingCutscene) yield return PlaySailingToRogovo();

            if (_player != null) _player.ResetWaterMotion();
            if (cc != null) cc.enabled = true;
            if (_cameraController != null)
            {
                _cameraController.BeginGameplayTransitionFromCutscene();
            }
            if (_player != null)
            {
                _player.IsControlEnabled = true;
            }

            _captainConversationActive = false;
            _isPlayingEndingCutscene = false;
            if (!string.IsNullOrWhiteSpace(cityScenePath))
                yield return ArriveInCity();
            else
                ShowStatusToast("Разговор завершён. Назначьте сцену города через меню Dimari.");
            SaveGame();
        }

        private void PlaceHeroOnCliffSurface(Vector3 position)
        {
            Vector3 ground = GroundCinematicPosition(position);
            _player.transform.position = ground;
            if (_characterAnimator == null || !_characterAnimator.isHuman) return;
            Transform left = _characterAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = _characterAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (left == null || right == null) return;
            // Foot bones sit above the sole. Correct only small rig/root offsets;
            // never pull a raised walking foot all the way down to the ground.
            float lowestFoot = Mathf.Min(left.position.y, right.position.y);
            float soleClearance = 0.08f;
            float correction = Mathf.Clamp(ground.y + soleClearance - lowestFoot, -0.25f, 0.35f);
            _player.transform.position += Vector3.up * correction;
        }

        private Vector3 GroundCinematicPosition(Vector3 position)
        {
            float bestHeight = float.NegativeInfinity;
            Vector3 result = position;
            foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 4f, Vector3.down,
                12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(_player.transform) ||
                    (_shipTransform != null && hit.transform.IsChildOf(_shipTransform))) continue;
                if (hit.normal.y < 0.45f) continue;
                // Prefer the upper walkable surface, not a lower overlapping rock collider.
                if (hit.point.y > position.y + 1.5f) continue;
                if (hit.point.y > bestHeight) { bestHeight = hit.point.y; result.y = hit.point.y + 0.04f; }
            }
            return result;
        }

        private bool TryFindShipStairRoute(Vector3 captainLocal, out Vector3 bottom, out Vector3 top)
        {
            bottom = top = Vector3.zero;
            Transform bottomAnchor = null, topAnchor = null;
            foreach (Transform part in _shipTransform.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "StairBottom") bottomAnchor = part;
                if (part.name == "StairTop") topAnchor = part;
            }
            if (bottomAnchor != null && topAnchor != null)
            {
                bottom = _shipTransform.InverseTransformPoint(bottomAnchor.position);
                top = _shipTransform.InverseTransformPoint(topAnchor.position);
                return true;
            }
            float best = float.PositiveInfinity;
            foreach (MeshFilter mesh in _shipTransform.GetComponentsInChildren<MeshFilter>())
            {
                string name = mesh.name.ToLowerInvariant();
                if ((!name.Contains("stair") && !name.Contains("stairs") && !name.Contains("ladder")) ||
                    name.Contains("rail") || mesh.sharedMesh == null) continue;
                Bounds bounds = mesh.sharedMesh.bounds;
                Bounds local = new Bounds(_shipTransform.InverseTransformPoint(mesh.transform.TransformPoint(bounds.center)), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    local.Encapsulate(_shipTransform.InverseTransformPoint(mesh.transform.TransformPoint(corner)));
                }
                // Choose either staircase, with the upper end nearest the captain.
                bool alongZ = local.size.z >= local.size.x;
                Vector3 endA = local.center, endB = local.center;
                if (alongZ) { endA.z = local.min.z; endB.z = local.max.z; }
                else { endA.x = local.min.x; endB.x = local.max.x; }
                // Determine direction from actual tread heights, not proximity to the captain.
                MeshCollider stairCollider = mesh.GetComponent<MeshCollider>();
                if (stairCollider == null)
                {
                    stairCollider = mesh.gameObject.AddComponent<MeshCollider>();
                    stairCollider.sharedMesh = mesh.sharedMesh;
                }
                Physics.SyncTransforms();
                Vector3 sampleA = Vector3.Lerp(endA, endB, 0.06f);
                Vector3 sampleB = Vector3.Lerp(endA, endB, 0.94f);
                if (!SampleStairSurface(stairCollider, sampleA, local.max.y + 1f, out sampleA) ||
                    !SampleStairSurface(stairCollider, sampleB, local.max.y + 1f, out sampleB)) continue;
                if (Mathf.Abs(sampleA.y - sampleB.y) < 0.15f) continue;
                Vector3 upper = sampleA.y > sampleB.y ? sampleA : sampleB;
                Vector3 lower = sampleA.y > sampleB.y ? sampleB : sampleA;
                float distance = (upper - captainLocal).sqrMagnitude;
                if (distance < best)
                {
                    best = distance; bottom = lower; top = upper;
                    _cinematicStairCollider = stairCollider;
                }
            }
            return best < float.PositiveInfinity;
        }

        private bool SampleStairSurface(Collider collider, Vector3 local, float rayHeight, out Vector3 surface)
        {
            surface = local;
            Vector3 origin = _shipTransform.TransformPoint(new Vector3(local.x, rayHeight, local.z));
            Ray ray = new Ray(origin, -_shipTransform.up);
            if (!collider.Raycast(ray, out RaycastHit hit, 30f)) return false;
            surface = _shipTransform.InverseTransformPoint(hit.point) + Vector3.up * 0.025f;
            return true;
        }

        private IEnumerator WalkShipSegment(Vector3 from, Vector3 to, bool onStairs = false)
        {
            float duration = Mathf.Max(0.3f, Vector3.Distance(from, to) / 1.25f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector3 step = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                if (onStairs && _cinematicStairCollider != null &&
                    SampleStairSurface(_cinematicStairCollider, step, Mathf.Max(from.y, to.y) + 1f, out Vector3 tread))
                    step.y = tread.y;
                _player.transform.position = _shipTransform.TransformPoint(step);
                Vector3 facing = Vector3.ProjectOnPlane(_shipTransform.TransformDirection(to - from), Vector3.up);
                if (facing.sqrMagnitude > 0.001f) _player.transform.rotation = Quaternion.LookRotation(facing);
                if (_characterAnimator != null)
                {
                    _characterAnimator.SetFloat("Speed", 2f);
                    _characterAnimator.SetFloat("MotionSpeed", 1f);
                    _characterAnimator.SetBool("Grounded", true);
                }
                if (_mainCam != null)
                {
                    Vector3 look = _player.transform.position + Vector3.up * 1.3f;
                    Vector3 camera = look - _player.transform.forward * 3f + _shipTransform.right * 1.2f + Vector3.up * 0.6f;
                    camera = SafeCutsceneCamera(look, camera, _player.transform, _npcTransform);
                    _mainCam.transform.SetPositionAndRotation(camera, Quaternion.LookRotation(look - camera));
                }
                yield return null;
            }
        }

        private bool CameraPointBlocked(Vector3 point, Transform ignoreA, Transform ignoreB)
        {
            foreach (Collider collider in Physics.OverlapSphere(point, 0.22f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (ignoreA != null && collider.transform.IsChildOf(ignoreA)) continue;
                if (ignoreB != null && collider.transform.IsChildOf(ignoreB)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Keeps cutscene cameras outside hulls, rocks and sails; falls back to an overhead shot.</summary>
        private Vector3 SafeCutsceneCamera(Vector3 focus, Vector3 desired, Transform ignoreA, Transform ignoreB)
        {
            if (!keepCutsceneCameraOutOfGeometry) return desired;
            if (!CameraPointBlocked(desired, ignoreA, ignoreB)) return desired;
            Vector3 direction = desired - focus;
            float distance = direction.magnitude;
            if (distance > 0.01f && Physics.SphereCast(focus, 0.2f, direction / distance, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore)
                && !(ignoreA != null && hit.transform.IsChildOf(ignoreA)) && !(ignoreB != null && hit.transform.IsChildOf(ignoreB)))
            {
                Vector3 pulled = focus + direction / distance * Mathf.Max(0.4f, hit.distance - 0.12f);
                if (!CameraPointBlocked(pulled, ignoreA, ignoreB)) return pulled;
            }
            for (int step = 1; step <= 4; step++)
            {
                Vector3 raised = focus + Vector3.up * (0.8f * step) + direction.normalized * 0.6f * step;
                if (!CameraPointBlocked(raised, ignoreA, ignoreB)) return raised;
            }
            return focus + Vector3.up * 3f;
        }

        private IEnumerator PlayCaptainConversation()
        {
            _hasReachedShip = true;
            if (_player == null || _npcTransform == null || _shipTransform == null) yield break;
            // A short montage cut brings the hero aboard; no walking across open water.
            float fade = 0f;
            while (fade < 0.6f)
            {
                fade += Time.deltaTime;
                _endingFade = Mathf.Clamp01(fade / 0.6f);
                yield return null;
            }
            Vector3 captainLocal = _shipTransform.InverseTransformPoint(_npcTransform.position);
            // Stand the hero outboard of the captain (towards the nearest rail). The camera then
            // sees the two of them against the sea instead of the cabin or the sail.
            Vector3 center = _measuredShipLocalBounds.center;
            Vector3 outboard = Vector3.ProjectOnPlane(captainLocal - center, Vector3.up);
            if (outboard.sqrMagnitude < 0.25f) outboard = Vector3.forward;
            outboard.Normalize();
            Vector3 meetingLocal = captainLocal + outboard * 1.25f;
            if (autoFitCrewToMeasuredDeck && _shipDeckMeasured)
            {
                float halfX = _measuredShipLocalBounds.extents.x * 0.7f;
                float halfZ = _measuredShipLocalBounds.extents.z * 0.7f;
                meetingLocal.x = Mathf.Clamp(meetingLocal.x, -halfX, halfX);
                meetingLocal.z = Mathf.Clamp(meetingLocal.z, -halfZ, halfZ);
                meetingLocal.y = DeckLocalYAt(meetingLocal) + 0.02f;
                // Stay within talking distance of the helmsman; do not wander off the deck.
                Vector3 openPoint = FindOpenDeckPoint(_shipTransform, meetingLocal);
                if (Vector3.Distance(openPoint, captainLocal) <= 2.2f) meetingLocal = openPoint;
                if (Vector3.Distance(meetingLocal, captainLocal) < 0.9f)
                {
                    Vector3 sideways = Vector3.Cross(Vector3.up, outboard).normalized;
                    meetingLocal = captainLocal + sideways * 1.2f;
                    meetingLocal.y = DeckLocalYAt(meetingLocal) + 0.02f;
                }
            }
            else
            {
                meetingLocal.y = captainLocal.y;
            }
            bool stairsValid = TryFindShipStairRoute(captainLocal, out Vector3 stairBottom, out Vector3 stairTop);
            if (stairsValid && _shipDeckMeasured &&
                (stairTop.y < _measuredDeckLocalY - 0.6f || stairBottom.y < _measuredShipLocalBounds.min.y - 0.5f))
                stairsValid = false;
            if (stairsValid)
            {
                meetingLocal.y = stairTop.y;
                Vector3 approachLocal = stairBottom + (stairBottom - stairTop).normalized * 1.5f;
                approachLocal.y = stairBottom.y;
                _player.transform.position = _shipTransform.TransformPoint(approachLocal);
                _endingFade = 0f;
                yield return WalkShipSegment(approachLocal, stairBottom);
                yield return WalkShipSegment(stairBottom, stairTop, true);
                yield return WalkShipSegment(stairTop, meetingLocal);
            }
            else
            {
                // No invented diagonal route through the cabin if the mesh has no separate stairs.
                _player.transform.position = _shipTransform.TransformPoint(meetingLocal);
                _endingFade = 0f;
                Debug.LogWarning("[Dimari] Не найдена отдельная лестница. Добавьте StairBottom и StairTop в префаб корабля для точного маршрута.");
            }
            if (_characterAnimator != null) _characterAnimator.SetFloat("Speed", 0f);
            // Give control back for an explicit, proximity-gated interaction.
            CharacterController controller = _player.GetComponent<CharacterController>();
            _player.ResetWaterMotion();
            if (controller != null) controller.enabled = true;
            _player.IsControlEnabled = true;
            if (_cameraController != null) _cameraController.BeginGameplayTransitionFromCutscene();
            _waitingForCaptainInteraction = true;
            QuestStep captainQuest = new QuestStep { id = "talk_captain", title = "Поговорить с капитаном [E]." };
            questSteps.Add(captainQuest);
            ShowStatusToast("Нужно поговорить с капитаном. Подойдите и нажмите E.");
            // Let a previous interact press expire before accepting the dialogue command.
            yield return null;
            while (true)
            {
                bool near = Vector3.Distance(_player.transform.position, _npcTransform.position) <= 3.5f;
                _interactionPrompt = near ? "[E] Поговорить с капитаном" : "Подойдите к капитану, чтобы поговорить";
                if (!_isMenuOpen && near && DimariInput.IsInteractPressed()) break;
                yield return null;
            }
            _interactionPrompt = "";
            _player.IsControlEnabled = false;
            _player.StopLocomotionAnimation();
            if (controller != null) controller.enabled = false;
            if (_cameraController != null)
            {
                _cameraController.IsCutsceneControlled = true;
                _cameraController.UpdateCharacterMeshVisibility(false);
            }
            _waitingForCaptainInteraction = false;
            captainQuest.isCompleted = true;
            meetingLocal = _shipTransform.InverseTransformPoint(_player.transform.position);
            if (heroCaptainQuestion == null)
                heroCaptainQuestion = Resources.Load<AudioClip>("Voice_Hero_CaptainQuestion");
            if (pirateCaptainAnswer == null)
                pirateCaptainAnswer = Resources.Load<AudioClip>("Voice_Pirate_CaptainAnswer");
            _captainConversationActive = true;
            const string question = "Товарищ, где я? Я не помню, как я тут оказался. Подскажите, где я?";
            const string answer = "Слышь, сухопутная крыса, ты на необитаемом острове! И скажу я тебе по-честному: повезло тебе, что старый морской волк бросил тут свой якорь. Прыгай на борт — и мы с тобой доплывём до города, чтоб меня черти драли!";
            yield return CaptainDialogueLine("Герой: " + question, heroCaptainQuestion, 7f, false, meetingLocal);
            yield return CaptainDialogueLine("Капитан: " + answer, pirateCaptainAnswer, 19f, true, meetingLocal);
        }

        private IEnumerator ArriveInCity(bool pizzaPreview = false)
        {
            if (!Application.CanStreamedLevelBeLoaded(cityScenePath))
            {
                Debug.LogError("[Dimari] Сцена города не найдена в Build Settings: " + cityScenePath);
                ShowStatusToast("Сцена города не настроена. Лесная сцена сохранена.");
                yield break;
            }
            _isPlayingEndingCutscene = true;
            _player.IsControlEnabled = false;
            _cameraController.IsCutsceneControlled = true;
            float fade = 0f;
            while (fade < 0.6f)
            {
                fade += Time.deltaTime;
                _endingFade = Mathf.Clamp01(fade / 0.6f);
                yield return null;
            }
            Scene oldScene = gameObject.scene;
            yield return SceneManager.LoadSceneAsync(cityScenePath, LoadSceneMode.Additive);
            Scene city = SceneManager.GetSceneByPath(cityScenePath);
            Transform arrival = null;
            foreach (GameObject root in city.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "CityArrivalPoint") arrival = child;
            if (arrival == null)
            {
                yield return SceneManager.UnloadSceneAsync(city);
                _endingFade = 0f;
                _isPlayingEndingCutscene = false;
                _player.IsControlEnabled = true;
                _cameraController.BeginGameplayTransitionFromCutscene();
                Debug.LogError("[Dimari] В сцене города нужен CityArrivalPoint на безопасном месте улицы.");
                yield break;
            }
            foreach (GameObject root in city.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
            Transform[] travellers = { transform, _player.transform, _mainCam.transform, _npcTransform };
            foreach (Transform traveller in travellers)
            {
                if (traveller == null) continue;
                traveller.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(traveller.gameObject, city);
            }
            CharacterController cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Vector3 spawnPosition = arrival.position;
            Quaternion spawnRotation = arrival.rotation;
            bool pizzaSpawn = pizzaPreview && TryFindPizzaPreviewSpawn(city, out spawnPosition, out spawnRotation);
            if (!pizzaSpawn) { spawnPosition = arrival.position; spawnRotation = arrival.rotation; }
            _player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (_npcTransform != null)
                _npcTransform.SetPositionAndRotation(arrival.position + arrival.right * 2f, arrival.rotation);
            SceneManager.SetActiveScene(city);
            yield return SceneManager.UnloadSceneAsync(oldScene);
            _endingFade = 0f;
            _isChapter2Reached = false;
            _captainConversationActive = false;
            _cityChapterTitleTimer = 7f;
            _interactionPrompt = "";
            _waitingForCaptainInteraction = false;
            _showHUD = true;
            questSteps.Clear();
            questSteps.Add(new QuestStep { id = "city_administration", title = "Найти администрацию Рогово и узнать о Димарике." });
            if (pizzaSpawn)
            {
                questSteps.Clear();
                questSteps.Add(new QuestStep { id = "city_pizza_express", title = "Найти «Пицца Экспресс 24» и разыскать Димарика." });
                if (_npcTransform != null) _npcTransform.gameObject.SetActive(false);
                ShowStatusToast("Тестовый старт у пиццерии. Сохранение не загружалось.");
            }
            else if (!pizzaPreview) yield return CityCaptainFarewell(arrival);
            else ShowStatusToast("Безопасное место у пиццерии не найдено — старт у причала.");
            _player.ResetWaterMotion();
            if (cc != null) cc.enabled = true;
            _player.IsControlEnabled = true;
            _cameraController.BeginGameplayTransitionFromCutscene();
            _isPlayingEndingCutscene = false;
        }

        // Read-only placement search: never disables city colliders or changes the scene.
        private bool TryFindPizzaPreviewSpawn(Scene city, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Physics.SyncTransforms();
            foreach (GameObject root in city.GetRootGameObjects())
            foreach (PizzaExpressEntrance entrance in root.GetComponentsInChildren<PizzaExpressEntrance>())
            {
                Transform door = entrance.transform;
                Vector3 outward = -door.forward;
                foreach (float distance in new[] { 2f, 3f, 4f })
                foreach (float side in new[] { 0f, -1.5f, 1.5f })
                {
                    Vector3 probe = door.position + outward * distance + door.right * side;
                    RaycastHit ground;
                    if (!Physics.Raycast(probe + Vector3.up * 2f, Vector3.down, out ground, 4f, ~0, QueryTriggerInteraction.Ignore)
                        || ground.normal.y < 0.8f) continue;
                    Vector3 feet = ground.point + Vector3.up * 0.08f;
                    CharacterController controller = _player.GetComponent<CharacterController>();
                    float radius = controller != null ? controller.radius * Mathf.Max(Mathf.Abs(_player.transform.lossyScale.x), Mathf.Abs(_player.transform.lossyScale.z)) : 0.4f;
                    float height = controller != null ? controller.height * Mathf.Abs(_player.transform.lossyScale.y) : 1.8f;
                    radius = Mathf.Max(radius, 0.35f);
                    height = Mathf.Max(height, radius * 2f);
                    if (Physics.CheckCapsule(feet + Vector3.up * radius, feet + Vector3.up * (height - radius), radius, ~0, QueryTriggerInteraction.Ignore)) continue;
                    position = feet;
                    rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(door.position - feet, Vector3.up));
                    return true;
                }
            }
            return false;
        }

        private IEnumerator CityCaptainFarewell(Transform arrival)
        {
            if (_npcTransform == null) yield break;
            // The ship and its wheel have been unloaded. Never retain the helm pose ashore.
            _captainWheel = null;
            Vector3 toHero = Vector3.ProjectOnPlane(_player.transform.position - _npcTransform.position, Vector3.up);
            if (toHero.sqrMagnitude > 0.01f)
                _npcTransform.rotation = Quaternion.LookRotation(toHero);
            AudioClip clip = Resources.Load<AudioClip>("Voice_Pirate_CityArrival");
            float duration = clip != null ? clip.length + 0.5f : 15f;
            ShowMonologue("Капитан: Еле доплыли до энтого берега, чтоб его в кильватер! Я пошёл хлебнуть ромцу где-нибудь, а ты дуй ищи администрацию — авось там подскажут, где он, сухопутный краб, схоронился.", duration);
            AudioSource voice = null;
            if (clip != null)
            {
                voice = _mainCam.gameObject.AddComponent<AudioSource>();
                voice.spatialBlend = 0f;
                voice.clip = clip;
                PlayStoryVoice(voice);
            }
            float farewellElapsed = 0f;
            while (farewellElapsed < duration)
            {
                if (_isMenuOpen) { yield return null; continue; }
                farewellElapsed += Time.deltaTime;
                Vector3 look = _npcTransform.position + Vector3.up * 1.45f;
                Vector3 camera = _npcTransform.position + _npcTransform.forward * 2.8f
                    + _npcTransform.right * 0.65f + Vector3.up * 1.55f;
                _mainCam.transform.SetPositionAndRotation(camera, Quaternion.LookRotation(look - camera));
                yield return null;
            }
            if (voice != null) Destroy(voice);
            // Try both directions along the street; stop rather than walking through a wall.
            Vector3 direction = arrival.forward;
            if (!CityCaptainStepClear(direction, 1f)) direction = -direction;
            _npcTransform.rotation = Quaternion.LookRotation(direction);
            Transform leftLeg = null, rightLeg = null;
            Quaternion leftRest = Quaternion.identity, rightRest = Quaternion.identity;
            if (_captainAnimator != null && _captainAnimator.isHuman)
            {
                leftLeg = _captainAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightLeg = _captainAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                if (leftLeg != null) leftRest = leftLeg.localRotation;
                if (rightLeg != null) rightRest = rightLeg.localRotation;
                _captainAnimator.enabled = false;
            }
            for (float t = 0f; t < 4.5f; t += Time.deltaTime)
            {
                float step = 1.35f * Time.deltaTime;
                if (!CityCaptainStepClear(direction, Mathf.Max(step, 0.5f))) break;
                Vector3 next = _npcTransform.position + direction * step;
                bool grounded = false;
                float groundY = float.NegativeInfinity;
                foreach (RaycastHit hit in Physics.RaycastAll(next + Vector3.up * 1.5f,
                    Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(_npcTransform) || hit.transform.IsChildOf(_player.transform)
                        || hit.normal.y < 0.65f) continue;
                    groundY = Mathf.Max(groundY, hit.point.y);
                    grounded = true;
                }
                if (!grounded) break;
                next.y = groundY;
                _npcTransform.position = next;
                float swing = Mathf.Sin(t * 9f) * 23f;
                if (leftLeg != null) leftLeg.localRotation = leftRest * Quaternion.Euler(swing, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = rightRest * Quaternion.Euler(-swing, 0f, 0f);
                Vector3 look = _npcTransform.position + Vector3.up * 1.2f;
                Vector3 camera = arrival.position - arrival.forward * 2f + Vector3.up * 1.7f;
                _mainCam.transform.SetPositionAndRotation(camera, Quaternion.LookRotation(look - camera));
                yield return null;
            }
            if (leftLeg != null) leftLeg.localRotation = leftRest;
            if (rightLeg != null) rightLeg.localRotation = rightRest;
            // End the departure on a cut, leaving no stationary captain at the arrival point.
            _endingFade = 1f;
            yield return new WaitForSeconds(0.25f);
            _npcTransform.gameObject.SetActive(false);
            _endingFade = 0f;
        }

        private bool CityCaptainStepClear(Vector3 direction, float distance)
        {
            foreach (RaycastHit hit in Physics.SphereCastAll(_npcTransform.position + Vector3.up * 0.9f,
                0.3f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(_npcTransform) || hit.transform.IsChildOf(_player.transform)) continue;
                return false;
            }
            return true;
        }

        private bool TryFindOpenWaterVoyage(out Vector3 start, out Vector3 end)
        {
            start = end = _shipTransform.position;
            float level = start.y;
            Vector3 origin = start;
            float length = Mathf.Clamp(sailingDistance, 20f, Mathf.Max(20f, oceanSize * 0.25f));
            // Keep both hull and tracking camera well clear of land. Conservative checks
            // intentionally reject questionable routes instead of sailing through scenery.
            float[] radii = { 0f, 60f, 120f, 220f, 350f, 550f };
            foreach (float radius in radii)
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                    Vector3 candidate = origin + direction * radius;
                    candidate.y = level;
                    Vector3 destination = sailingDestination != null ? sailingDestination.position : candidate + direction * length;
                    destination.y = level;
                    if ((destination - candidate).sqrMagnitude < 100f) continue;
                    if (!IsInsideOcean(candidate) || !IsInsideOcean(destination)) continue;
                    Quaternion rotation = Quaternion.LookRotation(destination - candidate);
                    float corridorLength = Vector3.Distance(candidate, destination);
                    Vector3 center = (candidate + destination) * 0.5f + Vector3.up * 8f;
                    Collider[] obstacles = Physics.OverlapBox(center,
                        new Vector3(Mathf.Max(35f, shipModelSize), 12f, corridorLength * 0.5f + shipModelSize),
                        rotation, ~0, QueryTriggerInteraction.Ignore);
                    bool clear = true;
                    foreach (Collider obstacle in obstacles)
                    {
                        if (obstacle.transform.IsChildOf(_shipTransform) ||
                            obstacle.transform.IsChildOf(_player.transform) ||
                            obstacle.name == "Dimari_SurroundingOcean") continue;
                        clear = false;
                        break;
                    }
                    if (!clear) continue;
                    start = candidate; end = destination;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Nearest generated sea cell centre, so ships never stand on the island.</summary>
        private bool FindOpenSeaNear(Vector3 near, float maxDistance, out Vector3 result)
        {
            result = near;
            if (_seaCells == null || cliffArrivalPoint == null) return false;
            int columns = _seaCells.GetLength(0), rows = _seaCells.GetLength(1);
            float dx = _seaWidth / columns, dz = _seaLength / rows;
            Quaternion rotation = BayRotation();
            Vector3 origin = cliffArrivalPoint.position; origin.y = oceanLevel;
            float best = float.PositiveInfinity;
            for (int x = 0; x < columns; x++)
                for (int z = 0; z < rows; z++)
                {
                    if (!_seaCells[x, z]) continue;
                    Vector3 point = origin + rotation * new Vector3(
                        -_seaWidth * 0.5f + (x + 0.5f) * dx, 0f, bayStartFromCliff + (z + 0.5f) * dz);
                    float distance = Vector3.Distance(point, near);
                    if (distance >= best || distance > maxDistance) continue;
                    best = distance;
                    result = point;
                }
            return best < float.PositiveInfinity;
        }

        /// <summary>Plain open-sea route used when the strict corridor search finds nothing.</summary>
        private bool TrySimpleSeaRoute(out Vector3 start, out Vector3 end)
        {
            start = end = _shipTransform != null ? _shipTransform.position : cliffArrivalPoint.position;
            if (cliffArrivalPoint == null) return false;
            if (sailingDestination != null)
            {
                start.y = _cutsceneWaterLevel;
                end = sailingDestination.position;
                end.y = _cutsceneWaterLevel;
                return (end - start).sqrMagnitude > 400f;
            }
            // Корабль уходит вперёд от обрыва по воде, которая построена в проёме.
            Vector3 direction = Vector3.ProjectOnPlane(cliffArrivalPoint.forward, Vector3.up);
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            direction.Normalize();
            start.y = _cutsceneWaterLevel;
            float waterLength = Mathf.Clamp(cutsceneWaterSize, 60f, 600f);
            float length = Mathf.Clamp(sailingDistance, 40f, Mathf.Max(40f, waterLength * 0.55f));
            end = start + direction * length;
            end.y = _cutsceneWaterLevel;
            return (end - start).sqrMagnitude > 400f;
        }

        private Quaternion BayRotation()
        {
            return Quaternion.Euler(0f, cliffArrivalPoint != null ? cliffArrivalPoint.eulerAngles.y : 0f, 0f);
        }

        private bool IsInsideOcean(Vector3 point)
        {
            if (cliffArrivalPoint == null) return false;
            Vector3 local = Quaternion.Inverse(BayRotation()) * (point - cliffArrivalPoint.position);
            if (_seaCells == null) return false;
            if (Mathf.Abs(local.x) >= _seaWidth * 0.5f || local.z <= bayStartFromCliff ||
                local.z >= bayStartFromCliff + _seaLength) return false;
            int x = Mathf.FloorToInt((local.x + _seaWidth * 0.5f) / _seaWidth * _seaCells.GetLength(0));
            int z = Mathf.FloorToInt((local.z - bayStartFromCliff) / _seaLength * _seaCells.GetLength(1));
            return _seaCells[x, z];
        }

        private IEnumerator PlaySailingToRogovo()
        {
            if (_shipTransform == null || _npcTransform == null || _player == null) yield break;
            Vector3 heroLocal = _shipTransform.InverseTransformPoint(_player.transform.position);
            Physics.SyncTransforms();
            if (!TryFindOpenWaterVoyage(out Vector3 seaStart, out Vector3 seaEnd))
            {
                if (TrySimpleSeaRoute(out seaStart, out seaEnd))
                {
                    Debug.LogWarning("[Dimari] Строгий коридор не найден; используется простой морской маршрут "
                        + seaStart.ToString("F1") + " → " + seaEnd.ToString("F1") + ". Плавание к Рогово состоится.");
                }
                else
                {
                    Debug.LogWarning("[Dimari] Морской маршрут не найден. Плавание отменено: проверьте Ocean Size, уровень воды и Sailing Destination.");
                    ShowStatusToast("Для плавания нужен свободный участок моря.");
                    yield break;
                }
            }
            float fadeTime = 0f;
            while (fadeTime < 0.6f)
            {
                fadeTime += Time.deltaTime;
                _endingFade = Mathf.Clamp01(fadeTime / 0.6f);
                yield return null;
            }
            Transform parent = _shipTransform.parent;
            _sailingStart = parent != null ? parent.InverseTransformPoint(seaStart) : seaStart;
            _sailingEnd = parent != null ? parent.InverseTransformPoint(seaEnd) : seaEnd;
            _shipBasePos = _sailingStart;
            Quaternion heading = Quaternion.LookRotation((seaEnd - seaStart).normalized, Vector3.up);
            _shipBaseRot = parent != null ? Quaternion.Inverse(parent.rotation) * heading : heading;
            _shipTransform.SetPositionAndRotation(seaStart, heading);
            _player.transform.position = _shipTransform.TransformPoint(heroLocal);
            _sailingElapsed = 0f;
            _isSailing = true;
            FrameCaptainConversation(false);
            fadeTime = 0f;
            while (fadeTime < 0.6f)
            {
                fadeTime += Time.deltaTime;
                _endingFade = 1f - Mathf.Clamp01(fadeTime / 0.6f);
                _player.transform.position = _shipTransform.TransformPoint(heroLocal);
                FrameCaptainConversation(false);
                yield return null;
            }
            _endingFade = 0f;
            string[] lines =
            {
                "Капитан: Держись крепче, сухопутный! Курс на Рогово. Так кого ты там ищешь?",
                "Герой: Димарика. Он работает в администрации в селе Рогово. Ездит на BMW третьей серии, 320d. Надеюсь, с ним всё в порядке.",
                "Капитан: В администрации, говоришь? Значит, бумажные штормы ему знакомы! А как мне узнать твоего приятеля на берегу?",
                "Герой: Включи Дору или Мэйби Бэйби — он наверняка узнает. А ещё он чертовски любит пиво и семечки. И мечтает купить Changan UNI-V, хотя у самого уже BMW.",
                "Капитан: Ха! У каждого морского волка своя мечта. Мне бы парус без дыр, а ему новую сухопутную посудину! Только пиво до швартовки не открывать!",
                "Герой: Договорились. Лишь бы найти его. Я до сих пор не понимаю, что случилось на острове. Доберёмся до Рогово — начну с администрации. Может, там знают, куда он пропал.",
                "Капитан: Доберёмся, парень. Сперва порт, потом твоя администрация. А пока смотри на горизонт: море не любит, когда ему не доверяют."
            };
            for (int i = 0; i < lines.Length; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>("Voice_Sailing_" + (i + 1));
                yield return CaptainDialogueLine(lines[i], clip, Mathf.Max(5f, lines[i].Length / 13f),
                    i % 2 == 0, heroLocal);
            }
            _isSailing = false;
            // Keep the final world position; normal wave bobbing continues here.
            _sailingStart = _shipBasePos;
        }

        // Only story speech owned by this manager; forest/menu ambience stays audible.
        private readonly List<AudioSource> _storyVoices = new List<AudioSource>();
        private readonly HashSet<AudioSource> _menuPausedVoices = new HashSet<AudioSource>();

        private void PlayStoryVoice(AudioSource voice)
        {
            voice.playOnAwake = false;
            _storyVoices.Add(voice);
            voice.Play();
            SyncStoryVoiceMenuState();
        }

        private void SyncStoryVoiceMenuState()
        {
            bool silence = _isMenuOpen || !_hasGameStarted;
            for (int i = _storyVoices.Count - 1; i >= 0; i--)
            {
                AudioSource voice = _storyVoices[i];
                if (voice == null)
                {
                    _menuPausedVoices.Remove(voice);
                    _storyVoices.RemoveAt(i);
                    continue;
                }
                if (silence && voice.isPlaying)
                {
                    voice.Pause();
                    _menuPausedVoices.Add(voice);
                }
                else if (!silence && _menuPausedVoices.Remove(voice)) voice.UnPause();
            }
        }

        private IEnumerator CaptainDialogueLine(string subtitle, AudioClip clip, float fallbackDuration,
            bool captainSpeaking, Vector3 heroLocalPosition)
        {
            float duration = Mathf.Clamp(clip != null ? clip.length + 0.4f : fallbackDuration, 1f, 90f);
            ShowMonologue(subtitle, duration);
            AudioSource voice = null;
            if (clip != null && _mainCam != null)
            {
                voice = _mainCam.gameObject.AddComponent<AudioSource>();
                voice.spatialBlend = 0f;
                voice.clip = clip;
                PlayStoryVoice(voice);
            }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_isMenuOpen) { yield return null; continue; }
                elapsed += Time.unscaledDeltaTime;
                _player.StopLocomotionAnimation();
                _player.transform.position = _shipTransform.TransformPoint(heroLocalPosition);
                Vector3 toCaptain = Vector3.ProjectOnPlane(_npcTransform.position - _player.transform.position, _shipTransform.up);
                if (toCaptain.sqrMagnitude > 0.001f)
                    _player.transform.rotation = Quaternion.LookRotation(toCaptain, _shipTransform.up);
                FrameCaptainConversation(captainSpeaking);
                yield return null;
            }
            if (voice != null) Destroy(voice);
        }

        private void FrameCaptainConversation(bool captainSpeaking)
        {
            if (_mainCam == null) return;
            if (_isSailing)
            {
                // Wide shot computed from the real ship size, so a large hull never contains the camera.
                float shipSpan = Mathf.Max(12f, Mathf.Max(_measuredShipLocalBounds.size.x, _measuredShipLocalBounds.size.z));
                float orbit = Mathf.Sin(_sailingElapsed * 0.07f);
                Vector3 sailingLookTarget = _shipTransform.position + Vector3.up * (shipSpan * 0.25f);
                Vector3 camera = _shipTransform.position
                    + _shipTransform.right * (shipSpan * 1.15f + orbit * 4f)
                    - _shipTransform.forward * (shipSpan * 0.9f)
                    + Vector3.up * (shipSpan * 0.45f + 3f);
                camera = SafeCutsceneCamera(sailingLookTarget, camera, _player.transform, _npcTransform);
                _mainCam.transform.SetPositionAndRotation(camera, Quaternion.LookRotation(sailingLookTarget - camera));
                return;
            }
            Vector3 heroHead = _player.transform.position + Vector3.up * 1.5f;
            Vector3 captainHead = _npcTransform.position + Vector3.up * 1.5f;
            // Inside a cabin the wide shot would put the camera through a wall or the ceiling.
            // Use a close profile shot in front of the speaker instead.
            bool cramped = Physics.Raycast(captainHead, Vector3.up, out RaycastHit headroom, 2f, ~0, QueryTriggerInteraction.Ignore)
                && !headroom.transform.IsChildOf(_npcTransform) && !headroom.transform.IsChildOf(_player.transform);
            if (cramped)
            {
                Vector3 face = captainSpeaking ? captainHead : heroHead;
                Vector3 listener = captainSpeaking ? heroHead : captainHead;
                Vector3 toListener = Vector3.ProjectOnPlane(listener - face, Vector3.up);
                if (toListener.sqrMagnitude < 0.01f) toListener = _shipTransform.forward;
                toListener.Normalize();
                Vector3 profileSide = Vector3.Cross(Vector3.up, toListener).normalized;
                Vector3 close = face + toListener * 0.85f + profileSide * 0.35f - Vector3.up * 0.06f;
                close = SafeCutsceneCamera(face, close, _player.transform, _npcTransform);
                _mainCam.transform.SetPositionAndRotation(close, Quaternion.LookRotation(face - close));
                return;
            }
            Vector3 direction = Vector3.ProjectOnPlane(captainHead - heroHead, Vector3.up);
            if (direction.sqrMagnitude < 0.01f) direction = _shipTransform.forward;
            direction.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            // A side two-shot: both faces visible, the hero's backpack is not a wall in the lens.
            Vector3 look = captainSpeaking ? captainHead : heroHead;
            Vector3 midpoint = (heroHead + captainHead) * 0.5f;
            Vector3 first = midpoint + side * 1.45f - direction * 0.15f + Vector3.up * 0.05f;
            Vector3 position = SafeCutsceneCamera(look, first, _player.transform, _npcTransform);
            _mainCam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
        }

        public void OpenMainMenuOnStartup()
        {
            _isMenuOpen = true;
            SyncStoryVoiceMenuState();
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
            SyncStoryVoiceMenuState();
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

        /// <summary>Closes the pause menu and restores gameplay, even if the normal resume path is blocked.</summary>
        public void ForceCloseMenu()
        {
            _isMenuOpen = false;
            SyncStoryVoiceMenuState();
            _isSettingsOpen = false;
            _chapterSelectionOpen = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_cameraController != null)
            {
                _cameraController.IsMenuCameraActive = false;
                _cameraController.IsCutsceneControlled = false;
                _cameraController.UpdateCharacterMeshVisibility(_cameraController.isFirstPerson);
            }
            if (_player != null && !_isPlayingEndingCutscene && (_director == null || !_director.IsPlayingCutscene))
            {
                _player.IsControlEnabled = true;
            }
        }

        /// <summary>Emergency F8 recovery: ends any paused/cutscene stuck state and returns the player to gameplay.</summary>
        public void RecoverGameplay()
        {
            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_cameraController == null) _cameraController = FindAnyObjectByType<ForestCameraController>();
            if (_director == null) _director = FindAnyObjectByType<ForestAwakeningDirector>();
            if (_director != null)
            {
                _director.StopAllCoroutines();
                _director.enabled = false;
                _director.FinishAwakeningImmediately();
            }
            var maybeBaby = FindAnyObjectByType<Dimari.Cinematics.DonsportMaybeBabyCutscene>();
            if (maybeBaby != null) maybeBaby.Abort();
            var reception = FindAnyObjectByType<Dimari.Cinematics.DonsportReceptionCutscene>();
            if (reception != null) reception.Abort();
            _hasGameStarted = true;
            _showHUD = true;
            _isMenuOpen = false;
            _isSettingsOpen = false;
            _chapterSelectionOpen = false;
            _isTabletViewerOpen = false;
            _isPlayingPickupAnim = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_cameraController != null)
            {
                _cameraController.IsMenuCameraActive = false;
                _cameraController.IsCutsceneControlled = false;
                _cameraController.UpdateCharacterMeshVisibility(_cameraController.isFirstPerson);
            }
            if (_player != null)
            {
                _player.IsControlEnabled = true;
                _player.StopLocomotionAnimation();
            }
            ShowStatusToast("Управление и HUD восстановлены (F8).");
        }

        public void ResumeFromPause()
        {
            _isMenuOpen = false;
            SyncStoryVoiceMenuState();
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
            else if (_player != null && !_isPlayingEndingCutscene)
            {
                _player.IsControlEnabled = true;
                _showHUD = true;
            }
        }

        public void StartNewGame()
        {
            _hasReachedShip = false;
            _waitingForCaptainInteraction = false;
            StopAllCoroutines();
            _isSailing = false;
            _endingFade = 0f;
            _isPlayingEndingCutscene = false;
            _isPlayingPickupAnim = false;
            if (_player == null) _player = FindAnyObjectByType<ForestPlayerController>();
            if (_player != null)
            {
                CharacterController controller = _player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = true;
            }
            _isMenuOpen = false;
            SyncStoryVoiceMenuState();
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
            if (Dimari.Cinematics.PizzaSellerCutscene.IsRunning || Dimari.Cinematics.DonsportReceptionCutscene.IsRunning
                || Dimari.Cinematics.DonsportMaybeBabyCutscene.IsRunning)
            { ShowStatusToast("Сохранитесь после окончания разговора."); return; }
            int cityStage = 0;
            foreach (QuestStep step in questSteps)
            {
                if (step.id == "city_donsport") cityStage = 2;
                else if (step.id == "city_pizza_express" && cityStage < 1) cityStage = 1;
            }
            PlayerPrefs.SetInt("Dimari_Save_CityDialogueStage", cityStage);
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
            SyncStoryVoiceMenuState();
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

            // Reconstruct the dynamic city dialogue objective only when loading in the city.
            // New Game does not consult these save keys.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Chapter2_Rogovo")
            {
                int cityStage = PlayerPrefs.GetInt("Dimari_Save_CityDialogueStage", 0);
                if (cityStage >= 1)
                {
                    questSteps.Clear();
                    questSteps.Add(new QuestStep { id = "city_pizza_express", title = "Найти «Пицца Экспресс 24» и разыскать Димарика.", isCompleted = cityStage >= 2 });
                    if (cityStage >= 2) questSteps.Add(new QuestStep { id = "city_donsport", title = "Поехать в спортивный зал «Донспорт» и расспросить о Димарике.", isCompleted = PlayerPrefs.GetInt("Dimari_Save_Quest_city_donsport", 0) == 1 });
                    if (cityStage >= 2 && PlayerPrefs.GetInt("Dimari_Save_Quest_city_donsport", 0) == 1)
                        questSteps.Add(new QuestStep { id = "city_maybe_baby", title = "Найти Мэйби Бэйби среди посетителей тренажёрного зала Донспорта.", isCompleted = PlayerPrefs.GetInt("Dimari_Save_Quest_city_maybe_baby", 0) == 1 });
                    if (PlayerPrefs.GetInt("Dimari_Save_Quest_city_maybe_baby", 0) == 1)
                        questSteps.Add(new QuestStep { id = "city_avtorusal", title = "Поехать в «Авторусь»: Димарик хотел купить Changan UNI-V.", isCompleted = PlayerPrefs.GetInt("Dimari_Save_Quest_city_avtorusal", 0) == 1 });
                    Dimari.Cinematics.OfficeFishCutscene.Completed = true;
                }
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

            if (stepId == "find_backpack" || stepId == "find_tablet")
            {
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
                // Запускаем кинематографичную кат-сцену с анимацией ходьбы к воде и кораблю!
                StartCoroutine(PlayWalkToWaterAndShipCutsceneRoutine(targetObj));
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

        private void DrawEndingApproachMarker()
        {
            if (_player == null || _mainCam == null || !_hasGameStarted || _isMenuOpen ||
                !_hasPickedUpTablet || _isTabletViewerOpen || _isChapter2Reached) return;
            bool captainTarget = _waitingForCaptainInteraction && _npcTransform != null;
            if (_hasReachedShip && !captainTarget) return;
            if (_isPlayingEndingCutscene && !captainTarget) return;
            Transform target = captainTarget ? _npcTransform : _endingApproachMarker;
            if (target == null) return;
            Vector3 screen = _mainCam.WorldToScreenPoint(target.position + Vector3.up * 2f);
            float distance = Vector3.Distance(_player.transform.position, target.position);
            GUIStyle markerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter, richText = true
            };
            float x = screen.z > 0f ? Mathf.Clamp(screen.x, 150f, Screen.width - 150f) : Screen.width * 0.5f;
            float y = screen.z > 0f ? Mathf.Clamp(Screen.height - screen.y, 100f, Screen.height - 100f) : Screen.height - 140f;
            string hint = screen.z > 0f ? (captainTarget ? "◆ КАПИТАН" : "◆ К ОБРЫВУ") : "Повернитесь к метке";
            string action = captainTarget && distance <= 3.5f ? "[E] Поговорить" : "подойдите";
            GUI.Box(new Rect(x - 140f, y - 35f, 280f, 70f),
                $"<color=#FFE18A>{hint}</color>\n<color=#FFFFFF>{distance:0} м — {action}</color>", markerStyle);
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;

            EnsureStyles();
            if (_endingFade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, _endingFade);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }
            DrawEndingApproachMarker();

            if (_isMenuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                DrawDyingLightStyleMenu();
                DrawToastIfActive();
                return;
            }

            if (_cityChapterTitleTimer > 0f)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 250f, 100f, 500f, 75f),
                    "<color=#FFE18A><size=26><b>ГЛАВА 2 — РОГОВО</b></size></color>\n<color=#FFFFFF>Поиски Димарика продолжаются</color>",
                    new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.MiddleCenter });
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

            if (!_isTabletViewerOpen && !_isPlayingEndingCutscene)
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
                "<color=#FFDB6B><b>ПЛАНШЕТ ДИМАРИКА  •  ГАЛЕРЕЯ: ПОСЛЕДНЕЕ ФОТО  •  ОРИЕНТИР: ИДТИ К ВОДЕ (КОРАБЛЬ →)</b></color>",
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

            if (_chapterSelectionOpen && !_isSettingsOpen)
            {
                GUI.Label(new Rect(64f, 170f, 420f, 35f), "ВЫБОР ГЛАВЫ", _chapter2TitleStyle);
                if (DrawMenuButton(new Rect(64f, 220f, 420f, 50f), "ГЛАВА 1 — ОСТРОВ", 0)) _requestedChapter = 1;
                if (DrawMenuButton(new Rect(64f, 282f, 420f, 50f), "ГЛАВА 2 — РОГОВО", 1)) _requestedChapter = 2;
                if (EnablePizzaChapterPreview && DrawMenuButton(new Rect(64f, 344f, 520f, 50f), "ТЕСТ — ПОЯВИТЬСЯ У ПИЦЦЕРИИ", 2)) _requestedChapter = 3;
                GUI.Label(new Rect(64f, 407f, 520f, 55f), "Запуск с начала главы. Текущий прогресс не переносится.", _settingsLabelStyle);
                if (DrawMenuButton(new Rect(64f, 477f, 420f, 46f), "НАЗАД", 3)) _chapterSelectionOpen = false;
                return;
            }
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
                    _requestedChapter = 1;
                    return;
                }
                row++;
                if (DrawMenuButton(new Rect(btnX, btnY + spacing * row, btnW, btnH), "ВЫБОР ГЛАВЫ", row))
                {
                    _chapterSelectionOpen = true;
                    return;
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
                "<color=#E8ECE8><b>ВЕР. 1.6.0</b></color>",
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

            float w = 440f;
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
            float boxW = 680f;
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
                "<color=#FFFFFF><b>У берега найден корабль! Отправление во 2-ю главу: «Городок Димарика, Рогово»</b></color>",
                _subtitleStyle
            );
        }
    }
}
