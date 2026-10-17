using UnityEngine;
using Dimari.Player;
using Dimari.Story;

namespace Dimari.City
{
    public class CityLifeAgent : MonoBehaviour
    {
        public CityLifeGraph graph;
        public int node, previous=-1;
        public float speed=1.1f;
        public bool vehicle, seated;
        public float talkingUntil;
        Rigidbody body;
        Animator animator;
        Transform[] bones;
        Quaternion[] rest;
        Vector3 lastPosition;
        float phase;
        void Start()
        {
            body=GetComponent<Rigidbody>();animator=GetComponentInChildren<Animator>();
            if(animator!=null&&animator.isHuman)
            {
                animator.enabled=false;
                HumanBodyBones[] ids={HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.Head};
                bones=new Transform[ids.Length];rest=new Quaternion[ids.Length];
                for(int i=0;i<ids.Length;i++){bones[i]=animator.GetBoneTransform(ids[i]);if(bones[i]!=null)rest[i]=bones[i].localRotation;}
            }
            lastPosition=transform.position;phase=Random.value*6;
        }
        void FixedUpdate()
        {
            if(seated||graph==null||graph.nodes==null||node<0||node>=graph.nodes.Length||Time.time<talkingUntil)return;
            Vector3 target=graph.nodes[node].position;Vector3 delta=target-transform.position;delta.y=0;
            if(delta.magnitude<0.3f)
            {
                int[] links=graph.nodes[node].next;if(links==null||links.Length==0)return;
                int chosen=-1;Vector3 forward=transform.forward;
                for(int attempt=0;attempt<links.Length*3;attempt++)
                {
                    int next=links[Random.Range(0,links.Length)];
                    if(next==previous&&links.Length>1)continue;
                    Vector3 dir=(graph.nodes[next].position-target).normalized;
                    if(vehicle&&Vector3.Dot(forward,dir)<-0.1f)continue;
                    chosen=next;break;
                }
                if(chosen<0)return;previous=node;node=chosen;return;
            }
            Vector3 direction=delta.normalized;
            float range=vehicle?4.5f:0.9f;
            Vector3 center=transform.position+Vector3.up*(vehicle?0.85f:0.8f)+direction*(range*0.5f+0.7f);
            foreach(Collider hit in Physics.OverlapBox(center,new Vector3(vehicle?0.95f:0.28f,0.5f,range*0.5f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform))continue;
                // Stop for any solid obstacle, not only other agents. Ground stays below the probe.
                return;
            }
            Vector3 position=Vector3.MoveTowards(transform.position,target,speed*Time.fixedDeltaTime);
            Quaternion rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),Time.fixedDeltaTime*(vehicle?65:180));
            if(body!=null){body.MovePosition(position);body.MoveRotation(rotation);}else transform.SetPositionAndRotation(position,rotation);
        }
        void LateUpdate()
        {
            if(bones==null)return;
            bool moving=Vector3.Distance(transform.position,lastPosition)>0.0002f;lastPosition=transform.position;
            float swing=moving?Mathf.Sin(Time.time*5+phase)*22:0;
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null)
            {
                float angle=i==0?swing:i==1?-swing:i==2?Mathf.Max(0,-swing):i==3?Mathf.Max(0,swing):i==4?-swing*0.5f:i==5?swing*0.5f:Mathf.Sin(Time.time+phase)*2;
                if(Time.time<talkingUntil&&(i==4||i==5))angle+=Mathf.Sin(Time.time*2.7f+phase)*7;
                bones[i].localRotation=rest[i]*Quaternion.Euler(angle,0,0);
            }
        }
    }
}
