using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dimari.Cinematics
{
    // Oversized opaque box head, deliberately covering the old face AND hair.
    [DefaultExecutionOrder(500)]
    public class DonsportPhotoFace : MonoBehaviour
    {
        public Material faceMaterial;
        public Texture2D faceTexture;
        // Legacy serialized fields retained for scene compatibility; no longer size the head.
        public float photoWidth = 0.22f, forwardOffset = 0.145f, verticalOffset = -0.025f;
        public Vector3 boxSize = new Vector3(0.44f,0.50f,0.44f);
        public float boxCenterAboveHead = 0.08f;
        Material photograph, sides;
        Texture2D sideTexture;
        Mesh mesh;
        GameObject box;
        Transform head;
        Quaternion headToActor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitialScene() { EnsureInstalled(); }
        static void SceneLoaded(Scene scene, LoadSceneMode mode) { EnsureInstalled(); }
        static void EnsureInstalled()
        {
            foreach(var story in FindObjectsByType<DonsportReceptionCutscene>(FindObjectsSortMode.None))
            {
                if(story.maybeBaby==null)continue;
                var face=story.maybeBaby.GetComponent<DonsportPhotoFace>();
                if(face==null)face=story.maybeBaby.gameObject.AddComponent<DonsportPhotoFace>();
                face.enabled=true;
            }
        }
        void Start()
        {
            var animator=GetComponentInChildren<Animator>();
            // Always prefer the shipped portrait, not a stale scene material texture.
            var bundled=Resources.Load<Texture2D>("DonsportMaybeBabyFace");
            if(bundled!=null)faceTexture=bundled;
            var bundledMaterial=Resources.Load<Material>("DonsportPhotoBox");
            var shader=bundledMaterial!=null?bundledMaterial.shader:(faceMaterial!=null?faceMaterial.shader:Shader.Find("Dimari/PhotoFaceDepth"));
            if(animator==null||!animator.isHuman||faceTexture==null||shader==null)
            { Debug.LogError("[Dimari] BOX HEAD NOT CREATED: missing Humanoid, portrait or PhotoFaceDepth shader.",this);return; }
            head=animator.GetBoneTransform(HumanBodyBones.Head);
            if(head==null){Debug.LogError("[Dimari] BOX HEAD NOT CREATED: no Head bone.",this);return;}
            headToActor=Quaternion.Inverse(head.rotation)*transform.rotation;
            photograph=new Material(shader);photograph.SetTexture("_MainTex",faceTexture);
            sideTexture=new Texture2D(1,1);sideTexture.SetPixel(0,0,new Color(0.18f,0.12f,0.09f));sideTexture.Apply();
            sides=new Material(shader);sides.SetTexture("_MainTex",sideTexture);
            mesh=new Mesh { name="Opaque rectangular photograph head" };
            // Each quad has independent UVs. Front (+Z) is the portrait; other five faces are opaque.
            var vertices=new Vector3[24];var uv=new Vector2[24];var front=new int[6];var other=new int[30];
            Vector3[][] faces={
                new[]{new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)},
                new[]{new Vector3(.5f,-.5f,-.5f),new Vector3(-.5f,-.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(.5f,.5f,-.5f)},
                new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(-.5f,.5f,.5f),new Vector3(-.5f,.5f,-.5f)},
                new[]{new Vector3(.5f,-.5f,.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(.5f,.5f,.5f)},
                new[]{new Vector3(-.5f,.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f)},
                new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,-.5f,.5f),new Vector3(-.5f,-.5f,.5f)} };
            for(int f=0;f<6;f++)
            {
                int n=f*4;for(int v=0;v<4;v++)vertices[n+v]=faces[f][v];
                uv[n]=new Vector2(0,0);uv[n+1]=new Vector2(1,0);uv[n+2]=new Vector2(1,1);uv[n+3]=new Vector2(0,1);
                int[] triangles={n,n+2,n+1,n,n+3,n+2};
                if(f==0)front=triangles;else System.Array.Copy(triangles,0,other,(f-1)*6,6);
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.subMeshCount=2;mesh.SetTriangles(front,0);mesh.SetTriangles(other,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            box=new GameObject("MAYBE BABY — PHOTO BOX HEAD v3");SceneManager.MoveGameObjectToScene(box,gameObject.scene);
            box.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=box.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{photograph,sides};
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            UpdateBox();
            Debug.Log("[Dimari] PHOTO BOX HEAD v3 created: opaque head cover with portrait. Size="+boxSize,this);
        }
        void UpdateBox()
        {
            if(box==null||head==null)return;
            Quaternion rotation=head.rotation*headToActor;
            box.transform.SetPositionAndRotation(head.position+rotation*new Vector3(0,boxCenterAboveHead,0),rotation);
            // Old photoWidth/offset values cannot shrink this replacement back inside the original head.
            box.transform.localScale=new Vector3(Mathf.Max(.38f,boxSize.x),Mathf.Max(.44f,boxSize.y),Mathf.Max(.38f,boxSize.z));
            box.SetActive(isActiveAndEnabled&&head.gameObject.activeInHierarchy);
        }
        void LateUpdate(){UpdateBox();}
        void OnDisable(){if(box!=null)box.SetActive(false);}
        void OnEnable(){if(box!=null)UpdateBox();}
        void OnDestroy()
        {
            if(box!=null)Destroy(box);if(mesh!=null)Destroy(mesh);
            if(photograph!=null)Destroy(photograph);if(sides!=null)Destroy(sides);if(sideTexture!=null)Destroy(sideTexture);
        }
    }
}
