using UnityEngine;
using UnityEngine.Rendering;
using Dimari.Core;

namespace Dimari.Player
{
    /// <summary>
    /// Кинематографичная камера:
    /// 1) В Главном Меню / Паузе (IsMenuCameraActive = true) — ставит живой 3D-ракурс на главного персонажа справа экрана
    ///    в стиле меню Dying Light: The Beast (Скриншот 2).
    /// 2) В кат-сцене пробуждения (IsCutsceneControlled = true) — управляется режиссёром ForestAwakeningDirector.
    /// 3) В игре — плавный вид от 3-го лица или от 1-го лица (клавиша V).
    /// </summary>
    public class ForestCameraController : MonoBehaviour
    {
        [Header("Цель слежения")]
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 1.52f, 0f);
        public Vector3 firstPersonOffset = new Vector3(0f, 1.62f, 0.08f);

        [Header("Настройки 3-го лица")]
        public float distance = 3.1f;
        public float minDistance = 0.6f;
        public float mouseSensitivity = 2.2f;
        public float minPitch = -35f;
        public float maxPitch = 65f;
        public float positionSmoothTime = 0.06f;
        public LayerMask collisionMask = ~0;

        [Header("Режим камеры (1-е / 3-е лицо)")]
        public bool isFirstPerson = false;

        /// <summary>
        /// Когда true — камерой управляет режиссёр кат-сцены пробуждения (ForestAwakeningDirector).
        /// </summary>
        public bool IsCutsceneControlled { get; set; } = false;

        /// <summary>
        /// Когда true — камера показывает живую 3D кат-сцену с главным героем справа экрана для Главного Меню / Паузы.
        /// </summary>
        public bool IsMenuCameraActive { get; set; } = false;

        private float _yaw;
        private float _pitch = 10f;
        private Vector3 _currentVelocity;
        private float _blendFromCutscene = 1f;
        private Vector3 _cutsceneEndPos;
        private Quaternion _cutsceneEndRot;
        private Renderer[] _characterRenderers;
        private bool _lastShadowsOnlyState = false;
        private Camera _cam;
        private float _defaultFov = 60f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _defaultFov = _cam.fieldOfView;
            }
            mouseSensitivity = PlayerPrefs.GetFloat("Dimari_MouseSens", 2.2f);
            isFirstPerson = PlayerPrefs.GetInt("Dimari_FirstPerson", 0) == 1;
        }

        private void Start()
        {
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _characterRenderers = target.GetComponentsInChildren<Renderer>(true);
            }
            UpdateCharacterMeshVisibility(false);
        }

        public void BeginGameplayTransitionFromCutscene()
        {
            _cutsceneEndPos = transform.position;
            _cutsceneEndRot = transform.rotation;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _pitch = 8f;
                _characterRenderers = target.GetComponentsInChildren<Renderer>(true);
            }
            _blendFromCutscene = 0f;
            IsCutsceneControlled = false;
            IsMenuCameraActive = false;
            if (_cam != null) _cam.fieldOfView = _defaultFov;
            UpdateCharacterMeshVisibility(isFirstPerson);
        }

        public void UpdateCharacterMeshVisibility(bool shadowsOnly)
        {
            if (_characterRenderers == null && target != null)
            {
                _characterRenderers = target.GetComponentsInChildren<Renderer>(true);
            }
            if (_characterRenderers == null) return;

            _lastShadowsOnlyState = shadowsOnly;
            ShadowCastingMode mode = shadowsOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (Renderer r in _characterRenderers)
            {
                if (r != null)
                {
                    r.shadowCastingMode = mode;
                }
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 1. Режим живой 3D кат-сцены Главного Меню / Паузы (персонаж справа экрана, как в Dying Light: The Beast)
            if (IsMenuCameraActive)
            {
                if (_lastShadowsOnlyState)
                {
                    UpdateCharacterMeshVisibility(false);
                }

                if (_cam != null)
                {
                    _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 40f, Time.unscaledDeltaTime * 5f);
                }

                Vector3 charPos = target.position;
                Vector3 charForward = target.forward;

                // Stable live background: trees animate, but the menu camera does not sway.
                charForward.y = 0f;
                if (charForward.sqrMagnitude < 0.001f) charForward = Vector3.forward;
                charForward.Normalize();
                Vector3 menuCamPos = charPos + charForward * 2.65f + Vector3.up * 1.28f;
                Vector3 chest = charPos + Vector3.up * 1.18f;
                Quaternion rotation = Quaternion.LookRotation(chest - menuCamPos, Vector3.up)
                    * Quaternion.Euler(0f, -20f, 0f);
                transform.SetPositionAndRotation(menuCamPos, rotation);
                return;
            }

            if (_cam != null && Mathf.Abs(_cam.fieldOfView - _defaultFov) > 0.1f && !IsCutsceneControlled)
            {
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _defaultFov, Time.deltaTime * 4f);
            }

            // 2. Режим кат-сцены пробуждения
            if (IsCutsceneControlled)
            {
                return;
            }

            bool shouldHideMeshForCamera = isFirstPerson || (_blendFromCutscene < 0.25f && isFirstPerson);
            if (shouldHideMeshForCamera != _lastShadowsOnlyState)
            {
                UpdateCharacterMeshVisibility(shouldHideMeshForCamera);
            }

            if (DimariInput.IsToggleCameraPressed())
            {
                isFirstPerson = !isFirstPerson;
                PlayerPrefs.SetInt("Dimari_FirstPerson", isFirstPerson ? 1 : 0);
                UpdateCharacterMeshVisibility(isFirstPerson);
            }

            Vector2 look = DimariInput.GetLookDelta();
            _yaw += look.x * mouseSensitivity;
            _pitch -= look.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion desiredRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPosition;

            if (isFirstPerson)
            {
                desiredPosition = target.position + target.TransformDirection(firstPersonOffset);
                target.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
            else
            {
                Vector3 pivot = target.position + targetOffset;
                Vector3 dir = desiredRotation * Vector3.back;
                float actualDist = distance;

                if (Physics.SphereCast(pivot, 0.2f, dir, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(target))
                    {
                        actualDist = Mathf.Clamp(hit.distance - 0.15f, minDistance, distance);
                    }
                }

                desiredPosition = pivot + dir * actualDist;
            }

            if (_blendFromCutscene < 1f)
            {
                _blendFromCutscene = Mathf.MoveTowards(_blendFromCutscene, 1f, Time.deltaTime * 1.1f);
                float smoothT = Mathf.SmoothStep(0f, 1f, _blendFromCutscene);
                transform.position = Vector3.Lerp(_cutsceneEndPos, desiredPosition, smoothT);
                transform.rotation = Quaternion.Slerp(_cutsceneEndRot, desiredRotation, smoothT);
            }
            else
            {
                if (isFirstPerson)
                {
                    transform.position = desiredPosition;
                }
                else
                {
                    transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, positionSmoothTime);
                }
                transform.rotation = desiredRotation;
            }
        }
    }
}
