using UnityEngine;
using Dimari.Core;
using Dimari.Player;

namespace Dimari.Story
{
    /// <summary>
    /// Интерактивный сюжетный объект в лесу (рюкзак, записка, кострище, ориентир).
    /// При приближении игрока показывает подсказку [E] Осмотреть и запускает монолог/выполнение квеста.
    /// </summary>
    public class InteractableStoryObject : MonoBehaviour
    {
        [Header("Параметры сюжетного объекта")]
        public string questStepId = "backpack";
        public string objectTitle = "Старый рюкзак";
        [TextArea(2, 5)]
        public string inspectMonologue = "Внутри промокнувший блокнот. На последней странице надпись: «Не сходи с тропы после заката...»";
        public float interactionDistance = 2.6f;

        private Transform _playerTransform;
        private ForestPlayerController _playerController;
        private bool _isPlayerNear;

        private void Update()
        {
            if (_playerTransform == null)
            {
                ForestPlayerController found = FindAnyObjectByType<ForestPlayerController>();
                if (found != null)
                {
                    _playerController = found;
                    _playerTransform = found.transform;
                }
                return;
            }

            if (_playerController != null && !_playerController.IsControlEnabled)
            {
                return;
            }

            float dist = Vector3.Distance(_playerTransform.position, transform.position);
            bool inRange = dist <= interactionDistance;

            if (inRange)
            {
                _isPlayerNear = true;
                if (StoryQuestManager.Instance != null)
                {
                    StoryQuestManager.Instance.SetInteractionPrompt($"[E] Осмотреть: {objectTitle}");
                }

                if (DimariInput.IsInteractPressed())
                {
                    Inspect();
                }
            }
            else if (_isPlayerNear)
            {
                _isPlayerNear = false;
                if (StoryQuestManager.Instance != null)
                {
                    StoryQuestManager.Instance.SetInteractionPrompt("");
                }
            }
        }

        private void Inspect()
        {
            if (StoryQuestManager.Instance != null)
            {
                StoryQuestManager.Instance.CompleteQuestStep(questStepId, inspectMonologue);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
    }
}
