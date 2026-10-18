using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dimari.City;
using Dimari.Cinematics;

namespace Dimari.Editor
{
    public static class DonsportGymUpgrade
    {
        public const string Stamp="Donsport gym exercise upgrade v2";
        const string Art="Assets/Dimari/Art/DonsportStory/";
        [MenuItem("Dimari/Донспорт — исправить лицо и включить тренировки")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var root=GameObject.Find("Dimari_Donsport");
            if(root==null){EditorUtility.DisplayDialog("Донспорт","Откройте Chapter2_Rogovo с построенным Донспортом.","OK");return;}
            ApplyTo(root,true);
        }
        public static void ApplyTo(GameObject root,bool report)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||root.scene.name!="Chapter2_Rogovo")return;
            var story=root.GetComponent<DonsportReceptionCutscene>();
            if(story==null||story.maybeBaby==null){if(report)EditorUtility.DisplayDialog("Донспорт","Сначала установите разговор Елизаветы.","OK");return;}
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Donsport face and exercises");
            try
            {
                // Repair material on already installed scenes too, not only on fresh creation.
                AssetDatabase.ImportAsset(Art+"MaybeBabyFace.png",ImportAssetOptions.ForceUpdate);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"MaybeBabyFace.png");
                var shader=Shader.Find("Dimari/PhotoFaceDepth");
                var face=story.maybeBaby.GetComponent<DonsportPhotoFace>();
                if(face==null)face=Undo.AddComponent<DonsportPhotoFace>(story.maybeBaby.gameObject);
                Undo.RecordObject(face,"Repair face photo reference");
                if(texture!=null&&shader!=null)
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"MaybeBabyFace.mat");
                    if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Art+"MaybeBabyFace.mat");}
                    Undo.RecordObject(material,"Restore face texture");material.shader=shader;material.SetTexture("_MainTex",texture);
                    EditorUtility.SetDirty(material);face.faceMaterial=material;face.faceTexture=texture;face.enabled=true;
                }
                else {face.enabled=false;Debug.LogWarning("[Dimari] Photo/shader missing. Face overlay disabled; original model retained.",face);}
                EditorUtility.SetDirty(face);
                // The old white primitive can hide the face on rigs with unusual head axes.
                foreach(Transform child in story.maybeBaby.GetComponentsInChildren<Transform>(true))
                    if(child.name=="Cream headband")Undo.DestroyObjectImmediate(child.gameObject);
                Transform crowd=root.transform.Find("Donsport story gym visitors");
                var visitors=new List<Transform>();
                if(crowd!=null)foreach(Transform child in crowd)if(child!=story.maybeBaby)visitors.Add(child);
                int runners=0,curls=0,stretch=0,station=0;
                foreach(Transform visitor in visitors)
                {
                    var animator=visitor.GetComponentInChildren<Animator>();if(animator==null||!animator.isHuman)continue;
                    var exercise=visitor.GetComponent<DonsportExercise>();
                    if(exercise==null)exercise=Undo.AddComponent<DonsportExercise>(visitor.gameObject);
                    Undo.RecordObject(exercise,"Configure exercise");Undo.RecordObject(visitor,"Place gym athlete");
                    var agent=visitor.GetComponent<CityLifeAgent>();if(agent!=null){Undo.RecordObject(agent,"Stationary exercise");agent.enabled=false;}
                    Transform equipment=null;
                    if(station<3)equipment=FindEquipment(root.transform,"Treadmill belt",-10+station*4);
                    else if(station<6)equipment=FindEquipment(root.transform,"Training bench cushion",3+(station-3)*4);
                    if(equipment!=null)
                    {
                        var renderer=equipment.GetComponent<Renderer>();
                        float top=renderer!=null?renderer.bounds.max.y:equipment.position.y;
                        Vector3 anchor=equipment.position;anchor.y=top+0.03f;
                        float feet=float.PositiveInfinity;
                        foreach(var bodyRenderer in visitor.GetComponentsInChildren<Renderer>())feet=Mathf.Min(feet,bodyRenderer.bounds.min.y);
                        Vector3 actorPosition=anchor;
                        if(station<3&&!float.IsInfinity(feet))actorPosition.y+=visitor.position.y-feet;
                        visitor.SetPositionAndRotation(actorPosition,root.transform.rotation*(station<3?Quaternion.identity:Quaternion.Euler(0,180,0)));
                        if(station<3){exercise.activity=DonsportExercise.Activity.Treadmill;runners++;}
                        else {exercise.activity=DonsportExercise.Activity.SeatedCurls;exercise.seatWorldPosition=anchor+Vector3.up*0.08f;AddWeights(visitor,animator);curls++;}
                        // Equipment athletes do not push the player or fight the bench collider.
                        foreach(Collider collider in visitor.GetComponentsInChildren<Collider>())
                        {Undo.RecordObject(collider,"Exercise actor collision");collider.isTrigger=true;}
                    }
                    else {exercise.activity=DonsportExercise.Activity.Stretch;stretch++;}
                    exercise.phase=station*0.73f;exercise.enabled=true;EditorUtility.SetDirty(exercise);station++;
                }
                if(root.transform.Find(Stamp)==null){var stamp=new GameObject(Stamp);stamp.transform.SetParent(root.transform,false);Undo.RegisterCreatedObjectUndo(stamp,"Gym upgrade stamp");}
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);Physics.SyncTransforms();Undo.CollapseUndoOperations(group);
                string message="Фото лица переназначено; белый аксессуар удалён.\nДорожки: "+runners+", гантели на скамье: "+curls+", разминка: "+stretch
                    +".\nСохраните Ctrl+S. Проверьте лицо и упражнения в Play. Это процедурные движения существующих моделей, не motion capture. Мэйби Бэйби остаётся доступной для подхода.";
                Debug.Log("[Dimari] "+message,root);if(report)EditorUtility.DisplayDialog("Донспорт",message,"OK");
            }
            catch(System.Exception e){Undo.RevertAllDownToGroup(group);Debug.LogException(e);}
        }
        static Transform FindEquipment(Transform root,string name,float x)
        {
            foreach(Transform child in root.GetComponentsInChildren<Transform>())
                if(child.name==name&&Mathf.Abs(root.InverseTransformPoint(child.position).x-x)<0.3f)return child;
            return null;
        }
        static void AddWeights(Transform person,Animator animator)
        {
            foreach(var id in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
            {
                Transform hand=animator.GetBoneTransform(id);if(hand==null||hand.Find("Exercise dumbbell")!=null)continue;
                var holder=new GameObject("Exercise dumbbell");holder.transform.SetParent(hand,false);Undo.RegisterCreatedObjectUndo(holder,"Exercise hand prop");
                // Use world scale rather than assuming the imported skeleton uses metre-sized bones.
                holder.transform.position=hand.position;holder.transform.rotation=person.rotation;
                Vector3 scale=hand.lossyScale;holder.transform.localScale=new Vector3(1/Mathf.Max(0.001f,Mathf.Abs(scale.x)),1/Mathf.Max(0.001f,Mathf.Abs(scale.y)),1/Mathf.Max(0.001f,Mathf.Abs(scale.z)));
                for(int i=-1;i<=1;i++)
                {
                    var part=GameObject.CreatePrimitive(PrimitiveType.Cylinder);part.name=i==0?"Grip":"Weight";
                    Object.DestroyImmediate(part.GetComponent<Collider>());part.transform.SetParent(holder.transform,false);
                    part.transform.localPosition=new Vector3(i*0.12f,0,0);part.transform.localRotation=Quaternion.Euler(0,0,90);
                    part.transform.localScale=i==0?new Vector3(0.035f,0.12f,0.035f):new Vector3(0.15f,0.035f,0.15f);
                }
            }
        }
    }
}
