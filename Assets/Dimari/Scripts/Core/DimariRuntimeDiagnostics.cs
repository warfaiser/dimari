using System.Text;
using UnityEngine;
using Dimari.Cinematics;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.Core
{
    /// <summary>
    /// Аварийные клавиши, которые работают независимо от сюжетного менеджера:
    /// F8 — вернуть управление и HUD, F9 — показать, что перед камерой и что происходит,
    /// F10 — принудительно включить максимальную детализацию LOD (убирает billboard-квады).
    /// ESC дополнительно обрабатывается, если основной менеджер не успел обработать его сам.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class DimariRuntimeDiagnostics : MonoBehaviour
    {
        static DimariRuntimeDiagnostics instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var existing = Object.FindAnyObjectByType<DimariRuntimeDiagnostics>();
            if (existing != null) { instance = existing; return; }
            var go = new GameObject("Dimari — аварийная диагностика (F8/F9/F10)");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<DimariRuntimeDiagnostics>();
        }

        void Update()
        {
            if (Pressed(KeyCode.Escape))
            {
                var story = StoryQuestManager.Instance;
                // Only when the normal ESC handler did not run this frame (broken/stuck manager).
                if (story != null && StoryQuestManager.LastMenuHotkeyFrame != Time.frameCount)
                {
                    if (story.IsMenuOpen) story.ForceCloseMenu();
                    else story.OpenPauseMenu();
                }
            }
            if (Pressed(KeyCode.F8)) Recover();
            if (Pressed(KeyCode.F9)) Inspect();
            if (Pressed(KeyCode.F10)) ForceHighestDetail();
        }

        static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                switch (key)
                {
                    case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                    case KeyCode.F8: return keyboard.f8Key.wasPressedThisFrame;
                    case KeyCode.F9: return keyboard.f9Key.wasPressedThisFrame;
                    case KeyCode.F10: return keyboard.f10Key.wasPressedThisFrame;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        static void Recover()
        {
            var story = StoryQuestManager.Instance;
            if (story != null)
            {
                story.RecoverGameplay();
                Debug.Log("[Dimari] F8: управление и HUD восстановлены.");
                return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            Debug.LogWarning("[Dimari] F8: StoryQuestManager не найден — освобождены курсор и timeScale.");
        }

        static void ForceHighestDetail()
        {
            LODGroup[] groups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            foreach (var group in groups) if (group != null) group.ForceLOD(0);
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 2f);
            Debug.Log("[Dimari] F10: принудительный LOD0 для " + groups.Length + " групп, lodBias=" + QualitySettings.lodBias.ToString("F1"));
        }

        static void Inspect()
        {
            var log = new StringBuilder();
            log.AppendLine("[Dimari] ===== F9: диагностика сцены =====");
            log.AppendLine("Сцена: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                + " | timeScale=" + Time.timeScale.ToString("F2"));

            var story = StoryQuestManager.Instance;
            if (story == null) log.AppendLine("StoryQuestManager: НЕТ");
            else log.AppendLine("Меню=" + story.IsMenuOpen + " | игра начата=" + story.HasGameStarted
                + " | HUD=" + story.IsHudVisible + " | планшет=" + story.IsTabletViewerOpen);
            var player = Object.FindAnyObjectByType<ForestPlayerController>();
            if (player == null) log.AppendLine("Игрок: НЕТ");
            else log.AppendLine("Игрок: управление=" + player.IsControlEnabled + " | позиция=" + player.transform.position.ToString("F1"));
            log.AppendLine("Курсор: " + Cursor.lockState + " | виден=" + Cursor.visible);
            log.AppendLine("Кат-сцены: приём=" + DonsportReceptionCutscene.IsRunning
                + " | Мэйби Бэйби=" + DonsportMaybeBabyCutscene.IsRunning);

            Camera cam = Camera.main;
            if (cam == null) log.AppendLine("Camera.main: НЕТ");
            else
            {
                log.AppendLine("Камера: " + cam.name + " @ " + cam.transform.position.ToString("F1"));
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
                {
                    log.AppendLine("ПОД ПРИЦЕЛОМ: " + Path(hit.transform) + " | " + hit.distance.ToString("F1") + " м");
                    var renderer = hit.transform.GetComponent<Renderer>();
                    if (renderer == null) renderer = hit.transform.GetComponentInParent<Renderer>();
                    if (renderer != null)
                    {
                        log.AppendLine("  размер: " + renderer.bounds.size.ToString("F1") + " | меш: "
                            + (renderer is SkinnedMeshRenderer ? "skinned" : "static"));
                        var material = renderer.sharedMaterial;
                        log.AppendLine("  материал: " + (material != null ? material.name : "НЕТ")
                            + " | шейдер: " + (material != null && material.shader != null ? material.shader.name : "НЕТ/ОШИБКА"));
                    }
                    var lod = hit.transform.GetComponentInParent<LODGroup>();
                    if (lod != null)
                    {
                        LOD[] levels = lod.GetLODs();
                        log.AppendLine("  LODGroup: " + Path(lod.transform) + " | уровней: " + levels.Length
                            + " | renderers LOD0: " + (levels.Length > 0 ? levels[0].renderers.Length : 0));
                    }
                }
                else log.AppendLine("ПОД ПРИЦЕЛОМ: пусто (луч 1000 м)");

                log.AppendLine("Крупные объекты рядом (до 45 м, размер > 7 м):");
                int found = 0;
                Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    Bounds b = r.bounds;
                    float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (size < 7f) continue;
                    if (Vector3.Distance(cam.transform.position, b.center) > 45f) continue;
                    log.AppendLine("  • " + Path(r.transform) + " | " + b.size.ToString("F1")
                        + " | " + (r.sharedMaterial != null ? r.sharedMaterial.name : "нет материала"));
                    if (++found >= 15) break;
                }
                if (found == 0) log.AppendLine("  нет");
            }
            log.Append("======================================");
            Debug.Log(log.ToString());
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
