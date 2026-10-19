using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Story;

namespace Dimari.Editor
{
    /// <summary>
    /// Проверяет точку обрыва (от неё строятся причал, корабль и капитан) и ищет
    /// открытое место, если текущая точка внутри скалы или гигантского объекта.
    /// </summary>
    public static class DimariCaptainSceneFix
    {
        const float SearchRadius = 35f;
        const float GiantThreshold = 25f;

        [MenuItem("Dimari — проверить точку обрыва и сцену капитана")]
        public static void Inspect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сцена капитана", "Остановите Play: проверка сцены выполняется в режиме редактирования.", "OK");
                return;
            }
            var manager = Object.FindAnyObjectByType<StoryQuestManager>(FindObjectsInactive.Include);
            if (manager == null || manager.cliffArrivalPoint == null)
            {
                EditorUtility.DisplayDialog("Сцена капитана",
                    "В открытой сцене нет StoryQuestManager или не назначена точка обрыва Cliff Arrival Point.\n"
                    + "Причал и корабль строятся от этой точки.", "OK");
                return;
            }

            Vector3 point = manager.cliffArrivalPoint.position;
            var log = new StringBuilder();
            log.AppendLine("[Dimari] Точка обрыва: " + point.ToString("F1") + " | объект " + PointPath(manager.cliffArrivalPoint));

            bool ground = Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out RaycastHit groundHit, 30f, ~0, QueryTriggerInteraction.Ignore);
            log.AppendLine(ground
                ? "Земля: " + groundHit.point.y.ToString("F1") + " | уклон " + Vector3.Angle(groundHit.normal, Vector3.up).ToString("F0") + "° | " + PointPath(groundHit.transform)
                : "Земля: НЕ найдена под точкой");

            int overlaps = 0;
            foreach (var collider in Physics.OverlapSphere(point + Vector3.up * 1.2f, 2.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == null || collider.isTrigger) continue;
                if (manager != null && collider.transform.IsChildOf(manager.transform)) continue;
                log.AppendLine("  перекрытие: " + PointPath(collider.transform) + " (" + collider.GetType().Name + ")");
                overlaps++;
            }

            var giants = FindGiants(point, SearchRadius);
            foreach (var giant in giants)
                log.AppendLine("  гигант: " + PointPath(giant.renderer.transform) + " | " + giant.size.ToString("F0") + " м | " + giant.distance.ToString("F0") + " м");

            Transform sceneShip = FindByName("Dimari_WaterShip") ?? FindByName("AssetStore_Ship_Model");
            if (sceneShip != null)
            {
                Renderer[] renderers = sceneShip.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    log.AppendLine("Корабль в сцене: " + PointPath(sceneShip) + " | габариты " + bounds.size.ToString("F1"));
                }
            }
            else log.AppendLine("Корабль в сцене: нет (создаётся при старте игры)");

            Debug.Log(log.ToString());
            bool blocked = overlaps > 0 || giants.Count > 0 || !ground;

            int choice = EditorUtility.DisplayDialogComplex("Точка обрыва и капитан",
                (blocked
                    ? "Точка обрыва выглядит заблокированной: перекрытий " + overlaps + ", гигантских объектов рядом " + giants.Count + ".\n"
                    : "Точка обрыва выглядит свободной: перекрытий " + overlaps + ", гигантских объектов рядом " + giants.Count + ".\n")
                + "Земля: " + (ground ? groundHit.point.y.ToString("F1") : "не найдена") + "\n\n"
                + "Найти открытое место рядом и переместить точку? Причал, корабль и капитан строятся от неё.\n"
                + "Подробности с путями объектов — в Console.",
                "Оставить как есть", "Найти чистое место", "Только отчёт");
            if (choice != 1) return;

            if (!FindCleanSpot(manager.cliffArrivalPoint.position, out Vector3 clean, out string reason))
            {
                EditorUtility.DisplayDialog("Чистое место не найдено",
                    "Не удалось найти открытую ровную площадку в радиусе " + SearchRadius.ToString("F0") + " м.\n" + reason
                    + "\n\nПоставьте пустой объект на безопасном краю обрыва, Z направьте к морю, и назначьте его в Cliff Arrival Point.", "OK");
                return;
            }
            Undo.RecordObject(manager.cliffArrivalPoint, "Move cliff arrival point");
            manager.cliffArrivalPoint.position = clean;
            EditorUtility.SetDirty(manager.cliffArrivalPoint);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeTransform = manager.cliffArrivalPoint;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[Dimari] Точка обрыва перемещена: " + point.ToString("F1") + " → " + clean.ToString("F1"));
            EditorUtility.DisplayDialog("Точка обрыва перемещена",
                "Новая точка: " + clean.ToString("F1") + "\n" + reason
                + "\n\nСохраните сцену Ctrl+S и проверьте в Play. Отмена — Ctrl+Z.", "OK");
        }

        struct Giant
        {
            public Renderer renderer;
            public float size;
            public float distance;
        }

        static List<Giant> FindGiants(Vector3 point, float radius)
        {
            var result = new List<Giant>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || !renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (size < GiantThreshold) continue;
                float distance = Vector3.Distance(point, bounds.ClosestPoint(point));
                if (distance > radius) continue;
                result.Add(new Giant { renderer = renderer, size = size, distance = distance });
            }
            result.Sort((a, b) => a.distance.CompareTo(b.distance));
            return result;
        }

        static bool FindCleanSpot(Vector3 origin, out Vector3 result, out string reason)
        {
            result = origin;
            reason = "";
            float best = float.PositiveInfinity;
            bool found = false;
            for (float radius = 4f; radius <= SearchRadius; radius += 4f)
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2f / 16f;
                    Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!Physics.Raycast(candidate + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Vector3.Angle(hit.normal, Vector3.up) > 25f) continue;
                    // Open sky: nothing solid directly above the future pier and ship.
                    if (Physics.Raycast(hit.point + Vector3.up * 1.5f, Vector3.up, out _, 30f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    bool blocked = false;
                    foreach (var collider in Physics.OverlapCapsule(hit.point + Vector3.up * 0.6f, hit.point + Vector3.up * 2.2f, 1.4f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (collider == null || collider.isTrigger) continue;
                        if (collider.GetComponentInParent<Terrain>() != null) continue;
                        if (collider is TerrainCollider) continue;
                        blocked = true;
                        break;
                    }
                    if (blocked) continue;
                    if (FindGiants(hit.point, 22f).Count > 0) continue;
                    if (radius < best)
                    {
                        best = radius;
                        result = hit.point + Vector3.up * 0.02f;
                        found = true;
                    }
                }
            if (!found) { reason = "Все проверенные точки внутри скал, под навесом или на склоне."; return false; }
            reason = "Свободная площадка в " + best.ToString("F0") + " м от прежней точки, над ней открытое небо.";
            return true;
        }

        static Transform FindByName(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (transform.name == name) return transform;
            return null;
        }

        static string PointPath(Transform t)
        {
            if (t == null) return "<null>";
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
