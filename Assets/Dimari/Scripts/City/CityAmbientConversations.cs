using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.City
{
    public class CityAmbientConversations : MonoBehaviour
    {
        class Talk
        {
            public bool cafe; public string a,b,topic; public int audio;
            public Talk(bool c,string x,string y,int clip=-1,string subject="Еда") { cafe=c;a=x;b=y;audio=clip;topic=subject; }
        }
        readonly List<Talk> bank=new List<Talk>();
        readonly HashSet<Talk> used=new HashSet<Talk>();
        string subtitle="",previousTopic="";
        ForestPlayerController player;
        AudioSource source;
        CityLifeAgent activeA,activeB,lastA,lastB;
        bool interrupted;
        void Awake()
        {
            bank.Add(new Talk(true,"Опять весь центр перекопали. Вчера до остановки десять минут шёл в обход.","Зато теперь у пиццерии можно спокойно пройти. Пойдём через площадь.",0));
            bank.Add(new Talk(true,"Я возьму пепперони. Только в этот раз давай без споров, кому достался последний кусок.","Тогда сразу две закажем. И салфеток побольше, у тебя опять весь стол будет в соусе.",4));
            bank.Add(new Talk(true,"Смотри, тут даже пицца с трюфелем есть. А я всё время одну маргариту беру.","Ну попробуй что-нибудь новое. Только сначала цену посмотри, любитель экспериментов.",6));
            string[] dimarik={
                "Ты Димарика случайно не знаешь?|Лично нет. Но слышал, его тут разыскивают.",
                "Тот парень у стойки спрашивал про Димарика.|Надеюсь, найдёт. Неприятно, когда человек не выходит на связь.",
                "Если Димарика встретим, скажем, что его ищут?|Конечно. Только сначала убедимся, что это он.",
                "Кто-нибудь оставил номер для связи с Димариком?|Не знаю. Лучше спросить сотрудника, а не строить догадки.",
                "Димарик эту пиццерию вроде любит?|Может быть. Я бы на месте его друга начал со стойки.",
                "Ты бы что делал, если друг вдруг пропал?|Позвонил общим знакомым. И не стал бы верить каждому слуху."
            };
            foreach(string line in dimarik){var parts=line.Split('|');bank.Add(new Talk(true,parts[0],parts[1],-1,"Димарик"));}
            string[] cafe={
                "Еда|Тебе какой кусок оставить?|Тот, где больше сыра.",
                "Еда|Острая нормально?|Лучше соус отдельно попросим.",
                "Еда|С собой будем брать?|Одну коробку домой, остальное здесь.",
                "Еда|Ты уже заказал?|Нет, ждал, пока ты выберешь.",
                "Еда|Чай остыл.|Ты меню уже полчаса читаешь.",
                "Еда|Возьмём большую на всех?|Давай. Только последний кусок делим честно.",
                "Еда|Мне без лука, пожалуйста.|Не забудь сказать на кассе.",
                "Еда|Пахнет свежим тестом.|Кажется, только что новую партию в печь поставили.",
                "Работа|Сегодня смена наконец без аврала.|Тогда телефон убери. У тебя обед, а не планёрка.",
                "Работа|Начальник опять совещание перенёс.|Надеюсь, не на наш ужин.",
                "Кино|Посмотрим вечером детектив?|Только без спойлеров. Ты в прошлый раз всё рассказал.",
                "Кино|Новый фильм уже видел?|Нет. Давай выберем сеанс после ужина.",
                "Погода|С утра холодно, а сейчас куртку некуда деть.|Положи рядом. Главное, опять здесь не забудь.",
                "Погода|До дождя домой успеем?|Если ты не будешь фотографировать каждый кусок.",
                "Поездки|На выходных хочу выбраться за город.|Давай поездом. Хоть раз без поисков парковки.",
                "Поездки|У моря сейчас, наверное, хорошо.|Сначала отпуск согласуй, мечтатель.",
                "Дом|Я наконец собрал тот шкаф.|И сколько деталей осталось лишних?",
                "Дом|Сосед опять сверлит по вечерам.|Пригласи его на пиццу. Хоть час тишины будет.",
                "Спорт|Матч вчера смотрел?|Смотрел. Последние пять минут чуть чай не пролил.",
                "Спорт|С понедельника начинаю бегать.|Только не за автобусом, как обычно.",
                "Музыка|Что у тебя весь день в наушниках?|Старый альбом. Никак не надоест.",
                "Музыка|На концерт в субботу пойдём?|Пойдём, если ещё остались билеты.",
                "Город|На площади новые скамейки поставили.|После еды прогуляемся и проверим.",
                "Город|Здесь раньше совсем другой магазин был.|Да, а теперь хоть вечером есть где посидеть.",
                "Покупки|Опять забыл список продуктов.|Главное, не вернись с одним печеньем.",
                "Покупки|Хочу новый чайник купить.|Старый ведь работает. Или цвет пиццерии понравился?",
                "Питомцы|Кот утром спрятал мой носок.|Он просто не хотел отпускать тебя на работу.",
                "Питомцы|Собака сегодня весь парк обошла.|Значит, вечером выгуливать уже она тебя будет.",
                "Друзья|Давно всей компанией не собирались.|Напишем ребятам. Тут большой стол как раз пригодится.",
                "Друзья|Помнишь нашу поездку с палатками?|Особенно то, как мы забыли сами палатки.",
                "Техника|Телефон опять сел к обеду.|Меньше снимай пиццу, больше ешь.",
                "Техника|Навигатор повёл меня через двор.|В следующий раз спроси дорогу у человека.",
                "Планы|Что будешь делать вечером?|Ничего срочного. И это лучший план за неделю.",
                "Планы|Завтра пораньше встретимся?|Давай. Только поставь не один будильник.",
                "Книги|Дочитал ту книгу, которую советовал?|Пока нет. Не рассказывай, чем закончится.",
                "Книги|В библиотеке выставку открыли.|Зайдём после обеда, она совсем рядом."
            };
            foreach(string line in cafe){var p=line.Split('|');bank.Add(new Talk(true,p[1],p[2],-1,p[0]));}
            var audio=new GameObject("Ambient speech source");audio.transform.SetParent(transform,false);
            source=audio.AddComponent<AudioSource>();source.playOnAwake=false;source.Stop();source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=2;source.maxDistance=14;source.volume=0.65f;
        }
        void OnEnable(){StartCoroutine(Run());}
        bool Allowed()
        {
            var story=StoryQuestManager.Instance;
            Scene scene=SceneManager.GetActiveScene();
            if(scene.name!="Chapter2_Rogovo"||gameObject.scene!=scene||story==null||!story.HasGameStarted||story.IsMenuOpen
                ||player==null||!player.IsControlEnabled)return false;
            Camera camera=Camera.main;
            if(camera==null)return false;
            var view=camera.GetComponent<ForestCameraController>();
            return InsidePizza()&&(view==null||(!view.IsMenuCameraActive&&!view.IsCutsceneControlled));
        }
        void Update()
        {
            // Stop immediately even while the coroutine is in a between-lines WaitForSeconds
            // or Time.timeScale is zero. Hidden subtitles alone must not leave audible speech.
            if(Allowed())return;
            interrupted=true;subtitle="";
            if(source!=null)source.Stop();
            ClearActors();
        }
        static int Table(CityLifeAgent guest,Transform pizza)
        {
            Vector3 point=guest.transform.position;var animator=guest.GetComponentInChildren<Animator>();
            if(animator!=null&&animator.isHuman){var hips=animator.GetBoneTransform(HumanBodyBones.Hips);if(hips!=null)point=hips.position;}
            Vector3 p=pizza.InverseTransformPoint(point);
            if(Mathf.Abs(p.x)>6||Mathf.Abs(p.z)>5)return -1;
            return p.x<0?0:(p.z< -1.025f?1:2);
        }
        bool InsidePizza()
        {
            var pizza=GameObject.Find("Dimari_PizzaExpress24");if(pizza==null||player==null)return false;
            Vector3 p=pizza.transform.InverseTransformPoint(player.transform.position);
            return Mathf.Abs(p.x)<5.9f&&p.z> -4.85f&&p.z<4.85f&&p.y> -0.5f&&p.y<3.8f;
        }
        bool CanHear(CityLifeAgent actor,bool requireVisible=false)
        {
            if(actor==null||player==null||actor.gameObject.scene!=player.gameObject.scene&&actor.gameObject.scene!=gameObject.scene)return false;
            if(!actor.seated||actor.vehicle||!InsidePizza())return false;
            Vector3 point=actor.transform.position+Vector3.up*1.2f;
            var animator=actor.GetComponentInChildren<Animator>();if(animator!=null&&animator.isHuman){var head=animator.GetBoneTransform(HumanBodyBones.Head);if(head!=null)point=head.position;}
            bool rendered=false;foreach(var r in actor.GetComponentsInChildren<Renderer>())if(r.enabled&&r.gameObject.activeInHierarchy){rendered=true;break;}
            Camera cam=Camera.main;if(!rendered||cam==null||Vector3.Distance(player.transform.position,point)>10)return false;
            if(requireVisible){Vector3 p=cam.WorldToViewportPoint(point);if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)return false;}
            Vector3 origin=cam.transform.position;Vector3 delta=point-origin;
            foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            {if(hit.transform.IsChildOf(actor.transform)||hit.transform.IsChildOf(player.transform))continue;return false;}
            return true;
        }
        bool ChoosePair(out CityLifeAgent a,out CityLifeAgent b)
        {
            a=null;b=null;var agents=FindObjectsByType<CityLifeAgent>();
            var building=GameObject.Find("Dimari_PizzaExpress24");Transform pizza=building!=null?building.transform:null;
            if(pizza==null||!InsidePizza())return false;
            int candidates=0;
            for(int i=0;i<agents.Length;i++)
            {
                var first=agents[i];if(!first.seated||!CanHear(first)||first.name=="Pizza seller Obzhorka"||first.vehicle||first.gameObject.scene!=gameObject.scene||Time.time<first.talkingUntil||Vector3.Distance(first.transform.position,player.transform.position)>11)continue;
                for(int j=i+1;j<agents.Length;j++)
                {
                    var second=agents[j];if(!second.seated||second.name=="Pizza seller Obzhorka"||second.vehicle||second.gameObject.scene!=gameObject.scene||Time.time<second.talkingUntil||first.seated!=second.seated||Vector3.Distance(second.transform.position,player.transform.position)>11||Vector3.Distance(first.transform.position,second.transform.position)>3.2f)continue;
                    if(first.seated&&(pizza==null||Table(first,pizza)<0||Table(first,pizza)!=Table(second,pizza)))continue;
                    if(!CanHear(second)||(!CanHear(first,true)&&!CanHear(second,true)))continue;
                    if((first==lastA&&second==lastB)||(first==lastB&&second==lastA))continue;
                    if(Random.Range(0,++candidates)==0){a=first;b=second;}
                }
            }
            // A single remaining pair is still allowed to talk in later cycles.
            if(a==null&&lastA!=null&&lastB!=null)
            {lastA=null;lastB=null;return ChoosePair(out a,out b);}
            return a!=null;
        }
        Talk ChooseTalk(bool cafe)
        {
            var choices=new List<Talk>();
            foreach(var talk in bank)if(talk.cafe==cafe&&!used.Contains(talk)&&talk.topic!=previousTopic)choices.Add(talk);
            if(choices.Count==0)foreach(var talk in bank)if(talk.cafe==cafe&&!used.Contains(talk))choices.Add(talk);
            if(choices.Count==0){used.RemoveWhere(t=>t.cafe==cafe);foreach(var talk in bank)if(talk.cafe==cafe)choices.Add(talk);}
            var result=choices[Random.Range(0,choices.Count)];used.Add(result);previousTopic=result.topic;return result;
        }
        IEnumerator Run()
        {
            yield return new WaitForSeconds(3);
            while(true)
            {
                if(player==null)player=FindAnyObjectByType<ForestPlayerController>();
                if(!Allowed()||!ChoosePair(out CityLifeAgent a,out CityLifeAgent b)){yield return new WaitForSeconds(2);continue;}
                activeA=a;activeB=b;lastA=a;lastB=b;Talk talk=ChooseTalk(true);interrupted=false;
                a.talkingUntil=b.talkingUntil=Time.time+90;a.conversationPartner=b.transform;b.conversationPartner=a.transform;
                yield return Say(a,talk.a,talk.audio);
                if(!interrupted)yield return Say(b,talk.b,talk.audio<0?-1:talk.audio+1);
                ClearActors();subtitle="";
                yield return new WaitForSeconds(Random.Range(5f,10f));
            }
        }
        void ClearActors()
        {
            foreach(var actor in new[]{activeA,activeB})if(actor!=null){actor.talkingUntil=0;actor.speakingUntil=0;actor.conversationPartner=null;}
            activeA=null;activeB=null;
        }
        IEnumerator Say(CityLifeAgent actor,string line,int audio)
        {
            if(interrupted||!Allowed()||actor==null||!CanHear(actor)){interrupted=true;yield break;}
            AudioClip clip=audio<0?null:Resources.Load<AudioClip>("Crowd_"+audio);
            source.transform.position=actor.transform.position+Vector3.up*1.4f;source.clip=clip;if(clip!=null)source.Play();
            float duration=clip!=null?clip.length:Mathf.Clamp(line.Length/16f,3,8);actor.speakingUntil=Time.time+duration;
            for(float t=0;t<duration;t+=Time.deltaTime)
            {
                if(interrupted||!Allowed()||actor==null||!CanHear(actor))
                {interrupted=true;source.Stop();subtitle="";yield break;}
                subtitle=(actor.seated?"Посетитель: ":"Прохожий: ")+line;yield return null;
            }
            if(actor!=null)actor.speakingUntil=0;subtitle="";yield return new WaitForSeconds(0.35f);
        }
        void OnDisable(){StopAllCoroutines();ClearActors();subtitle="";if(source!=null)source.Stop();}
        void OnGUI()
        {
            if(string.IsNullOrEmpty(subtitle)||!Allowed())return;
            GUI.depth=-120;
            GUI.Box(new Rect(Screen.width*0.12f,Screen.height*0.81f,Screen.width*0.76f,Mathf.Max(74,Screen.height*0.11f)),subtitle,
                new GUIStyle(GUI.skin.box){fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height/48f),16,24),wordWrap=true,alignment=TextAnchor.MiddleCenter});
        }
    }
}
