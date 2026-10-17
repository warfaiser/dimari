using UnityEngine;
using Dimari.Core;
using Dimari.Player;

namespace Dimari.Story
{
    public class PizzaExpressEntrance : MonoBehaviour
    {
        public Transform door;
        bool open;
        ForestPlayerController player;
        bool prompted;
        void Update()
        {
            if (player == null) player = FindAnyObjectByType<ForestPlayerController>();
            var story = StoryQuestManager.Instance;
            if (player == null || story == null || story.IsMenuOpen || !player.IsControlEnabled) return;
            bool near = Vector3.Distance(player.transform.position, transform.position) < 2.6f;
            if (near)
            {
                story.SetInteractionPrompt("[E] " + (open ? "Закрыть" : "Открыть") + " дверь «Пицца Экспресс 24»");
                prompted = true;
                if (DimariInput.IsInteractPressed()) open = !open;
            }
            else if (prompted) { story.SetInteractionPrompt(""); prompted = false; }
            if (door != null) door.localRotation = Quaternion.RotateTowards(door.localRotation, Quaternion.Euler(0, open ? -100 : 0, 0), Time.deltaTime * 150);
        }
        void OnGUI()
        {
            var story = StoryQuestManager.Instance;
            Camera camera = Camera.main;
            if (story == null || camera == null || player == null || story.IsMenuOpen || !player.IsControlEnabled) return;
            bool quest = false;
            foreach (var step in story.questSteps) if (step.id == "city_pizza_express") quest = true;
            if (!quest) return;
            float distance = Vector3.Distance(player.transform.position, transform.position);
            if (distance < 3) return;
            Vector3 p = camera.WorldToViewportPoint(transform.position + Vector3.up * 2);
            string direction = p.z < 0 ? "Обернитесь" : p.x < 0 ? "← Слева" : p.x > 1 ? "Справа →" : "Ко входу";
            float x = p.z > 0 ? Mathf.Clamp(p.x * Screen.width, 160, Screen.width - 160) : Screen.width * 0.5f;
            float y = p.z > 0 ? Mathf.Clamp((1-p.y)*Screen.height, 140, Screen.height - 190) : 160;
            GUI.Box(new Rect(x-150,y,300,65), "ПИЦЦА ЭКСПРЕСС 24\n" + distance.ToString("0") + " м — " + direction,
                new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
        }
    }
}
