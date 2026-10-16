using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dimari.Editor
{
    public static class DimariOfficeSetup
    {
        private const string Folder = "Assets/Office Room Furniture";
        private const string Source = Folder + "/Demo/Demo Scene.unity";
        private const string Destination = "Assets/Dimari/Scenes/Rogovo_Administration.unity";

        [MenuItem("Dimari/Администрация — подготовить офис")]
        public static void Prepare()
        {
            if (Application.isPlaying) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!AssetDatabase.IsValidFolder(Folder) || !File.Exists(Source) || shader == null)
            {
                EditorUtility.DisplayDialog("Администрация", "Нужны Office Room Furniture и URP/Lit. Ожидаемая сцена: " + Source, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Подготовка офиса",
                "Будет создана отдельная сцена администрации. Материалы ТОЛЬКО Office Room Furniture будут настроены для URP. Их изменения затронут также демосцену пакета. Лес и город не обрабатываются. Продолжить?", "Подготовить", "Отмена")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Dimari/Scenes");
            AssetDatabase.Refresh();
            if (!File.Exists(Destination) && !AssetDatabase.CopyAsset(Source, Destination))
            {
                Debug.LogError("[Dimari] Не удалось скопировать офис.");
                return;
            }
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Imported FBX submaterials are read-only: do not mutate model importer data.
                if (!path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) continue;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                Undo.RecordObject(material, "Prepare office material");
                bool alreadyUrp = material.shader != null && material.shader.name.StartsWith("Universal Render Pipeline/");
                if (!alreadyUrp)
                {
                    Texture color = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    Color tint = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    Vector2 scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
                    Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
                    material.shader = shader;
                    material.SetTexture("_BaseMap", color);
                    material.SetColor("_BaseColor", tint);
                    material.SetTextureScale("_BaseMap", scale);
                    material.SetTextureOffset("_BaseMap", offset);
                }
                // Old demo materials can have mirror-like highlights after conversion.
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
                if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null)
                    material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Scene scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            // Baked Built-in lighting/reflection data is not portable to URP. Clear only the copy.
            Lightmapping.Clear();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>(true))
                {
                    probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                    probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
                    probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                    EditorUtility.SetDirty(probe);
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Dimari] Подготовлена копия офиса: " + Destination + ". Материалов обработано: " + changed +
                ". Проверьте вид комнаты. Вход, коллайдеры и переход из города ещё не настроены.");
            EditorUtility.DisplayDialog("Администрация", "Открыта копия Rogovo_Administration. Старые запечённые отражения и освещение очищены; материалы офиса обработаны. Пришлите скриншот комнаты без режима Prefab. Вход и переход настроим отдельно.", "OK");
        }
    }
}
