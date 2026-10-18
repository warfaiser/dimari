using UnityEngine;
using Dimari.Story;

namespace Dimari.City
{
    // In-place procedural Humanoid exercises; no paid assets, root motion or route changes.
    [DefaultExecutionOrder(250)]
    public class DonsportExercise : MonoBehaviour
    {
        public enum Activity { Treadmill, SeatedCurls, Stretch }
        public Activity activity;
        public Vector3 seatWorldPosition;
        public float phase;
        Animator animator;
        bool animatorEnabled, ready;
        Transform[] bones;
        Quaternion[] rest;
        Vector3 basePosition;
        float elapsed;
        readonly HumanBodyBones[] ids={HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,
            HumanBodyBones.Spine,HumanBodyBones.Hips};
        void Start()
        {
            animator=GetComponentInChildren<Animator>();
            if(animator==null||!animator.isHuman){enabled=false;return;}
            animatorEnabled=animator.enabled;animator.enabled=false;
            bones=new Transform[ids.Length];rest=new Quaternion[ids.Length];
            for(int i=0;i<ids.Length;i++){bones[i]=animator.GetBoneTransform(ids[i]);if(bones[i]!=null)rest[i]=bones[i].localRotation;}
            if(activity==Activity.SeatedCurls && bones[9]!=null)
                transform.position+=seatWorldPosition-bones[9].position;
            basePosition=transform.position;ready=true;
        }
        void ResetPose()
        {if(bones!=null)for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=rest[i];}
        void Rotate(int i,float x,float z=0)
        {if(bones[i]!=null)bones[i].localRotation=rest[i]*Quaternion.Euler(x,0,z);}
        void Aim(HumanBodyBones id,HumanBodyBones child,Vector3 direction)
        {
            var a=animator.GetBoneTransform(id);var b=animator.GetBoneTransform(child);
            if(a!=null&&b!=null&&(b.position-a.position).sqrMagnitude>0.00001f)
                a.rotation=Quaternion.FromToRotation(b.position-a.position,direction.normalized)*a.rotation;
        }
        void LateUpdate()
        {
            if(!ready)return;
            var story=StoryQuestManager.Instance;
            if(story==null||!story.HasGameStarted||story.IsMenuOpen)return;
            elapsed+=Time.deltaTime;float t=elapsed+phase;
            ResetPose();transform.position=basePosition;
            if(activity==Activity.Treadmill)
            {
                float swing=Mathf.Sin(t*7.5f),other=-swing;
                Rotate(0,swing*28);Rotate(1,other*28);
                Rotate(2,Mathf.Max(0,-swing)*55);Rotate(3,Mathf.Max(0,-other)*55);
                Rotate(4,-swing*22);Rotate(5,swing*22);Rotate(6,-65);Rotate(7,-65);
                Rotate(8,7);transform.position+=transform.up*(Mathf.Abs(Mathf.Sin(t*7.5f))*0.035f);
            }
            else if(activity==Activity.SeatedCurls)
            {
                Aim(HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,transform.forward);
                Aim(HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,transform.forward);
                Aim(HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,-transform.up);
                Aim(HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,-transform.up);
                float curl=(Mathf.Sin(t*1.8f)+1)*0.5f;
                Aim(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,-transform.up-transform.right*0.12f);
                Aim(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,-transform.up+transform.right*0.12f);
                Vector3 lift=Vector3.Slerp(-transform.up,transform.up+transform.forward*0.35f,curl);
                Aim(HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,lift);
                Aim(HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,lift);
            }
            else
            {
                float lift=(Mathf.Sin(t*0.8f)+1)*0.5f;
                Aim(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,Vector3.Slerp(-transform.up-transform.right,transform.up-transform.right*0.25f,lift));
                Aim(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,Vector3.Slerp(-transform.up+transform.right,transform.up+transform.right*0.25f,lift));
                Rotate(8,0,Mathf.Sin(t*0.8f)*5);
            }
        }
        void OnDisable()
        {if(!ready)return;ResetPose();transform.position=basePosition;if(animator!=null)animator.enabled=animatorEnabled;}
        void OnEnable() { if(ready&&animator!=null)animator.enabled=false; }
    }
}
