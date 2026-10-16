using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dimari.Vehicles;

namespace Dimari.Editor
{
    public static class DimariCarSetup
    {
        [MenuItem("Dimari/Машина — подогнать колёса к аркам")]
        public static void FitWheels()
        {
            if (Application.isPlaying) return;
            var car = Object.FindAnyObjectByType<CityDriveableCar>();
            if (car == null || car.wheelVisuals == null || car.wheelVisuals.Length != 4
                || car.wheelVisuals.Any(w => w == null))
            { EditorUtility.DisplayDialog("Колёса", "Сначала добавьте четыре визуальных колеса.", "OK"); return; }
            Transform visual = car.transform.Find("CarVisual");
            if (visual == null) return;
            // Read vertices from the asset mesh, not the renderer's rotated world AABB.
            var vertices = new System.Collections.Generic.List<Vector3>();
            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                using (var data = Mesh.AcquireReadOnlyMeshData(filter.sharedMesh))
                {
                    var points = new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount, Unity.Collections.Allocator.Temp);
                    data[0].GetVertices(points);
                    foreach (Vector3 point in points)
                        vertices.Add(car.transform.InverseTransformPoint(filter.transform.TransformPoint(point)));
                    points.Dispose();
                }
            }
            if (vertices.Count < 100) { Debug.LogWarning("[Dimari] Недостаточно геометрии для поиска арок."); return; }
            Vector3 min = vertices[0], max = vertices[0];
            foreach (Vector3 v in vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            float width = max.x - min.x, length = max.z - min.z;
            float mid = (max.z + min.z) * 0.5f;
            Vector3[] centers = new Vector3[4];
            float[] radii = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float sign = i % 2 == 0 ? -1 : 1;
                // An empty wheel mount, when supplied by the author, beats any geometric guess.
                Transform mount = visual.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.ToLowerInvariant().Contains("wheel") && t.GetComponent<Renderer>() == null)
                    .Where(t => { var p = car.transform.InverseTransformPoint(t.position);
                        return p.x * sign > width * 0.2f && (i < 2 ? p.z > mid : p.z < mid); })
                    .OrderBy(t => Vector3.Distance(t.position, car.wheels[i].transform.position)).FirstOrDefault();
                if (mount != null)
                {
                    centers[i] = car.transform.InverseTransformPoint(mount.position);
                    radii[i] = car.wheels[i].radius;
                    continue;
                }
                float bestHeight = float.NegativeInfinity, bestZ = 0;
                // Wheel openings lift the lower edge of the outer side panel.
                for (int sample = 0; sample <= 60; sample++)
                {
                    float fraction = Mathf.Lerp(0.12f, 0.4f, sample / 60f);
                    float z = mid + (i < 2 ? 1 : -1) * length * fraction;
                    float bottom = float.PositiveInfinity;
                    int count = 0;
                    foreach (Vector3 v in vertices)
                    {
                        if (v.x * sign < width * 0.36f || Mathf.Abs(v.z - z) > length * 0.018f) continue;
                        bottom = Mathf.Min(bottom, v.y); count++;
                    }
                    if (count < 3 || bottom < min.y + 0.12f || bottom > min.y + 1f) continue;
                    if (bottom > bestHeight) { bestHeight = bottom; bestZ = z; }
                }
                if (float.IsNegativeInfinity(bestHeight))
                { EditorUtility.DisplayDialog("Колёса", "Не удалось надёжно определить все арки. Ничего не перемещено. Нужен скриншот раскрытого CarVisual.", "OK"); return; }
                float radius = Mathf.Clamp((bestHeight - min.y) * 0.48f, 0.27f, 0.48f);
                centers[i] = new Vector3(sign * width * 0.405f, min.y + radius + 0.015f, bestZ);
                radii[i] = radius;
            }
            Undo.RecordObject(car, "Fit wheel arches");
            for (int i = 0; i < 4; i++)
            {
                Undo.RecordObject(car.wheels[i], "Fit wheel physics");
                Undo.RecordObject(car.wheels[i].transform, "Fit wheel physics position");
                Undo.RecordObject(car.wheelVisuals[i], "Fit visual wheel");
                float scale = radii[i] / car.wheels[i].radius;
                car.wheelVisuals[i].localScale *= scale;
                car.wheelVisuals[i].localPosition = centers[i];
                // Match initial wheel pose to the suspension's target compression.
                car.wheels[i].transform.localPosition = centers[i] + Vector3.up * car.wheels[i].suspensionDistance * 0.5f;
                car.wheels[i].radius = radii[i];
            }
            Rigidbody body = car.GetComponent<Rigidbody>();
            Undo.RecordObject(body, "Car rigidbody settings");
            body.mass = 1450;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            EditorUtility.SetDirty(car);
            EditorSceneManager.MarkSceneDirty(car.gameObject.scene);
            EditorSceneManager.SaveScene(car.gameObject.scene);
            Selection.activeGameObject = car.gameObject;
            Debug.Log("[Dimari] Колёса подогнаны по точкам модели/нижней кромке арок. Геометрический подбор приблизительный: проверьте вид сбоку перед вождением. Изменения можно отменить через Undo.");
        }

        [MenuItem("Dimari/Машина — добавить недостающие колёса")]
        public static void AddMissingWheels()
        {
            if (Application.isPlaying) return;
            var car = Object.FindAnyObjectByType<CityDriveableCar>();
            if (car == null || car.wheels == null || car.wheels.Length != 4)
            { EditorUtility.DisplayDialog("Колёса", "Откройте город с установленной Dimari_CityCar.", "OK"); return; }
            string path = AssetDatabase.FindAssets("Sport_Wheel_1 t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == "Sport_Wheel_1");
            GameObject prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (prefab == null) { Debug.LogError("[Dimari] Не найден префаб Sport_Wheel_1."); return; }
            Undo.RecordObject(car, "Attach car wheels");
            if (car.wheelVisuals == null || car.wheelVisuals.Length != 4) car.wheelVisuals = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                if (car.wheelVisuals[i] != null) continue;
                GameObject pivot = new GameObject("Dimari_VisualWheel_" + i);
                Undo.RegisterCreatedObjectUndo(pivot, "Attach wheel");
                pivot.transform.SetParent(car.transform, false);
                pivot.transform.position = car.wheels[i].transform.position - car.transform.up * 0.1f;
                GameObject mesh = Object.Instantiate(prefab, pivot.transform);
                mesh.name = "WheelMesh";
                foreach (MonoBehaviour script in mesh.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                foreach (Collider collider in mesh.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (Rigidbody body in mesh.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                Renderer[] renderers = mesh.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { Undo.DestroyObjectImmediate(pivot); continue; }
                // Measure in the car's local axes even when it is parked at a nonzero yaw.
                Quaternion parkedRotation = car.transform.rotation;
                car.transform.rotation = Quaternion.identity;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Vector3 axle = bounds.size.x <= bounds.size.y && bounds.size.x <= bounds.size.z ? Vector3.right
                    : bounds.size.y <= bounds.size.z ? Vector3.up : Vector3.forward;
                mesh.transform.rotation = Quaternion.FromToRotation(axle, Vector3.right) * mesh.transform.rotation;
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float diameter = Mathf.Max(bounds.size.y, bounds.size.z);
                if (diameter > 0.001f) mesh.transform.localScale *= car.wheels[i].radius * 2f / diameter;
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                mesh.transform.position += pivot.transform.position - bounds.center;
                if (i % 2 == 0) mesh.transform.RotateAround(pivot.transform.position, Vector3.up, 180f);
                car.transform.rotation = parkedRotation;
                car.wheelVisuals[i] = pivot.transform;
            }
            EditorUtility.SetDirty(car);
            EditorSceneManager.MarkSceneDirty(car.gameObject.scene);
            EditorSceneManager.SaveScene(car.gameObject.scene);
            Selection.activeGameObject = car.gameObject;
            Debug.Log("[Dimari] Отдельные колёса добавлены и подключены к WheelCollider. Проверьте совпадение с арками; положение физических осей пока рассчитано по габаритам кузова.");
        }

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
