using UnityEngine;
using Dimari.Core;
using Dimari.Atmosphere;
using Dimari.Story;

namespace Dimari.Player
{
    /// <summary>
    /// Контроллер персонажа для сюжетной 3D-игры в лесу (Unity 6.6).
    /// Использует точные параметры таймингов и скоростей официального Unity ThirdPersonController
    /// (Walk = 2.0, Sprint = 5.335, FallTimeout = 0.25с), чтобы анимация ходьбы и бега была идеально ровной и плавной.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ForestPlayerController : MonoBehaviour
    {
        [Header("Скорость передвижения (соответствует Starter Assets)")]
        public float walkSpeed = 2.0f;
        public float runSpeed = 5.335f;
        public float rotationSmoothTime = 0.12f;
        public float speedChangeRate = 10.0f;

        [Header("Прыжок и гравитация")]
        public float jumpHeight = 1.2f;
        public float gravity = -15.0f;
        public float jumpTimeout = 0.50f;
        public float fallTimeout = 0.25f;
        public float groundedOffset = -0.14f;
        public float groundedRadius = 0.28f;
        public LayerMask groundLayers = ~0;

        [Header("Ссылки")]
        public Transform cameraTransform;
        public Animator characterAnimator;
        public Transform visualModelRoot;

        public bool IsControlEnabled { get; set; } = true;
        public bool IsGrounded { get; private set; } = true;
        public float CurrentNormalizedSpeed { get; private set; }

        private CharacterController _controller;
        private float _speed;
        private float _animationBlend;
        private float _targetRotation;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;
        private float _footstepDistance;
        private bool _isStandingOnRock;

        private static readonly int AnimIDSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimIDGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AnimIDGroundedStarter = Animator.StringToHash("Grounded");
        private static readonly int AnimIDJump = Animator.StringToHash("Jump");
        private static readonly int AnimIDFreeFall = Animator.StringToHash("FreeFall");
        private static readonly int AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");

        private void Awake()
        {
            // 1. Исправляем коллайдеры демо-сцены TTFEL (убираем плоский Word_Collision и ставим точные коллайдеры на Geology)
            FixSceneGroundCollidersAndSnap();

            _controller = GetComponent<CharacterController>();
            // Официальные размеры капсулы из префаба PlayerArmature (Starter Assets)
            _controller.height = 1.8f;
            _controller.radius = 0.28f;
            _controller.skinWidth = 0.04f;
            _controller.stepOffset = 0.35f;
            _controller.slopeLimit = 50f;
            _controller.center = new Vector3(0f, 0.93f, 0f);

            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }
            if (visualModelRoot == null && characterAnimator != null)
            {
                visualModelRoot = characterAnimator.transform;
            }
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (characterAnimator != null)
            {
                // Отключаем Root Motion, чтобы анимация не уводила персонажа вбок при ходьбе
                characterAnimator.applyRootMotion = false;

                if (characterAnimator.gameObject != gameObject &&
                    characterAnimator.GetComponent<ForestAnimationEventReceiver>() == null)
                {
                    characterAnimator.gameObject.AddComponent<ForestAnimationEventReceiver>();
                }
            }

            _jumpTimeoutDelta = jumpTimeout;
            _fallTimeoutDelta = fallTimeout;

            SnapTransformToRealGround(transform);
            SnapStoryObjectsToRealGround();
        }

        public static void FixSceneGroundCollidersAndSnap()
        {
            GameObject[] allObjects = FindObjectsByType<GameObject>();
            foreach (GameObject go in allObjects)
            {
                if (go == null || !go.activeInHierarchy) continue;
                string lowerName = go.name.ToLowerInvariant();

                if (lowerName == "word_collision" || lowerName == "world_collision" ||
                    lowerName == "dimari_safetygroundcollider" || lowerName.Contains("collision_box"))
                {
                    go.SetActive(false);
                    continue;
                }

                Collider col = go.GetComponent<Collider>();
                if (col != null &&
                    go.GetComponent<MeshRenderer>() == null &&
                    go.GetComponent<SkinnedMeshRenderer>() == null &&
                    go.GetComponent<Terrain>() == null &&
                    go.GetComponent<CharacterController>() == null &&
                    go.GetComponent<InteractableStoryObject>() == null &&
                    !go.CompareTag("Player"))
                {
                    col.enabled = false;
                }
            }

            Terrain[] terrains = FindObjectsByType<Terrain>();
            foreach (Terrain t in terrains)
            {
                if (t.GetComponent<TerrainCollider>() == null && t.terrainData != null)
                {
                    TerrainCollider tc = t.gameObject.AddComponent<TerrainCollider>();
                    tc.terrainData = t.terrainData;
                }
            }

            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>();
            foreach (MeshRenderer mr in renderers)
            {
                if (mr == null || !mr.gameObject.activeInHierarchy) continue;

                if (mr.transform.root.CompareTag("Player") || mr.GetComponentInParent<InteractableStoryObject>() != null)
                    continue;

                string pathLower = GetHierarchyPathLower(mr.transform);
                string nameLower = mr.gameObject.name.ToLowerInvariant();

                bool isSmallVegetationOrFx =
                    nameLower.Contains("grass") || nameLower.Contains("fern") || nameLower.Contains("flower") ||
                    nameLower.Contains("bush") || nameLower.Contains("shrub") || nameLower.Contains("plant") ||
                    nameLower.Contains("weed") || nameLower.Contains("leaf") || nameLower.Contains("leaves") ||
                    nameLower.Contains("branch") || nameLower.Contains("sapling") || nameLower.Contains("ray") ||
                    nameLower.Contains("beam") || nameLower.Contains("shaft") || nameLower.Contains("fog") ||
                    nameLower.Contains("dust") || nameLower.Contains("sky") || nameLower.Contains("cloud") ||
                    nameLower.Contains("billboard") || nameLower.Contains("wind");

                if (isSmallVegetationOrFx)
                {
                    Collider[] vegCols = mr.GetComponents<Collider>();
                    foreach (Collider c in vegCols)
                    {
                        c.enabled = false;
                    }
                    continue;
                }

                bool isInGeology = pathLower.Contains("geology") || pathLower.Contains("terrain") ||
                                   pathLower.Contains("ground") || pathLower.Contains("landscape") ||
                                   pathLower.Contains("environment");

                bool isGroundOrRockName =
                    nameLower.Contains("ground") || nameLower.Contains("terrain") || nameLower.Contains("floor") ||
                    nameLower.Contains("landscape") || nameLower.Contains("earth") || nameLower.Contains("soil") ||
                    nameLower.Contains("moss") || nameLower.Contains("cliff") || nameLower.Contains("rock") ||
                    nameLower.Contains("stone") || nameLower.Contains("boulder") || nameLower.Contains("hill") ||
                    nameLower.Contains("slope") || nameLower.Contains("path") || nameLower.Contains("road") ||
                    nameLower.Contains("mountain") || nameLower.Contains("valley") || nameLower.Contains("bank");

                Bounds b = mr.bounds;
                bool isWideGroundMesh = b.size.x > 3.5f && b.size.z > 3.5f && !pathLower.Contains("vegetation");

                if (isInGeology || isGroundOrRockName || isWideGroundMesh)
                {
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    Collider[] existingCols = mr.GetComponents<Collider>();
                    MeshCollider meshCol = null;
                    foreach (Collider c in existingCols)
                    {
                        if (c is MeshCollider mc)
                        {
                            meshCol = mc;
                        }
                        else
                        {
                            c.enabled = false;
                        }
                    }

                    if (meshCol == null)
                    {
                        meshCol = mr.gameObject.AddComponent<MeshCollider>();
                    }

                    meshCol.sharedMesh = mf.sharedMesh;
                    meshCol.convex = false;
                    meshCol.enabled = true;
                }
            }

            Physics.SyncTransforms();
        }

        private static string GetHierarchyPathLower(Transform t)
        {
            string path = t.name;
            Transform p = t.parent;
            while (p != null)
            {
                path = p.name + "/" + path;
                p = p.parent;
            }
            return path.ToLowerInvariant();
        }

        public static void SnapTransformToRealGround(Transform target)
        {
            if (target == null) return;

            CharacterController cc = target.GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            Vector3 origin = target.position + Vector3.up * 15f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);

            float bestY = float.MinValue;
            bool found = false;

            foreach (RaycastHit h in hits)
            {
                if (h.transform.IsChildOf(target)) continue;
                if (h.transform.GetComponentInParent<InteractableStoryObject>() != null) continue;

                if (!found || h.point.y > bestY)
                {
                    bestY = h.point.y;
                    found = true;
                }
            }

            if (found)
            {
                target.position = new Vector3(target.position.x, bestY, target.position.z);
            }

            if (cc != null) cc.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        private static void SnapStoryObjectsToRealGround()
        {
            InteractableStoryObject[] props = FindObjectsByType<InteractableStoryObject>();
            foreach (InteractableStoryObject prop in props)
            {
                if (prop != null)
                {
                    SnapTransformToRealGround(prop.transform);
                }
            }
        }

        private void Update()
        {
            JumpAndGravity();
            GroundedCheck();

            if (IsControlEnabled)
            {
                // Гарантируем, что после окончания кат-сцены пробуждения визуальная модель стоит идеально ровно
                if (visualModelRoot != null && visualModelRoot != transform)
                {
                    visualModelRoot.localPosition = Vector3.zero;
                    visualModelRoot.localRotation = Quaternion.identity;
                }
                MoveCharacter();
            }
            else
            {
                _animationBlend = Mathf.Lerp(_animationBlend, 0f, Time.deltaTime * speedChangeRate);
                CurrentNormalizedSpeed = 0f;
                UpdateLocomotionAnimator(0f, 0f);
                _controller.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
            }
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(
                transform.position.x,
                transform.position.y - groundedOffset,
                transform.position.z
            );

            bool sphereHit = false;
            Collider[] overlaps = Physics.OverlapSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider col in overlaps)
            {
                if (!col.transform.IsChildOf(transform))
                {
                    sphereHit = true;
                    string colName = col.gameObject.name.ToLowerInvariant();
                    _isStandingOnRock = colName.Contains("rock") || colName.Contains("cliff") ||
                                        colName.Contains("stone") || colName.Contains("boulder");
                    break;
                }
            }

            IsGrounded = sphereHit || _controller.isGrounded;

            if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
            {
                if (HasParameter(characterAnimator, AnimIDGrounded))
                    characterAnimator.SetBool(AnimIDGrounded, IsGrounded);

                if (HasParameter(characterAnimator, AnimIDGroundedStarter))
                    characterAnimator.SetBool(AnimIDGroundedStarter, IsGrounded);
            }
        }

        private void JumpAndGravity()
        {
            if (IsGrounded)
            {
                // Сбрасываем таймер падения — благодаря этому на кочках и склонах не дёргается анимация падения!
                _fallTimeoutDelta = fallTimeout;

                if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
                {
                    SetJumpBoolIfPresent(false);
                    if (HasParameter(characterAnimator, AnimIDFreeFall))
                        characterAnimator.SetBool(AnimIDFreeFall, false);
                }

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (IsControlEnabled && DimariInput.IsJumpPressed() && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    TriggerOrSetJump(true);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = jumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // Включаем анимацию падения только если реально летим в воздухе дольше 0.25 сек
                    if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null &&
                        HasParameter(characterAnimator, AnimIDFreeFall))
                    {
                        characterAnimator.SetBool(AnimIDFreeFall, true);
                    }
                }
            }

            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private void MoveCharacter()
        {
            Vector2 moveInput = DimariInput.GetMoveInput();
            bool isSprinting = DimariInput.IsSprintHeld();

            float targetSpeed = isSprinting ? runSpeed : walkSpeed;
            if (moveInput == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * speedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            CurrentNormalizedSpeed = Mathf.Clamp01(_animationBlend / runSpeed);

            Vector3 inputDirection = new Vector3(moveInput.x, 0.0f, moveInput.y).normalized;

            if (moveInput != Vector2.zero)
            {
                float camYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + camYaw;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // При спуске по склону прижимаем движение к поверхности без второго вызова Move
            float verticalStep = _verticalVelocity * Time.deltaTime;
            if (IsGrounded && _verticalVelocity <= 0f && moveInput != Vector2.zero)
            {
                verticalStep = -4.5f * Time.deltaTime;
            }

            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, verticalStep, 0.0f));

            // Резервный таймер шагов (если в контроллере анимаций нет встроенных AnimationEvents)
            if (IsGrounded && _speed > 0.4f)
            {
                _footstepDistance += _speed * Time.deltaTime;
                float stepInterval = isSprinting ? 2.1f : 1.45f;
                if (_footstepDistance >= stepInterval)
                {
                    _footstepDistance = 0f;
                    if (ProceduralForestAudio.Instance != null)
                    {
                        ProceduralForestAudio.Instance.PlayFootstep(isSprinting, _isStandingOnRock);
                    }
                }
            }

            UpdateLocomotionAnimator(_animationBlend, moveInput.magnitude);
        }

        private void UpdateLocomotionAnimator(float blendSpeed, float inputMagnitude)
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;

            // В контроллере StarterAssetsThirdPerson пороги BlendTree: 0 = Idle, 2 = Walk_N, 6 = Run_N.
            // В нашем запасном контроллере: 0 = Idle, 0.5 = Walk, 1.0 = Run.
            bool isStarterAssetsController = HasParameter(characterAnimator, AnimIDMotionSpeed);
            float speedParam = isStarterAssetsController
                ? blendSpeed
                : Mathf.Clamp01(blendSpeed / runSpeed);

            if (HasParameter(characterAnimator, AnimIDSpeed))
                characterAnimator.SetFloat(AnimIDSpeed, speedParam);

            if (HasParameter(characterAnimator, AnimIDMotionSpeed))
                characterAnimator.SetFloat(AnimIDMotionSpeed, inputMagnitude > 0.01f ? 1.0f : 1.0f);
        }

        private void TriggerOrSetJump(bool jumping)
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;

            foreach (AnimatorControllerParameter param in characterAnimator.parameters)
            {
                if (param.nameHash == AnimIDJump)
                {
                    if (param.type == AnimatorControllerParameterType.Trigger && jumping)
                        characterAnimator.SetTrigger(AnimIDJump);
                    else if (param.type == AnimatorControllerParameterType.Bool)
                        characterAnimator.SetBool(AnimIDJump, jumping);
                    break;
                }
            }
        }

        private void SetJumpBoolIfPresent(bool val)
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;

            foreach (AnimatorControllerParameter param in characterAnimator.parameters)
            {
                if (param.nameHash == AnimIDJump && param.type == AnimatorControllerParameterType.Bool)
                {
                    characterAnimator.SetBool(AnimIDJump, val);
                    break;
                }
            }
        }

        private static bool HasParameter(Animator anim, int paramHash)
        {
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.nameHash == paramHash) return true;
            }
            return false;
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && ProceduralForestAudio.Instance != null)
            {
                _footstepDistance = 0f;
                ProceduralForestAudio.Instance.PlayFootstep(_speed > walkSpeed + 0.5f, _isStandingOnRock);
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && ProceduralForestAudio.Instance != null)
            {
                ProceduralForestAudio.Instance.PlayJumpLand();
            }
        }
    }

    public class ForestAnimationEventReceiver : MonoBehaviour
    {
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && ProceduralForestAudio.Instance != null)
            {
                ProceduralForestAudio.Instance.PlayFootstep(false);
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && ProceduralForestAudio.Instance != null)
            {
                ProceduralForestAudio.Instance.PlayJumpLand();
            }
        }
    }
}
