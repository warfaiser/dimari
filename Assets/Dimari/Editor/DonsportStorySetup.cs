using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.City;
using Dimari.Cinematics;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class DonsportStorySetup
    {
        const string Art = "Assets/Dimari/Art/DonsportStory/";
        static DonsportStorySetup()
        { EditorApplication.delayCall += Auto; EditorSceneManager.sceneOpened += (s,m) => EditorApplication.delayCall += Auto; }
        static void Auto()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var root=GameObject.Find("Dimari_Donsport");
            if(root==null)return;
            if(root.GetComponent<DonsportReceptionCutscene>()==null) Install(false);
            else if(root.transform.Find(DonsportGymUpgrade.Stamp)==null) DonsportGymUpgrade.ApplyTo(root,false);
        }
        [MenuItem("Dimari/Донспорт — Елизавета и поиск Мэйби Бэйби")]
        public static void Apply() { Install(true); }
        static void Install(bool report)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name!="Chapter2_Rogovo")return;
            var building=GameObject.Find("Dimari_Donsport"); if(building==null) { if(report)EditorUtility.DisplayDialog("Донспорт","Сначала постройте Донспорт существующей командой.","OK"); return; }
            if(building.GetComponent<DonsportReceptionCutscene>()!=null) { DonsportGymUpgrade.ApplyTo(building,report);return; }
            Transform reception=building.transform.Find("Donsport receptionist");
            if(reception==null) { Debug.LogWarning("[Dimari] В Донспорте отсутствует receptionist: импортируйте бесплатные NPC и постройте клуб. Ничего не заменено.");return; }
            GameObject source=reception.gameObject;
            var pool=new List<GameObject>();bool femaleFound=false;
            if(AssetDatabase.IsValidFolder("Assets/Dimari/NPCs"))
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Dimari/NPCs"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid),name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                var candidate=AssetDatabase.LoadAssetAtPath<GameObject>(path);var anim=candidate.GetComponentInChildren<Animator>();
                if(anim==null||!anim.isHuman)continue;
                pool.Add(candidate);
                if(!femaleFound&&(name.Contains("female")||name.Contains("woman")||name.Contains("girl"))) {source=candidate;femaleFound=true;}
            }
            var sourceAnimator=source.GetComponentInChildren<Animator>();
            if(sourceAnimator==null||!sourceAnimator.isHuman){Debug.LogWarning("[Dimari] Для посетителей нужен импортированный Humanoid. Изменений нет.");return;}
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Donsport reception story");
            try
            {
                var story=Undo.AddComponent<DonsportReceptionCutscene>(building);story.elizaveta=reception;
                Undo.RecordObject(reception.gameObject,"Name receptionist");reception.name="Елизавета — ресепшен";
                // Existing receptionist stays in her authored position; no manual transforms needed.
                var receptionistAgent=reception.GetComponent<CityLifeAgent>();if(receptionistAgent!=null){Undo.RecordObject(receptionistAgent,"Stationary receptionist");receptionistAgent.enabled=false;}
                var crowd=new GameObject("Donsport story gym visitors");crowd.transform.SetParent(building.transform,false);Undo.RegisterCreatedObjectUndo(crowd,"Gym visitors");
                Physics.SyncTransforms();int count=0;
                foreach(float z in new[]{3.2f,4.4f,7.3f}) foreach(float x in new[]{-12f,-7.7f,-3.7f,1.6f,5.6f,9.6f,12.5f})
                {
                    if(count>=9)continue;
                    Vector3 p=building.transform.TransformPoint(new Vector3(x,0.1f,z));
                    if(!Physics.Raycast(p+Vector3.up,Vector3.down,out RaycastHit ground,1.5f,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<0.9f||Mathf.Abs(ground.point.y-p.y)>0.25f)continue;
                    p=ground.point+Vector3.up*0.03f;
                    if(Physics.CheckCapsule(p+Vector3.up*0.35f,p+Vector3.up*1.5f,0.3f,~0,QueryTriggerInteraction.Ignore))continue;
                    var agent=CityLifeSetup.Human(count==0||pool.Count==0?source:pool[count%pool.Count],crowd.transform,p,building.transform.rotation*Quaternion.Euler(0,180+count*31,0),false);
                    agent.graph=null;agent.enabled=false;agent.name=count==0?"Мэйби Бэйби":"Gym visitor "+count;
                    if(count==0)
                    {
                        story.maybeBaby=agent.transform;
                        var face=agent.gameObject.AddComponent<DonsportPhotoFace>();
                        Shader shader=Shader.Find("Dimari/PhotoFaceDepth");
                        if(shader!=null)
                        {
                            var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"MaybeBabyFace.mat");
                            if(material==null){material=new Material(shader);material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"MaybeBabyFace.png");AssetDatabase.CreateAsset(material,Art+"MaybeBabyFace.mat");}
                            material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"MaybeBabyFace.png"));
                            EditorUtility.SetDirty(material);
                            face.faceMaterial=material;
                            face.faceTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"MaybeBabyFace.png");
                        }

                    }
                    count++;Physics.SyncTransforms();if(count>=9)break;
                }
                if(story.maybeBaby==null||count<4)throw new InvalidOperationException("Нет свободного места в зале. Все изменения отменены.");
                DonsportGymUpgrade.ApplyTo(building,false);
                EditorUtility.SetDirty(story);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(building.scene);Undo.CollapseUndoOperations(group);
                string message="Разговор Елизаветы: 6 реплик. Посетителей в зале: "+count+". Мэйби Бэйби — среди них. Сохраните Ctrl+S.\nПроекция лица по фото — прототип: проверьте совпадение с головой модели в Play; это не гарантированная точная 3D-копия. Все шесть реплик озвучены Voice_Donsport_0…5; при отсутствии файла остаются субтитры.";
                Debug.Log("[Dimari] "+message,building);if(report)EditorUtility.DisplayDialog("Донспорт",message,"OK");
            }
            catch(Exception error){Undo.RevertAllDownToGroup(group);Debug.LogException(error);}
        }
        [MenuItem("Dimari/Донспорт — повторить разговор Елизаветы (Play, у стойки)")]
        public static void Replay()
        { if(!Application.isPlaying)return;var scene=UnityEngine.Object.FindAnyObjectByType<DonsportReceptionCutscene>();if(scene!=null)scene.ReplayForPreview(); }
    }
}
