using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Dimari.Cinematics;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.Core
{
    /// <summary>
    /// Аварийные клавиши диагностики:
    /// F7 — скрыть самый крупный объект около камеры (повторять, пока чёрная плоскость не исчезнет),
    /// F8 — вернуть управление/HUD, F9 — отчёт на экране и в файле, F10 — принудительный LOD0.
    /// ESC дополнительно обрабатывается, если основной менеджер не обработал его сам.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class DimariRuntimeDiagnostics : MonoBehaviour
    {
        static DimariRuntimeDiagnostics instance;

        struct Candidate
        {
            public Renderer renderer;
            public float size;
            public float distance;
        }

        readonly List<Candidate> giants = new List<Candidate>();
        readonly List<Renderer> hidden = new List<Renderer>();
        bool showReport;
        string report = "";
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var existing = Object.FindAnyObjectByType<DimariRuntimeDiagnostics>();
            if (existing != null) { instance = existing; return; }
            var go = new GameObject("Dimari — аварийная диагностика (F7/F8/F9/F10)");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<DimariRuntimeDiagnostics>();
        }

        void Update()
        {
            if (Pressed(KeyCode.Escape))
            {
                var manager = FindManager(true);
                if (manager != null && StoryQuestManager.LastMenuHotkeyFrame != Time.frameCount)
                {
                    if (manager.IsMenuOpen) manager.ForceCloseMenu();
                    else manager.OpenPauseMenu();
                }
            }
            if (Pressed(KeyCode.F6)) HideUnderCrosshair();
            if (Pressed(KeyCode.F7)) HideLargest();
            if (Pressed(KeyCode.F8)) Recover();
            if (Pressed(KeyCode.F9)) { showReport = !showReport; if (showReport) BuildReport(true); }
            if (Pressed(KeyCode.F10)) ForceHighestDetail();
            if (showReport && Time.unscaledTime >= nextScan) { BuildReport(false); nextScan = Time.unscaledTime + 5f; }
        }

        /// <summary>Finds the manager even when its GameObject is inactive.</summary>
        static StoryQuestManager FindManager(bool activate)
        {
            var manager = StoryQuestManager.Instance ?? Object.FindAnyObjectByType<StoryQuestManager>();
            if (manager == null)
            {
                foreach (var any in Resources.FindObjectsOfTypeAll<StoryQuestManager>())
                {
                    if (any == null || any.gameObject.scene.name != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) continue;
                    manager = any;
                    break;
                }
            }
            if (activate && manager != null && !manager.gameObject.activeInHierarchy) manager.gameObject.SetActive(true);
            return manager;
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
                    case KeyCode.F6: return keyboard.f6Key.wasPressedThisFrame;
                    case KeyCode.F7: return keyboard.f7Key.wasPressedThisFrame;
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

        void HideUnderCrosshair()
        {
            Camera cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Dimari] F6: нет Camera.main."); return; }
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (!Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning("[Dimari] F6: под прицелом пусто — наведите камеру на объект.");
                return;
            }
            var renderer = hit.transform.GetComponent<Renderer>() ?? hit.transform.GetComponentInParent<Renderer>();
            if (renderer == null) { Debug.LogWarning("[Dimari] F6: у объекта нет рендерера: " + Path(hit.transform)); return; }
            renderer.enabled = false;
            hidden.Add(renderer);
            var material = renderer.sharedMaterial;
            Debug.LogWarning("[Dimari] F6: СКРЫТ объект под прицелом: " + Path(renderer.transform)
                + "\n  расстояние " + hit.distance.ToString("F2") + " м | размер " + renderer.bounds.size.ToString("F1")
                + " | материал " + (material != null ? material.name : "нет")
                + " | шейдер " + (material != null && material.shader != null ? material.shader.name : "НЕТ")
                + "\n  родитель: " + (renderer.transform.parent != null ? Path(renderer.transform.parent) : "нет")
                + "\nЕсли экран очистился — это и был тот объект. Отправьте эти строки.", renderer);
            BuildReport(true);
        }

        void HideLargest()
        {
            ScanGiants();
            foreach (var candidate in giants)
            {
                if (candidate.renderer == null || !candidate.renderer.enabled) continue;
                candidate.renderer.enabled = false;
                hidden.Add(candidate.renderer);
                Debug.LogWarning("[Dimari] F7: скрыт объект " + Path(candidate.renderer.transform)
                    + " | размер " + candidate.size.ToString("F1") + " м | материал "
                    + (candidate.renderer.sharedMaterial != null ? candidate.renderer.sharedMaterial.name : "нет")
                    + "\nЕсли чёрная плоскость исчезла — пришлите эту строку. F7 ещё раз скроет следующий.");
                BuildReport(true);
                return;
            }
            Debug.LogWarning("[Dimari] F7: больше крупных объектов рядом нет.");
        }

        void Recover()
        {
            var manager = FindManager(true);
            if (manager != null)
            {
                manager.RecoverGameplay();
                Debug.Log("[Dimari] F8: управление и HUD восстановлены.");
                return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            Debug.LogError("[Dimari] F8: StoryQuestManager отсутствует в этой сцене — освобождены только курсор и timeScale. "
                + "Меню не появится, пока не создана система задач: Dimari → Диагностика — создать систему меню и задач.");
        }

        static void ForceHighestDetail()
        {
            LODGroup[] groups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            foreach (var group in groups) if (group != null) group.ForceLOD(0);
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 2f);
            Debug.Log("[Dimari] F10: LOD0 для " + groups.Length + " групп, lodBias=" + QualitySettings.lodBias.ToString("F1"));
        }

        void ScanGiants()
        {
            giants.Clear();
            Camera cam = Camera.main;
            Vector3 origin = cam != null ? cam.transform.position : Vector3.zero;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || !renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (size < 6f) continue;
                float distance = Vector3.Distance(origin, bounds.center);
                if (distance > 90f) continue;
                giants.Add(new Candidate { renderer = renderer, size = size, distance = distance });
            }
            giants.Sort((a, b) => b.size.CompareTo(a.size));
        }

        void BuildReport(bool writeFile)
        {
            ScanGiants();
            var text = new StringBuilder();
            text.AppendLine("Dimari F9 | сцена " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                + " | timeScale " + Time.timeScale.ToString("F2"));
            var manager = FindManager(false);
            if (manager == null) text.AppendLine("StoryQuestManager: НЕТ (меню/ESC работать не могут)");
            else text.AppendLine("StoryQuestManager: есть | active=" + manager.gameObject.activeInHierarchy
                + " | меню=" + manager.IsMenuOpen + " | игра=" + manager.HasGameStarted + " | HUD=" + manager.IsHudVisible);
            var player = Object.FindAnyObjectByType<ForestPlayerController>();
            text.AppendLine(player == null ? "Игрок: НЕТ" : "Игрок: управление=" + player.IsControlEnabled + " | " + player.transform.position.ToString("F1"));
            text.AppendLine("ESC-фолбэк: активен | скрыто F6/F7: " + hidden.Count);
            Camera viewCamera = Camera.main;
            if (viewCamera != null && Physics.Raycast(viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out RaycastHit center, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                var centerRenderer = center.transform.GetComponent<Renderer>() ?? center.transform.GetComponentInParent<Renderer>();
                var centerMaterial = centerRenderer != null ? centerRenderer.sharedMaterial : null;
                text.AppendLine("ПОД ПРИЦЕЛОМ: " + Short(Path(center.transform), 64)
                    + "\n    " + center.distance.ToString("F1") + " м | размер "
                    + (centerRenderer != null ? centerRenderer.bounds.size.ToString("F1") : "нет рендерера")
                    + " | " + (centerMaterial != null ? centerMaterial.name : "нет материала")
                    + " | " + (centerMaterial != null && centerMaterial.shader != null ? centerMaterial.shader.name : "шейдер НЕТ"));
            }
            else text.AppendLine("ПОД ПРИЦЕЛОМ: пусто");
            text.AppendLine("Крупные объекты рядом (6+ м), по размеру:");
            int index = 0;
            foreach (var candidate in giants)
            {
                if (index++ >= 10) break;
                var material = candidate.renderer.sharedMaterial;
                text.AppendLine((candidate.renderer.enabled ? "• " : "× ") + Short(Path(candidate.renderer.transform), 64)
                    + "\n    " + candidate.size.ToString("F0") + " м | " + candidate.distance.ToString("F0") + " м от камеры | "
                    + (material != null ? material.name : "нет материала") + " | "
                    + (material != null && material.shader != null ? material.shader.name : "шейдер НЕТ"));
            }
            if (giants.Count == 0) text.AppendLine("  крупных объектов рядом нет");
            report = text.ToString();
            if (!writeFile) return;
            Debug.Log("[Dimari] " + report);
            try
            {
                string projectFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "DimariReport.txt"));
                File.WriteAllText(projectFile, report);
                File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "DimariReport.txt"), report);
                Debug.Log("[Dimari] Отчёт записан: " + projectFile);
            }
            catch (System.Exception e) { Debug.LogWarning("[Dimari] Не удалось записать отчёт: " + e.Message); }
        }

        static string Short(string value, int limit)
        {
            return value.Length <= limit ? value : "…" + value.Substring(value.Length - limit + 1);
        }

        void OnGUI()
        {
            if (!showReport) return;
            float width = Mathf.Min(Screen.width - 30f, 900f);
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.Clamp(Screen.height / 55, 13, 19),
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
            GUI.depth = -300;
            GUI.Box(new Rect(15f, 15f, width, Screen.height * 0.62f), report, style);
            GUI.Box(new Rect(15f, Screen.height * 0.62f + 20f, width, 34f),
                "F9 — закрыть • F7 — скрыть самый крупный объект • F8 — вернуть управление • F10 — LOD0", style);
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
