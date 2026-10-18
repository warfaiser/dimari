using UnityEngine;
namespace Dimari.Cinematics
{
    // Photo is projected onto the rigged head surface, not a camera-facing quad.
    // Prototype fitting: requires visual validation for the particular imported humanoid.
    public class DonsportPhotoFace : MonoBehaviour
    {
        public Material faceMaterial;
        Transform head;
        readonly System.Collections.Generic.List<SkinnedMeshRenderer> overlays = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        MaterialPropertyBlock properties;
        void Start()
        {
            var animator=GetComponentInChildren<Animator>();
            if(animator==null||!animator.isHuman||faceMaterial==null)return;
            head=animator.GetBoneTransform(HumanBodyBones.Head); if(head==null)return;
            foreach(var original in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(original.sharedMesh==null)continue;
                var go=new GameObject("Photo face surface"); go.transform.SetParent(original.transform,false);
                var overlay=go.AddComponent<SkinnedMeshRenderer>();overlay.sharedMesh=original.sharedMesh;
                overlay.bones=original.bones;overlay.rootBone=original.rootBone;overlay.localBounds=original.localBounds;
                var materials=new Material[original.sharedMesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=faceMaterial;
                overlay.sharedMaterials=materials;overlay.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                overlays.Add(overlay);
            }
            properties=new MaterialPropertyBlock();
        }
        void LateUpdate()
        {
            if(head==null||properties==null)return;
            properties.SetMatrix("_FaceWorldToLocal",Matrix4x4.TRS(head.position-transform.up*0.025f,Quaternion.LookRotation(transform.forward,transform.up),Vector3.one).inverse);
            foreach(var overlay in overlays)if(overlay!=null)overlay.SetPropertyBlock(properties);
        }
    }
}
