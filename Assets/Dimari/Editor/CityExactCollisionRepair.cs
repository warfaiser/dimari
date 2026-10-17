using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dimari.Editor
{
    public static class CityExactCollisionRepair
    {
        [MenuItem("Dimari/Город — исправить невидимую стену на дороге")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "Chapter2_Rogovo")
            {
                EditorUtility.DisplayDialog("Коллизия города", "Остановите Play и откройте сцену Chapter2_Rogovo в редакторе.", "OK");
                return;
            }
            var proxies = new List<MeshCollider>();
            var meshes = new List<MeshFilter>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "city_part_collider" || t.parent == null || t.parent.name != "demo_city_by_versatile_studio") continue;
                Transform main = t.parent.Find("city_part_main");
                if (main == null) continue;
                MeshCollider[] candidates = t.GetComponents<MeshCollider>();
                if (candidates.Length == 0) continue;
                // Preflight before touching the proxy: all active visual meshes need static collision.
                var local = new List<MeshFilter>();
                foreach (MeshFilter filter in main.GetComponentsInChildren<MeshFilter>())
                {
                    Renderer renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
                    if (filter.sharedMesh == null || filter.GetComponentInParent<Rigidbody>() != null)
                    {
                        EditorUtility.DisplayDialog("Коллизия города", "Неподходящая геометрия: " + filter.name + ". Изменения не применены.", "OK");
                        return;
                    }
                    local.Add(filter);
                }
                if (local.Count == 0) continue;
                proxies.AddRange(candidates);
                foreach (MeshFilter filter in local) if (!meshes.Contains(filter)) meshes.Add(filter);
            }
            if (proxies.Count == 0 || meshes.Count == 0)
            {
                EditorUtility.DisplayDialog("Коллизия города", "Не найдена пара city_part_collider / city_part_main из отчёта. Ничего не отключено.", "OK");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace city proxy with exact visible collision");
            try
            {
                foreach (MeshFilter filter in meshes)
                {
                    MeshCollider collider = filter.GetComponent<MeshCollider>();
                    if (collider == null) collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                    else Undo.RecordObject(collider, "Exact city mesh collision");
                    collider.enabled = false;
                    collider.convex = false;
                    collider.isTrigger = false;
                    collider.sharedMesh = filter.sharedMesh;
                    collider.enabled = true;
                    if (collider.sharedMesh != filter.sharedMesh || !collider.enabled)
                        throw new InvalidOperationException("Не удалось назначить коллайдер: " + filter.name);
                    EditorUtility.SetDirty(collider);
                }
                foreach (MeshCollider proxy in proxies)
                {
                    Undo.RecordObject(proxy, "Disable obsolete city collision proxy");
                    proxy.enabled = false;
                    EditorUtility.SetDirty(proxy);
                }
                Physics.SyncTransforms();
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("Коллизия города", "Точная коллизия видимой геометрии: " + meshes.Count
                    + ". Отключено упрощённых MeshCollider: " + proxies.Count
                    + ".\nПроверьте пешком и на машине переход перед Донспортом. Затем сохраните сцену Ctrl+S. Отмена: Ctrl+Z вне Play.\nГрафы населения не перестроены; после проверки можно запустить исправление населения.", "OK");
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
                EditorUtility.DisplayDialog("Коллизия города", "Изменения отменены: " + error.Message, "OK");
            }
        }
    }
}
