using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dimari.Editor
{
    public static class AdministrationWomanSetup
    {
        const string Root = "Assets/AdministrationWoman";
        const string Model = Root + "/source/Buhalter.fbx";
        const string Output = Root + "/Generated";

        [MenuItem("Dimari/Администрация — подготовить женщину")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("Персонаж", "Не найден " + Model + ". Импортируйте папки source и textures в " + Root, "OK");
                return;
            }
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (source == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Материалы", "Не найден URP/Lit. Настройка остановлена; старые материалы не изменены.", "OK");
                return;
            }
            Material[] materials = new Material[2];
            for (int i = 0; i < 2; i++)
            {
                Texture2D color = FindTexture("buh3_packed" + i + "_diffuse", false);
                Texture2D normal = FindTexture("buh3_packed" + i + "_normal", true);
                Texture2D specular = FindTexture("buh3_packed" + i + "_specular", false);
                if (color == null || normal == null || specular == null)
                {
                    EditorUtility.DisplayDialog("Текстуры", "Не найдены все три текстуры buh3_packed" + i + " в " + Root + ". Префаб не создан.", "OK");
                    return;
                }
                string path = Output + "/Woman_Packed" + i + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetTexture("_BaseMap", color);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_WorkflowMode", 0);
                mat.EnableKeyword("_SPECULAR_SETUP");
                mat.SetTexture("_SpecGlossMap", specular);
                mat.EnableKeyword("_SPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 0.25f);
                EditorUtility.SetDirty(mat);
                materials[i] = mat;
            }
            GameObject instance = UnityEngine.Object.Instantiate(source);
            instance.name = "AdministrationWoman";
            try
            {
                int unresolved = 0;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string name = slots[i] != null ? slots[i].name.ToLowerInvariant() : "";
                        // Do not infer atlas assignment from slot order: an incorrect atlas corrupts UV appearance.
                        int atlas = name.Contains("packed0") ? 0 : name.Contains("packed1") ? 1 : -1;
                        if (atlas < 0 && slots[i] != null && slots[i].mainTexture != null)
                        {
                            string texture = slots[i].mainTexture.name.ToLowerInvariant();
                            atlas = texture.Contains("packed0") ? 0 : texture.Contains("packed1") ? 1 : -1;
                        }
                        if (atlas >= 0) slots[i] = materials[atlas];
                        else { unresolved++; Debug.LogWarning("[Dimari] Не определён атлас материала: " + name + " у " + renderer.name); }
                    }
                    renderer.sharedMaterials = slots;
                }
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                bool valid = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
                PrefabUtility.SaveAsPrefabAsset(instance, Output + "/AdministrationWoman.prefab");
                AssetDatabase.SaveAssets();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/AdministrationWoman.prefab");
                EditorGUIUtility.PingObject(Selection.activeObject);
                EditorUtility.DisplayDialog("Персонаж подготовлен",
                    "Прежняя сцена не изменена. Префаб: " + Output + "/AdministrationWoman.prefab\nHumanoid: " + (valid ? "OK" : "нужна проверка Configure / Console") +
                    "\nМатериалов без определённого атласа: " + unresolved +
                    "\nПришлите скриншот этого окна и предпросмотра префаба. Анимации и размещение пока не добавлены.", "OK");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static Texture2D FindTexture(string name, bool normal)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (importer.textureType != type || (!normal && !importer.sRGBTexture))
                    {
                        importer.textureType = type;
                        if (!normal) importer.sRGBTexture = true;
                        importer.SaveAndReimport();
                    }
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }
    }
}
