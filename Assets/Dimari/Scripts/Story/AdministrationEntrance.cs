using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Core;
using Dimari.Player;

namespace Dimari.Story
{
    public class AdministrationEntrance : MonoBehaviour
    {
        public string destinationScene = "Assets/Dimari/Scenes/Rogovo_Administration.unity";
        public float interactionDistance = 2.5f;
        private bool busy, prompted;
        private ForestPlayerController player;

        private void Update()
        {
            if (Dimari.Cinematics.OfficeFishCutscene.Completed) return;
            if (player == null) player = FindAnyObjectByType<ForestPlayerController>();
            StoryQuestManager story = StoryQuestManager.Instance;
            if (player == null || story == null || busy || story.IsMenuOpen || !player.IsControlEnabled) return;
            bool near = Vector3.Distance(player.transform.position, transform.position) < interactionDistance;
            if (near)
            {
                story.SetInteractionPrompt("[E] Войти в администрацию Рогово");
                prompted = true;
                if (DimariInput.IsInteractPressed()) StartCoroutine(Enter());
            }
            else if (prompted) { story.SetInteractionPrompt(""); prompted = false; }
        }

        private IEnumerator Enter()
        {
            if (!Application.CanStreamedLevelBeLoaded(destinationScene))
            { Debug.LogError("[Dimari] Добавьте кабинет администрации в Scene List: " + destinationScene); yield break; }
            busy = true;
            StoryQuestManager story = StoryQuestManager.Instance;
            story.SetInteractionPrompt("");
            player.StopLocomotionAnimation();
            player.IsControlEnabled = false;
            Camera camera = Camera.main;
            ForestCameraController view = camera != null ? camera.GetComponent<ForestCameraController>() : null;
            if (view != null) view.IsCutsceneControlled = true;
            if (camera != null)
            {
                Vector3 start = camera.transform.position;
                Vector3 end = transform.position - transform.forward * 2.2f + Vector3.up * 1.6f;
                for (float t = 0; t < 1.8f; t += Time.deltaTime)
                {
                    camera.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t / 1.8f));
                    camera.transform.rotation = Quaternion.LookRotation(transform.position + Vector3.up * 1.3f - camera.transform.position);
                    yield return null;
                }
            }
            fade = 1f;
            Scene oldScene = gameObject.scene;
            yield return SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Additive);
            Scene office = SceneManager.GetSceneByPath(destinationScene);
            Transform spawn = null;
            foreach (GameObject root in office.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (item.name == "AdministrationArrivalPoint") spawn = item;
            if (spawn == null)
            {
                yield return SceneManager.UnloadSceneAsync(office);
                Debug.LogError("[Dimari] В кабинете нет AdministrationArrivalPoint. Создайте точку появления.");
                fade = 0; busy = false; player.IsControlEnabled = true;
                if (view != null) view.BeginGameplayTransitionFromCutscene();
                yield break;
            }
            foreach (GameObject root in office.GetRootGameObjects())
            {
                foreach (Camera cam in root.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
            Transform[] travellers = { story.transform, player.transform, camera != null ? camera.transform : null };
            foreach (Transform traveller in travellers)
            {
                if (traveller == null) continue;
                traveller.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(traveller.gameObject, office);
            }
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            player.ResetWaterMotion();
            if (controller != null) controller.enabled = true;
            SceneManager.SetActiveScene(office);
            story.questSteps.Clear();
            story.questSteps.Add(new StoryQuestManager.QuestStep { id = "administration_office", title = "Осмотреть кабинет администрации и найти сведения о Димарике." });
            // Keep this portal alive until the cut is complete; unload its city afterwards.
            fade = 0;
            if (view != null) view.BeginGameplayTransitionFromCutscene();
            player.IsControlEnabled = true;
            Dimari.Cinematics.OfficeFishCutscene.Begin(player, camera, story, oldScene.path,
                transform.position - transform.forward * 2.8f + Vector3.up * 0.15f,
                Quaternion.LookRotation(-transform.forward, Vector3.up));
            SceneManager.UnloadSceneAsync(oldScene);
        }
        private float fade;
        private void OnGUI()
        {
            if (fade <= 0)
            {
                DrawObjectiveMarker();
                return;
            }
            GUI.depth = -1000;
            GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void DrawObjectiveMarker()
        {
            if (Dimari.Cinematics.OfficeFishCutscene.Completed) return;
            StoryQuestManager story = StoryQuestManager.Instance;
            Camera camera = Camera.main;
            if (busy || player == null || camera == null || story == null || story.IsMenuOpen
                || !player.IsControlEnabled) return;
            float distance = Vector3.Distance(player.transform.position, transform.position);
            Vector3 point = camera.WorldToViewportPoint(transform.position + Vector3.up * 2.3f);
            bool onScreen = point.z > 0 && point.x > 0.08f && point.x < 0.92f
                && point.y > 0.14f && point.y < 0.85f;
            Vector3 relative = camera.transform.InverseTransformPoint(transform.position + Vector3.up);
            string direction = relative.x < 0 ? "← СЛЕВА" : "СПРАВА →";
            if (relative.z < 0 && Mathf.Abs(relative.x) < Mathf.Abs(relative.z) * 0.35f)
                direction = "↶ ПОЗАДИ — ОБЕРНИТЕСЬ";
            float width = Mathf.Min(340f, Screen.width - 24f);
            float height = 86f;
            float x = onScreen ? point.x * Screen.width : (relative.x < 0 ? width * 0.5f + 18f : Screen.width - width * 0.5f - 18f);
            float y = onScreen ? (1f - point.y) * Screen.height : Screen.height * 0.43f;
            x = Mathf.Clamp(x, width * 0.5f + 12f, Screen.width - width * 0.5f - 12f);
            y = Mathf.Clamp(y, 160f, Screen.height - 150f);
            Rect box = new Rect(x - width * 0.5f, y - height * 0.5f, width, height);
            GUI.depth = -110;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.94f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.8f, 0.22f);
            GUI.DrawTexture(new Rect(box.x, box.y, 4, box.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                richText = true, wordWrap = true
            };
            string hint = distance < interactionDistance ? "[E] Войти в администрацию" : (onScreen ? "Подойдите ко входу" : direction);
            GUI.Label(box, "<color=#FFE18A>◆ АДМИНИСТРАЦИЯ РОГОВО</color>\n"
                + distance.ToString("0") + " м — " + hint, style);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.4f);
            Gizmos.DrawRay(transform.position, transform.forward * 2);
        }
    }
}
