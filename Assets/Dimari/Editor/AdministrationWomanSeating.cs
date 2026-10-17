using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dimari.Editor
{
    public static class AdministrationWomanSeating
    {
        [MenuItem("Dimari/Администрация — посадить женщину на выбранное кресло")]
        static void Seat()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject chair = Selection.activeGameObject;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Rogovo_Administration" || chair == null || chair.scene != scene)
            {
                EditorUtility.DisplayDialog("Кресло", "Откройте сцену Rogovo_Administration и выделите в Hierarchy нужное кресло целиком (не отдельную деталь).", "OK");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AdministrationWoman/Generated/AdministrationWoman.prefab");
            if (prefab == null) { EditorUtility.DisplayDialog("Персонаж", "Сначала выполните подготовку женщины.", "OK"); return; }
            Renderer[] renderers = chair.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y > 2f || bounds.size.x > 2f || bounds.size.z > 2f)
            {
                EditorUtility.DisplayDialog("Кресло", "Выбран слишком большой объект. Выделите только кресло, а не всю комнату.", "OK"); return;
            }
            var woman = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(woman, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(woman, "Seat administration woman");
            woman.name = "Dimari_SeatedAdministrationWoman";
            Animator animator = woman.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                Undo.DestroyObjectImmediate(woman);
                EditorUtility.DisplayDialog("Аватар", "Нужен корректный Humanoid-аватар.", "OK"); return;
            }
            // Store a static pose. A controller would overwrite these transforms on entering Play.
            animator.applyRootMotion = false;
            animator.enabled = false;
            Bounds body = new Bounds(); bool first = true;
            foreach (Renderer r in woman.GetComponentsInChildren<Renderer>())
            { if (first) { body = r.bounds; first = false; } else body.Encapsulate(r.bounds); }
            if (!first && body.size.y > 0.01f) woman.transform.localScale *= 1.65f / body.size.y;
            Vector3 forward = Vector3.ProjectOnPlane(chair.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            woman.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Aim(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, forward - right * 0.12f);
            Aim(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, forward + right * 0.12f);
            Aim(animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, Vector3.down);
            Aim(animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, Vector3.down);
            Aim(animator, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, forward);
            Aim(animator, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, forward);
            Aim(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, Vector3.down - right * 0.15f);
            Aim(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, Vector3.down + right * 0.15f);
            Aim(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, forward + Vector3.down * 0.25f);
            Aim(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, forward + Vector3.down * 0.25f);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            // Initial estimate from the chair's overall bounds, not an exact seat mesh measurement.
            Vector3 seat = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.46f, bounds.center.z);
            woman.transform.position += seat + Vector3.up * 0.13f - hips.position;
            EditorUtility.SetDirty(woman);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = woman;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorUtility.DisplayDialog("Женщина посажена", "Создана статичная сидячая поза. Высота сиденья и направление оценены по креслу: проверьте сбоку в Scene и пришлите скриншот. Сцена не сохранена автоматически; Ctrl+Z отменяет создание. Анимации и диалоги не добавлены.", "OK");
        }

        static void Aim(Animator animator, HumanBodyBones bone, HumanBodyBones child, Vector3 direction)
        {
            Transform a = animator.GetBoneTransform(bone), b = animator.GetBoneTransform(child);
            if (a == null || b == null || (b.position - a.position).sqrMagnitude < 0.000001f) return;
            a.rotation = Quaternion.FromToRotation(b.position - a.position, direction.normalized) * a.rotation;
        }
    }
}
