using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.City
{
    public class CityAmbientConversations : MonoBehaviour
    {
        [System.Serializable] class Talk { public bool cafe; public string a,b; public int audio=-1; public Talk(bool c,string x,string y,int clip=-1){cafe=c;a=x;b=y;audio=clip;} }
        readonly List<Talk> deck=new List<Talk>();
        string subtitle="";
        ForestPlayerController player;
        AudioSource source;
        void Awake()
        {
            deck.Add(new Talk(false,"Опять весь центр перекопали. Вчера до остановки десять минут шёл в обход.","Зато теперь у пиццерии можно спокойно пройти. Пойдём через площадь.",0));
            deck.Add(new Talk(false,"В администрации сегодня видел какого-то парня. Всё спрашивал про пропавшего коллегу.","Надеюсь, найдётся. Тут все друг друга знают, поспрашивает и разберётся.",2));
            deck.Add(new Talk(true,"Я возьму пепперони. Только в этот раз давай без споров, кому достался последний кусок.","Тогда сразу две закажем. И салфеток побольше, у тебя опять весь стол будет в соусе.",4));
            deck.Add(new Talk(true,"Смотри, тут даже пицца с трюфелем есть. А я всё время одну маргариту беру.","Ну попробуй что-нибудь новое. Только сначала цену посмотри, любитель экспериментов.",6));
            string[] street={"Автобус уже ушёл?|Ничего, следующий через десять минут.","Ночью дождь обещали.|Я зонт ещё утром в машине оставила.","У тебя телефон ловит?|Только возле перекрёстка, здесь опять пропадает сеть.","Ключи не видел?|Посмотри в кармане куртки, ты всегда туда кладёшь.","В парке фонари починили.|Отлично, можно вечером прогуляться.","Магазин ещё открыт?|До одиннадцати, успеем.","Сосед опять ремонт затеял.|Хоть бы предупредил заранее.","Смотри, новый фасад сделали.|Да, раньше на этом месте совсем другое здание было.","На работе сегодня тихо?|Тихо было, пока начальник не приехал.","Пойдём пешком, тут недалеко.|Только через переход, не перебегай дорогу."};
            string[] cafe={"Тебе какой кусок оставить?|Тот, где больше сыра.","Острая нормально?|Лучше соус отдельно попросим.","С собой будем брать?|Одну коробку домой, остальное здесь.","Ты уже заказал?|Нет, ждал, пока ты выберешь.","Чай остыл.|Ты меню уже полчаса читаешь.","Возьмём сорок два сантиметра?|На двоих многовато, позовём ещё кого-нибудь.","Гавайскую любишь?|Сегодня давай без ананасов.","Пахнет свежим тестом.|Кажется, только что новую партию в печь поставили.","Мне без лука, пожалуйста.|Не забудь сказать на кассе.","Сфотографируй меню.|Сначала поедим, потом будем всем показывать."};
            foreach(string s in street){var p=s.Split('|');deck.Add(new Talk(false,p[0],p[1]));}
            foreach(string s in cafe){var p=s.Split('|');deck.Add(new Talk(true,p[0],p[1]));}
            source=gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=2;source.maxDistance=14;source.volume=0.65f;
        }
        void OnEnable(){StartCoroutine(Run());}
        IEnumerator Run()
        {
            yield return new WaitForSeconds(10);
            while(true)
            {
                player=FindAnyObjectByType<ForestPlayerController>();
                var story=StoryQuestManager.Instance;
                if(player==null||!player.IsControlEnabled||story==null||story.IsMenuOpen){yield return new WaitForSeconds(2);continue;}
                var agents=FindObjectsByType<CityLifeAgent>(FindObjectsSortMode.None);
                CityLifeAgent a=null,b=null;
                foreach(var first in agents)
                {
                    if(first.vehicle||Vector3.Distance(first.transform.position,player.transform.position)>11)continue;
                    foreach(var second in agents)
                    {
                        if(first==second||second.vehicle||first.seated!=second.seated||Vector3.Distance(first.transform.position,second.transform.position)>5)continue;
                        a=first;b=second;break;
                    }
                    if(a!=null)break;
                }
                if(a!=null)
                {
                    var choices=new List<int>();
                    for(int i=0;i<deck.Count;i++)if(deck[i].cafe==a.seated&&deck[i].audio>=0)choices.Add(i);
                    if(choices.Count==0)for(int i=0;i<deck.Count;i++)if(deck[i].cafe==a.seated)choices.Add(i);
                    if(choices.Count>0)
                    {
                        int chosen=choices[Random.Range(0,choices.Count)];Talk talk=deck[chosen];deck.RemoveAt(chosen);
                        a.talkingUntil=b.talkingUntil=Time.time+35;
                        yield return Say(a,talk.a,talk.audio);
                        yield return Say(b,talk.b,talk.audio<0?-1:talk.audio+1);
                        a.talkingUntil=b.talkingUntil=Time.time;subtitle="";
                    }
                }
                yield return new WaitForSeconds(Random.Range(25,45));
            }
        }
        IEnumerator Say(CityLifeAgent actor,string line,int audio)
        {
            AudioClip clip=audio<0?null:Resources.Load<AudioClip>("Crowd_"+audio);
            source.transform.position=actor.transform.position+Vector3.up*1.4f;
            source.clip=clip;if(clip!=null)source.Play();
            float duration=clip!=null?clip.length:Mathf.Max(3,line.Length/14f);
            for(float t=0;t<duration;t+=Time.deltaTime)
            {
                if(player==null||!player.IsControlEnabled||StoryQuestManager.Instance.IsMenuOpen){source.Stop();subtitle="";yield break;}
                subtitle=Vector3.Distance(player.transform.position,actor.transform.position)<12?(actor.seated?"Посетитель: ":"Прохожий: ")+line:"";
                yield return null;
            }
            subtitle="";yield return new WaitForSeconds(0.4f);
        }
        void OnDisable(){StopAllCoroutines();subtitle="";if(source!=null)source.Stop();}
        void OnGUI()
        {
            if(string.IsNullOrEmpty(subtitle))return;
            GUI.Box(new Rect(Screen.width*0.16f,Screen.height*0.71f,Screen.width*0.68f,75),subtitle,
                new GUIStyle(GUI.skin.box){fontSize=19,wordWrap=true,alignment=TextAnchor.MiddleCenter});
        }
    }
}
