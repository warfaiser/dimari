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

        [MenuItem("Dimari/Администрация — автоматически настроить вход")]
        public static void AutoEntrance()
        {
            if (Application.isPlaying) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Chapter2_Rogovo")
            { EditorUtility.DisplayDialog("Вход", "Откройте Chapter2_Rogovo.", "OK"); return; }
            var entrance = Object.FindAnyObjectByType<Dimari.Story.AdministrationEntrance>();
            if (entrance == null)
            { EditorUtility.DisplayDialog("Вход", "Сначала создайте вход командой «создать вход в городе».", "OK"); return; }
            // Use the existing entrance as an approximate anchor, not SceneView's changing pivot.
            Vector3 seed = entrance.transform.position;
            Transform building = null;
            float best = float.PositiveInfinity;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name != "office_building_1_with_base (2)") continue;
                    float distance = Vector3.ProjectOnPlane(item.position - seed, Vector3.up).sqrMagnitude;
                    if (distance < best) { best = distance; building = item; }
                }
            if (building == null)
            { EditorUtility.DisplayDialog("Вход", "Не найдено выбранное здание office_building_1_with_base (2). Ничего не изменено.", "OK"); return; }
            Vector3 inward = Vector3.ProjectOnPlane(building.position - seed, Vector3.up).normalized;
            if (inward.sqrMagnitude < 0.1f) inward = building.forward;
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            bool found = false;
            Vector3 chosen = seed;
            // Prefer the current approach; search towards the door, then laterally around obstructions.
            float[] offsets = { 0f, 1f, -1f, 2f, -2f };
            foreach (float lateral in offsets)
            {
                for (int step = 3; step >= -2; step--)
                {
                    Vector3 candidate = seed + inward * step * 0.5f + side * lateral;
                    RaycastHit[] hits = Physics.RaycastAll(candidate + Vector3.up * 15f, Vector3.down, 40f,
                        ~0, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (RaycastHit hit in hits)
                    {
                        if (hit.normal.y < 0.8f || Mathf.Abs(hit.point.y - seed.y) > 5f) continue;
                        Vector3 feet = hit.point + Vector3.up * 0.05f;
                        if (Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.5f,
                            0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        chosen = feet; found = true; break;
                    }
                    if (found) break;
                }
                if (found) break;
            }
            if (!found)
            { EditorUtility.DisplayDialog("Вход", "Не найден безопасный пол рядом со входом. Общий коллайдер города не изменён. Пришлите Console и скриншот входа.", "OK"); return; }
            Undo.RecordObject(entrance.transform, "Automatic administration entrance");
            entrance.transform.SetPositionAndRotation(chosen, Quaternion.LookRotation(inward));
            entrance.destinationScene = Destination;
            entrance.interactionDistance = 2.5f;
            Transform oldSign = entrance.transform.Find("AdministrationSign");
            if (oldSign != null) Undo.DestroyObjectImmediate(oldSign.gameObject);
            GameObject sign = new GameObject("AdministrationSign");
            Undo.RegisterCreatedObjectUndo(sign, "Administration sign");
            sign.transform.SetParent(entrance.transform, false);
            sign.transform.localPosition = new Vector3(0, 2.7f, 0.5f);
            TextMesh label = sign.AddComponent<TextMesh>();
            label.text = "АДМИНИСТРАЦИЯ\nРОГОВО";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.045f;
            label.color = new Color(1f, 0.88f, 0.5f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(item => item.path == Destination);
            scenes.Add(new EditorBuildSettingsScene(Destination, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorUtility.SetDirty(entrance);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = entrance.gameObject;
            Debug.Log("[Dimari] Вход автоматически поставлен на свободную поверхность: " + chosen +
                ". Добавлена вывеска, кабинет включён в Scene List, город сохранён. Проверка полного маршрута от улицы и ракурса камеры требуется в Play.");
        }

        [MenuItem("Dimari/Администрация — создать вход в городе")]
        public static void CreateEntrance()
        {
            if (Application.isPlaying) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Chapter2_Rogovo")
            { EditorUtility.DisplayDialog("Вход", "Откройте Chapter2_Rogovo и приблизьте Scene к двери администрации.", "OK"); return; }
            if (Object.FindAnyObjectByType<Dimari.Story.AdministrationEntrance>() != null)
            { EditorUtility.DisplayDialog("Вход", "Вход уже создан. Найдите AdministrationEntrance в Hierarchy.", "OK"); return; }
            GameObject entrance = new GameObject("AdministrationEntrance");
            Undo.RegisterCreatedObjectUndo(entrance, "Administration entrance");
            entrance.AddComponent<Dimari.Story.AdministrationEntrance>();
            if (SceneView.lastActiveSceneView != null) entrance.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = entrance;
            if (System.IO.File.Exists(Destination))
            {
                var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                scenes.RemoveAll(item => item.path == Destination);
                scenes.Add(new EditorBuildSettingsScene(Destination, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Dimari] Поставьте AdministrationEntrance ПЕРЕД дверью на уровне ног, снаружи стены. Синяя ось Z направлена в дверь. Сохраните город. Точка создана в центре обзора, не на поверхности автоматически.");
        }

        [MenuItem("Dimari/Администрация — создать точку в кабинете")]
        public static void CreateOfficeArrival()
        {
            if (Application.isPlaying) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Destination)
            { EditorUtility.DisplayDialog("Кабинет", "Откройте Rogovo_Administration.", "OK"); return; }
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "AdministrationArrivalPoint") { Selection.activeGameObject = root; return; }
            GameObject point = new GameObject("AdministrationArrivalPoint");
            Undo.RegisterCreatedObjectUndo(point, "Office arrival");
            if (SceneView.lastActiveSceneView != null) point.transform.position = SceneView.lastActiveSceneView.pivot;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
                    MeshCollider collider = Undo.AddComponent<MeshCollider>(mesh.gameObject);
                    collider.sharedMesh = mesh.sharedMesh;
                }
            Selection.activeGameObject = point;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Dimari] Поставьте AdministrationArrivalPoint на свободный пол кабинета, не в мебель. Z — направление взгляда. Проверьте коллайдеры пола и сохраните сцену.");
        }

        [MenuItem("Dimari/Администрация — осветить интерьер")]
        public static void LightInterior()
        {
            if (Application.isPlaying) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Destination)
            {
                EditorUtility.DisplayDialog("Свет", "Сначала откройте Assets/Dimari/Scenes/Rogovo_Administration.unity", "OK");
                return;
            }
            Transform room = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "OfficeRoom") room = child;
            if (room == null)
            {
                EditorUtility.DisplayDialog("Свет", "Не найден OfficeRoom. Пришлите скриншот раскрытой Hierarchy.", "OK");
                return;
            }
            Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y < 1.8f || bounds.size.x > 100f || bounds.size.z > 100f)
            {
                EditorUtility.DisplayDialog("Свет", "Необычный масштаб OfficeRoom. Нужна проверка размеров перед расстановкой света.", "OK");
                return;
            }
            GameObject rig = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Dimari_OfficeLighting") rig = root;
            if (rig != null) Undo.DestroyObjectImmediate(rig);
            rig = new GameObject("Dimari_OfficeLighting");
            Undo.RegisterCreatedObjectUndo(rig, "Office lighting");
            float ceiling = bounds.max.y - 0.45f;
            // Four soft lights distribute illumination without depending on old baked data.
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    GameObject lamp = new GameObject("Ceiling soft light");
                    lamp.transform.SetParent(rig.transform);
                    lamp.transform.position = new Vector3(bounds.center.x + x * bounds.size.x * 0.22f,
                        ceiling, bounds.center.z + z * bounds.size.z * 0.22f);
                    Light light = lamp.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.94f, 0.84f);
                    light.intensity = 2f;
                    light.range = Mathf.Max(bounds.size.x, bounds.size.z, bounds.size.y) * 1.4f;
                    light.shadows = LightShadows.None;
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    light.renderMode = LightRenderMode.ForcePixel;
                }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.38f);
            RenderSettings.fog = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = rig;
            Debug.Log("[Dimari] Добавлен мягкий свет в копию администрации. Яркость ламп регулируется параметром Intensity. Проверьте Scene/Game в URP.");
        }

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
