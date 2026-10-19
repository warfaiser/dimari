using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Cinematics;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class DonsportMaybeBabySetup
    {
        static DonsportMaybeBabySetup()
        {
            EditorApplication.delayCall += Auto;
            EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += Auto;
        }
        static void Auto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var story = Object.FindAnyObjectByType<DonsportReceptionCutscene>();
            if (story == null || story.maybeBaby == null) return;
            Ensure(story, false);
        }

        [MenuItem("Dimari/Донспорт — кат-сцена разговора с Мэйби Бэйби")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var story = Object.FindAnyObjectByType<DonsportReceptionCutscene>();
            if (story == null || story.maybeBaby == null)
            {
                EditorUtility.DisplayDialog("Мэйби Бэйби", "Сначала установите историю Донспорта и создайте посетителей зала.", "OK");
                return;
            }
            Ensure(story, true);
        }

        public static void Ensure(DonsportReceptionCutscene story, bool report)
        {
            if (story == null || story.maybeBaby == null) return;
            var cutscene = story.GetComponent<DonsportMaybeBabyCutscene>();
            bool changed = false;
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<DonsportMaybeBabyCutscene>(story.gameObject);
                changed = true;
            }
            if (cutscene.maybeBaby != story.maybeBaby)
            {
                Undo.RecordObject(cutscene, "Assign Мэйби Бэйби to conversation");
                cutscene.maybeBaby = story.maybeBaby;
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(cutscene);
                EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
                Debug.Log("[Dimari] Кат-сцена Мэйби Бэйби установлена в Scene. Сохраните Ctrl+S.", story);
            }
            if (report)
                EditorUtility.DisplayDialog("Мэйби Бэйби",
                    changed
                        ? "Разговор установлен в сцене: 6 реплик, планшет с фото и передача трусов. Сохраните Ctrl+S.\nВ Play кат-сцена начнётся сама, когда герой подойдёт к певице."
                        : "Разговор уже установлен в сцене. Сохраните Ctrl+S, если меняли объект.",
                    "OK");
        }
    }
}
