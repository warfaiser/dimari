using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Story;

namespace Dimari.Editor
{
    public static class DimariSceneDiagnostics
    {
        struct Item
        {
            public Renderer renderer;
            public float size;
        }

        [MenuItem("Dimari/Диагностика — найти гигантский объект в Scene")]
        public static void FindGiant()
        {
            var scene = SceneManager.GetActiveScene();
            var items = new List<Item>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>())
            {
                if (renderer == null || renderer.gameObject.scene != scene) continue;
                Bounds bounds = renderer.bounds;
                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (size >= 7f) items.Add(new Item { renderer = renderer, size = size });
            }
            items.Sort((a, b) => b.size.CompareTo(a.size));

            var log = new StringBuilder();
            log.AppendLine("[Dimari] Гигантские объекты в сцене " + scene.name + ": " + items.Count);
            for (int i = 0; i < items.Count && i < 20; i++)
            {
                var r = items[i].renderer;
                string shader = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "НЕТ";
                log.AppendLine("• " + Path(r.transform) + " | " + r.bounds.size.ToString("F1")
                    + " | " + (r.sharedMaterial != null ? r.sharedMaterial.name : "нет материала") + " | " + shader);
                Debug.Log("[Dimari] Гигант: " + Path(r.transform) + " | размер " + r.bounds.size.ToString("F1")
                    + " | центр " + r.bounds.center.ToString("F1"), r);
            }
            Debug.Log(log.ToString());

            if (items.Count == 0)
            {
                EditorUtility.DisplayDialog("Гигантский объект",
                    "Объектов размером больше 7 м не найдено. Пришлите в Console результат F9 во время Play — там будет имя объекта под прицелом.", "OK");
                return;
            }

            Selection.activeGameObject = items[0].renderer.gameObject;
            EditorGUIUtility.PingObject(items[0].renderer.gameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
            int choice = EditorUtility.DisplayDialogComplex("Самый крупный объект",
                "Найден и выделен:\n" + Path(items[0].renderer.transform)
                + "\nРазмер: " + items[0].renderer.bounds.size.ToString("F1")
                + "\nМатериал: " + (items[0].renderer.sharedMaterial != null ? items[0].renderer.sharedMaterial.name : "нет")
                + "\n\nПолный список — в Console.\nОтключить этот объект (можно отменить Ctrl+Z)?",
                "Оставить и закрыть", "Отключить объект", "Только выделить");
            if (choice == 1)
            {
                Undo.RecordObject(items[0].renderer, "Disable giant renderer");
                items[0].renderer.enabled = false;
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[Dimari] Отключён гигантский рендерер: " + Path(items[0].renderer.transform), items[0].renderer);
            }
        }

        [MenuItem("Dimari/Диагностика — список LOD-групп (чёрные деревья)")]
        public static void ListLods()
        {
            LODGroup[] groups = Object.FindObjectsByType<LODGroup>();
            var log = new StringBuilder();
            log.AppendLine("[Dimari] LOD-групп в сцене: " + groups.Length);
            int billboardLike = 0;
            foreach (var group in groups)
            {
                if (group == null) continue;
                LOD[] levels = group.GetLODs();
                if (levels.Length <= 1) continue;
                var last = levels[levels.Length - 1].renderers;
                string material = "нет";
                for (int i = 0; i < last.Length; i++)
                {
                    if (last[i] == null || last[i].sharedMaterial == null) continue;
                    material = last[i].sharedMaterial.name + " / " + (last[i].sharedMaterial.shader != null ? last[i].sharedMaterial.shader.name : "НЕТ");
                    break;
                }
                if (material.ToLowerInvariant().Contains("billboard") || material.ToLowerInvariant().Contains("tree"))
                {
                    billboardLike++;
                    Debug.Log("[Dimari] Возможный billboard-квад: " + Path(group.transform) + " | уровней " + levels.Length + " | " + material, group);
                }
            }
            Debug.Log(log.ToString() + "Похожих на billboard групп: " + billboardLike
                + "\nF10 в Play принудительно включает LOD0 у всех групп.");
            EditorUtility.DisplayDialog("LOD-диагностика",
                "LOD-групп: " + groups.Length + "\nПохожих на billboard: " + billboardLike
                + "\n\nПодробности с путями объектов — в Console.", "OK");
        }

        [MenuItem("Dimari/Корабль — убрать заглушку StylShip_Box из сцены")]
        public static void RemoveShipPlaceholder()
        {
            var scene = SceneManager.GetActiveScene();
            int removed = 0;
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (filter == null || filter.sharedMesh == null) continue;
                string meshName = filter.sharedMesh.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
                if (!meshName.Contains("stylshipbox") && !meshName.Contains("shipbox") && !meshName.Contains("shipcollider")) continue;
                Transform victim = filter.transform;
                for (Transform t = filter.transform; t != null; t = t.parent)
                    if (t.name == "Dimari_WaterShip" || t.name == "AssetStore_Ship_Model") victim = t;
                Debug.Log("[Dimari] Удалена заглушка корабля «" + Path(victim) + "» (меш " + filter.sharedMesh.name + ").", victim);
                Object.DestroyImmediate(victim.gameObject);
                removed++;
            }

            int cleared = 0;
            foreach (var manager in Object.FindObjectsByType<StoryQuestManager>())
            {
                if (manager == null || manager.shipPrefab == null) continue;
                bool placeholder = false;
                foreach (MeshFilter filter in manager.shipPrefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    string meshName = filter.sharedMesh.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
                    if (meshName.Contains("stylshipbox") || meshName.Contains("shipbox") || meshName.Contains("shipcollider"))
                    { placeholder = true; break; }
                }
                if (!placeholder) continue;
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("shipPrefab").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                cleared++;
            }

            if (removed > 0 || cleared > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[Dimari] Готово: удалено объектов — " + removed + ", очищено полей Ship Prefab — " + cleared
                    + ". Сохраните сцену (Ctrl+S), чтобы заглушка больше не появлялась.");
            }
            else
            {
                Debug.Log("[Dimari] Заглушка StylShip_Box в сцене не найдена — всё чисто.");
            }
            EditorUtility.DisplayDialog("Dimari",
                removed + cleared > 0
                    ? "Заглушка корабля удалена. Сохраните сцену (Ctrl+S).\nУдалено объектов: " + removed + ", очищено Ship Prefab: " + cleared
                    : "Заглушка StylShip_Box не найдена — сцена чистая.",
                "OK");
        }

        [MenuItem("Dimari/Диагностика — корабль, капитан и точка обрыва")]
        public static void CaptainCheck()
        {
            var manager = Object.FindAnyObjectByType<StoryQuestManager>(FindObjectsInactive.Include);
            var report = new StringBuilder();
            Camera camera = Camera.main;
            report.AppendLine("[Dimari] Проверка сцены капитана");
            if (manager == null)
            {
                report.AppendLine("StoryQuestManager: НЕТ в открытой сцене.");
            }
            else
            {
                Transform cliff = manager.cliffArrivalPoint;
                report.AppendLine(cliff == null
                    ? "Точка обрыва: НЕ назначена (Cliff Arrival Point)."
                    : "Точка обрыва: " + Path(cliff) + " @ " + cliff.position.ToString("F1"));
                if (cliff != null)
                {
                    bool ground = Physics.Raycast(cliff.position + Vector3.up * 3f, Vector3.down,
                        out RaycastHit groundHit, 40f, ~0, QueryTriggerInteraction.Ignore);
                    report.AppendLine(ground
                        ? "  земля под точкой: " + groundHit.point.y.ToString("F1") + " | уклон "
                          + Vector3.Angle(groundHit.normal, Vector3.up).ToString("F0") + "°"
                        : "  земля под точкой: НЕ найдена");
                    int blockers = 0;
                    foreach (var collider in Physics.OverlapSphere(cliff.position + Vector3.up * 1.2f, 2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (collider == null || collider.isTrigger) continue;
                        if (collider.transform.IsChildOf(manager.transform)) continue;
                        report.AppendLine("  перекрытие: " + Path(collider.transform));
                        blockers++;
                    }
                    if (blockers == 0) report.AppendLine("  перекрытий у точки нет");
                }
            }
            Transform ship = null;
            foreach (var candidate in Object.FindObjectsByType<Transform>())
            {
                if (candidate.name == "Dimari_WaterShip" || candidate.name == "AssetStore_Ship_Model") { ship = candidate; break; }
            }
            if (ship == null) report.AppendLine("Корабль в сцене: нет (создаётся при старте игры).");
            else
            {
                Renderer[] renderers = ship.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) report.AppendLine("Корабль: " + Path(ship) + " | рендереров нет");
                else
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    report.AppendLine("Корабль: " + Path(ship) + " | габариты " + bounds.size.ToString("F1")
                        + " | масштаб " + ship.localScale.ToString("F1"));
                }
            }
            if (camera != null && Physics.Raycast(camera.ViewportPointToRay(new Vector3(0.5f, 0.5f)),
                out RaycastHit center, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                report.AppendLine("Под прицелом камеры: " + Path(center.transform) + " | "
                    + center.distance.ToString("F1") + " м");
            }
            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Корабль, капитан и точка обрыва",
                "Отчёт записан в Console (окно Console должно быть открыто).\n\n"
                + "Если в Play коричневая стена — нажмите F9 и F6: они покажут точное имя объекта.", "OK");
        }

        [MenuItem("Dimari/Корабль — превью сцены обрыва в Scene")]
        public static void PreviewCliffScene()
        {
            var manager = Object.FindFirstObjectByType<StoryQuestManager>();
            if (manager == null || manager.cliffArrivalPoint == null)
            {
                EditorUtility.DisplayDialog("Dimari", "В сцене нет StoryQuestManager с меткой Cliff Arrival Point.", "OK");
                return;
            }
            RemoveCliffPreview();
            Physics.SyncTransforms();
            Transform cliff = manager.cliffArrivalPoint;
            var root = new GameObject("Dimari_CliffPreview");
            root.transform.SetPositionAndRotation(cliff.position, cliff.rotation);

            float drop = Mathf.Clamp(manager.shipWaterDropBelowCliff, 2.5f, 40f);
            float waterLevel = cliff.position.y - drop;
            float waterSize = Mathf.Clamp(manager.cutsceneWaterSize, 60f, 600f);
            float shipSize = Mathf.Max(8.5f, manager.shipModelSize);

            Material water = NewMaterial(new Color(0.08f, 0.32f, 0.42f, 0.94f));
            Material wood = NewMaterial(new Color(0.36f, 0.24f, 0.14f, 1f));
            Material dark = NewMaterial(new Color(0.24f, 0.15f, 0.09f, 1f));

            Box("Water", root.transform, new Vector3(0f, -drop, waterSize * 0.4f),
                new Vector3(waterSize, 0.12f, waterSize), water);

            Vector3 flatForward = Vector3.ProjectOnPlane(cliff.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;
            Vector3 origin = new Vector3(cliff.position.x, waterLevel, cliff.position.z);
            float gap = shipSize * 1.5f;
            for (float distance = 12f; distance <= 90f; distance += 2f)
            {
                bool empty = true;
                foreach (float lateral in new[] { 0f, -6f, 6f })
                {
                    Vector3 probe = origin + flatForward * distance + flatRight * lateral;
                    foreach (RaycastHit hit in Physics.RaycastAll(probe + Vector3.up * 150f, Vector3.down, 320f))
                    {
                        if (hit.collider is TerrainCollider || hit.point.y > waterLevel + 0.4f) { empty = false; break; }
                    }
                    if (!empty) break;
                }
                if (empty) { gap = distance; break; }
            }
            float shipDistance = gap + Mathf.Max(4f, shipSize * 0.3f);
            float hullFront = shipDistance - shipSize * 0.5f - 2.5f;

            int steps = Mathf.Clamp(Mathf.CeilToInt(drop / 0.26f), 6, 64);
            float rise = drop / steps, run = 0.38f, first = 0.9f;
            for (int i = 0; i < steps; i++)
                Box("Step" + i, root.transform,
                    new Vector3(0f, -rise * (i + 1) - 0.11f, first + run * (i + 0.5f)),
                    new Vector3(2.6f, 0.22f, run + 0.05f), i % 2 == 0 ? wood : dark);

            float landingZ = first + run * steps + 1.6f;
            Box("Landing", root.transform, new Vector3(0f, -drop + 0.02f, landingZ + 2.4f),
                new Vector3(4.6f, 0.22f, 7f), wood);
            float walkwayStart = landingZ + 5.9f;
            float walkwayEnd = Mathf.Max(walkwayStart + 1f, hullFront);
            if (walkwayEnd - walkwayStart > 1.5f)
                Box("Walkway", root.transform, new Vector3(0f, -drop + 0.02f, (walkwayStart + walkwayEnd) * 0.5f),
                    new Vector3(2.2f, 0.2f, walkwayEnd - walkwayStart), wood);

            Vector3 shipPosition = root.transform.TransformPoint(new Vector3(0f, 0f, shipDistance));
            shipPosition.y = waterLevel;
            GameObject ship = null;
            if (manager.shipPrefab != null) ship = Object.Instantiate(manager.shipPrefab);
            if (ship == null)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    string lower = path.ToLowerInvariant();
                    if (!(lower.Contains("ship") || lower.Contains("pirate") || lower.Contains("galleon"))) continue;
                    if (name.Contains("box") || name.Contains("collider") || name.Contains("proxy")) continue;
                    ship = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (ship != null) break;
                }
            }
            if (ship != null)
            {
                GameObject instance = Object.Instantiate(ship, root.transform);
                instance.name = "PreviewShip";
                instance.transform.position = shipPosition;
                instance.transform.rotation = root.transform.rotation * Quaternion.Euler(0f, 15f, 0f);
            }
            else Debug.LogWarning("[Dimari] Для превью не найден корабль. Назначьте его в поле Ship Prefab.");

            Debug.Log("[Dimari] Превью сцены обрыва построено: проём " + gap.ToString("F0") + " м, корабль "
                + shipDistance.ToString("F0") + " м, вода на " + waterLevel.ToString("F1") + " м. "
                + "Превью удаляется автоматически при Play или пунктом «Корабль — убрать превью сцены обрыва».", root);
        }

        [MenuItem("Dimari/Корабль — убрать превью сцены обрыва")]
        public static void RemoveCliffPreview()
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                if (go != null && go.name == "Dimari_CliffPreview") Object.DestroyImmediate(go);
        }

        static Material NewMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        static void Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        static string Path(Transform t)
        {
            if (t == null) return "<null>";
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
