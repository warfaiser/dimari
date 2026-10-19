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
        public Vector3 boxSize = new Vector3(0.23f,0.28f,0.24f);
        public float boxCenterAboveHead = 0.08f;
        [HideInInspector] public int headSizingVersion;
        public void EnsureNormalHeadSize()
        {
            if(headSizingVersion>=1)return;
            boxSize=new Vector3(0.23f,0.28f,0.24f);
            headSizingVersion=1;
        }
        [HideInInspector] public GameObject scenePhotoBox;
        [HideInInspector] public bool originalHeadHidden;
        [HideInInspector] public Vector3 originalHeadScale = Vector3.one;
        public void HideOriginalHead()
        {
            var bone=FindHead(transform);if(bone==null)return;
            if(!originalHeadHidden){originalHeadScale=bone.localScale;originalHeadHidden=true;}
            // The replacement box is a separate scene root: shrinking the source bone
            // hides its skinned face/hair without shrinking the photograph or the body.
            bone.localScale=originalHeadScale*0.001f;
        }
        void RestoreOriginalHead()
        {
            if(!originalHeadHidden)return;
            var bone=FindHead(transform);if(bone!=null)bone.localScale=originalHeadScale;
        }
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
            foreach(var story in FindObjectsByType<DonsportReceptionCutscene>())
            {
                if(story.maybeBaby==null)continue;
                var face=story.maybeBaby.GetComponent<DonsportPhotoFace>();
                if(face==null)face=story.maybeBaby.gameObject.AddComponent<DonsportPhotoFace>();
                face.enabled=true;
            }
        }
        public static Transform FindHead(Transform actor)
        {
            var animator=actor.GetComponentInChildren<Animator>(true);
            if(animator!=null&&animator.avatar!=null&&animator.avatar.isValid&&animator.isHuman)
            {
                var bone=animator.GetBoneTransform(HumanBodyBones.Head);
                if(bone!=null)return bone;
            }
            // Imported rigs can have no usable Humanoid mapping outside Play.
            foreach(var bone in actor.GetComponentsInChildren<Transform>(true))
            {
                string name=bone.name.ToLowerInvariant();
                int separator=name.LastIndexOf(':');if(separator>=0)name=name.Substring(separator+1);
                if(name=="head"||name=="bip001 head"||name=="bip01 head"||name=="mixamorig_head")return bone;
            }
            return null;
        }
        void Start()
        {
            EnsureNormalHeadSize();
            head=FindHead(transform);
            if(head==null){Debug.LogError("[Dimari] BOX HEAD: Head bone not found on "+name,this);return;}
            headToActor=Quaternion.Inverse(head.rotation)*transform.rotation;
            // A saved scene box already carries valid material references. Reuse it first.
            if(scenePhotoBox!=null)
            {
                box=scenePhotoBox;HideOriginalHead();UpdateBox();
                Debug.Log("[Dimari] Using saved Scene photo box head v4.",this);
                return;
            }
            if(faceTexture==null)faceTexture=Resources.Load<Texture2D>("DonsportMaybeBabyFace");
            var bundledMaterial=Resources.Load<Material>("DonsportPhotoBox");
            var shader=faceMaterial!=null?faceMaterial.shader:(bundledMaterial!=null?bundledMaterial.shader:Shader.Find("Dimari/PhotoFaceDepth"));
            if(faceTexture==null||shader==null)
            { Debug.LogError("[Dimari] BOX HEAD: missing portrait texture or shader. Run the Scene head setup command.",this);return; }
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
            HideOriginalHead();UpdateBox();
            Debug.Log("[Dimari] PHOTO BOX HEAD v3 created: opaque head cover with portrait. Size="+boxSize,this);
        }
        void UpdateBox()
        {
            if(box==null||head==null)return;
            Quaternion rotation=head.rotation*headToActor;
            box.transform.SetPositionAndRotation(head.position+rotation*new Vector3(0,boxCenterAboveHead,0),rotation);
            // Old photoWidth/offset values cannot shrink this replacement back inside the original head.
            box.transform.localScale=new Vector3(Mathf.Max(.10f,boxSize.x),Mathf.Max(.12f,boxSize.y),Mathf.Max(.10f,boxSize.z));
            box.SetActive(isActiveAndEnabled&&head.gameObject.activeInHierarchy);
        }
        void LateUpdate(){if(box!=null)HideOriginalHead();UpdateBox();}
        void OnDisable(){if(box!=null){box.SetActive(false);RestoreOriginalHead();}}
        void OnEnable(){if(box!=null){HideOriginalHead();UpdateBox();}}
        void OnDestroy()
        {
            if(box!=null)Destroy(box);if(mesh!=null)Destroy(mesh);
            if(photograph!=null)Destroy(photograph);if(sides!=null)Destroy(sides);if(sideTexture!=null)Destroy(sideTexture);
        }
    }
}
