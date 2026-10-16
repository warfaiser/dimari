using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dimari.Editor
{
    public static class DimariPassageRepair
    {
        [MenuItem("Dimari/Администрация — восстановить пол за проходом")]
        public static void RestoreVisibleGround()
        {
            if (Application.isPlaying)
            { EditorUtility.DisplayDialog("Пол", "Остановите Play и откройте Chapter2_Rogovo.", "OK"); return; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Chapter2_Rogovo") return;
            int added = 0, existing = 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore visible city ground collision");
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    // These are the actual rendered street/pavement meshes, NOT city_part_collider.
                    bool street = filter.name == "city_part_demo_main1" || filter.name == "city_part_demo_main2"
                        || filter.name == "city_part_highway";
                    bool administration = false;
                    for (Transform parent = filter.transform; parent != null; parent = parent.parent)
                        if (parent.name == "office_building_1_with_base (2)") administration = true;
                    if ((!street && !administration) || filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
                    var renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled) continue;
                    // Do not replace or disable existing collision surfaces.
                    if (filter.GetComponent<Collider>() != null) { existing++; continue; }
                    MeshCollider collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    collider.isTrigger = false;
                    EditorUtility.SetDirty(collider);
                    added++;
                }
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // Report actual support on both sides of the diagnosed wall, rather than claiming success blindly.
            foreach (float z in new[] { 38.6f, 40.2f, 42f, 45f })
            {
                Vector3 origin = new Vector3(-58.58f, 12f, z);
                RaycastHit hit;
                if (Physics.Raycast(origin, Vector3.down, out hit, 30f, ~0, QueryTriggerInteraction.Ignore))
                    Debug.Log("[Dimari] Пол: Z=" + z + ", Y=" + hit.point.y + ", поверхность=" + hit.collider.name + ", нормаль=" + hit.normal);
                else Debug.LogWarning("[Dimari] Пол: НЕТ ОПОРЫ при Z=" + z + ". Нужна дополнительная проверка геометрии.");
            }
            Debug.Log("[Dimari] Коллизия видимой дороги/площадки: добавлено=" + added + ", уже было=" + existing +
                ". Вырез прохода не отменён. Город сохранён. Проверьте путь пешком; новые коллайдеры могут выявить реальные бордюры и ступени.");
        }

        [MenuItem("Dimari/Администрация — убрать невидимую стену прохода")]
        public static void Repair()
        {
            if (Application.isPlaying)
            { EditorUtility.DisplayDialog("Проход", "Сначала остановите Play. Изменения нужно сохранить в городской сцене.", "OK"); return; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Chapter2_Rogovo") return;
            MeshCollider collider = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshCollider item in root.GetComponentsInChildren<MeshCollider>(true))
                    if (item.name == "city_part_collider") collider = item;
            if (collider == null || collider.sharedMesh == null)
            { Debug.LogError("[Dimari] Не найден city_part_collider."); return; }
            // Exact world-space obstruction reported by the player's capsule cast.
            // Keep the ground and cut only steep collision faces near that point.
            Bounds opening = new Bounds(new Vector3(-58.58f, 1.7f, 39.36f), new Vector3(3.2f, 3.1f, 1.4f));
            if (!EditorUtility.DisplayDialog("Локальный проход", "Создать в копии коллайдера проход шириной 3.2 м в месте диагностированной стены? Пол и исходный меш ассета сохраняются. Изменится только эта городская сцена.", "Создать", "Отмена")) return;
            Mesh source = collider.sharedMesh;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            int affected = 0;
            using (var data = Mesh.AcquireReadOnlyMeshData(source))
            {
                var positions = new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount, Unity.Collections.Allocator.Temp);
                data[0].GetVertices(positions);
                for (int sub = 0; sub < data[0].subMeshCount; sub++)
                {
                    var descriptor = data[0].GetSubMesh(sub);
                    if (descriptor.topology != MeshTopology.Triangles) continue;
                    var triangles = new Unity.Collections.NativeArray<int>(descriptor.indexCount, Unity.Collections.Allocator.Temp);
                    data[0].GetIndices(triangles, sub);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        var polygon = new List<Vector3> {
                            collider.transform.TransformPoint(positions[triangles[t]]),
                            collider.transform.TransformPoint(positions[triangles[t+1]]),
                            collider.transform.TransformPoint(positions[triangles[t+2]]) };
                        Vector3 normal = Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]).normalized;
                        Bounds triangleBounds = new Bounds(polygon[0], Vector3.zero);
                        triangleBounds.Encapsulate(polygon[1]); triangleBounds.Encapsulate(polygon[2]);
                        if (Mathf.Abs(normal.y) > 0.45f || !triangleBounds.Intersects(opening))
                        { Emit(polygon, collider.transform, vertices, indices); continue; }
                        // Subtract a box, not entire triangles: large city faces retain their outside portions.
                        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                        float[] limits = { opening.max.x, -opening.min.x, opening.max.y, -opening.min.y, opening.max.z, -opening.min.z };
                        for (int plane = 0; plane < 6 && polygon.Count >= 3; plane++)
                        {
                            var outside = Clip(polygon, normals[plane], limits[plane], false);
                            Emit(outside, collider.transform, vertices, indices);
                            polygon = Clip(polygon, normals[plane], limits[plane], true);
                        }
                        if (polygon.Count >= 3) affected++;
                    }
                    triangles.Dispose();
                }
                positions.Dispose();
            }
            if (affected == 0)
            { Debug.LogWarning("[Dimari] В выбранной зоне не найдена вертикальная преграда. Возможно, проход уже вырезан. Коллайдер не изменён."); return; }
            Mesh mesh = new Mesh { name = "RogovoCollision_WithAdministrationPassage", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            if (!AssetDatabase.IsValidFolder("Assets/Dimari/Generated")) AssetDatabase.CreateFolder("Assets/Dimari", "Generated");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Dimari/Generated/RogovoCollision_Passage.asset");
            AssetDatabase.CreateAsset(mesh, path);
            Undo.RecordObject(collider, "Open administration collision passage");
            collider.sharedMesh = mesh;
            EditorUtility.SetDirty(collider);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); Physics.SyncTransforms();
            Debug.Log("[Dimari] Создан локальный проход в city_part_collider. Затронуто треугольников: " + affected +
                ". Исходный меш не изменён. Проверьте проход и ступени в Play. Undo восстанавливает ссылку на прежний коллайдер.");
        }
        private static List<Vector3> Clip(List<Vector3> polygon, Vector3 normal, float limit, bool keepInside)
        {
            var result = new List<Vector3>();
            if (polygon.Count == 0) return result;
            Vector3 a = polygon[polygon.Count - 1];
            float da = Vector3.Dot(normal, a) - limit;
            foreach (Vector3 b in polygon)
            {
                float db = Vector3.Dot(normal, b) - limit;
                bool acceptA = keepInside ? da <= 0 : da >= 0;
                bool acceptB = keepInside ? db <= 0 : db >= 0;
                if (acceptA != acceptB) result.Add(Vector3.LerpUnclamped(a, b, da / (da - db)));
                if (acceptB) result.Add(b);
                a = b; da = db;
            }
            return result;
        }
        private static void Emit(List<Vector3> polygon, Transform transform, List<Vector3> vertices, List<int> indices)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                if (Vector3.Cross(polygon[i] - polygon[0], polygon[i+1] - polygon[0]).sqrMagnitude < 0.00000001f) continue;
                int start = vertices.Count;
                vertices.Add(transform.InverseTransformPoint(polygon[0]));
                vertices.Add(transform.InverseTransformPoint(polygon[i]));
                vertices.Add(transform.InverseTransformPoint(polygon[i+1]));
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }
        }
    }
}
