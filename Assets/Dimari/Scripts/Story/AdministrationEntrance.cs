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
            SceneManager.UnloadSceneAsync(oldScene);
        }
        private float fade;
        private void OnGUI()
        {
            if (fade <= 0) return;
            GUI.depth = -1000;
            GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.4f);
            Gizmos.DrawRay(transform.position, transform.forward * 2);
        }
    }
}
