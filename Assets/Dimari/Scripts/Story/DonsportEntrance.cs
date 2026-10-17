using UnityEngine;
using Dimari.Core;
using Dimari.Player;
using Dimari.Vehicles;

namespace Dimari.Story
{
    public class DonsportEntrance : MonoBehaviour
    {
        public Transform building,door;
        ForestPlayerController player;
        Camera visibilityCamera;
        bool open,prompted,savedOcclusion,previousOcclusion;
        void RestoreVisibility()
        {if(savedOcclusion&&visibilityCamera!=null)visibilityCamera.useOcclusionCulling=previousOcclusion;savedOcclusion=false;}
        void Update()
        {
            if(player==null)player=FindAnyObjectByType<ForestPlayerController>();
            var story=StoryQuestManager.Instance;
            if(story==null||player==null||story.IsMenuOpen||!player.IsControlEnabled)return;
            bool near=Vector3.Distance(player.transform.position,transform.position)<2.8f;
            if(near)
            {
                story.SetInteractionPrompt(open?"[E] Закрыть дверь · Донспорт":"[E] Войти в «Донспорт» — открыть дверь");prompted=true;
                if(DimariInput.IsInteractPressed())
                {
                    Vector3 p=building!=null?building.InverseTransformPoint(player.transform.position):Vector3.one*100;
                    bool doorway=p.x> -2.9f&&p.x< -0.3f&&Mathf.Abs(p.z+9)<0.85f;
                    if(!open||!doorway)open=!open;
                }
            }
            else if(prompted){story.SetInteractionPrompt("");prompted=false;}
            if(door!=null)door.localRotation=Quaternion.RotateTowards(door.localRotation,Quaternion.Euler(0,open?-100:0,0),Time.deltaTime*140);
        }
        void LateUpdate()
        {
            Camera current=Camera.main;
            if(current!=visibilityCamera){RestoreVisibility();visibilityCamera=current;}
            if(current==null||building==null)return;
            Vector3 point=player!=null?player.transform.position:current.transform.position;
            Vector3 p=building.InverseTransformPoint(point),c=building.InverseTransformPoint(current.transform.position);
            bool near=(Mathf.Abs(p.x)<17&&p.z> -24&&p.z<12&&p.y> -2&&p.y<10)||(Mathf.Abs(c.x)<17&&c.z> -24&&c.z<12&&c.y> -2&&c.y<10);
            if(near){if(!savedOcclusion){previousOcclusion=current.useOcclusionCulling;savedOcclusion=true;}current.useOcclusionCulling=false;}
            else RestoreVisibility();
        }
        void OnDisable()
        {RestoreVisibility();if(prompted&&StoryQuestManager.Instance!=null)StoryQuestManager.Instance.SetInteractionPrompt("");prompted=false;}
        void OnGUI()
        {
            var story=StoryQuestManager.Instance;Camera cam=Camera.main;var car=CityDriveableCar.ActiveDriver;
            if(story==null||cam==null||story.IsMenuOpen||player==null||(!player.IsControlEnabled&&car==null))return;
            bool quest=false;foreach(var step in story.questSteps)if(step.id=="city_donsport"&&!step.isCompleted)quest=true;
            if(!quest)return;
            Vector3 origin=car!=null?car.transform.position:player.transform.position;
            float distance=Vector3.Distance(origin,transform.position);Vector3 p=cam.WorldToViewportPoint(transform.position+Vector3.up*2.6f);
            bool on=p.z>0&&p.x>0.06f&&p.x<0.94f&&p.y>0.08f&&p.y<0.9f;
            string hint=p.z<0?"Позади — развернитесь":p.x<0?"← Слева":p.x>1?"Справа →":"Двигайтесь ко входу";
            if(on)hint=car!=null?"Парковка перед зданием":"Подойдите к главному входу";
            if(distance<4)hint=car!=null?"Остановитесь и выйдите из машины":"[E] Открыть дверь · Ресепшен внутри";
            float width=Mathf.Min(360,Screen.width-24),x=on?Mathf.Clamp(p.x*Screen.width,width/2+12,Screen.width-width/2-12):Screen.width/2f;
            float y=on?Mathf.Clamp((1-p.y)*Screen.height,110,Screen.height-210):120;
            int depth=GUI.depth;GUI.depth=-110;
            GUI.Box(new Rect(x-width/2,y,width,86),"<color=#FFE18A><b>◆ ФИТНЕС-КЛУБ ДОНСПОРТ</b></color>\n"+distance.ToString("0")+" м · "+hint,
                new GUIStyle(GUI.skin.box){richText=true,fontSize=Mathf.Clamp(Screen.height/48,16,23),wordWrap=true,alignment=TextAnchor.MiddleCenter});GUI.depth=depth;
        }
    }
}
