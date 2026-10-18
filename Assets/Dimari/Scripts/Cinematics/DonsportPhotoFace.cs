using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dimari.Cinematics
{
    // Deliberately flat rectangular photo, requested instead of mesh projection.
    public class DonsportPhotoFace : MonoBehaviour
    {
        public Material faceMaterial;
        public Texture2D faceTexture;
        public float photoWidth = 0.22f;
        public float forwardOffset = 0.145f;
        public float verticalOffset = -0.025f;
        Material runtimeMaterial;
        Mesh photoMesh;
        GameObject photo;
        Transform head;
        Quaternion headToActor;
        void Start()
        {
            var animator = GetComponentInChildren<Animator>();
            if (faceTexture == null) faceTexture = Resources.Load<Texture2D>("DonsportMaybeBabyFace");
            if (animator == null || !animator.isHuman || faceTexture == null || faceMaterial == null)
            { Debug.LogWarning("[Dimari] Rectangular photo skipped: missing rig, photo or material.", this); return; }
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;
            headToActor = Quaternion.Inverse(head.rotation) * transform.rotation;
            runtimeMaterial = new Material(faceMaterial);
            runtimeMaterial.SetTexture("_MainTex", faceTexture);
            photoMesh = new Mesh { name = "Rectangular head photograph" };
            photoMesh.vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0), new Vector3(0.5f,0.5f,0), new Vector3(-0.5f,0.5f,0) };
            photoMesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            photoMesh.triangles = new[] { 0,2,1,0,3,2 };
            photoMesh.RecalculateNormals(); photoMesh.RecalculateBounds();
            // Separate root avoids inherited FBX bone scale/shear; LateUpdate follows the head.
            photo = new GameObject("Rectangular photograph on head");
            SceneManager.MoveGameObjectToScene(photo, gameObject.scene);
            photo.AddComponent<MeshFilter>().sharedMesh = photoMesh;
            var renderer = photo.AddComponent<MeshRenderer>(); renderer.sharedMaterial = runtimeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            UpdatePhoto();
        }
        void UpdatePhoto()
        {
            if (photo == null || head == null) return;
            Quaternion rotation = head.rotation * headToActor;
            photo.transform.SetPositionAndRotation(head.position + rotation * new Vector3(0,verticalOffset,forwardOffset), rotation);
            float width = Mathf.Max(0.05f,photoWidth);
            photo.transform.localScale = new Vector3(width,width * faceTexture.height / (float)faceTexture.width,1);
            photo.SetActive(isActiveAndEnabled && head.gameObject.activeInHierarchy);
        }
        void LateUpdate() { UpdatePhoto(); }
        void OnDisable() { if (photo != null) photo.SetActive(false); }
        void OnEnable() { if (photo != null) UpdatePhoto(); }
        void OnDestroy()
        {
            if (photo != null) Destroy(photo);
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
            if (photoMesh != null) Destroy(photoMesh);
        }
    }
}
