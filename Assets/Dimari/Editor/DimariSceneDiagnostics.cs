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
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
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
            LODGroup[] groups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
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
            foreach (var candidate in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
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
