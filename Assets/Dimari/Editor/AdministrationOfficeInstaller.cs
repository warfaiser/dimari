using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dimari.Editor
{
    public static class AdministrationOfficeInstaller
    {
        const string Path = "Assets/Dimari/Scenes/Rogovo_Administration.unity";
        const string Group = "Dimari_OfficeRealWorldScale";

        [MenuItem("Dimari/Администрация — исправить размер и посадить женщину")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Path) == null)
            { EditorUtility.DisplayDialog("Кабинет", "Не найдена сцена " + Path, "OK"); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AdministrationWoman/Generated/AdministrationWoman.prefab") == null)
            { EditorUtility.DisplayDialog("Персонаж", "Сначала выполните команду подготовки женщины.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Path) scene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Single);
            GameObject chair = null, desk = null, group = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "OfficeChair_1") chair = t.gameObject;
                    if (t.name == "OfficeDesk_1") desk = t.gameObject;
                    if (t.name == Group) group = t.gameObject;
                    if (t.name == "Dimari_SeatedAdministrationWoman")
                    { EditorUtility.DisplayDialog("Кабинет", "Женщина уже размещена. Повторная установка остановлена, чтобы не создавать дубликат.", "OK"); return; }
                }
            if (chair == null || !GetBounds(chair, out Bounds bounds) || bounds.size.y < 0.01f)
            { EditorUtility.DisplayDialog("Кабинет", "Не найдено кресло OfficeChair_1 с видимой геометрией. Сцена не изменена.", "OK"); return; }
            if (group == null)
            {
                float factor = 1.15f / bounds.size.y;
                if (!EditorUtility.DisplayDialog("Размер кабинета", "Высота кресла сейчас: " + bounds.size.y.ToString("F2") + " м.\nКомната, мебель и точка появления будут масштабированы вместе в " + factor.ToString("F3") + " раза, до высоты кресла 1.15 м.\nСцена будет помечена изменённой, но не сохранена автоматически. Продолжить?", "Исправить", "Отмена")) return;
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Normalize administration office");
                GameObject[] roots = scene.GetRootGameObjects();
                group = new GameObject(Group);
                group.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                Undo.RegisterCreatedObjectUndo(group, "Office scale group");
                foreach (GameObject root in roots) Undo.SetTransformParent(root.transform, group.transform, "Group office");
                Undo.RecordObject(group.transform, "Scale office");
                group.transform.localScale = Vector3.one * factor;
                Undo.CollapseUndoOperations(undo);
            }
            // Source chair uses an X=-90 import rotation, so its local forward is not the facing direction.
            // Face the desk instead, without rotating the chair mesh.
            Selection.activeGameObject = chair;
            AdministrationWomanSeating.Seat();
            GameObject woman = Selection.activeGameObject;
            if (woman != null && woman.name == "Dimari_SeatedAdministrationWoman" && desk != null && GetBounds(desk, out Bounds deskBounds))
            {
                Animator animator = woman.GetComponentInChildren<Animator>();
                Transform hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                Vector3 pivot = hips != null ? hips.position : woman.transform.position;
                Vector3 direction = Vector3.ProjectOnPlane(deskBounds.center - pivot, Vector3.up);
                if (direction.sqrMagnitude > 0.001f)
                {
                    Undo.RecordObject(woman.transform, "Face office desk");
                    Quaternion delta = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Inverse(woman.transform.rotation);
                    woman.transform.rotation = delta * woman.transform.rotation;
                    woman.transform.position = pivot + delta * (woman.transform.position - pivot);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Dimari] Размер кабинета исправлен по креслу высотой 1.15 м. Проверьте посадку, пол и появление героя; затем сохраните сцену Ctrl+S. Исходные ассеты мебели не изменены.");
        }

        static bool GetBounds(GameObject obj, out Bounds bounds)
        {
            bounds = new Bounds(); bool found = false;
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            { if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds); }
            return found;
        }
    }
}
