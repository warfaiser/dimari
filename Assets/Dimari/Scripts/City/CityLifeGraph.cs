using UnityEngine;
namespace Dimari.City
{
    public class CityLifeGraph : MonoBehaviour
    {
        [System.Serializable] public class Node { public Vector3 position; public int[] next; }
        public Node[] nodes;
    }
}
