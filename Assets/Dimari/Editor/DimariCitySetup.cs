using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dimari.Story;

namespace Dimari.Editor
{
    public static class DimariCitySetup
    {
        [MenuItem("Dimari/Город — привязать выбранную сцену")]
        public static void BindScene()
        {
            if (Application.isPlaying) return;
            SceneAsset asset = Selection.activeObject as SceneAsset;
            StoryQuestManager manager = Object.FindAnyObjectByType<StoryQuestManager>();
            if (asset == null || manager == null)
            {
                EditorUtility.DisplayDialog("Город", "В лесной сцене выберите в Project файл .unity демосцены города, затем повторите команду. Нужен StoryQuestManager в открытой сцене.", "OK");
                return;
            }
            string path = AssetDatabase.GetAssetPath(asset);
            var scenes = EditorBuildSettings.scenes.ToList();
            EditorBuildSettingsScene existing = scenes.Find(s => s.path == path);
            if (existing != null) existing.enabled = true;
            else scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Undo.RecordObject(manager, "Bind city scene");
            manager.cityScenePath = path;
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log("[Dimari] Сцена города привязана: " + path + ". Сохраните лесную сцену. В городе нужен CityArrivalPoint.");
        }

        [MenuItem("Dimari/Город — создать точку прибытия")]
        public static void CreateArrival()
        {
            if (Application.isPlaying) return;
            GameObject point = new GameObject("CityArrivalPoint");
            Undo.RegisterCreatedObjectUndo(point, "Create city arrival");
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null) point.transform.position = view.pivot;
            Selection.activeGameObject = point;
            EditorSceneManager.MarkSceneDirty(point.scene);
            Debug.Log("[Dimari] Поставьте CityArrivalPoint на свободную улицу/причал на уровне ног. Ось Z — направление взгляда. Сохраните городскую сцену.");
        }
    }
}
