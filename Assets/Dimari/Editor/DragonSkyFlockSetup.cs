using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dimari.Atmosphere;

namespace Dimari.Editor
{
    public static class DragonSkyFlockSetup
    {
        [MenuItem("Dimari/Драконы — настроить стаю в сцене")]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Драконы", "Сначала остановите Play, чтобы сохранить настройки в сцене.", "OK");
                return;
            }
            GameObject prefab = DragonSkyFlock.FindImportedDragon();
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Драконы", "Префаб Dragon PBR не найден. Проверьте импорт пакета.", "OK");
                return;
            }
            DragonSkyFlock flock = Object.FindAnyObjectByType<DragonSkyFlock>();
            if (flock == null)
            {
                GameObject root = new GameObject("Dimari_DragonSkyFlock");
                Undo.RegisterCreatedObjectUndo(root, "Create dragon flock");
                flock = Undo.AddComponent<DragonSkyFlock>(root);
            }
            Undo.RecordObject(flock, "Configure dragon flock");
            flock.dragonPrefab = prefab;
            // Store explicit references so the models and animation are included in builds.
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLowerInvariant().Contains("dragon")) continue;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip == null) continue;
                    string name = clip.name.ToLowerInvariant();
                    if (name.Contains("fly") || name.Contains("flight") || name.Contains("glide"))
                    { flock.flightClip = clip; break; }
                }
                if (flock.flightClip != null) break;
            }
            EditorUtility.SetDirty(flock);
            EditorSceneManager.MarkSceneDirty(flock.gameObject.scene);
            Selection.activeGameObject = flock.gameObject;
            Debug.Log("[Dimari] Стая настроена: " + AssetDatabase.GetAssetPath(prefab) + ". Сохраните сцену Ctrl+S.");
        }
    }
}
