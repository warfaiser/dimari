#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Dimari.Atmosphere;
using Dimari.Cinematics;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.EditorTools
{
    /// <summary>
    /// Автоматический мастер Варианта 1 для Unity 6.6:
    /// 1) 100% графика демо-сцены The Toby Foliage Engine / Light (2.0.0)
    /// 2) Новый персонаж из First Person + Third Person | Character Controllers (2.0.1) (папка Assets/Starter Assets)
    /// 3) Совместимость с анимациями Human Basic Motions FREE (2.4.2) (папка Assets/Kevin Iglesias)
    /// 4) Кат-сцена пробуждения на траве и сюжетные квесты.
    /// </summary>
    [InitializeOnLoad]
    public class DimariForestSetupWindow : EditorWindow
    {
        private const string AutoPopupPrefKey = "Dimari_Variant1_AutoShown_v4";
        private const string TargetDemoSceneCopyPath = "Assets/Dimari/Scenes/ForestAwakening_TTFEL.unity";

        static DimariForestSetupWindow()
        {
            EditorApplication.delayCall += TryAutoOpenWindow;
        }

        private static void TryAutoOpenWindow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!SessionState.GetBool(AutoPopupPrefKey, false))
            {
                SessionState.SetBool(AutoPopupPrefKey, true);
                ShowWindow();
            }
        }

        private bool _hasTobyFoliage;
        private bool _hasStarterCharacter;
        private bool _hasBasicMotions;

        private string _cachedTobyUnityPackagePath;
        private string _cachedStarterCharacterPackagePath;
        private string _cachedBasicMotionsUnityPackagePath;

        private List<string> _tobyScenePaths = new List<string>();
        private List<GameObject> _tobyTrees = new List<GameObject>();
        private List<GameObject> _tobyPlantsAndGrass = new List<GameObject>();
        private List<GameObject> _tobyRocks = new List<GameObject>();
        private GameObject _tobyWindController;
        private Material _tobyGroundMaterial;
        private List<string> _tobyInternalPackages = new List<string>();

        // Новый персонаж из First Person + Third Person | Character Controllers (Assets/Starter Assets)
        private GameObject _starterArmaturePrefab;
        private Avatar _starterAvatar;
        private RuntimeAnimatorController _starterAnimatorController;

        // Запасной персонаж и клипы из Human Basic Motions FREE (Assets/Kevin Iglesias)
        private GameObject _basicMotionsModelPrefab;
        private Avatar _basicMotionsAvatar;
        private AnimationClip _idleClip;
        private AnimationClip _walkClip;
        private AnimationClip _runClip;
        private AnimationClip _jumpClip;

        private Vector2 _scrollPos;
        private int _selectedDemoSceneIndex = 0;
        private int _cameraModeChoice = 0;
        private int _animationSourceChoice = 0;

        [MenuItem("Dimari/🔄 Заменить персонажа на нового (First Person + Third Person) в 1 клик", false, 1)]
        public static void QuickReplaceWithNewCharacter()
        {
            DimariForestSetupWindow window = GetWindow<DimariForestSetupWindow>("Dimari — Новый Персонаж");
            window.minSize = new Vector2(620f, 620f);
            window.ScanProjectAssets();
            window.UpgradeOrBuildWithNewCharacter();
        }

        [MenuItem("Dimari/🌲 Вариант 1: Собрать сцену на базе Демо-леса TTFEL (В 1 клик)", false, 2)]
        public static void QuickBuildFromDemo()
        {
            DimariForestSetupWindow window = GetWindow<DimariForestSetupWindow>("Dimari — Вариант 1 (TTFEL)");
            window.minSize = new Vector2(620f, 620f);
            window.ScanProjectAssets();
            window.BuildFromTobyDemoScene();
        }

        [MenuItem("Dimari/⚙️ Открыть окно мастера настройки леса", false, 3)]
        public static void ShowWindow()
        {
            DimariForestSetupWindow window = GetWindow<DimariForestSetupWindow>("Dimari — Мастер Игры");
            window.minSize = new Vector2(620f, 620f);
            window.ScanProjectAssets();
            window.Show();
        }

        [MenuItem("Dimari/⛰️ Исправить ходьбу по воздуху (Обновить коллайдеры рельефа)", false, 4)]
        public static void QuickFixGroundColliders()
        {
            ForestPlayerController.FixSceneGroundCollidersAndSnap();
            ForestPlayerController player = FindAnyObjectByType<ForestPlayerController>();
            if (player != null)
            {
                ForestPlayerController.SnapTransformToRealGround(player.transform);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "Коллайдеры рельефа исправлены!",
                "Плоский невидимый пол (Word_Collision) отключён, а на все холмы и камни Geology установлены точные MeshCollider.\n\nНажмите ▶ PLAY!",
                "Отлично!"
            );
        }

        [MenuItem("Dimari/🔊 Подключить скачанные звуки природы и шагов (В 1 клик)", false, 5)]
        public static void QuickSetupAudioPacks()
        {
            ImportCachedAudioPackagesIfFound();
            EnsureGeneratedForestWavAssets();

            ProceduralForestAudio audioSys = FindAnyObjectByType<ProceduralForestAudio>();
            if (audioSys == null)
            {
                GameObject sysObj = GameObject.Find("Dimari_GameSystems");
                if (sysObj == null) sysObj = new GameObject("Dimari_GameSystems");
                audioSys = sysObj.AddComponent<ProceduralForestAudio>();
            }

            int foundCount = AutoAssignImportedAudioClips(audioSys);
            EditorUtility.SetDirty(audioSys);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Звуки природы и шагов подключены!",
                $"Найдено и подключено аудиофайлов в проект: {foundCount}\n\n" +
                $"• Фоновый лес/ветер: {audioSys.forestAmbienceLoops.Count}\n" +
                $"• Пение лесных птиц: {audioSys.birdChirpClips.Count}\n" +
                $"• Шаги по траве/земле (ходьба): {audioSys.grassWalkClips.Count}\n" +
                $"• Шаги по траве/земле (бег): {audioSys.grassRunClips.Count}\n" +
                $"• Шаги по камням и скалам: {audioSys.rockWalkClips.Count}\n" +
                $"• Прыжки и приземление: {audioSys.jumpLandClips.Count}\n\n" +
                "Нажмите ▶ PLAY!",
                "Супер!"
            );
        }

        private void OnEnable()
        {
            ScanProjectAssets();
        }

        private void ScanLocalAssetStoreCache()
        {
            _cachedTobyUnityPackagePath = null;
            _cachedStarterCharacterPackagePath = null;
            _cachedBasicMotionsUnityPackagePath = null;

            List<string> candidateCacheDirs = new List<string>();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                candidateCacheDirs.Add(Path.Combine(appData, "Unity", "Asset Store-5.x"));
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                candidateCacheDirs.Add(Path.Combine(localAppData, "Unity", "Asset Store-5.x"));
            }

            foreach (string dir in candidateCacheDirs)
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    string[] packages = Directory.GetFiles(dir, "*.unitypackage", SearchOption.AllDirectories);
                    foreach (string pkg in packages)
                    {
                        string lower = pkg.ToLowerInvariant();
                        if (_cachedTobyUnityPackagePath == null &&
                            (lower.Contains("toby") || lower.Contains("foliage engine")))
                        {
                            _cachedTobyUnityPackagePath = pkg;
                        }

                        if (_cachedStarterCharacterPackagePath == null &&
                            (lower.Contains("first person") || lower.Contains("third person") ||
                             lower.Contains("character controller") || lower.Contains("starter")))
                        {
                            _cachedStarterCharacterPackagePath = pkg;
                        }

                        if (_cachedBasicMotionsUnityPackagePath == null &&
                            (lower.Contains("basic motions") || lower.Contains("kevin iglesias")))
                        {
                            _cachedBasicMotionsUnityPackagePath = pkg;
                        }
                    }
                }
                catch
                {
                    // Игнорируем системные ошибки доступа к папкам
                }
            }
        }

        public void ScanProjectAssets()
        {
            ScanLocalAssetStoreCache();

            _tobyScenePaths.Clear();
            _tobyTrees.Clear();
            _tobyPlantsAndGrass.Clear();
            _tobyRocks.Clear();
            _tobyInternalPackages.Clear();
            _tobyWindController = null;
            _tobyGroundMaterial = null;

            _starterArmaturePrefab = null;
            _starterAvatar = null;
            _starterAnimatorController = null;

            _basicMotionsModelPrefab = null;
            _basicMotionsAvatar = null;
            _idleClip = null;
            _walkClip = null;
            _runClip = null;
            _jumpClip = null;

            // 1. Поиск всех демо-сцен The Toby Foliage Engine / Light
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                if ((lower.Contains("toby") || lower.Contains("ttfe") || lower.Contains("foliage")) &&
                    !lower.Contains("assets/dimari/"))
                {
                    _tobyScenePaths.Add(path);
                }
            }

            // Поиск вложенных .unitypackage
            string[] allGuids = AssetDatabase.FindAssets("");
            foreach (string guid in allGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                {
                    string lower = path.ToLowerInvariant();
                    if (lower.Contains("toby") || lower.Contains("ttfe") || lower.Contains("foliage") ||
                        lower.Contains("urp") || lower.Contains("hdrp") || lower.Contains("built"))
                    {
                        if (!_tobyInternalPackages.Contains(path))
                            _tobyInternalPackages.Add(path);
                    }
                }
            }

            // 2. Поиск контроллера анимаций из Starter Assets (First Person + Third Person)
            string[] controllerGuids = AssetDatabase.FindAssets("t:AnimatorController");
            foreach (string guid in controllerGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                if ((lower.Contains("starter assets") || lower.Contains("starterassets") || lower.Contains("thirdperson")) &&
                    !lower.Contains("assets/dimari/"))
                {
                    _starterAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                    if (_starterAnimatorController != null) break;
                }
            }

            // 3. Поиск префабов (включая PlayerArmature из Starter Assets, манекена из Kevin Iglesias и префабы леса TTFEL)
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                bool isTobyAsset = lower.Contains("toby") || lower.Contains("ttfe") || lower.Contains("foliage engine");
                if (isTobyAsset)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    if (fileName.Contains("controller") || fileName.Contains("wind") || fileName.Contains("global"))
                    {
                        _tobyWindController = prefab;
                    }
                    else if (fileName.Contains("tree") || fileName.Contains("pine") || fileName.Contains("birch") ||
                             fileName.Contains("spruce") || fileName.Contains("fir") || fileName.Contains("oak") ||
                             fileName.Contains("beech") || fileName.Contains("maple") || fileName.Contains("sapling") ||
                             lower.Contains("/trees/"))
                    {
                        _tobyTrees.Add(prefab);
                    }
                    else if (fileName.Contains("rock") || fileName.Contains("stone") || fileName.Contains("boulder") ||
                             fileName.Contains("cliff") || fileName.Contains("log") || fileName.Contains("trunk") ||
                             lower.Contains("/rocks/") || lower.Contains("/stones/"))
                    {
                        _tobyRocks.Add(prefab);
                    }
                    else if (fileName.Contains("grass") || fileName.Contains("fern") || fileName.Contains("bush") ||
                             fileName.Contains("plant") || fileName.Contains("flower") || fileName.Contains("shrub") ||
                             fileName.Contains("weed") || lower.Contains("/grass/") || lower.Contains("/plants/") ||
                             lower.Contains("/bushes/") || lower.Contains("/foliage/"))
                    {
                        _tobyPlantsAndGrass.Add(prefab);
                    }
                }

                // Поиск префаба PlayerArmature в папке Starter Assets (с пробелом или без)
                bool isStarterAsset = lower.Contains("starter assets") || lower.Contains("starterassets") || fileName.Contains("playerarmature");
                if (isStarterAsset && _starterArmaturePrefab == null)
                {
                    GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate != null && candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                    {
                        // Предпочитаем сам PlayerArmature (а не NestedParent, где лежат камеры)
                        if (fileName.Contains("playerarmature") || !fileName.Contains("nested"))
                        {
                            _starterArmaturePrefab = candidate;
                            Animator candAnim = candidate.GetComponentInChildren<Animator>(true);
                            if (candAnim != null)
                            {
                                if (_starterAvatar == null && candAnim.avatar != null)
                                    _starterAvatar = candAnim.avatar;
                                if (_starterAnimatorController == null && candAnim.runtimeAnimatorController != null)
                                    _starterAnimatorController = candAnim.runtimeAnimatorController;
                            }
                        }
                    }
                }

                bool isBasicMotions = lower.Contains("kevin iglesias") || lower.Contains("basic motions") || lower.Contains("humanbasicmotions");
                if (isBasicMotions && _basicMotionsModelPrefab == null)
                {
                    GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate != null && candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                    {
                        _basicMotionsModelPrefab = candidate;
                        Animator candAnim = candidate.GetComponentInChildren<Animator>(true);
                        if (candAnim != null && candAnim.avatar != null && _basicMotionsAvatar == null)
                        {
                            _basicMotionsAvatar = candAnim.avatar;
                        }
                    }
                }
            }

            // 4. Поиск FBX-моделей в Starter Assets и Kevin Iglesias
            string[] modelGuids = AssetDatabase.FindAssets("t:Model");
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                if (lower.Contains("starter assets") || lower.Contains("starterassets") ||
                    lower.Contains("thirdpersoncontroller") || fileName == "armature")
                {
                    GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate != null && candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                    {
                        if (_starterArmaturePrefab == null)
                            _starterArmaturePrefab = candidate;

                        if (_starterAvatar == null)
                        {
                            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                            foreach (UnityEngine.Object sub in subAssets)
                            {
                                if (sub is Avatar av && av.isValid)
                                {
                                    _starterAvatar = av;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (lower.Contains("kevin iglesias") || lower.Contains("basic motions") || lower.Contains("dummy"))
                {
                    GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate != null && candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                    {
                        if (_basicMotionsModelPrefab == null)
                            _basicMotionsModelPrefab = candidate;

                        if (_basicMotionsAvatar == null)
                        {
                            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                            foreach (UnityEngine.Object sub in subAssets)
                            {
                                if (sub is Avatar av && av.isValid)
                                {
                                    _basicMotionsAvatar = av;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // 5. Поиск анимационных клипов
            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip");
            foreach (string guid in clipGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                bool isStarterClip = lower.Contains("starter assets") || lower.Contains("starterassets");
                bool isBasicMotionsClip = lower.Contains("kevin iglesias") || lower.Contains("basic motions") || lower.Contains("humanm@");

                if (!isStarterClip && !isBasicMotionsClip)
                    continue;

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (UnityEngine.Object obj in assets)
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                    {
                        string clipLower = clip.name.ToLowerInvariant();

                        if (_idleClip == null && (clipLower == "idle" || clipLower.Contains("idle01")))
                            _idleClip = clip;

                        if (_walkClip == null && (clipLower == "walk_n" || clipLower.Contains("walk01_forward") || (clipLower.Contains("walk") && clipLower.Contains("forward"))))
                            _walkClip = clip;

                        if (_runClip == null && (clipLower == "run_n" || clipLower.Contains("run01_forward") || clipLower.Contains("sprint01_forward")))
                            _runClip = clip;

                        if (_jumpClip == null && (clipLower.Contains("jumpstart") || clipLower.Contains("inplace_jump") || clipLower.Contains("jump01")))
                            _jumpClip = clip;
                    }
                }
            }

            _hasTobyFoliage = _tobyScenePaths.Count > 0 || _tobyTrees.Count > 0 || _tobyPlantsAndGrass.Count > 0;
            _hasStarterCharacter = _starterArmaturePrefab != null || _starterAnimatorController != null;
            _hasBasicMotions = _basicMotionsModelPrefab != null || _idleClip != null || _walkClip != null;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("🌲 Dimari — Замена на нового персонажа (First Person + Third Person 2.0.1)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Инструмент автоматически подключает нового персонажа из папки «Assets/Starter Assets» " +
                "в вашу лесную сцену (с сохранением пробуждения на траве, звука шагов и переключения вида 1-го / 3-го лица по клавише V).",
                MessageType.Info
            );

            GUILayout.Space(6f);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Шаг 1. Статус ваших пакетов:", EditorStyles.boldLabel);

            DrawStatusRow(
                "1) НОВЫЙ ПЕРСОНАЖ: First Person + Third Person | Character Controllers (2.0.1)",
                _hasStarterCharacter,
                _hasStarterCharacter
                    ? $"Найден в Assets/Starter Assets! Модель: {(_starterArmaturePrefab != null ? _starterArmaturePrefab.name : "Armature")}, " +
                      $"Контроллер: {(_starterAnimatorController != null ? _starterAnimatorController.name : "Подключён")}"
                    : "Нажмите «Обновить статус пакетов» ниже"
            );

            DrawStatusRow(
                "2) The Toby Foliage Engine / Light (2.0.0)",
                _hasTobyFoliage,
                _tobyScenePaths.Count > 0
                    ? $"Импортирован! Демо-сцена: {Path.GetFileName(_tobyScenePaths[0])}"
                    : "Готов к работе"
            );

            DrawStatusRow(
                "3) Human Basic Motions FREE (2.4.2)",
                _hasBasicMotions,
                _hasBasicMotions
                    ? $"Импортирован! Модель: {(_basicMotionsModelPrefab != null ? _basicMotionsModelPrefab.name : "Есть")}, " +
                      $"Анимации: Idle={(_idleClip != null ? "✓" : "—")} Walk={(_walkClip != null ? "✓" : "—")} Run={(_runClip != null ? "✓" : "—")}"
                    : "Готов к работе"
            );

            if (GUILayout.Button("🔄 Обновить статус пакетов", GUILayout.Height(26f)))
            {
                ScanProjectAssets();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Шаг 2. Настройки нового персонажа и камеры:", EditorStyles.boldLabel);

            _cameraModeChoice = EditorGUILayout.Popup(
                "Вид после пробуждения:",
                _cameraModeChoice,
                new[]
                {
                    "От 3-го лица (Видно нового персонажа + клавиша V для 1-го лица)",
                    "От 1-го лица (Глазами персонажа + клавиша V для 3-го лица)"
                }
            );

            _animationSourceChoice = EditorGUILayout.Popup(
                "Набор анимаций героя:",
                _animationSourceChoice,
                new[]
                {
                    "Родной контроллер из First Person + Third Person (StarterAssets)",
                    "Гибридный BlendTree (Human Basic Motions + StarterAssets)"
                }
            );

            GUILayout.Space(8f);

            GUI.backgroundColor = new Color(0.30f, 0.88f, 0.48f);
            if (GUILayout.Button("🔄 ЗАМЕНИТЬ ПЕРСОНАЖА НА НОВОГО (First Person + Third Person) В 1 КЛИК", GUILayout.Height(50f)))
            {
                UpgradeOrBuildWithNewCharacter();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(6f);

            if (GUILayout.Button("🌲 Пересобрать всю сцену с нуля из демо-леса TTFEL с новым персонажем", GUILayout.Height(34f)))
            {
                BuildFromTobyDemoScene();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusRow(string title, bool isReady, string details)
        {
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField((isReady ? "✅ " : "⏳ ") + title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(details, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        public void UpgradeOrBuildWithNewCharacter()
        {
            ScanProjectAssets();

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != TargetDemoSceneCopyPath && File.Exists(TargetDemoSceneCopyPath))
            {
                EditorSceneManager.OpenScene(TargetDemoSceneCopyPath, OpenSceneMode.Single);
            }
            else if (!File.Exists(TargetDemoSceneCopyPath) && _tobyScenePaths.Count > 0)
            {
                BuildFromTobyDemoScene();
                return;
            }

            EnsureGroundCollidersInScene();

            ForestPlayerController existingPlayer = FindAnyObjectByType<ForestPlayerController>();
            if (existingPlayer != null)
            {
                ForestPlayerController.SnapTransformToRealGround(existingPlayer.transform);
                Vector3 currentPos = existingPlayer.transform.position;
                Quaternion currentRot = existingPlayer.transform.rotation;
                Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

                SetupPlayerAndStoryInScene(currentPos, currentRot, cam);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            else
            {
                InjectGameplayIntoActiveScene();
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog(
                "Персонаж успешно заменён!",
                "В сцену установлен новый 3D-персонаж из «First Person + Third Person | Character Controllers»!\n\n" +
                "• Капсула заменена на полноценного 3D-персонажа\n" +
                "• Кат-сцена пробуждения на траве сохранена\n" +
                "• Переключение между 1-м и 3-м лицом — клавиша [V]\n\n" +
                "Нажмите ▶ PLAY вверху Unity!",
                "Отлично!"
            );
        }

        public void BuildFromTobyDemoScene()
        {
            ScanProjectAssets();

            if (_tobyScenePaths.Count == 0)
            {
                BuildProceduralForestScene();
                return;
            }

            EnsureDirectory("Assets/Dimari/Scenes");
            string sourceScenePath = _tobyScenePaths[Mathf.Clamp(_selectedDemoSceneIndex, 0, _tobyScenePaths.Count - 1)];

            if (File.Exists(TargetDemoSceneCopyPath))
            {
                AssetDatabase.DeleteAsset(TargetDemoSceneCopyPath);
            }
            AssetDatabase.CopyAsset(sourceScenePath, TargetDemoSceneCopyPath);
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(TargetDemoSceneCopyPath, OpenSceneMode.Single);

            InjectGameplayIntoActiveScene();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TargetDemoSceneCopyPath);
            AddSceneToBuildSettings(TargetDemoSceneCopyPath);

            EditorUtility.DisplayDialog(
                "Готово! Сцена с новым персонажем собрана",
                $"Сцена «{TargetDemoSceneCopyPath}» успешно обновлена с новым персонажем First Person + Third Person!\n\n" +
                "Нажмите ▶ PLAY вверху Unity.",
                "Запустить!"
            );
        }

        public void InjectGameplayIntoActiveScene()
        {
            ScanProjectAssets();
            EnsureGroundCollidersInScene();

            Camera[] allCameras = FindObjectsByType<Camera>();
            Camera primaryCam = Camera.main;
            if (primaryCam == null && allCameras.Length > 0)
            {
                primaryCam = allCameras.OrderByDescending(c => c.enabled && c.gameObject.activeInHierarchy).First();
            }

            foreach (Camera cam in allCameras)
            {
                if (cam != primaryCam)
                {
                    cam.gameObject.SetActive(false);
                }
            }

            Vector3 spawnPos = new Vector3(0f, 0.5f, 0f);
            Quaternion spawnRot = Quaternion.identity;

            if (primaryCam != null)
            {
                Vector3 camPos = primaryCam.transform.position;
                Vector3 camForward = primaryCam.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude < 0.01f) camForward = Vector3.forward;
                camForward.Normalize();

                spawnRot = Quaternion.LookRotation(camForward, Vector3.up);

                Vector3 probeOrigin = camPos + camForward * 2.0f + Vector3.up * 25f;
                if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, 150f))
                {
                    spawnPos = hit.point;
                }
                else if (Physics.Raycast(camPos + Vector3.up * 25f, Vector3.down, out RaycastHit hit2, 150f))
                {
                    spawnPos = hit2.point;
                }
                else
                {
                    float groundY = EstimateGroundHeightNear(camPos);
                    spawnPos = new Vector3(camPos.x, groundY, camPos.z);
                }

                MonoBehaviour[] camScripts = primaryCam.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour mb in camScripts)
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (!typeName.Contains("UniversalAdditionalCameraData") &&
                        !typeName.Contains("HDAdditionalCameraData") &&
                        !typeName.Contains("PostProcess") &&
                        !(mb is ForestCameraController))
                    {
                        mb.enabled = false;
                    }
                }
            }

            SetupPlayerAndStoryInScene(spawnPos, spawnRot, primaryCam);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void EnsureGroundCollidersInScene()
        {
            ForestPlayerController.FixSceneGroundCollidersAndSnap();
        }

        private float EstimateGroundHeightNear(Vector3 refPos)
        {
            float bestY = refPos.y - 1.7f;
            float bestDist = float.MaxValue;

            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>();
            foreach (MeshRenderer mr in renderers)
            {
                if (mr == null || !mr.gameObject.activeInHierarchy) continue;
                Bounds b = mr.bounds;
                if (b.size.x > 4f && b.size.z > 4f)
                {
                    float d = Vector2.Distance(new Vector2(refPos.x, refPos.z), new Vector2(b.center.x, b.center.z));
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestY = b.min.y;
                    }
                }
            }
            return bestY;
        }

        private void CreateSafetyGroundCollider(Vector3 center)
        {
            if (GameObject.Find("Dimari_SafetyGroundCollider") != null) return;
            GameObject safetyFloor = new GameObject("Dimari_SafetyGroundCollider");
            safetyFloor.transform.position = center;
            BoxCollider bc = safetyFloor.AddComponent<BoxCollider>();
            bc.size = new Vector3(250f, 0.2f, 250f);
            bc.center = new Vector3(0f, -0.1f, 0f);
        }

        private void SetupPlayerAndStoryInScene(Vector3 spawnPos, Quaternion spawnRot, Camera targetCamera = null)
        {
            ForestPlayerController oldPlayer = FindAnyObjectByType<ForestPlayerController>();
            if (oldPlayer != null) DestroyImmediate(oldPlayer.gameObject);

            ForestAwakeningDirector oldDirector = FindAnyObjectByType<ForestAwakeningDirector>();
            if (oldDirector != null) DestroyImmediate(oldDirector.gameObject);

            GameObject oldSystems = GameObject.Find("Dimari_GameSystems");
            if (oldSystems != null) DestroyImmediate(oldSystems);

            GameObject oldStoryRoot = GameObject.Find("Dimari_StoryObjects");
            if (oldStoryRoot != null) DestroyImmediate(oldStoryRoot);

            RuntimeAnimatorController chosenAnimController;
            if (_animationSourceChoice == 0 && _starterAnimatorController != null)
            {
                chosenAnimController = _starterAnimatorController;
            }
            else
            {
                chosenAnimController = CreateOrUpdatePlayerAnimatorController();
            }

            GameObject playerObj = new GameObject("Player_Dimari");
            playerObj.tag = "Player";
            playerObj.transform.SetPositionAndRotation(spawnPos, spawnRot);

            CharacterController cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.93f, 0f);

            ForestPlayerController playerController = playerObj.AddComponent<ForestPlayerController>();

            GameObject chosenPrefab = _starterArmaturePrefab != null ? _starterArmaturePrefab : _basicMotionsModelPrefab;
            Avatar chosenAvatar = _starterAvatar != null ? _starterAvatar : _basicMotionsAvatar;

            GameObject visualRoot;
            Animator animator = null;

            if (chosenPrefab != null)
            {
                visualRoot = (GameObject)PrefabUtility.InstantiatePrefab(chosenPrefab, playerObj.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(visualRoot))
                {
                    PrefabUtility.UnpackPrefabInstance(visualRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                visualRoot.name = _starterArmaturePrefab != null ? "Character_FirstThirdPerson_Armature" : "Character_BasicMotions";
                visualRoot.transform.localPosition = Vector3.zero;
                visualRoot.transform.localRotation = Quaternion.identity;

                CharacterController childCC = visualRoot.GetComponent<CharacterController>();
                if (childCC != null) DestroyImmediate(childCC);

                MonoBehaviour[] childScripts = visualRoot.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour mb in childScripts)
                {
                    if (mb == null) continue;
                    string tName = mb.GetType().Name;
                    if (tName.Contains("ThirdPersonController") ||
                        tName.Contains("FirstPersonController") ||
                        tName.Contains("StarterAssetsInputs") ||
                        tName.Contains("BasicRigidBodyPush") ||
                        tName.Contains("PlayerInput"))
                    {
                        DestroyImmediate(mb);
                    }
                }

                animator = visualRoot.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    animator = visualRoot.AddComponent<Animator>();
                }
                if (animator.avatar == null && chosenAvatar != null)
                {
                    animator.avatar = chosenAvatar;
                }
                animator.runtimeAnimatorController = chosenAnimController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                if (animator.GetComponent<ForestAnimationEventReceiver>() == null)
                {
                    animator.gameObject.AddComponent<ForestAnimationEventReceiver>();
                }
            }
            else
            {
                visualRoot = CreateFallbackCharacterVisual(playerObj.transform);
                animator = visualRoot.AddComponent<Animator>();
                animator.runtimeAnimatorController = chosenAnimController;
            }

            playerController.characterAnimator = animator;
            playerController.visualModelRoot = visualRoot.transform;

            // Настраиваем камеру (распаковываем родительский префаб, если Main Camera находится внутри Game Manage в демо-сцене)
            Camera mainCam = targetCamera != null ? targetCamera : Camera.main;
            if (mainCam == null)
            {
                mainCam = FindAnyObjectByType<Camera>();
            }
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
            else
            {
                if (PrefabUtility.IsPartOfPrefabInstance(mainCam.gameObject))
                {
                    GameObject outermostPrefab = PrefabUtility.GetOutermostPrefabInstanceRoot(mainCam.gameObject);
                    if (outermostPrefab != null)
                    {
                        PrefabUtility.UnpackPrefabInstance(outermostPrefab, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                }
                mainCam.transform.SetParent(null, true);
                mainCam.tag = "MainCamera";
                mainCam.nearClipPlane = 0.08f;
                if (mainCam.GetComponent<AudioListener>() == null)
                {
                    mainCam.gameObject.AddComponent<AudioListener>();
                }
            }

            ForestCameraController camController = mainCam.GetComponent<ForestCameraController>();
            if (camController == null)
            {
                camController = mainCam.gameObject.AddComponent<ForestCameraController>();
            }
            camController.target = playerObj.transform;
            camController.isFirstPerson = (_cameraModeChoice == 1);
            playerController.cameraTransform = mainCam.transform;

            GameObject systemsObj = new GameObject("Dimari_GameSystems");
            ProceduralForestAudio forestAudio = systemsObj.AddComponent<ProceduralForestAudio>();
            AutoAssignImportedAudioClips(forestAudio);
            StoryQuestManager questMgr = systemsObj.AddComponent<StoryQuestManager>();
            questMgr.InitializeDefaultQuests();

            ForestAwakeningDirector director = systemsObj.AddComponent<ForestAwakeningDirector>();
            director.playerController = playerController;
            director.cameraController = camController;
            director.playerVisualRoot = visualRoot.transform;
            director.showMainMenuOnStart = true;

            CreateStoryInteractiveProps(spawnPos, spawnRot);

            Selection.activeGameObject = playerObj;
        }

        public static void ImportCachedAudioPackagesIfFound()
        {
            List<string> candidateCacheDirs = new List<string>();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                candidateCacheDirs.Add(Path.Combine(appData, "Unity", "Asset Store-5.x"));

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
                candidateCacheDirs.Add(Path.Combine(localAppData, "Unity", "Asset Store-5.x"));

            foreach (string dir in candidateCacheDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    string[] packages = Directory.GetFiles(dir, "*.unitypackage", SearchOption.AllDirectories);
                    foreach (string pkg in packages)
                    {
                        string lower = pkg.ToLowerInvariant();
                        bool isAudioPack = lower.Contains("footstep") || lower.Contains("nature") ||
                                           lower.Contains("nox_sound") || lower.Contains("nox sound") ||
                                           lower.Contains("ambient") || lower.Contains("ambiance") ||
                                           lower.Contains("sound") || lower.Contains("audio") ||
                                           lower.Contains("bird") || lower.Contains("forest");
                        if (isAudioPack)
                        {
                            // Пропускаем уже импортированные большие не-аудио пакеты
                            if (lower.Contains("toby") || lower.Contains("foliage") || lower.Contains("starter") || lower.Contains("basic motions"))
                                continue;

                            AssetDatabase.ImportPackage(pkg, false);
                        }
                    }
                }
                catch
                {
                    // Игнорируем системные ошибки доступа
                }
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Создаёт настоящие .wav файлы в папке Assets/Dimari/Audio (лес, ветер, птицы, шаги по траве, бег, шаги по камню),
        /// чтобы все звуковые слоты гарантированно работали как полноценные аудио-ассеты.
        /// </summary>
        public static void EnsureGeneratedForestWavAssets()
        {
            EnsureDirectory("Assets/Dimari/Audio");
            bool createdAny = false;

            string windPath = "Assets/Dimari/Audio/Ambiance_Forest_Morning_Wind.wav";
            if (!File.Exists(windPath))
            {
                WriteWavFile(windPath, GenerateWindSamples(22050, 10.0f), 22050);
                createdAny = true;
            }

            for (int i = 1; i <= 3; i++)
            {
                string birdPath = $"Assets/Dimari/Audio/Nature_Forest_Bird_0{i}.wav";
                if (!File.Exists(birdPath))
                {
                    WriteWavFile(birdPath, GenerateBirdSamples(22050, 1.3f, 2100f + i * 280f, 2850f + i * 240f), 22050);
                    createdAny = true;
                }
            }

            for (int i = 1; i <= 4; i++)
            {
                string walkGrassPath = $"Assets/Dimari/Audio/Footsteps_Grass_Walk_0{i}.wav";
                if (!File.Exists(walkGrassPath))
                {
                    WriteWavFile(walkGrassPath, GenerateFootstepSamples(22050, 0.24f, 100 + i * 73, false, false), 22050);
                    createdAny = true;
                }

                string runGrassPath = $"Assets/Dimari/Audio/Footsteps_Grass_Run_0{i}.wav";
                if (!File.Exists(runGrassPath))
                {
                    WriteWavFile(runGrassPath, GenerateFootstepSamples(22050, 0.20f, 500 + i * 91, true, false), 22050);
                    createdAny = true;
                }

                string rockPath = $"Assets/Dimari/Audio/Footsteps_Rock_Walk_0{i}.wav";
                if (!File.Exists(rockPath))
                {
                    WriteWavFile(rockPath, GenerateFootstepSamples(22050, 0.18f, 900 + i * 57, false, true), 22050);
                    createdAny = true;
                }
            }

            if (createdAny)
            {
                AssetDatabase.Refresh();
            }
        }

        private static float[] GenerateWindSamples(int sampleRate, float duration)
        {
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[count];
            System.Random rng = new System.Random(101);
            float lp1 = 0f;
            float lp2 = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float gust = 0.55f + 0.35f * Mathf.Sin(t * Mathf.PI * 2f / duration)
                                   + 0.20f * Mathf.Sin(t * Mathf.PI * 6f / duration);
                lp1 += 0.032f * (white - lp1);
                lp2 += 0.012f * (lp1 - lp2);
                float rustle = (lp1 - lp2) * 1.7f;
                data[i] = Mathf.Clamp((lp2 * 0.75f + rustle * 0.6f) * gust * 0.45f, -0.95f, 0.95f);
            }
            return data;
        }

        private static float[] GenerateBirdSamples(int sampleRate, float duration, float fStart, float fEnd)
        {
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float norm = (float)i / count;
                float env = (Mathf.Exp(-Mathf.Pow((norm - 0.25f) * 8f, 2f)) +
                             0.85f * Mathf.Exp(-Mathf.Pow((norm - 0.65f) * 7f, 2f))) * 0.32f;
                float vibrato = Mathf.Sin(t * 28f * Mathf.PI * 2f) * 85f;
                float freq = Mathf.Lerp(fStart, fEnd, (norm * 2.4f) % 1f) + vibrato;
                phase += freq / sampleRate;
                data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * env;
            }
            return data;
        }

        private static float[] GenerateFootstepSamples(int sampleRate, float duration, int seed, bool isRun, bool isRock)
        {
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[count];
            System.Random rng = new System.Random(seed);
            float filter = 0f;
            for (int i = 0; i < count; i++)
            {
                float norm = (float)i / count;
                float decay = isRock ? 6.5f : (isRun ? 4.5f : 3.4f);
                float env = Mathf.Sin(norm * Mathf.PI) * Mathf.Exp(-norm * decay);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                filter += (isRock ? 0.35f : 0.16f) * (noise - filter);
                float thudFreq = isRock ? 150f : (isRun ? 110f : 88f);
                float thud = Mathf.Sin(norm * thudFreq * Mathf.PI * 2f * duration) * Mathf.Exp(-norm * 15f) * 0.45f;
                float texture = (noise * 0.45f + filter * 0.55f) * env;
                data[i] = Mathf.Clamp((thud + texture) * 0.78f, -0.95f, 0.95f);
            }
            return data;
        }

        private static void WriteWavFile(string filePath, float[] samples, int sampleRate)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                int byteRate = sampleRate * 2;
                int dataChunkSize = samples.Length * 2;

                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataChunkSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write((short)1); // Mono
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataChunkSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    short pcm = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                    bw.Write(pcm);
                }
            }
        }

        public static int AutoAssignImportedAudioClips(ProceduralForestAudio audioSys)
        {
            if (audioSys == null) return 0;

            EnsureGeneratedForestWavAssets();

            audioSys.forestAmbienceLoops.Clear();
            audioSys.birdChirpClips.Clear();
            audioSys.grassWalkClips.Clear();
            audioSys.grassRunClips.Clear();
            audioSys.rockWalkClips.Clear();
            audioSys.jumpLandClips.Clear();

            int totalAssigned = 0;
            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip");

            // Сначала собираем внешние ассеты из Asset Store (Nox_Sound, Starter Assets и др.), а затем резервные из Assets/Dimari/Audio
            List<string> orderedPaths = audioGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.ToLowerInvariant().Contains("assets/dimari/audio") ? 1 : 0)
                .ToList();

            foreach (string path in orderedPaths)
            {
                string lower = path.ToLowerInvariant();
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                // 1. Фоновые звуки природы, леса и ветра
                if ((lower.Contains("ambiance") || lower.Contains("ambience") || lower.Contains("nature") ||
                     fileName.Contains("forest") || fileName.Contains("wind") || fileName.Contains("river") || fileName.Contains("breeze")) &&
                    clip.length > 3.5f)
                {
                    audioSys.forestAmbienceLoops.Add(clip);
                    totalAssigned++;
                    continue;
                }

                // 2. Птицы
                if (fileName.Contains("bird") || fileName.Contains("chirp") || fileName.Contains("sparrow") || fileName.Contains("crow"))
                {
                    audioSys.birdChirpClips.Add(clip);
                    totalAssigned++;
                    continue;
                }

                // 3. Прыжки и приземления
                if (fileName.Contains("jump") || fileName.Contains("land"))
                {
                    audioSys.jumpLandClips.Add(clip);
                    totalAssigned++;
                    continue;
                }

                // 4. Шаги по камню и скалам
                if ((lower.Contains("rock") || lower.Contains("stone") || lower.Contains("gravel")) &&
                    (lower.Contains("footstep") || lower.Contains("step") || lower.Contains("walk") || lower.Contains("run")))
                {
                    audioSys.rockWalkClips.Add(clip);
                    totalAssigned++;
                    continue;
                }

                // 5. Шаги по траве / лесной земле / универсальные шаги (включая Footstep01..04 из Starter Assets)
                if (lower.Contains("footstep") || fileName.Contains("step") || fileName.Contains("walk") || fileName.Contains("run"))
                {
                    if (fileName.Contains("run") || fileName.Contains("sprint"))
                    {
                        audioSys.grassRunClips.Add(clip);
                    }
                    else
                    {
                        audioSys.grassWalkClips.Add(clip);
                    }
                    totalAssigned++;
                }
            }

            return totalAssigned;
        }

        public void BuildProceduralForestScene()
        {
            ScanProjectAssets();
            EnsureDirectory("Assets/Dimari/Scenes");

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string targetScenePath = "Assets/Dimari/Scenes/ForestAwakening_Generated.unity";

            SetupAtmosphericForestLighting();

            if (_tobyWindController != null)
            {
                GameObject windObj = (GameObject)PrefabUtility.InstantiatePrefab(_tobyWindController);
                windObj.name = "(TTFE) Wind Controller";
            }

            GameObject environmentRoot = new GameObject("Forest_Environment_TTFEL");
            CreateForestTerrainMesh(environmentRoot.transform);
            ScatterForestFoliage(environmentRoot.transform, 160, 350, 45);

            SetupPlayerAndStoryInScene(new Vector3(0f, 0.05f, 0f), Quaternion.identity);

            EditorSceneManager.SaveScene(newScene, targetScenePath);
            AddSceneToBuildSettings(targetScenePath);
        }

        private AnimatorController CreateOrUpdatePlayerAnimatorController()
        {
            EnsureDirectory("Assets/Dimari/Animations");
            string controllerPath = "Assets/Dimari/Animations/ForestPlayerAnimator.controller";

            if (File.Exists(controllerPath))
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

            EnsureLoopingClip(_idleClip);
            EnsureLoopingClip(_walkClip);
            EnsureLoopingClip(_runClip);

            BlendTree blendTree;
            AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out blendTree, 0);
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";

            if (_idleClip != null)
                blendTree.AddChild(_idleClip, 0.0f);
            if (_walkClip != null)
                blendTree.AddChild(_walkClip, 0.5f);
            if (_runClip != null)
                blendTree.AddChild(_runClip, 1.0f);
            else if (_walkClip != null)
                blendTree.AddChild(_walkClip, 1.0f);

            rootStateMachine.defaultState = locomotionState;

            if (_jumpClip != null)
            {
                AnimatorState jumpState = rootStateMachine.AddState("Jump");
                jumpState.motion = _jumpClip;

                AnimatorStateTransition toJump = locomotionState.AddTransition(jumpState);
                toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
                toJump.duration = 0.12f;
                toJump.hasExitTime = false;

                AnimatorStateTransition backToLocomotion = jumpState.AddTransition(locomotionState);
                backToLocomotion.hasExitTime = true;
                backToLocomotion.exitTime = 0.85f;
                backToLocomotion.duration = 0.2f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private void EnsureLoopingClip(AnimationClip clip)
        {
            if (clip == null) return;
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path)) return;

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
                if (clipAnimations == null || clipAnimations.Length == 0)
                {
                    clipAnimations = importer.defaultClipAnimations;
                }
                bool changed = false;
                for (int i = 0; i < clipAnimations.Length; i++)
                {
                    if (!clipAnimations[i].loopTime)
                    {
                        clipAnimations[i].loopTime = true;
                        changed = true;
                    }
                }
                if (changed)
                {
                    importer.clipAnimations = clipAnimations;
                    importer.SaveAndReimport();
                }
            }
        }

        private void CreateStoryInteractiveProps(Vector3 playerPos, Quaternion playerRot)
        {
            StoryQuestManager questMgr = FindAnyObjectByType<StoryQuestManager>();
            if (questMgr != null)
            {
                questMgr.RemoveStumpAndSpawnUpdatedStoryProps();
            }
        }

        private Vector3 SnapToGround(Vector3 pos, float fallbackY = 0f)
        {
            if (Physics.Raycast(pos + Vector3.up * 25f, Vector3.down, out RaycastHit hit, 80f))
            {
                return hit.point;
            }
            return new Vector3(pos.x, fallbackY, pos.z);
        }

        private void SetupAtmosphericForestLighting()
        {
            GameObject sunObj = new GameObject("Directional Light (Morning Sun)");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.91f, 0.76f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sunObj.transform.rotation = Quaternion.Euler(28f, -42f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.58f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.46f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.22f, 0.15f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.66f, 0.75f, 0.68f);
            RenderSettings.fogDensity = 0.012f;
        }

        private GameObject CreateForestTerrainMesh(Transform parent)
        {
            GameObject ground = new GameObject("ForestGround_Mesh");
            ground.transform.SetParent(parent);
            ground.transform.position = Vector3.zero;

            MeshFilter mf = ground.AddComponent<MeshFilter>();
            MeshRenderer mr = ground.AddComponent<MeshRenderer>();
            MeshCollider mc = ground.AddComponent<MeshCollider>();

            int resolution = 80;
            float size = 180f;
            Mesh mesh = new Mesh { name = "ForestClearingTerrain" };

            Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[resolution * resolution * 6];

            for (int z = 0, i = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++, i++)
                {
                    float wx = ((float)x / resolution - 0.5f) * size;
                    float wz = ((float)z / resolution - 0.5f) * size;

                    float distFromCenter = Mathf.Sqrt(wx * wx + wz * wz);
                    float hillFactor = Mathf.Clamp01((distFromCenter - 10f) / 35f);
                    float y = (Mathf.PerlinNoise(wx * 0.035f + 10f, wz * 0.035f + 10f) - 0.4f) * 6.5f * hillFactor
                            + (Mathf.PerlinNoise(wx * 0.12f, wz * 0.12f) - 0.5f) * 0.45f * hillFactor;

                    vertices[i] = new Vector3(wx, y, wz);
                    uvs[i] = new Vector2((float)x / resolution * 30f, (float)z / resolution * 30f);
                }
            }

            int tris = 0;
            int vert = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    triangles[tris + 0] = vert + 0;
                    triangles[tris + 1] = vert + resolution + 1;
                    triangles[tris + 2] = vert + 1;
                    triangles[tris + 3] = vert + 1;
                    triangles[tris + 4] = vert + resolution + 1;
                    triangles[tris + 5] = vert + resolution + 2;
                    vert++;
                    tris += 6;
                }
                vert++;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EnsureDirectory("Assets/Dimari/Generated");
            string meshPath = "Assets/Dimari/Generated/ForestClearingMesh.asset";
            if (File.Exists(meshPath)) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            mf.sharedMesh = mesh;
            mc.sharedMesh = mesh;

            if (_tobyGroundMaterial != null)
            {
                mr.sharedMaterial = _tobyGroundMaterial;
            }
            else
            {
                mr.sharedMaterial = CreateColoredMaterial("ForestMossGround", new Color(0.22f, 0.31f, 0.17f));
            }

            return ground;
        }

        private void ScatterForestFoliage(Transform parent, int treeCount, int plantCount, int rockCount)
        {
            System.Random rng = new System.Random(2026);

            GameObject treesRoot = new GameObject("Trees");
            treesRoot.transform.SetParent(parent);

            GameObject plantsRoot = new GameObject("Ferns_And_Grass");
            plantsRoot.transform.SetParent(parent);

            GameObject rocksRoot = new GameObject("Rocks_And_Logs");
            rocksRoot.transform.SetParent(parent);

            for (int i = 0; i < treeCount; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(7.5f, 75f, Mathf.Pow((float)rng.NextDouble(), 0.7f));
                Vector3 candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Mathf.Abs(candidate.x) < 2.6f && candidate.z > 0f && candidate.z < 45f) continue;

                Vector3 groundPos = SnapToGround(candidate);
                Quaternion rot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                float scale = Mathf.Lerp(0.82f, 1.32f, (float)rng.NextDouble());

                if (_tobyTrees.Count > 0)
                {
                    GameObject prefab = _tobyTrees[rng.Next(_tobyTrees.Count)];
                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, treesRoot.transform);
                    inst.transform.SetPositionAndRotation(groundPos, rot);
                    inst.transform.localScale = Vector3.one * scale;
                }
                else
                {
                    CreateFallbackPineTree(treesRoot.transform, groundPos, scale);
                }
            }

            for (int i = 0; i < plantCount; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(0.8f, 48f, Mathf.Pow((float)rng.NextDouble(), 0.85f));
                Vector3 candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 groundPos = SnapToGround(candidate);
                Quaternion rot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                float scale = Mathf.Lerp(0.8f, 1.35f, (float)rng.NextDouble());

                if (_tobyPlantsAndGrass.Count > 0)
                {
                    GameObject prefab = _tobyPlantsAndGrass[rng.Next(_tobyPlantsAndGrass.Count)];
                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, plantsRoot.transform);
                    inst.transform.SetPositionAndRotation(groundPos, rot);
                    inst.transform.localScale = Vector3.one * scale;
                }
            }

            for (int i = 0; i < rockCount; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(4.5f, 55f, (float)rng.NextDouble());
                Vector3 candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Mathf.Abs(candidate.x) < 2.5f && candidate.z > 0f && candidate.z < 35f) continue;

                Vector3 groundPos = SnapToGround(candidate);
                Quaternion rot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                float scale = Mathf.Lerp(0.7f, 1.45f, (float)rng.NextDouble());

                if (_tobyRocks.Count > 0)
                {
                    GameObject prefab = _tobyRocks[rng.Next(_tobyRocks.Count)];
                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rocksRoot.transform);
                    inst.transform.SetPositionAndRotation(groundPos, rot);
                    inst.transform.localScale = Vector3.one * scale;
                }
            }
        }

        private GameObject CreateFallbackCharacterVisual(Transform parent)
        {
            GameObject root = new GameObject("CharacterVisual_Fallback");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            Material bodyMat = CreateColoredMaterial("PlayerJacketMat", new Color(0.24f, 0.36f, 0.45f));

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.42f);
            DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            return root;
        }

        private void CreateFallbackPineTree(Transform parent, Vector3 pos, float scale)
        {
            GameObject tree = new GameObject("FallbackTree");
            tree.transform.SetParent(parent);
            tree.transform.position = pos;
            tree.transform.localScale = Vector3.one * scale;

            Material trunkMat = CreateColoredMaterial("FallbackTrunkMat", new Color(0.32f, 0.22f, 0.14f));
            Material foliageMat = CreateColoredMaterial("FallbackFoliageMat", new Color(0.16f, 0.34f, 0.18f));

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            trunk.transform.localScale = new Vector3(0.38f, 2.2f, 0.38f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(tree.transform);
            crown.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            crown.transform.localScale = new Vector3(2.8f, 4.2f, 2.8f);
            DestroyImmediate(crown.GetComponent<Collider>());
            crown.GetComponent<Renderer>().sharedMaterial = foliageMat;
        }

        private Material CreateColoredMaterial(string matName, Color color)
        {
            EnsureDirectory("Assets/Dimari/Materials");
            string path = $"Assets/Dimari/Materials/{matName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureDirectory(string assetFolderPath)
        {
            if (!Directory.Exists(assetFolderPath))
            {
                Directory.CreateDirectory(assetFolderPath);
                AssetDatabase.Refresh();
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Any(s => s.path == scenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
#endif
