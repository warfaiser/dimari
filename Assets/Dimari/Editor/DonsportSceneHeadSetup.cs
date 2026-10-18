using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Cinematics;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class DonsportSceneHeadSetup
    {
        const string Folder="Assets/Dimari/Generated/DonsportPhotoHead";
        static DonsportSceneHeadSetup()
        {
            EditorApplication.delayCall+=Auto;
            EditorSceneManager.sceneOpened+=(s,m)=>EditorApplication.delayCall+=Auto;
        }
        static void Auto()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}
            var story=Object.FindAnyObjectByType<DonsportReceptionCutscene>();
            if(story==null||story.gameObject.scene!=SceneManager.GetActiveScene()||story.maybeBaby==null)return;
            var face=story.maybeBaby.GetComponent<DonsportPhotoFace>();
            if(face==null||face.scenePhotoBox==null)Build(false);
        }
        [MenuItem("Dimari/Донспорт — создать голову с фото прямо в Scene")]
        public static void Apply(){Build(true);}
        static void Fail(string message,Transform actor,bool report)
        {
            Debug.LogError("[Dimari] Scene photo head: "+message,actor);
            if(report)EditorUtility.DisplayDialog("Голова не создана — точная причина",message,"OK");
        }
        static void Build(bool report)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")return;
            var story=Object.FindAnyObjectByType<DonsportReceptionCutscene>();
            if(story==null||story.gameObject.scene!=SceneManager.GetActiveScene()||story.maybeBaby==null)
            {if(report)EditorUtility.DisplayDialog("Голова в Scene","В открытой сцене не найден персонаж Мэйби Бэйби из установленной истории Донспорта.","OK");return;}
            var actor=story.maybeBaby;
            var head=DonsportPhotoFace.FindHead(actor);
            if(head==null)
            {Fail("Не найдена кость Head у "+actor.name+". Материал ещё не проверялся. Пришлите раскрытую иерархию персонажа.",actor,report);return;}
            const string texturePath="Assets/Dimari/Resources/DonsportMaybeBabyFace.png";
            const string shaderPath="Assets/Dimari/Art/DonsportStory/PhotoFace.shader";
            const string materialPath="Assets/Dimari/Resources/DonsportPhotoBox.mat";
            AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(shaderPath,ImportAssetOptions.ForceUpdate);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)
            {Fail("Не загружено фото по пути "+texturePath+". Проверьте распаковку Assets в корень проекта.",actor,report);return;}
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if(shader==null)
            {Fail("Не загружен шейдер по пути "+shaderPath+". Пришлите ошибки его импорта из Console.",actor,report);return;}
            var portrait=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(portrait==null)
            {
                // Preserve an unimportable previous material; create a separate valid asset instead.
                const string repairedPath="Assets/Dimari/Resources/DonsportPhotoBoxRepaired.mat";
                portrait=AssetDatabase.LoadAssetAtPath<Material>(repairedPath);
                if(portrait==null){portrait=new Material(shader);AssetDatabase.CreateAsset(portrait,repairedPath);}
            }
            Undo.RecordObject(portrait,"Repair portrait references by actual asset paths");
            portrait.shader=shader;portrait.SetTexture("_MainTex",texture);EditorUtility.SetDirty(portrait);
            if(portrait.GetTexture("_MainTex")!=texture)
            {Fail("Материал не принял фото в _MainTex. Пришлите Inspector материала.",actor,report);return;}
            if(!AssetDatabase.IsValidFolder("Assets/Dimari/Generated"))AssetDatabase.CreateFolder("Assets/Dimari","Generated");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Dimari/Generated","DonsportPhotoHead");
            // Use saved assets, never transient meshes/materials that disappear after reloading Unity.
            var brown=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Brown.asset");
            if(brown==null){brown=new Texture2D(1,1);brown.SetPixel(0,0,new Color(.18f,.12f,.09f));brown.Apply();AssetDatabase.CreateAsset(brown,Folder+"/Brown.asset");}
            var sides=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sides.mat");
            if(sides==null){sides=new Material(shader);AssetDatabase.CreateAsset(sides,Folder+"/Sides.mat");}
            Undo.RecordObject(sides,"Repair side material");sides.shader=shader;sides.SetTexture("_MainTex",brown);EditorUtility.SetDirty(sides);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Visible saved Scene photo head");
            try
            {
                var face=actor.GetComponent<DonsportPhotoFace>();if(face==null)face=Undo.AddComponent<DonsportPhotoFace>(actor.gameObject);
                Undo.RecordObject(face,"Save Scene photo head reference");
                if(face.scenePhotoBox!=null)Undo.DestroyObjectImmediate(face.scenePhotoBox);
                var root=new GameObject("MAYBE BABY — SAVED PHOTO BOX HEAD v4");SceneManager.MoveGameObjectToScene(root,actor.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(root,"Photo box head in Scene");
                root.transform.SetPositionAndRotation(head.position+actor.rotation*new Vector3(0,face.boxCenterAboveHead,0),actor.rotation);
                root.transform.localScale=new Vector3(Mathf.Max(.38f,face.boxSize.x),Mathf.Max(.44f,face.boxSize.y),Mathf.Max(.38f,face.boxSize.z));
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Opaque box covering original hair and face";
                cube.transform.SetParent(root.transform,false);Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.GetComponent<Renderer>().sharedMaterial=sides;
                var photo=GameObject.CreatePrimitive(PrimitiveType.Quad);photo.name="MAYBE BABY PHOTOGRAPH — front";
                photo.transform.SetParent(root.transform,false);photo.transform.localPosition=new Vector3(0,0,.501f);
                Object.DestroyImmediate(photo.GetComponent<Collider>());photo.GetComponent<Renderer>().sharedMaterial=portrait;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                {renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
                face.scenePhotoBox=root;face.faceMaterial=portrait;face.faceTexture=portrait.GetTexture("_MainTex") as Texture2D;face.enabled=true;
                EditorUtility.SetDirty(face);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);Undo.CollapseUndoOperations(group);
                if(report){Selection.activeGameObject=root;SceneView.lastActiveSceneView?.FrameSelected();EditorUtility.DisplayDialog("Готово — видно без Play","Голова-коробка создана прямо в Scene и выделена. Фото находится спереди персонажа. Сохраните сцену Ctrl+S. В игре используется эта же коробка, вторая не создаётся. Отмена: Ctrl+Z.","OK");}
                Debug.Log("[Dimari] SAVED SCENE PHOTO HEAD v4 created. Save scene with Ctrl+S.",root);
            }
            catch(System.Exception e){Undo.RevertAllDownToGroup(group);Debug.LogException(e);}
        }
    }
}
