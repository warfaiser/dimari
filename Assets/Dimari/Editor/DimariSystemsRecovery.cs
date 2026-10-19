using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Cinematics;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.Editor
{
    /// <summary>
    /// Восстанавливает отсутствующую систему задач/меню в открытой сцене.
    /// Без StoryQuestManager не рисуется меню и ESC ничего не открывает.
    /// </summary>
    public static class DimariSystemsRecovery
    {
        [MenuItem("Dimari/Диагностика — создать систему меню и задач")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Система меню",
                    "Остановите Play. Создавать систему нужно в режиме редактирования.", "OK");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            StoryQuestManager manager = Object.FindAnyObjectByType<StoryQuestManager>(FindObjectsInactive.Include);
            if (manager != null)
            {
                bool wasInactive = !manager.gameObject.activeInHierarchy;
                if (wasInactive)
                {
                    Undo.RecordObject(manager.gameObject, "Activate story system");
                    manager.gameObject.SetActive(true);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                Selection.activeGameObject = manager.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
                EditorUtility.DisplayDialog("Система меню",
                    "StoryQuestManager уже есть: " + FullPath(manager.transform)
                    + (wasInactive ? "\nОн был выключен — включён. Сохраните Ctrl+S." : "\nОн включён."), "OK");
                return;
            }

            ForestPlayerController player = Object.FindAnyObjectByType<ForestPlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Система меню",
                    "В открытой сцене нет игрока (ForestPlayerController). Откройте полноценную лесную сцену или сначала соберите её мастером настройки леса.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Dimari story system");
            GameObject systems = new GameObject("Dimari_GameSystems");
            Undo.RegisterCreatedObjectUndo(systems, "Create story systems");
            StoryQuestManager quest = Undo.AddComponent<StoryQuestManager>(systems);
            ForestAwakeningDirector director = Undo.AddComponent<ForestAwakeningDirector>(systems);
            director.playerController = player;
            director.cameraController = Object.FindAnyObjectByType<ForestCameraController>();
            director.playerVisualRoot = player.visualModelRoot;
            director.showMainMenuOnStart = true;
            Undo.RecordObject(quest, "Initialize quests");
            quest.InitializeDefaultQuests();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = systems;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorUtility.DisplayDialog("Система меню",
                "Создан объект Dimari_GameSystems с меню, задачами и вступлением.\nСохраните сцену Ctrl+S и запустите Play.\n\n"
                + "Если сцена собиралась мастером настройки леса, лучше запустить его заново — он правильно расставит корабль, NPC и предметы.", "OK");
        }

        static string FullPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
