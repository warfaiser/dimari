using System.Collections;
using UnityEngine;
using Dimari.Core;
using Dimari.Player;
using Dimari.Story;
using Dimari.Atmosphere;

namespace Dimari.Cinematics
{
    /// <summary>
    /// Режиссёр кат-сцены пробуждения персонажа и озвучки:
    /// Озвучивает и показывает монолог:
    /// «Ох, моя голова… Как я тут оказался? Помню, что вчера поехали мыть машину с Димариком,
    /// потом — вспышка, и я очутился здесь. Так, нужно осмотреться и найти хоть что-нибудь»
    /// </summary>
    public class ForestAwakeningDirector : MonoBehaviour
    {
        public static ForestAwakeningDirector Instance { get; private set; }

        [Header("Ссылки на игрока и камеру")]
        public ForestPlayerController playerController;
        public ForestCameraController cameraController;
        public Transform playerVisualRoot;

        [Header("Настройки запуска")]
        public bool showMainMenuOnStart = true;

        public bool IsPlayingCutscene => _isPlayingCutscene;

        private float _eyelidClosure = 0.0f;
        private float _groggyVignette = 0.0f;
        private bool _isPlayingCutscene = false;

        [System.NonSerialized] private Texture2D _blackTex;
        [System.NonSerialized] private Texture2D _softEdgeTex;
        [System.NonSerialized] private GUIStyle _skipHintStyle;

        private Vector3 _initialModelLocalPos;
        private Quaternion _initialModelLocalRot;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (playerController == null)
                playerController = FindAnyObjectByType<ForestPlayerController>();

            if (cameraController == null)
                cameraController = FindAnyObjectByType<ForestCameraController>();

            if (playerVisualRoot == null && playerController != null)
                playerVisualRoot = playerController.visualModelRoot;

            if (playerVisualRoot != null)
            {
                _initialModelLocalPos = Vector3.zero;
                _initialModelLocalRot = Quaternion.identity;
            }

            CreateOverlayTextures();

            // Start may run only after the manager re-enables this component for New Game.
            if (_isPlayingCutscene) return;
            if (StoryQuestManager.Instance != null)
            {
                _eyelidClosure = 0f;
                _groggyVignette = 0f;
                _isPlayingCutscene = false;
                StoryQuestManager.Instance.OpenMainMenuOnStartup();
            }
            else if (!showMainMenuOnStart)
            {
                StartAwakeningCutscene();
            }
        }

        private void Update()
        {
            if (_isPlayingCutscene && DimariInput.IsSkipOrContinuePressed())
            {
                StopAllCoroutines();
                if (ProceduralForestAudio.Instance != null)
                {
                    ProceduralForestAudio.Instance.StopVoice();
                }
                FinishAwakeningImmediately();
            }
        }

        public void StartAwakeningCutscene()
        {
            if (playerController == null) playerController = FindAnyObjectByType<ForestPlayerController>();
            if (cameraController == null) cameraController = FindAnyObjectByType<ForestCameraController>();
            if (playerVisualRoot == null && playerController != null) playerVisualRoot = playerController.visualModelRoot;
            CreateOverlayTextures();
            StopAllCoroutines();
            StartCoroutine(AwakeningSequenceRoutine());
        }

        private IEnumerator AwakeningSequenceRoutine()
        {
            _isPlayingCutscene = true;
            playerController.IsControlEnabled = false;
            cameraController.IsMenuCameraActive = false;
            cameraController.IsCutsceneControlled = true;

            if (StoryQuestManager.Instance != null)
            {
                StoryQuestManager.Instance.SetHUDVisible(false);
            }

            Vector3 basePos = playerController.transform.position;
            Quaternion baseRot = playerController.transform.rotation;
            Vector3 forward = baseRot * Vector3.forward;
            Vector3 right = baseRot * Vector3.right;

            // В начале персонаж лежит на траве
            if (playerVisualRoot != null)
            {
                playerVisualRoot.localPosition = new Vector3(0f, 0.06f, 0f);
                playerVisualRoot.localRotation = Quaternion.Euler(-80f, 0f, 10f);
            }

            cameraController.UpdateCharacterMeshVisibility(true);

            Vector3 groundEyePos = basePos + new Vector3(0f, 0.28f, 0f) - forward * 0.35f;
            Quaternion lookUpAtCanopyRot = Quaternion.LookRotation((Vector3.up * 1.35f + forward * 0.55f).normalized, -forward);

            cameraController.transform.position = groundEyePos;
            cameraController.transform.rotation = lookUpAtCanopyRot;

            _eyelidClosure = 1.0f;
            _groggyVignette = 1.0f;

            // Запускаем голосовую озвучку героя!
            if (ProceduralForestAudio.Instance != null)
            {
                ProceduralForestAudio.Instance.PlayWakingBreath();
                ProceduralForestAudio.Instance.PlayAwakeningIntroVoice();
            }

            // Фраза 1 (0.0 - 3.6 сек): «Ох, моя голова… Как я тут оказался?»
            if (StoryQuestManager.Instance != null)
            {
                StoryQuestManager.Instance.ShowMonologue("«Ох, моя голова… Как я тут оказался?»", 3.5f);
            }

            float t = 0f;
            float phase1Duration = 3.6f;
            while (t < phase1Duration)
            {
                t += Time.deltaTime;
                float norm = t / phase1Duration;

                if (norm < 0.30f)
                {
                    _eyelidClosure = 1.0f;
                }
                else if (norm < 0.70f)
                {
                    float openProgress = (norm - 0.30f) / 0.40f;
                    _eyelidClosure = Mathf.Lerp(1.0f, 0.32f, Mathf.SmoothStep(0f, 1f, openProgress));
                }
                else
                {
                    // Короткое зажмуривание от яркого солнца
                    float blink = Mathf.Sin(((norm - 0.70f) / 0.30f) * Mathf.PI);
                    _eyelidClosure = Mathf.Lerp(0.32f, 0.85f, blink);
                }

                float breathPitch = Mathf.Sin(t * 2.2f) * 2.0f;
                cameraController.transform.position = groundEyePos + Vector3.up * (Mathf.Sin(t * 2.2f) * 0.015f);
                cameraController.transform.rotation = lookUpAtCanopyRot * Quaternion.Euler(breathPitch, Mathf.Sin(t * 1.2f) * 3.0f, 0f);

                yield return null;
            }

            // Фраза 2 (3.6 - 9.4 сек): «Помню, что вчера поехали мыть машину с Димариком, потом — вспышка, и я очутился здесь.»
            // В этой фазе показываем кинематографичный ракурс со стороны на самого персонажа, который поднимается с травы!
            if (StoryQuestManager.Instance != null)
            {
                StoryQuestManager.Instance.ShowMonologue(
                    "«Помню, что вчера поехали мыть машину с Димариком, потом — вспышка, и я очутился здесь.»",
                    5.6f
                );
            }

            cameraController.UpdateCharacterMeshVisibility(false);

            t = 0f;
            float phase2Duration = 5.6f;
            Vector3 cinematicCamStart = basePos + forward * 2.2f + right * 1.1f + Vector3.up * 0.65f;
            Vector3 cinematicCamEnd = basePos + forward * 2.4f - right * 0.8f + Vector3.up * 1.35f;

            while (t < phase2Duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / phase2Duration);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                _eyelidClosure = Mathf.Lerp(0.25f, 0.0f, Mathf.Clamp01(norm * 2.5f));
                _groggyVignette = Mathf.Lerp(1.0f, 0.25f, smooth);

                // Персонаж плавно поднимается с земли в стойку Idle
                if (playerVisualRoot != null)
                {
                    playerVisualRoot.localPosition = Vector3.Lerp(new Vector3(0f, 0.06f, 0f), _initialModelLocalPos, smooth);
                    playerVisualRoot.localRotation = Quaternion.Slerp(Quaternion.Euler(-80f, 0f, 10f), _initialModelLocalRot, smooth);
                }

                // Камера плавно облетает поднимающегося героя
                Vector3 camPos = Vector3.Lerp(cinematicCamStart, cinematicCamEnd, smooth);
                Vector3 lookTarget = basePos + Vector3.up * Mathf.Lerp(0.45f, 1.35f, smooth);
                cameraController.transform.position = camPos;
                cameraController.transform.rotation = Quaternion.LookRotation(lookTarget - camPos, Vector3.up);

                yield return null;
            }

            // Фраза 3 (9.4 - 13.2 сек): «Так, нужно осмотреться и найти хоть что-нибудь»
            if (StoryQuestManager.Instance != null)
            {
                StoryQuestManager.Instance.ShowMonologue("«Так, нужно осмотреться и найти хоть что-нибудь.»", 4.0f);
            }

            t = 0f;
            float phase3Duration = 3.2f;
            Vector3 behindCamPos = basePos - forward * 2.8f + Vector3.up * 1.6f;
            Quaternion behindCamRot = Quaternion.LookRotation((basePos + Vector3.up * 1.4f + forward * 4f) - behindCamPos, Vector3.up);

            while (t < phase3Duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / phase3Duration);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                _groggyVignette = Mathf.Lerp(0.25f, 0f, smooth);
                cameraController.transform.position = Vector3.Lerp(cinematicCamEnd, behindCamPos, smooth);
                cameraController.transform.rotation = Quaternion.Slerp(cameraController.transform.rotation, behindCamRot, smooth);

                yield return null;
            }

            FinishAwakeningImmediately();
        }

        public void FinishAwakeningImmediately()
        {
            _isPlayingCutscene = false;
            _eyelidClosure = 0f;
            _groggyVignette = 0f;

            if (playerVisualRoot != null)
            {
                playerVisualRoot.localPosition = _initialModelLocalPos;
                playerVisualRoot.localRotation = _initialModelLocalRot;
            }

            if (cameraController != null)
            {
                cameraController.BeginGameplayTransitionFromCutscene();
            }

            if (playerController != null)
            {
                playerController.IsControlEnabled = true;
            }

            if (StoryQuestManager.Instance != null)
            {
                StoryQuestManager.Instance.SetHUDVisible(true);
                StoryQuestManager.Instance.ShowMonologue(
                    "«Так, нужно осмотреться и найти хоть что-нибудь: сначала поищу свой рюкзак и планшет в траве.»",
                    5.5f
                );
            }
        }

        private void CreateOverlayTextures()
        {
            _blackTex = new Texture2D(1, 1);
            _blackTex.SetPixel(0, 0, Color.black);
            _blackTex.Apply();

            int gradHeight = 64;
            _softEdgeTex = new Texture2D(1, gradHeight);
            _softEdgeTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < gradHeight; y++)
            {
                float a = (float)y / (gradHeight - 1);
                _softEdgeTex.SetPixel(0, y, new Color(0f, 0f, 0f, a * a));
            }
            _softEdgeTex.Apply();
        }

        private void OnGUI()
        {
            if (StoryQuestManager.Instance != null && StoryQuestManager.Instance.IsMenuOpen)
            {
                return;
            }

            if (!_isPlayingCutscene && _eyelidClosure <= 0.001f && _groggyVignette <= 0.001f)
            {
                return;
            }

            // Рисуем веки на слое глубже (depth = 10), чтобы субтитры StoryQuestManager (depth = -100) всегда были ПОВЕРХ век!
            GUI.depth = 10;

            if (_blackTex == null || _softEdgeTex == null)
            {
                CreateOverlayTextures();
            }

            float w = Screen.width;
            float h = Screen.height;

            if (_groggyVignette > 0.01f)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _groggyVignette * 0.35f);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), _blackTex);
                GUI.color = prev;
            }

            if (_eyelidClosure > 0.001f)
            {
                float closedLidHeight = (h * 0.55f) * _eyelidClosure;
                float softFeather = h * 0.18f;

                GUI.DrawTexture(new Rect(0f, 0f, w, closedLidHeight), _blackTex);
                GUI.DrawTextureWithTexCoords(
                    new Rect(0f, closedLidHeight, w, softFeather),
                    _softEdgeTex,
                    new Rect(0f, 0f, 1f, 1f)
                );

                GUI.DrawTexture(new Rect(0f, h - closedLidHeight, w, closedLidHeight), _blackTex);
                GUI.DrawTextureWithTexCoords(
                    new Rect(0f, h - closedLidHeight - softFeather, w, softFeather),
                    _softEdgeTex,
                    new Rect(0f, 1f, 1f, -1f)
                );
            }

            if (_isPlayingCutscene)
            {
                if (_skipHintStyle == null || _skipHintStyle.fontSize != 14)
                {
                    _skipHintStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        richText = true,
                        alignment = TextAnchor.LowerRight,
                        normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
                    };
                }
                GUI.Label(new Rect(w - 340f, h - 36f, 320f, 24f), "<color=#E8ECE8><b>[Пробел] Пропустить кат-сцену</b></color>", _skipHintStyle);
            }
        }
    }
}
