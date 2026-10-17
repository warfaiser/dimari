using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Cinematics;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class PizzaSellerCutsceneSetup
    {
        static PizzaSellerCutsceneSetup()
        {EditorApplication.delayCall+=Auto;EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Auto;}
        static void Auto()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}
            Apply(false);
        }
        [MenuItem("Dimari/Пицца — разговор с Просрочкой о Донспорте")]
        public static void Install(){Apply(true);}
        static void Apply(bool report)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Chapter2_Rogovo")return;
            var pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza==null)return;
            Transform seller=pizza.transform.Find("Pizza seller Obzhorka");
            if(seller==null)
            {Debug.LogWarning("[Dimari] Разговор не установлен: не найдена Pizza seller Obzhorka. Существующие люди не заменены.",pizza);return;}
            var cutscene=pizza.GetComponent<PizzaSellerCutscene>();bool changed=false;
            if(cutscene==null){cutscene=Undo.AddComponent<PizzaSellerCutscene>(pizza);changed=true;}
            if(cutscene.seller!=seller){Undo.RecordObject(cutscene,"Assign pizza seller");cutscene.seller=seller;changed=true;}
            if(changed){EditorSceneManager.MarkSceneDirty(pizza.scene);Debug.Log("[Dimari] Кат-сцена установлена: подойдите к продавцу со стороны зала и нажмите E. Сохраните сцену Ctrl+S.",pizza);}
            if(report)EditorUtility.DisplayDialog("Просрочка","Подойдите к стойке со стороны зала и нажмите E при задаче найти пиццерию. После пяти реплик появится задача ехать в «Донспорт». Сцену сохраните Ctrl+S вне Play. Новое здание спортзала не создаётся.","OK");
        }
    }
}
