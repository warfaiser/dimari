using UnityEngine;
using Dimari.Player;

namespace Dimari.City
{
    // Generated buildings are absent from the vendor scene's baked occlusion data.
    // Disable camera occlusion only near this restaurant, restoring it on leaving.
    public class PizzaInteriorVisibility : MonoBehaviour
    {
        Camera cameraOwner;
        ForestPlayerController player;
        bool saved,previous;
        void Restore()
        {if(saved&&cameraOwner!=null)cameraOwner.useOcclusionCulling=previous;saved=false;}
        bool Near(Vector3 world)
        {Vector3 p=transform.InverseTransformPoint(world);return Mathf.Abs(p.x)<8&&Mathf.Abs(p.z)<8&&p.y> -1&&p.y<5;}
        void LateUpdate()
        {
            Camera main=Camera.main;
            if(main!=cameraOwner){Restore();cameraOwner=main;}
            if(cameraOwner==null)return;
            if(player==null)player=FindAnyObjectByType<ForestPlayerController>();
            bool inside=Near(cameraOwner.transform.position)||(player!=null&&Near(player.transform.position));
            if(inside)
            {if(!saved){previous=cameraOwner.useOcclusionCulling;saved=true;}cameraOwner.useOcclusionCulling=false;}
            else Restore();
        }
        void OnDisable(){Restore();}
    }
}
