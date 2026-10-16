using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dimari.Vehicles;

namespace Dimari.Editor
{
    public static class DimariCarSetup
    {
        [MenuItem("Dimari/Машина — установить в городе")]
        public static void Install()
        {
            if (Application.isPlaying) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Chapter2_Rogovo")
            { EditorUtility.DisplayDialog("Машина", "Откройте Chapter2_Rogovo.", "OK"); return; }
            if (Object.FindAnyObjectByType<CityDriveableCar>() != null)
            { EditorUtility.DisplayDialog("Машина", "Dimari_CityCar уже установлен. Удалите его через Undo, если нужна повторная установка.", "OK"); return; }
            GameObject model = Selection.activeObject as GameObject;
            if (model == null || !EditorUtility.IsPersistent(model))
            {
                var candidates = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.ToLowerInvariant().Contains("sport") && p.ToLowerInvariant().Contains("car"))
                    .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .Where(p => p != null && p.GetComponentsInChildren<Renderer>().Length > 0).ToArray();
                if (candidates.Length == 1) model = candidates[0];
                else
                {
                    Debug.Log("[Dimari] Найденные префабы машины:\n" + string.Join("\n", candidates.Select(AssetDatabase.GetAssetPath)));
                    EditorUtility.DisplayDialog("Машина", "Нельзя надёжно выбрать модель автоматически. В Project выделите префаб машины из Sport Car Free и повторите команду. Список кандидатов выведен в Console.", "OK");
                    return;
                }
            }
            Transform arrival = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (item.name == "CityArrivalPoint") arrival = item;
            if (arrival == null) { Debug.LogError("[Dimari] Нет CityArrivalPoint для парковки машины."); return; }
            Physics.SyncTransforms();
            Vector3 parking = Vector3.zero;
            bool found = false;
            for (int d = 6; d <= 24 && !found; d += 3)
                foreach (float side in new[] { 0f, 4f, -4f, 8f, -8f })
                {
                    Vector3 candidate = arrival.position + arrival.forward * d + arrival.right * side;
                    foreach (RaycastHit hit in Physics.RaycastAll(candidate + Vector3.up * 5, Vector3.down, 10, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.normal.y < 0.9f) continue;
                        Vector3 center = hit.point + Vector3.up * 1.1f;
                        if (Physics.CheckBox(center, new Vector3(1.15f, 0.7f, 2.7f), arrival.rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                        parking = hit.point + Vector3.up * 0.15f; found = true; break;
                    }
                    if (found) break;
                }
            if (!found) { Debug.LogError("[Dimari] Не найдена свободная парковка рядом с прибытием. Машина не создана."); return; }
            GameObject car = new GameObject("Dimari_CityCar");
            Undo.RegisterCreatedObjectUndo(car, "Install city car");
            GameObject visual = Object.Instantiate(model, car.transform);
            visual.name = "CarVisual";
            // Do not run two independent controllers/cameras on the same car.
            foreach (MonoBehaviour script in visual.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (Camera camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (AudioListener listener in visual.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (AudioSource audio in visual.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Rigidbody rigidbody in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rigidbody);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Undo.DestroyObjectImmediate(car); return; }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float length = bounds.size.z;
            if (length < 0.01f) { Undo.DestroyObjectImmediate(car); return; }
            visual.transform.localScale *= 4.6f / length;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            BoxCollider hull = car.AddComponent<BoxCollider>();
            hull.center = new Vector3(0, Mathf.Max(0.65f, bounds.size.y * 0.55f), 0);
            hull.size = new Vector3(bounds.size.x * 0.88f, Mathf.Max(0.6f, bounds.size.y * 0.65f), 4.2f);
            car.AddComponent<Rigidbody>();
            CityDriveableCar controller = car.AddComponent<CityDriveableCar>();
            controller.wheels = new WheelCollider[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject wheel = new GameObject("PhysicsWheel_" + i);
                wheel.transform.SetParent(car.transform, false);
                wheel.transform.localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * bounds.size.x * 0.39f, 0.48f, i < 2 ? 1.45f : -1.4f);
                WheelCollider wc = wheel.AddComponent<WheelCollider>();
                wc.radius = 0.34f; wc.mass = 25; wc.suspensionDistance = 0.2f;
                JointSpring spring = wc.suspensionSpring;
                spring.spring = 35000; spring.damper = 4500; spring.targetPosition = 0.5f;
                wc.suspensionSpring = spring;
                controller.wheels[i] = wc;
            }
            // Only animate separate, unambiguous wheel meshes. Combined body meshes stay untouched.
            var wheelMeshes = visual.GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.name.ToLowerInvariant().Contains("wheel") || r.name.ToLowerInvariant().Contains("tire"))
                .Where(r => r.bounds.size.z < 1.2f && r.bounds.size.y < 1.2f).ToList();
            controller.wheelVisuals = new Transform[4];
            if (wheelMeshes.Count == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    var nearest = wheelMeshes.OrderBy(r => Vector3.Distance(r.bounds.center, controller.wheels[i].transform.position)).First();
                    // A centred wheel pivot is required for correct visual rotation.
                    if (Vector3.Distance(nearest.transform.position, nearest.bounds.center) < 0.12f)
                    {
                        controller.wheels[i].transform.position = nearest.bounds.center + Vector3.up * 0.1f;
                        controller.wheels[i].radius = Mathf.Clamp(nearest.bounds.size.y * 0.5f, 0.2f, 0.6f);
                        controller.wheelVisuals[i] = nearest.transform;
                    }
                    wheelMeshes.Remove(nearest);
                }
            }
            car.transform.SetPositionAndRotation(parking, arrival.rotation);
            Selection.activeGameObject = car;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Dimari] Машина установлена. Используется собственный WheelCollider-контроллер Dimari, не RCC. Размеры и оси модели, колёса, подвеску и парковку обязательно проверить в Play. Модель должна смотреть вдоль локальной +Z.");
        }
    }
}
