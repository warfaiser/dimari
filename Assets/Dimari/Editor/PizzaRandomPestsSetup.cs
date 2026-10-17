using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dimari.Editor
{
    // Install on script import / model import / Chapter2 open. Safe to repeat; no city population rebuild.
    [InitializeOnLoad]
    public static class PizzaRandomPestsSetup
    {
        const string Stamp="Random wander v1";
        static bool queued,running,warned;
        static PizzaRandomPestsSetup()
        {
            EditorSceneManager.sceneOpened+=OnSceneOpened;
            Queue();
        }
        static void OnSceneOpened(Scene scene,OpenSceneMode mode){warned=false;Queue();}
        public static void Queue()
        {
            if(queued||running)return;
            queued=true;EditorApplication.delayCall+=Auto;
        }
        static void Auto()
        {
            queued=false;
            if(EditorApplication.isPlayingOrWillChangePlaymode||running)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){Queue();return;}
            Apply(false);
        }
        [MenuItem("Dimari/Пицца — случайно бегающие мыши и тараканы")]
        public static void Install(){Apply(true);}
        static void Apply(bool force)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SceneManager.GetActiveScene().name!="Chapter2_Rogovo")
            {if(force)EditorUtility.DisplayDialog("Вредители","Остановите Play и откройте Chapter2_Rogovo. Настройка также запускается автоматически при открытии этой сцены.","OK");return;}
            var pizza=GameObject.Find("Dimari_PizzaExpress24");
            if(pizza==null){if(force)EditorUtility.DisplayDialog("Вредители","Не найдена существующая Dimari_PizzaExpress24. Новое здание не создаётся.","OK");return;}
            running=true;Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Random mouse and cockroach population");
            bool changed=false;string report="";
            try
            {
                foreach(string type in new[]{"Mouse","Cockroach"})
                {
                    Transform old=pizza.transform.Find("Pizza pests "+type);
                    if(!force&&old!=null&&old.Find(Stamp)!=null)continue;
                    if(!PizzaResidentsSetup.FindPestModel(type,out GameObject model,out string path))
                    {report+=type+": не найден FBX/OBJ с геометрией в Assets/Dimari/Imports/"+type+". Старые объекты сохранены. Если скачан только BLEND/GLB — нужна конвертация.\n";continue;}
                    report+=PizzaResidentsSetup.Pests(pizza.transform,type,type=="Mouse"?5:14,type=="Mouse"?0.22f:0.035f,type=="Mouse"?1.05f:0.32f,true);
                    Transform group=pizza.transform.Find("Pizza pests "+type);
                    if(group!=null&&group!=old&&group.childCount>0)
                    {
                        var stamp=new GameObject(Stamp);stamp.transform.SetParent(group,false);Undo.RegisterCreatedObjectUndo(stamp,"Mark configured random pests");changed=true;
                    }
                }
                if(changed)
                {
                    AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(pizza.scene);
                    Debug.Log("[Dimari] Случайные пробежки настроены. Сохраните сцену Ctrl+S вне Play.\n"+report,pizza);
                }
                else if(report.Length>0&&!warned){Debug.LogWarning("[Dimari] "+report,pizza);warned=true;}
                if(force)EditorUtility.DisplayDialog("Случайные пробежки",report+"\nЛюди, интерьер и продавец не изменены. Проверьте в Play и сохраните сцену вне Play (Ctrl+S).", "OK");
            }
            finally {Undo.CollapseUndoOperations(undo);running=false;}
        }
    }
    public class PizzaPestImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            foreach(string path in imported)
                if(path.StartsWith("Assets/Dimari/Imports/Mouse/")||path.StartsWith("Assets/Dimari/Imports/Cockroach/"))
                {PizzaRandomPestsSetup.Queue();return;}
        }
    }
}
