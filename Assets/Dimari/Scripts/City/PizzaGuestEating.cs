using UnityEngine;

namespace Dimari.City
{
    [DefaultExecutionOrder(200)]
    public class PizzaGuestEating : MonoBehaviour
    {
        public Transform cup, slice;
        public Vector3 platePosition;
        Animator animator;
        Transform upper, lower, hand, head, jaw;
        Quaternion jawRest, upperRest, lowerRest;
        Vector3 cupRest;
        Quaternion cupRotation;
        float offset;
        void Start()
        {
            animator=GetComponentInChildren<Animator>();if(animator==null||!animator.isHuman)return;
            upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);lower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand=animator.GetBoneTransform(HumanBodyBones.RightHand);head=animator.GetBoneTransform(HumanBodyBones.Head);jaw=animator.GetBoneTransform(HumanBodyBones.Jaw);
            if(jaw!=null)jawRest=jaw.localRotation;if(upper!=null)upperRest=upper.localRotation;if(lower!=null)lowerRest=lower.localRotation;
            if(cup!=null){cupRest=cup.position;cupRotation=cup.rotation;}offset=Random.Range(0,18f);
        }
        void LateUpdate()
        {
            if(upper==null||lower==null||hand==null||head==null)return;
            float phase=(Time.time+offset)%22;
            bool drink=phase>=12&&phase<17;
            bool eat=phase>=3&&phase<8;
            float lift=0;
            if(eat)lift=Mathf.SmoothStep(0,1,Mathf.Clamp01((phase-3)/1.2f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((8-phase)/1.2f));
            if(drink)lift=Mathf.SmoothStep(0,1,Mathf.Clamp01((phase-12)/1.2f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((17-phase)/1.2f));
            upper.localRotation=upperRest;lower.localRotation=lowerRest;
            Vector3 rest=drink&&cup!=null?cupRest+Vector3.up*0.05f:platePosition+Vector3.up*0.06f;
            Vector3 mouth=head.position+transform.forward*0.12f+Vector3.up*0.015f;
            Solve(upper,lower,hand,Vector3.Lerp(rest,mouth,lift),transform.right+transform.forward*0.3f);
            if(cup!=null)
            {
                cup.position=drink&&lift>0.04f?hand.position+Vector3.down*0.035f:cupRest;
                cup.rotation=drink?Quaternion.AngleAxis(-lift*30,transform.right)*cupRotation:cupRotation;
            }
            if(slice!=null){slice.gameObject.SetActive(eat&&lift>0.05f);slice.position=hand.position; slice.rotation=Quaternion.LookRotation(transform.forward,Vector3.up);}
            if(jaw!=null)jaw.localRotation=jawRest*Quaternion.Euler(eat&&lift>0.65f?5+Mathf.Abs(Mathf.Sin(Time.time*9))*9:0,0,0);
        }
        static void Solve(Transform a,Transform b,Transform c,Vector3 target,Vector3 pole)
        {
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);if(l1<0.001f||l2<0.001f)return;
            Vector3 delta=target-a.position;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+0.001f,l1+l2-0.001f);
            Vector3 dir=delta.normalized,bend=Vector3.ProjectOnPlane(pole,dir).normalized;float x=(l1*l1-l2*l2+d*d)/(2*d);
            Vector3 elbow=a.position+dir*x+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-x*x));
            a.rotation=Quaternion.FromToRotation(b.position-a.position,elbow-a.position)*a.rotation;
            b.rotation=Quaternion.FromToRotation(c.position-b.position,target-b.position)*b.rotation;
        }
    }
}
