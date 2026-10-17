using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Dimari.Editor
{
    public static class PizzaExpressReplacement
    {
        [MenuItem("Dimari/Пицца — заменить выбранное office_building_3 и открыть проход")]
        public static void Replace()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {EditorUtility.DisplayDialog("Пиццерия","Сначала остановите Play Mode (синяя кнопка сверху). Изменения в Play не сохранятся.","OK");return;}
            Scene scene=SceneManager.GetActiveScene();
            GameObject selected=Selection.activeGameObject;
            if(scene.name!="Chapter2_Rogovo"||selected==null||selected.scene!=scene||selected.name!="office_building_3")
            {EditorUtility.DisplayDialog("Пиццерия","Откройте Chapter2_Rogovo и выберите именно office_building_3, как на скриншоте.","OK");return;}
            foreach(GameObject go in scene.GetRootGameObjects())
                if(go.name=="Dimari_PizzaExpress24")
                {EditorUtility.DisplayDialog("Пиццерия","Пиццерия уже существует. Удалите только прежний Dimari_PizzaExpress24, если хотите перенести её сюда, затем повторите команду.","OK");return;}
            Renderer renderer=selected.GetComponent<Renderer>();
            if(renderer==null||Shader.Find("Universal Render Pipeline/Lit")==null)return;
            Bounds old=renderer.bounds;
            Vector3 facing=SceneView.lastActiveSceneView!=null?SceneView.lastActiveSceneView.camera.transform.position-old.center:Vector3.back;
            facing.y=0;
            facing=Mathf.Abs(facing.x)>Mathf.Abs(facing.z)?Vector3.right*Mathf.Sign(facing.x):Vector3.forward*Mathf.Sign(facing.z);
            float extent=Mathf.Abs(facing.x)>0?old.extents.x:old.extents.z;
            Vector3 probe=old.center+facing*(extent+2);
            if(!Physics.Raycast(new Vector3(probe.x,old.min.y+3,probe.z),Vector3.down,out RaycastHit hit,10,~0,QueryTriggerInteraction.Ignore)||hit.normal.y<0.7f)
            {EditorUtility.DisplayDialog("Площадка","Не удалось определить землю перед зданием. Ничего не изменено. Пришлите вид сбоку и координаты площадки.","OK");return;}
            Vector3 site=new Vector3(old.center.x,hit.point.y+0.06f,old.center.z);
            Quaternion rotation=Quaternion.LookRotation(-facing,Vector3.up);
            if(!EditorUtility.DisplayDialog("Замена здания","Скрыть выбранное здание с его основанием и поставить здесь пиццерию? Фасад будет обращён к текущей стороне обзора Scene. Будет создан локальный проход со стороны дороги. Сцена не сохраняется автоматически.","Заменить","Отмена"))return;
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Replace office with pizza and open access");
            GameObject hide=selected;
            if(selected.transform.parent!=null&&selected.transform.parent.name=="office_building_3_with_base")hide=selected.transform.parent.gameObject;
            Undo.RecordObject(hide,"Hide old office");hide.SetActive(false);
            if(PrefabUtility.IsPartOfPrefabInstance(hide))PrefabUtility.RecordPrefabInstancePropertyModifications(hide);
            Physics.SyncTransforms();
            PizzaExpressSetup.BuildAt(site,rotation);
            GameObject pizza=GameObject.Find("Dimari_PizzaExpress24");
            if(pizza==null){Undo.RevertAllDownToGroup(undo);return;}
            // Local volumes subtract only this footprint and a 3.2 m entrance corridor.
            float length=Mathf.Max(9,extent+3);
            Matrix4x4 worldToLocal=Matrix4x4.TRS(site,rotation,Vector3.one).inverse;
            Bounds footprint=new Bounds(new Vector3(0,(old.max.y-site.y+0.3f)/2,0),
                new Vector3(old.size.x+0.4f,Mathf.Max(4,old.max.y-site.y+0.3f),old.size.z+0.4f));
            // Rotation is cardinal: swap x/z dimensions for east/west facing lots.
            if(Mathf.Abs(facing.x)>0.5f)footprint.size=new Vector3(old.size.z+0.4f,footprint.size.y,old.size.x+0.4f);
            footprint.SetMinMax(new Vector3(footprint.min.x,0.25f,footprint.min.z),footprint.max);
            Bounds corridor=new Bounds(new Vector3(-4.1f,1.6f,-(5+length)/2),new Vector3(3.2f,3,Mathf.Max(1,length-5)));
            int modified=0;
            foreach(GameObject go in scene.GetRootGameObjects())
                foreach(MeshCollider collider in go.GetComponentsInChildren<MeshCollider>())
                {
                    if(collider.transform.IsChildOf(pizza.transform)||collider.sharedMesh==null)continue;
                    Renderer visible=collider.GetComponent<Renderer>();
                    if(collider.name!="city_part_collider"&&visible!=null&&visible.enabled)continue;
                    if(Cut(collider,worldToLocal,footprint,corridor))modified++;
                }
            // Independent support avoids the floor-loss regression from the earlier administration repair.
            var path=GameObject.CreatePrimitive(PrimitiveType.Cube);path.name="Pizza access walkway";
            path.transform.SetParent(pizza.transform,false);path.transform.localPosition=new Vector3(-4.1f,-0.045f,-(5+length)/2);
            path.transform.localScale=new Vector3(3.15f,0.1f,Mathf.Max(1,length-5)+0.5f);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=new Color(0.32f,0.32f,0.3f);
            Directory.CreateDirectory("Assets/Dimari/Generated/PizzaExpress");AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath("Assets/Dimari/Generated/PizzaExpress/AccessConcrete.mat"));
            path.GetComponent<Renderer>().sharedMaterial=mat;
            Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();Physics.SyncTransforms();
            Selection.activeGameObject=pizza;
            EditorUtility.DisplayDialog("Замена выполнена","Старое здание скрыто. Локально изменено скрытых MeshCollider: "+modified+". Добавлена опора под проходом.\nПроверьте путь от дороги и дверь E в Play. Если осталась преграда — пришлите диагностику; видимые ограды и отдельные BoxCollider не удаляются автоматически. После проверки сохраните Ctrl+S. Ctrl+Z сразу после команды отменяет замену.","OK");
        }

        static bool Cut(MeshCollider collider,Matrix4x4 frame,Bounds footprint,Bounds corridor)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();int affected=0;
            using(var data=Mesh.AcquireReadOnlyMeshData(collider.sharedMesh))
            {
                var positions=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);
                data[0].GetVertices(positions);
                for(int sub=0;sub<data[0].subMeshCount;sub++)
                {
                    var desc=data[0].GetSubMesh(sub);if(desc.topology!=MeshTopology.Triangles)continue;
                    var triangles=new Unity.Collections.NativeArray<int>(desc.indexCount,Unity.Collections.Allocator.Temp);data[0].GetIndices(triangles,sub);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var polygon=new List<Vector3>();
                        for(int j=0;j<3;j++)polygon.Add(frame.MultiplyPoint3x4(collider.transform.TransformPoint(positions[triangles[i+j]])));
                        var pieces=new List<List<Vector3>>{polygon};
                        Vector3 normal=Vector3.Cross(polygon[1]-polygon[0],polygon[2]-polygon[0]).normalized;
                        foreach(Bounds box in new[]{footprint,corridor})
                        {
                            if(box.Equals(corridor)&&Mathf.Abs(normal.y)>0.45f)continue;
                            var remainder=new List<List<Vector3>>();
                            foreach(var piece in pieces)
                            {
                                Bounds b=new Bounds(piece[0],Vector3.zero);foreach(Vector3 p in piece)b.Encapsulate(p);
                                if(!b.Intersects(box)){remainder.Add(piece);continue;}
                                var inside=piece;
                                Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
                                float[] limits={box.max.x,-box.min.x,box.max.y,-box.min.y,box.max.z,-box.min.z};
                                for(int plane=0;plane<6&&inside.Count>=3;plane++)
                                {
                                    var outside=Clip(inside,normals[plane],limits[plane],false);
                                    if(outside.Count>=3)remainder.Add(outside);
                                    inside=Clip(inside,normals[plane],limits[plane],true);
                                }
                                if(inside.Count>=3)affected++;
                            }
                            pieces=remainder;
                        }
                        foreach(var piece in pieces)for(int k=1;k+1<piece.Count;k++)
                        {
                            int start=vertices.Count;
                            foreach(int j in new[]{0,k,k+1})vertices.Add(collider.transform.InverseTransformPoint(frame.inverse.MultiplyPoint3x4(piece[j])));
                            indices.Add(start);indices.Add(start+1);indices.Add(start+2);
                        }
                    }
                    triangles.Dispose();
                }
                positions.Dispose();
            }
            if(affected==0)return false;
            var mesh=new Mesh{name="Pizza local collision repair",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Dimari/Generated/PizzaExpress/Collision.asset"));
            Undo.RecordObject(collider,"Local pizza collision cut");collider.sharedMesh=mesh;EditorUtility.SetDirty(collider);
            if(PrefabUtility.IsPartOfPrefabInstance(collider))PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            return true;
        }
        static List<Vector3> Clip(List<Vector3> polygon,Vector3 normal,float limit,bool inside)
        {
            var result=new List<Vector3>();if(polygon.Count==0)return result;
            Vector3 a=polygon[polygon.Count-1];float da=Vector3.Dot(normal,a)-limit;
            foreach(Vector3 b in polygon)
            {
                float db=Vector3.Dot(normal,b)-limit;bool aa=inside?da<=0:da>=0,bb=inside?db<=0:db>=0;
                if(aa!=bb)result.Add(Vector3.LerpUnclamped(a,b,da/(da-db)));
                if(bb)result.Add(b);a=b;da=db;
            }
            return result;
        }
    }
}
