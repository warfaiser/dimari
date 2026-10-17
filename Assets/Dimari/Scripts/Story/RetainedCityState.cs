using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Dimari.Story
{
    // Keep the actual city (including the parked vehicle and its runtime state), not a respawned copy.
    public static class RetainedCityState
    {
        static readonly List<GameObject> activeRoots = new List<GameObject>();
        static string path;
        static Material sky;
        static Color ambient, fogColor;
        static AmbientMode ambientMode;
        static float ambientIntensity, reflectionIntensity, fogDensity;
        static bool fog;
        static LightmapData[] lightmaps;
        public static void CaptureLighting(Scene city)
        {
            path=city.path; sky=RenderSettings.skybox; ambient=RenderSettings.ambientLight;
            ambientMode=RenderSettings.ambientMode;ambientIntensity=RenderSettings.ambientIntensity;
            reflectionIntensity=RenderSettings.reflectionIntensity;fog=RenderSettings.fog;
            fogColor=RenderSettings.fogColor;fogDensity=RenderSettings.fogDensity;lightmaps=LightmapSettings.lightmaps;
        }
        public static void Suspend(Scene city)
        {
            activeRoots.Clear();
            foreach(GameObject root in city.GetRootGameObjects())
                if(root.activeSelf){activeRoots.Add(root);root.SetActive(false);}
        }
        public static bool HasCity(string cityPath)
        { Scene city=SceneManager.GetSceneByPath(cityPath);return path==cityPath&&city.IsValid()&&city.isLoaded; }
        public static void Restore()
        {
            foreach(GameObject root in activeRoots)if(root!=null)root.SetActive(true);
            activeRoots.Clear();
            RenderSettings.skybox=sky;RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=ambientMode;
            RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.reflectionIntensity=reflectionIntensity;
            RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;
            if(lightmaps!=null)LightmapSettings.lightmaps=lightmaps;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){activeRoots.Clear();path=null;lightmaps=null;}
    }
}
