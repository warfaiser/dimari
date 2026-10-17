using UnityEngine;

namespace Dimari.City
{
    public class PizzaPest : MonoBehaviour
    {
        public Transform room;
        public float speed=0.3f, radius=0.025f;
        Vector3 target;
        float pause;
        Animator animator;
        void Start(){animator=GetComponentInChildren<Animator>();Pick();}
        void Pick()
        {
            target=room.TransformPoint(new Vector3(Random.Range(-5.5f,5.5f),0.055f,Random.Range(-4.4f,4.3f)));
            pause=Time.time+Random.Range(0.1f,1.4f);
        }
        void Update()
        {
            if(room==null)return;
            if(Time.time<pause){if(animator!=null)animator.speed=0;return;}
            Vector3 delta=target-transform.position;delta.y=0;
            if(delta.magnitude<0.08f){Pick();return;}
            Vector3 direction=delta.normalized;
            float distance=speed*Time.deltaTime;
            Vector3 origin=transform.position+Vector3.up*(radius+0.015f);
            bool blocked=false;
            foreach(RaycastHit hit in Physics.SphereCastAll(origin,radius,direction,distance+0.09f,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&hit.normal.y<0.6f){blocked=true;break;}
            Vector3 next=transform.position+direction*distance;
            // Do not cross unsupported gaps or climb onto furniture.
            if(blocked||!Physics.Raycast(next+Vector3.up*0.16f,Vector3.down,out RaycastHit ground,0.3f,~0,QueryTriggerInteraction.Ignore)
                ||ground.normal.y<0.8f||Mathf.Abs(ground.point.y-transform.position.y)>0.12f){Pick();return;}
            next.y=ground.point.y+0.005f;transform.position=next;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*420);
            if(animator!=null)animator.speed=1;
        }
    }
}
