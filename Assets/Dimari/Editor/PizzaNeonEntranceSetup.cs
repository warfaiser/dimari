using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Story;

namespace Dimari.Editor
{
    public static class PizzaNeonEntranceSetup
    {
        [MenuItem("Dimari/Пицца — неоновая вывеска и вход")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")
            {EditorUtility.DisplayDialog("Пиццерия","Остановите Play и откройте Chapter2_Rogovo.","OK");return;}
            GameObject pizza=GameObject.Find("Dimari_PizzaExpress24");
            if(pizza==null){EditorUtility.DisplayDialog("Пиццерия","Сначала постройте пиццерию.","OK");return;}
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dimari/Art/Pizza/Pizza_NeonSign.png");
            if(texture==null||Shader.Find("Universal Render Pipeline/Lit")==null)return;
            Directory.CreateDirectory("Assets/Dimari/Generated/PizzaExpress");AssetDatabase.Refresh();
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Pizza neon sign and playable door");
            foreach(Transform child in pizza.transform)
                if(child.name=="White sign fascia"||child.name=="24 badge"||child.name=="Sign ПИЦЦА"||child.name=="Sign ЭКСПРЕСС"||child.name=="Sign 24")
                {Undo.RecordObject(child.gameObject,"Hide old sign");child.gameObject.SetActive(false);}
            Transform previous=pizza.transform.Find("Dimari_NeonFacade");if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
            var root=new GameObject("Dimari_NeonFacade");root.transform.SetParent(pizza.transform,false);Undo.RegisterCreatedObjectUndo(root,"Neon facade");
            Material dark=Mat("NeonDarkTrim",new Color(0.045f,0.045f,0.045f));
            var backing=Box(root.transform,"Sign backing",new Vector3(0,3.15f,-5.23f),new Vector3(11.6f,1.05f,0.08f),dark,false);
            var sign=GameObject.CreatePrimitive(PrimitiveType.Quad);sign.name="pizza express 24 часа";sign.transform.SetParent(root.transform,false);
            sign.transform.localPosition=new Vector3(0,3.15f,-5.28f);sign.transform.localScale=new Vector3(6.1f,6.1f*texture.height/texture.width,1);Object.DestroyImmediate(sign.GetComponent<Collider>());
            Material letters=Mat("PizzaNeonLetters",Color.white);letters.SetTexture("_BaseMap",texture);letters.SetTexture("_EmissionMap",texture);
            letters.SetColor("_EmissionColor",Color.white*2);letters.EnableKeyword("_EMISSION");EditorUtility.SetDirty(letters);sign.GetComponent<Renderer>().sharedMaterial=letters;
            Material neon=Mat("RedNeonTube",new Color(1,0.015f,0.005f));neon.SetColor("_EmissionColor",new Color(6,0.03f,0.01f));neon.EnableKeyword("_EMISSION");EditorUtility.SetDirty(neon);
            // Rectangles frame the storefront, leaving the actual left-hand door unobstructed.
            foreach(float x in new[]{-3.25f,5.32f})Box(root.transform,"Vertical red neon",new Vector3(x,1.4f,-5.14f),new Vector3(0.025f,2.65f,0.025f),neon,false);
            foreach(float y in new[]{0.08f,2.72f})Box(root.transform,"Horizontal red neon",new Vector3(1.03f,y,-5.14f),new Vector3(8.6f,0.025f,0.025f),neon,false);
            foreach(float x in new[]{-4.92f,-3.28f})Box(root.transform,"Entrance neon",new Vector3(x,1.4f,-5.17f),new Vector3(0.025f,2.7f,0.025f),neon,false);
            Box(root.transform,"Entrance top neon",new Vector3(-4.1f,2.75f,-5.17f),new Vector3(1.66f,0.025f,0.025f),neon,false);
            Box(root.transform,"Supported entrance landing",new Vector3(-4.1f,-0.01f,-6.35f),new Vector3(2.6f,0.1f,2.8f),dark,true);
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Sign spill light");go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(-3.5f+i*3.5f,2.9f,-5.8f);
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=3;light.intensity=0.6f;light.color=i==1?Color.green:Color.red;light.shadows=LightShadows.None;
            }
            Transform hinge=pizza.transform.Find("Door hinge");
            if(hinge==null)
            {
                var go=new GameObject("Door hinge");hinge=go.transform;hinge.SetParent(pizza.transform,false);hinge.localPosition=new Vector3(-4.9f,0,-5);Undo.RegisterCreatedObjectUndo(go,"Create door");
                Box(hinge,"Entrance door",new Vector3(0.8f,1.3f,0),new Vector3(1.5f,2.6f,0.05f),dark,true);
            }
            else Undo.RecordObject(hinge,"Reset entrance door");
            Undo.RecordObject(hinge,"Close door");hinge.localRotation=Quaternion.identity;
            PizzaExpressEntrance portal=pizza.GetComponentInChildren<PizzaExpressEntrance>(true);
            if(portal==null)
            {
                var go=new GameObject("PizzaExpressEntrance");go.transform.SetParent(pizza.transform,false);go.transform.localPosition=new Vector3(-4.1f,0.1f,-5.25f);
                Undo.RegisterCreatedObjectUndo(go,"Door interaction");portal=Undo.AddComponent<PizzaExpressEntrance>(go);
            }
            Undo.RecordObject(portal,"Connect door");portal.door=hinge;
            int cut=PizzaExpressReplacement.OpenDoorAccess(pizza.transform);
            Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(pizza.scene);Physics.SyncTransforms();Selection.activeGameObject=pizza;
            EditorUtility.DisplayDialog("Вывеска и вход","Вывеска pizza express / 24 часа установлена. Вход слева: подойдите и нажмите E, затем проходите пешком. Локально исправлено коллайдеров: "+cut+".\nПроверьте в Play, затем сохраните Ctrl+S. Для ореола неона нужен Bloom в вашем URP Volume; глобальные настройки города не менялись.","OK");
        }
        static Material Mat(string name,Color color)
        {
            string path="Assets/Dimari/Generated/PizzaExpress/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",0.3f);EditorUtility.SetDirty(m);return m;
        }
        static GameObject Box(Transform root,string name,Vector3 position,Vector3 scale,Material material,bool collider)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    }
}
