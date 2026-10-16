using UnityEngine;
using Dimari.Core;
using Dimari.Player;
using Dimari.Story;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Dimari.Vehicles
{
    [RequireComponent(typeof(Rigidbody))]
    public class CityDriveableCar : MonoBehaviour
    {
        public WheelCollider[] wheels;
        public Transform[] wheelVisuals;
        private Quaternion[] visualOffsets;
        public float motorTorque = 1500f, brakeTorque = 3500f, maximumSpeed = 100f;
        private Rigidbody body;
        private ForestPlayerController player;
        private ForestCameraController view;
        private Camera camera;
        private bool driving, prompted;
        private Vector2 input;
        private bool handbrake;
        private Renderer[] playerRenderers;
        private bool[] rendererStates;
        private float steer;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (wheelVisuals != null && wheels != null)
            {
                visualOffsets = new Quaternion[wheelVisuals.Length];
                for (int i = 0; i < wheelVisuals.Length && i < wheels.Length; i++)
                    if (wheelVisuals[i] != null) visualOffsets[i] = Quaternion.Inverse(wheels[i].transform.rotation) * wheelVisuals[i].rotation;
            }
            body.mass = 1450f;
            body.centerOfMass = new Vector3(0, 0.35f, 0);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        private void Update()
        {
            if (player == null) player = FindAnyObjectByType<ForestPlayerController>();
            StoryQuestManager story = StoryQuestManager.Instance;
            if (player == null || story == null) return;
            if (driving)
            {
                // Keep the hidden hero with the car so story distances remain coherent.
                player.transform.position = transform.position + Vector3.up;
                input = story.IsMenuOpen ? Vector2.zero : DimariInput.GetMoveInput();
                handbrake = false;
#if ENABLE_INPUT_SYSTEM
                handbrake = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                handbrake = Input.GetKey(KeyCode.Space);
#endif
                if (!story.IsMenuOpen && DimariInput.IsInteractPressed()) TryExit();
                return;
            }
            if (!player.IsControlEnabled || story.IsMenuOpen) return;
            bool near = Vector3.Distance(player.transform.position, transform.position) < 4f;
            if (near)
            {
                story.SetInteractionPrompt("[E] Сесть за руль");
                prompted = true;
                if (DimariInput.IsInteractPressed()) Enter();
            }
            else if (prompted) { story.SetInteractionPrompt(""); prompted = false; }
        }
        private void Enter()
        {
            camera = Camera.main;
            view = camera != null ? camera.GetComponent<ForestCameraController>() : null;
            if (camera == null || view == null) return;
            driving = true;
            StoryQuestManager.Instance.SetInteractionPrompt("");
            player.StopLocomotionAnimation();
            player.ResetWaterMotion();
            player.IsControlEnabled = false;
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            rendererStates = new bool[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++)
            { rendererStates[i] = playerRenderers[i].enabled; playerRenderers[i].enabled = false; }
            view.IsCutsceneControlled = true;
        }
        private void TryExit()
        {
            if (body.linearVelocity.magnitude > 1.5f) return;
            foreach (Vector3 offset in new[] { Vector3.left * 2f, Vector3.right * 2f, Vector3.back * 3.5f })
            {
                Vector3 candidate = transform.TransformPoint(offset);
                bool found = false;
                Vector3 feet = candidate;
                foreach (RaycastHit hit in Physics.RaycastAll(candidate + Vector3.up * 2f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.normal.y < 0.7f) continue;
                    feet = hit.point + Vector3.up * 0.08f;
                    if (!Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    { found = true; break; }
                }
                if (!found) continue;
                driving = false; input = Vector2.zero;
                player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0, transform.eulerAngles.y, 0));
                for (int i = 0; i < playerRenderers.Length; i++)
                    if (playerRenderers[i] != null) playerRenderers[i].enabled = rendererStates[i];
                CharacterController controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = true;
                player.IsControlEnabled = true;
                view.BeginGameplayTransitionFromCutscene();
                return;
            }
        }
        private void FixedUpdate()
        {
            if (wheels == null || wheels.Length != 4) return;
            bool paused = StoryQuestManager.Instance != null && StoryQuestManager.Instance.IsMenuOpen;
            float speed = Vector3.Dot(body.linearVelocity, transform.forward);
            float throttle = driving && !paused ? input.y : 0;
            bool reversingInput = Mathf.Abs(speed) > 0.7f && throttle * speed < 0;
            float torque = reversingInput ? 0 : throttle * motorTorque;
            if (Mathf.Abs(speed) * 3.6f > (throttle < 0 ? 25f : maximumSpeed)) torque = 0;
            float braking = !driving || paused || reversingInput ? brakeTorque : (Mathf.Abs(throttle) < 0.01f ? 100f : 0f);
            float angle = Mathf.Lerp(32f, 9f, Mathf.Clamp01(Mathf.Abs(speed) / 28f));
            steer = Mathf.MoveTowards(steer, driving && !paused ? input.x * angle : 0, 65f * Time.fixedDeltaTime);
            for (int i = 0; i < 4; i++)
            {
                wheels[i].steerAngle = i < 2 ? steer : 0;
                wheels[i].motorTorque = i >= 2 ? torque * 0.5f : 0;
                wheels[i].brakeTorque = handbrake && i >= 2 ? brakeTorque * 1.5f : braking;
            }
            AntiRoll(0, 1); AntiRoll(2, 3);
        }
        private void AntiRoll(int a, int b)
        {
            WheelHit hitA, hitB;
            bool groundedA = wheels[a].GetGroundHit(out hitA), groundedB = wheels[b].GetGroundHit(out hitB);
            float travelA = groundedA ? (-wheels[a].transform.InverseTransformPoint(hitA.point).y - wheels[a].radius) / wheels[a].suspensionDistance : 1f;
            float travelB = groundedB ? (-wheels[b].transform.InverseTransformPoint(hitB.point).y - wheels[b].radius) / wheels[b].suspensionDistance : 1f;
            float force = (travelA - travelB) * 5500f;
            if (groundedA) body.AddForceAtPosition(wheels[a].transform.up * -force, wheels[a].transform.position);
            if (groundedB) body.AddForceAtPosition(wheels[b].transform.up * force, wheels[b].transform.position);
        }
        private void LateUpdate()
        {
            if (wheelVisuals != null && visualOffsets != null)
                for (int i = 0; i < wheelVisuals.Length && i < wheels.Length; i++)
                {
                    if (wheelVisuals[i] == null) continue;
                    Vector3 position; Quaternion rotation;
                    wheels[i].GetWorldPose(out position, out rotation);
                    wheelVisuals[i].SetPositionAndRotation(position, rotation * visualOffsets[i]);
                }
            if (!driving || camera == null) return;
            Vector3 focus = transform.position + Vector3.up * 1.3f;
            Vector3 desired = focus - transform.forward * 6f + Vector3.up * 2.2f;
            Vector3 delta = desired - focus;
            foreach (RaycastHit hit in Physics.SphereCastAll(focus, 0.25f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform)) continue;
                float distance = Vector3.Distance(focus, desired);
                if (hit.distance < distance) desired = focus + delta.normalized * Mathf.Max(0.5f, hit.distance - 0.2f);
            }
            camera.transform.position = Vector3.Lerp(camera.transform.position, desired, 1 - Mathf.Exp(-8f * Time.deltaTime));
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position);
        }
        private void OnGUI()
        {
            if (!driving || (StoryQuestManager.Instance != null && StoryQuestManager.Instance.IsMenuOpen)) return;
            GUI.Box(new Rect(Screen.width - 370, Screen.height - 120, 350, 95),
                Mathf.RoundToInt(body.linearVelocity.magnitude * 3.6f) + " км/ч\nW/S — газ / тормоз / назад | A/D — руль\nПробел — ручник | E — выйти после остановки");
        }
    }
}
