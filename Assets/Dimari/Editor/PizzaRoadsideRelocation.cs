using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.City;

namespace Dimari.Editor
{
    public static class PizzaRoadsideRelocation
    {
        [MenuItem("Dimari/Пицца — перенести к дороге вместо выбранного здания")]
        public static void Move()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")
            {EditorUtility.DisplayDialog("Перенос","Остановите Play и откройте Chapter2_Rogovo.","OK");return;}
            GameObject pizza=GameObject.Find("Dimari_PizzaExpress24"),target=Selection.activeGameObject;
            if(pizza==null||target==null||target.scene!=pizza.scene||target.transform.IsChildOf(pizza.transform)||pizza.transform.IsChildOf(target.transform))
            {EditorUtility.DisplayDialog("Перенос","Выделите одноэтажное здание справа, которое нужно убрать. Не выбирайте Roof пиццерии или весь город. В Scene смотрите на выбранное здание со стороны дороги.","OK");return;}
            if(target.name.Contains("city_part")||target.name.Contains("collider"))return;
            Renderer[] renderers=target.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            Bounds bounds=renderers[0].bounds;foreach(Renderer renderer in renderers)bounds.Encapsulate(renderer.bounds);
            if(bounds.size.y>8||bounds.size.x>90||bounds.size.z>90||bounds.size.x<3||bounds.size.z<3)
            {EditorUtility.DisplayDialog("Здание","Размер выбранного объекта не похож на отдельное одноэтажное здание. Выберите его целиком в Hierarchy, но не группу квартала.","OK");return;}
            Vector3 toward=SceneView.lastActiveSceneView!=null?SceneView.lastActiveSceneView.camera.transform.position-bounds.center:-pizza.transform.forward;
            toward.y=0;
            toward=Mathf.Abs(toward.x)>Mathf.Abs(toward.z)?Vector3.right*Mathf.Sign(toward.x):Vector3.forward*Mathf.Sign(toward.z);
            float extent=Mathf.Abs(toward.x)>0?bounds.extents.x:bounds.extents.z;
            Vector3 probe=bounds.center+toward*(extent+2);
            Physics.SyncTransforms();
            if(!Physics.Raycast(new Vector3(probe.x,bounds.min.y+3,probe.z),Vector3.down,out RaycastHit ground,12,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<0.85f)
            {EditorUtility.DisplayDialog("Земля","Не найдена ровная опора перед зданием. Ничего не изменено. Пришлите выбранный объект и координаты в Inspector.","OK");return;}
            // Place the new facade just behind the old street-facing edge, instead of in the rear courtyard.
            Vector3 destination=bounds.center+toward*(extent-5.8f);destination.y=ground.point.y+0.06f;
            Quaternion rotation=Quaternion.LookRotation(-toward,Vector3.up);
            foreach(Collider obstacle in Physics.OverlapBox(destination+Vector3.up*1.8f,new Vector3(6.2f,1.4f,5.2f),rotation,~0,QueryTriggerInteraction.Ignore))
            {
                if(obstacle.transform.IsChildOf(target.transform)||obstacle.transform.IsChildOf(pizza.transform))continue;
                Renderer visible=obstacle.GetComponent<Renderer>();
                if(visible!=null&&visible.enabled&&obstacle.name!="city_part_collider")
                {EditorUtility.DisplayDialog("Площадка занята","Новая пиццерия пересекает «"+obstacle.name+"». Ничего не изменено. Пришлите выделенное здание и общий вид площадки.","OK");return;}
            }
            if(!EditorUtility.DisplayDialog("Перенос пиццерии","Скрыть «"+target.name+"» и перенести готовую пиццерию к его краю со стороны текущего обзора Scene? Интерьер и посетители сохранятся. Сцена не сохраняется автоматически.","Перенести","Отмена"))return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Move pizza roadside and remove selected shop");
            Matrix4x4 oldInverse=pizza.transform.worldToLocalMatrix;Quaternion oldRotation=pizza.transform.rotation;
            var visitors=new List<CityLifeAgent>();var points=new List<Vector3>();var rotations=new List<Quaternion>();
            foreach(var guest in Object.FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None))
            {
                if(!guest.seated||guest.gameObject.scene!=pizza.scene||guest.transform.IsChildOf(pizza.transform))continue;
                Vector3 local=oldInverse.MultiplyPoint3x4(guest.transform.position);
                if(Mathf.Abs(local.x)>6.2f||Mathf.Abs(local.z)>5.2f)continue;
                visitors.Add(guest);points.Add(local);rotations.Add(Quaternion.Inverse(oldRotation)*guest.transform.rotation);
            }
            // Plate positions are stored in world space; move them too, along with props parented to pizza.
            var meals=new List<PizzaGuestEating>();var plates=new List<Vector3>();
            foreach(var meal in Object.FindObjectsByType<PizzaGuestEating>(FindObjectsSortMode.None))
            {
                if(meal.gameObject.scene!=pizza.scene)continue;
                Vector3 local=oldInverse.MultiplyPoint3x4(meal.platePosition);
                if(Mathf.Abs(local.x)>6.2f||Mathf.Abs(local.z)>5.2f)continue;
                meals.Add(meal);plates.Add(local);
            }
            Undo.RecordObject(target,"Hide roadside building");target.SetActive(false);
            if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            Undo.RecordObject(pizza.transform,"Move completed pizzeria");pizza.transform.SetPositionAndRotation(destination,rotation);
            for(int i=0;i<visitors.Count;i++)
            {Undo.RecordObject(visitors[i].transform,"Move pizza visitor");visitors[i].transform.SetPositionAndRotation(pizza.transform.TransformPoint(points[i]),rotation*rotations[i]);}
            for(int i=0;i<meals.Count;i++){Undo.RecordObject(meals[i],"Move plate target");meals[i].platePosition=pizza.transform.TransformPoint(plates[i]);}
            int cut=PizzaExpressReplacement.ClearReplacementSite(pizza.transform,bounds);
            Transform oldPath=pizza.transform.Find("Pizza access walkway");
            if(oldPath!=null){Undo.RecordObject(oldPath.gameObject,"Hide old long approach");oldPath.gameObject.SetActive(false);}
            Transform previous=pizza.transform.Find("Roadside access");if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
            var path=GameObject.CreatePrimitive(PrimitiveType.Cube);path.name="Roadside access";path.transform.SetParent(pizza.transform,false);
            path.transform.localPosition=new Vector3(-4.1f,-0.015f,-6.5f);path.transform.localScale=new Vector3(3.1f,0.1f,4.5f);
            var foundation=pizza.transform.Find("Foundation");if(foundation!=null)path.GetComponent<Renderer>().sharedMaterial=foundation.GetComponent<Renderer>().sharedMaterial;
            Undo.RegisterCreatedObjectUndo(path,"Supported road access");
            Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(pizza.scene);AssetDatabase.SaveAssets();Physics.SyncTransforms();Selection.activeGameObject=pizza;SceneView.lastActiveSceneView?.FrameSelected();
            EditorUtility.DisplayDialog("Перенос готов","Пиццерия перенесена, выбранное здание скрыто. Перенесено внешних посетителей: "+visitors.Count+". Локально изменено коллайдеров: "+cut+".\nПроверьте подход от дороги, отсутствие пересечений с соседями, пол и дверь E в Play. Затем Ctrl+S. Если нужно отменить — Ctrl+Z сразу после команды. Маркер следует за входом автоматически.","OK");
        }
    }
}
