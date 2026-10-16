using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dimari.Story;

namespace Dimari.Editor
{
    public static class DimariCitySetup
    {
        [MenuItem("Dimari/Глава 2 — подготовить выбранный город")]
        public static void PrepareChapterTwo()
        {
            if (Application.isPlaying) return;
            SceneAsset source = Selection.activeObject as SceneAsset;
            StoryQuestManager manager = Object.FindAnyObjectByType<StoryQuestManager>();
            if (source == null || manager == null)
            {
                EditorUtility.DisplayDialog("Глава 2", "Откройте лесную сцену. В Project выделите .unity демосцену Demo City By Versatile Studio и повторите команду.", "OK");
                return;
            }
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath == manager.gameObject.scene.path)
            {
                EditorUtility.DisplayDialog("Глава 2", "Выбрана лесная сцена. Выберите файл сцены города в Project.", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Dimari/Scenes"))
                AssetDatabase.CreateFolder("Assets/Dimari", "Scenes");
            string destination = "Assets/Dimari/Scenes/Chapter2_Rogovo.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(destination) == null &&
                !AssetDatabase.CopyAsset(sourcePath, destination))
            {
                Debug.LogError("[Dimari] Не удалось скопировать городскую сцену.");
                return;
            }
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(destination).isLoaded)
            {
                EditorUtility.DisplayDialog("Глава 2", "Копия города уже открыта. Сохраните и закройте её, оставив лесную сцену, затем повторите команду.", "OK");
                return;
            }
            var city = EditorSceneManager.OpenScene(destination, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                Transform arrival = null;
                foreach (GameObject root in city.GetRootGameObjects())
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (child.name == "CityArrivalPoint") arrival = child;
                if (arrival == null)
                {
                    MeshFilter street = null;
                    float best = -1f;
                    foreach (GameObject root in city.GetRootGameObjects())
                        foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>(true))
                        {
                            string name = mesh.name.ToLowerInvariant();
                            if (!name.Contains("road") && !name.Contains("street") && !name.Contains("pavement") &&
                                !name.Contains("ground") && !name.Contains("asphalt")) continue;
                            Renderer renderer = mesh.GetComponent<Renderer>();
                            if (mesh.sharedMesh == null || renderer == null) continue;
                            if (renderer.bounds.size.y > 2f) continue;
                            float area = renderer.bounds.size.x * renderer.bounds.size.z;
                            if (area > best) { best = area; street = mesh; }
                        }
                    if (street == null)
                    {
                        EditorUtility.DisplayDialog("Нужна точка прибытия", "Не удалось надёжно найти улицу по именам объектов. Откройте копию Chapter2_Rogovo и поставьте CityArrivalPoint вручную на свободной улице, затем повторите команду.", "OK");
                        return;
                    }
                    if (street.GetComponent<Collider>() == null)
                        street.gameObject.AddComponent<MeshCollider>().sharedMesh = street.sharedMesh;
                    Physics.SyncTransforms();
                    Bounds bounds = street.GetComponent<Renderer>().bounds;
                    Collider ground = street.GetComponent<Collider>();
                    Vector3 position = bounds.center;
                    Ray ray = new Ray(new Vector3(position.x, bounds.max.y + 5f, position.z), Vector3.down);
                    if (!ground.Raycast(ray, out RaycastHit hit, 20f))
                    {
                        Debug.LogError("[Dimari] Центр выбранной улицы не имеет поверхности. Поставьте CityArrivalPoint вручную.");
                        return;
                    }
                    GameObject point = new GameObject("CityArrivalPoint");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(point, city);
                    point.transform.position = hit.point + Vector3.up * 0.08f;
                    arrival = point.transform;
                }
                EditorSceneManager.MarkSceneDirty(city);
                EditorSceneManager.SaveScene(city);
                Undo.RecordObject(manager, "Configure chapter two");
                manager.cityScenePath = destination;
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                var builds = EditorBuildSettings.scenes.ToList();
                var existing = builds.Find(scene => scene.path == destination);
                if (existing != null) existing.enabled = true;
                else builds.Add(new EditorBuildSettingsScene(destination, true));
                EditorBuildSettings.scenes = builds.ToArray();
                EditorSceneManager.SaveScene(manager.gameObject.scene);
                Debug.Log("[Dimari] Глава 2 подготовлена: " + destination + ". Проверьте CityArrivalPoint: рядом должны свободно помещаться герой и капитан. Оригинальная демосцена не изменена.");
            }
            finally
            {
                EditorSceneManager.CloseScene(city, true);
            }
        }

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
