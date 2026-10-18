using UnityEngine;
namespace Dimari.Cinematics
{
    // Photo is projected onto the rigged head surface, not a camera-facing quad.
    // Prototype fitting: requires visual validation for the particular imported humanoid.
    public class DonsportPhotoFace : MonoBehaviour
    {
        public Material faceMaterial;
        public Texture2D faceTexture;
        Material runtimeMaterial;
        Transform head;
        readonly System.Collections.Generic.List<SkinnedMeshRenderer> overlays = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        MaterialPropertyBlock properties;
        void Start()
        {
            var animator=GetComponentInChildren<Animator>();
            if(faceTexture==null)faceTexture=Resources.Load<Texture2D>("DonsportMaybeBabyFace");
            if(animator==null||!animator.isHuman||faceMaterial==null||faceTexture==null)
            { Debug.LogWarning("[Dimari] Face overlay skipped: missing rig, material or photo. Original face retained.",this); return; }
            runtimeMaterial=new Material(faceMaterial);
            runtimeMaterial.SetTexture("_MainTex",faceTexture);
            head=animator.GetBoneTransform(HumanBodyBones.Head); if(head==null)return;
            foreach(var original in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(original.sharedMesh==null)continue;
                var go=new GameObject("Photo face surface"); go.transform.SetParent(original.transform,false);
                var overlay=go.AddComponent<SkinnedMeshRenderer>();overlay.sharedMesh=original.sharedMesh;
                overlay.bones=original.bones;overlay.rootBone=original.rootBone;overlay.localBounds=original.localBounds;
                var materials=new Material[original.sharedMesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=runtimeMaterial;
                overlay.sharedMaterials=materials;overlay.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                overlays.Add(overlay);
            }
            properties=new MaterialPropertyBlock();
        }
        void OnDisable() { foreach(var overlay in overlays)if(overlay!=null)overlay.enabled=false; }
        void OnEnable() { foreach(var overlay in overlays)if(overlay!=null)overlay.enabled=true; }
        void OnDestroy() { if(runtimeMaterial!=null)Destroy(runtimeMaterial); }
        void LateUpdate()
        {
            if(head==null||properties==null)return;
            properties.SetMatrix("_FaceWorldToLocal",Matrix4x4.TRS(head.position-transform.up*0.025f,Quaternion.LookRotation(transform.forward,transform.up),Vector3.one).inverse);
            foreach(var overlay in overlays)if(overlay!=null)overlay.SetPropertyBlock(properties);
        }
    }
}
