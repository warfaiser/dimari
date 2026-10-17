using UnityEngine;
using Dimari.Story;

namespace Dimari.City
{
    // Local-room wandering: no NavMesh rebuild, root motion, or changes to the city population.
    public class PizzaPest : MonoBehaviour
    {
        public Transform room;
        public float speed=0.8f, radius=0.035f;
        float pauseUntil, runUntil, turnAt, heading, pace=1, animationRate=1;
        Animator animator;
        Animation legacy;
        readonly RaycastHit[] groundHits=new RaycastHit[24];
        readonly RaycastHit[] sweepHits=new RaycastHit[32];
        readonly Collider[] overlaps=new Collider[24];
        void Start()
        {
            animator=GetComponentInChildren<Animator>();legacy=GetComponentInChildren<Animation>();
            if(animator!=null){animator.applyRootMotion=false;animationRate=animator.speed;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
            BeginRun();pauseUntil=Time.time+Random.Range(0,1.8f);
            if(animator!=null&&animator.runtimeAnimatorController!=null)
            {animator.Update(0);animator.Play(0,0,Random.value);}
        }
        void BeginRun()
        {
            heading=Random.Range(-180f,180f);pace=Random.Range(0.75f,1.35f);
            runUntil=Time.time+Random.Range(2,6f);turnAt=Time.time+Random.Range(0.35f,1.4f);
        }
        void Moving(bool moving)
        {
            if(animator!=null)animator.speed=moving?animationRate*pace:0;
            if(legacy!=null)foreach(AnimationState state in legacy)state.speed=moving?pace:0;
        }
        bool Step(Vector3 direction,float distance,out Vector3 next)
        {
            next=transform.position+direction*distance;
            Vector3 local=room.InverseTransformPoint(next);
            if(Mathf.Abs(local.x)>5.5f||local.z< -4.45f||local.z>4.35f)return false;
            Vector3 origin=transform.position+Vector3.up*(radius+0.025f);
            int count=Physics.SphereCastNonAlloc(origin,radius,direction,sweepHits,distance+0.06f,~0,QueryTriggerInteraction.Ignore);
            if(count==sweepHits.Length)return false;
            for(int i=0;i<count;i++)if(!sweepHits[i].transform.IsChildOf(transform)&&sweepHits[i].normal.y<0.65f)return false;
            count=Physics.OverlapSphereNonAlloc(next+Vector3.up*(radius+0.025f),radius,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(!overlaps[i].transform.IsChildOf(transform))return false;
            count=Physics.RaycastNonAlloc(next+Vector3.up*0.13f,Vector3.down,groundHits,0.3f,~0,QueryTriggerInteraction.Ignore);
            if(count==groundHits.Length)return false;
            float nearest=float.PositiveInfinity;RaycastHit support=new RaycastHit();
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=groundHits[i];if(hit.transform.IsChildOf(transform)||hit.distance>=nearest)continue;
                support=hit;nearest=hit.distance;
            }
            if(float.IsInfinity(nearest)||support.normal.y<0.85f||Mathf.Abs(support.point.y-transform.position.y)>0.08f)return false;
            // Never use a person, another pest, or a movable physics prop as a floor.
            if(support.collider.attachedRigidbody!=null||support.collider.GetComponentInParent<CityLifeAgent>()!=null||support.collider.GetComponentInParent<PizzaPest>()!=null)return false;
            next.y=support.point.y+0.005f;return true;
        }
        void Update()
        {
            if(room==null||Time.deltaTime<=0||(StoryQuestManager.Instance!=null&&StoryQuestManager.Instance.IsMenuOpen)){Moving(false);return;}
            if(Time.time<pauseUntil){Moving(false);return;}
            if(Time.time>=runUntil)
            {BeginRun();pauseUntil=Time.time+Random.Range(0.25f,2.2f);Moving(false);return;}
            if(Time.time>=turnAt){heading+=Random.Range(-65f,65f);turnAt=Time.time+Random.Range(0.35f,1.4f);}
            float distance=Mathf.Min(speed*pace*Time.deltaTime,0.08f);
            int side=Random.value<0.5f?-1:1;
            for(int i=0;i<8;i++)
            {
                float offset=i==0?0:(i==7?180:((i+1)/2)*40*(i%2==1?side:-side));
                Vector3 direction=room.rotation*Quaternion.Euler(0,heading+offset,0)*Vector3.forward;
                direction.y=0;direction.Normalize();
                if(!Step(direction,distance,out Vector3 next))continue;
                heading+=offset;transform.position=next;
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*720);
                Moving(true);return;
            }
            // Do not teleport out of obstacles or gaps. Try another heading after a short random pause.
            BeginRun();pauseUntil=Time.time+Random.Range(0.2f,0.65f);Moving(false);
        }
        void OnDisable(){Moving(false);}
    }
}
