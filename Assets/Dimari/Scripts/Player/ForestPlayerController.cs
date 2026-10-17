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

        [Header("Плавность ходьбы")]
        [Min(0.03f)] public float accelerationTime = 0.18f;
        [Min(0.03f)] public float brakingTime = 0.11f;
        [Range(0.3f, 1f)] public float backwardsSpeedMultiplier = 0.65f;

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

        public bool IsSwimming { get; private set; }
        public float swimSpeed = 2.3f;
        private bool _waterBoardPrompt;
        private Transform _ridingShip;
        private Vector3 _ridingLocalPosition;
        private CharacterController _controller;

        public void StopLocomotionAnimation()
        {
            _speed = 0f;
            _horizontalVelocity = Vector3.zero;
            _movementDamping = Vector3.zero;
            _footstepDistance = 0f;
            _animationBlend = 0f;
            CurrentNormalizedSpeed = 0f;
            UpdateLocomotionAnimator(0f, 0f);
        }

        public void ResetWaterMotion()
        {
            _verticalVelocity = 0f;
            StopLocomotionAnimation();
            IsSwimming = false;
            _ridingShip = null;
        }

        private bool UpdateSwimming()
        {
            StoryQuestManager story = StoryQuestManager.Instance;
            if (_waterBoardPrompt && story != null)
            {
                story.SetInteractionPrompt("");
                _waterBoardPrompt = false;
            }
            if (!IsControlEnabled || story == null || !story.TryGetWaterHeight(transform.position, out float water))
            { IsSwimming = false; return false; }
            float depth = water - transform.position.y;
            if (depth < (IsSwimming ? 0.55f : 0.85f)) { IsSwimming = false; return false; }
            if (!IsSwimming) StopLocomotionAnimation();
            IsSwimming = true;
            IsGrounded = false;
            _ridingShip = null;
            _verticalVelocity = 0f;
            Vector2 input = DimariInput.GetMoveInput();
            Camera main = Camera.main;
            ForestCameraController view = main != null ? main.GetComponent<ForestCameraController>() : null;
            float yaw = view != null ? view.MovementYaw : transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 horizontal = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            float bob = Mathf.Sin(Time.time * 1.8f) * 0.035f;
            float vertical = Mathf.Clamp((water - 1.25f + bob - transform.position.y) * 5f, -2f, 4f);
            _controller.Move((horizontal * swimSpeed + Vector3.up * vertical) * Time.deltaTime);
            CurrentNormalizedSpeed = input.magnitude * 0.3f;
            // The installed locomotion controller has no swimming clip: avoid running/falling in water.
            UpdateLocomotionAnimator(0f, 0f);
            if (story.CanBoardFromWater(transform.position))
            {
                story.SetInteractionPrompt("[E] Подняться на корабль");
                _waterBoardPrompt = true;
                if (DimariInput.IsInteractPressed()) story.BoardShipFromWater();
            }
            return true;
        }

        private void CarryWithShip()
        {
            if (_ridingShip != null && IsControlEnabled && _verticalVelocity <= 0f)
            {
                Vector3 delta = _ridingShip.TransformPoint(_ridingLocalPosition) - transform.position;
                _controller.Move(delta);
            }
        }

        private void CacheShipSupport()
        {
            _ridingShip = null;
            StoryQuestManager story = StoryQuestManager.Instance;
            if (story == null || story.ShipRoot == null || !IsControlEnabled || _verticalVelocity > 0f) return;
            foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.25f,
                Vector3.down, 0.65f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(story.ShipRoot) && hit.normal.y > 0.5f)
                {
                    _ridingShip = story.ShipRoot;
                    _ridingLocalPosition = _ridingShip.InverseTransformPoint(transform.position);
                    break;
                }
            }
        }
        private Vector3 _horizontalVelocity, _movementDamping;
        private float _lastFootstepTime = -10f, _lastAnimationFootstepTime = -10f, _lastLandTime = -10f;
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
            // Cinematics move the transform directly while the controller is disabled.
            if (_controller == null || !_controller.enabled || !_controller.gameObject.activeInHierarchy) return;
            if (StoryQuestManager.Instance != null && StoryQuestManager.Instance.IsMenuOpen) return;
            CarryWithShip();
            if (UpdateSwimming()) return;
            GroundedCheck();
            JumpAndGravity();

            if (IsControlEnabled)
            {
                // Гарантируем, что после окончания кат-сцены пробуждения визуальная модель стоит идеально ровно
                if (visualModelRoot != null && visualModelRoot != transform)
                {
                    visualModelRoot.localPosition = Vector3.zero;
                    visualModelRoot.localRotation = Quaternion.identity;
                }
                MoveCharacter();
                CacheShipSupport();
            }
            else
            {
                StopLocomotionAnimation();
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
                    _ridingShip = null;
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

            _verticalVelocity = Mathf.Max(_verticalVelocity + gravity * Time.deltaTime, -_terminalVelocity);
        }

        private void MoveCharacter()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector2 moveInput = Vector2.ClampMagnitude(DimariInput.GetMoveInput(), 1f);
            bool isSprinting = DimariInput.IsSprintHeld() && moveInput.y > 0.1f;
            float targetSpeed = isSprinting ? runSpeed : walkSpeed;
            if (moveInput.y < -0.1f) targetSpeed *= backwardsSpeedMultiplier;

            Camera activeCamera = Camera.main;
            if (activeCamera != null) cameraTransform = activeCamera.transform;
            ForestCameraController view = cameraTransform != null
                ? cameraTransform.GetComponent<ForestCameraController>() : null;
            float facingYaw = view != null ? view.MovementYaw
                : (cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y);
            float bodyYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, facingYaw, ref _rotationVelocity,
                Mathf.Max(0.03f, rotationSmoothTime), 720f, dt);
            transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            // W still follows mouse yaw, independently of the body's short visual turn easing.
            Vector3 desired = Quaternion.Euler(0f, facingYaw, 0f) * new Vector3(moveInput.x, 0f, moveInput.y) * targetSpeed;
            float smoothTime = desired.sqrMagnitude < _horizontalVelocity.sqrMagnitude ? brakingTime : accelerationTime;
            _horizontalVelocity = Vector3.SmoothDamp(_horizontalVelocity, desired, ref _movementDamping,
                Mathf.Max(0.03f, smoothTime), Mathf.Infinity, dt);
            if (desired.sqrMagnitude < 0.0001f && _horizontalVelocity.sqrMagnitude < 0.0025f)
            { _horizontalVelocity = Vector3.zero; _movementDamping = Vector3.zero; }

            float verticalStep = _verticalVelocity * dt;
            if (IsGrounded && _verticalVelocity <= 0f && _horizontalVelocity.sqrMagnitude > 0.01f)
                verticalStep = -4.5f * dt;

            // Measure only this locomotion Move, not the moving ship's separate carry step.
            Vector3 before = transform.position;
            _controller.Move(_horizontalVelocity * dt + Vector3.up * verticalStep);
            Vector3 actual = (transform.position - before) / dt; actual.y = 0f;
            _speed = actual.magnitude;
            if (_speed < _horizontalVelocity.magnitude * 0.35f)
            { _horizontalVelocity = actual; _movementDamping = Vector3.zero; }
            float visibleSpeed = _speed < 0.12f ? 0f : _speed;
            _animationBlend = Mathf.Lerp(_animationBlend, visibleSpeed,
                1f - Mathf.Exp(-Mathf.Max(1f, speedChangeRate) * dt));
            if (_animationBlend < 0.03f) _animationBlend = 0f;
            CurrentNormalizedSpeed = Mathf.Clamp01(_speed / Mathf.Max(0.1f, runSpeed));

            if (IsGrounded && _speed > 0.25f)
            {
                _footstepDistance += _speed * dt;
                float stepDistance = isSprinting ? 1.5f : 0.9f;
                // Animation events are authoritative when present; this is only the fallback.
                if (_footstepDistance >= stepDistance && Time.time - _lastAnimationFootstepTime > 0.6f)
                { _footstepDistance %= stepDistance; PlayLocomotionFootstep(); }
            }
            else _footstepDistance = 0f;
            UpdateLocomotionAnimator(_animationBlend, moveInput.magnitude);
        }

        private void UpdateLocomotionAnimator(float blendSpeed, float inputMagnitude)
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;
            bool starter = HasParameter(characterAnimator, AnimIDMotionSpeed);
            float walkThreshold = starter ? 2f : 0.5f;
            float runThreshold = starter ? 6f : 1f;
            // A normalized Speed = speed/runSpeed never reached the fallback's pure Walk at 0.5.
            // Map both native blend trees explicitly so ordinary walking is not an idle/walk mixture.
            float walk = Mathf.Max(0.1f, walkSpeed), run = Mathf.Max(walk + 0.1f, runSpeed);
            float speedParam = blendSpeed <= walk
                ? Mathf.Lerp(0f, walkThreshold, Mathf.Clamp01(blendSpeed / walk))
                : Mathf.Lerp(walkThreshold, runThreshold, Mathf.InverseLerp(walk, run, blendSpeed));
            if (HasParameter(characterAnimator, AnimIDSpeed)) characterAnimator.SetFloat(AnimIDSpeed, speedParam);
            if (HasParameter(characterAnimator, AnimIDMotionSpeed)) characterAnimator.SetFloat(AnimIDMotionSpeed, 1f);
        }

        private void PlayLocomotionFootstep()
        {
            if (!IsControlEnabled || IsSwimming || !IsGrounded || _speed < 0.25f || Time.time - _lastFootstepTime < 0.18f
                || (StoryQuestManager.Instance != null && StoryQuestManager.Instance.IsMenuOpen)) return;
            _lastFootstepTime = Time.time;
            if (ProceduralForestAudio.Instance != null)
                ProceduralForestAudio.Instance.PlayFootstep(_speed > walkSpeed + 0.5f, _isStandingOnRock);
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

        public void ReceiveFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight <= 0.5f) return;
            _lastAnimationFootstepTime = Time.time;
            _footstepDistance = 0f;
            PlayLocomotionFootstep();
        }
        private void OnFootstep(AnimationEvent animationEvent) { ReceiveFootstep(animationEvent); }
        public void ReceiveLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight <= 0.5f || !IsControlEnabled || IsSwimming
                || Time.time - _lastLandTime < 0.2f
                || (StoryQuestManager.Instance != null && StoryQuestManager.Instance.IsMenuOpen)) return;
            _lastLandTime = Time.time;
            if (ProceduralForestAudio.Instance != null) ProceduralForestAudio.Instance.PlayJumpLand();
        }
        private void OnLand(AnimationEvent animationEvent) { ReceiveLand(animationEvent); }
    }

    public class ForestAnimationEventReceiver : MonoBehaviour
    {
        private ForestPlayerController owner;
        private void Awake() { owner = GetComponentInParent<ForestPlayerController>(); }
        private void OnFootstep(AnimationEvent animationEvent)
        { if (owner != null) owner.ReceiveFootstep(animationEvent); }
        private void OnLand(AnimationEvent animationEvent)
        { if (owner != null) owner.ReceiveLand(animationEvent); }
    }
}
