using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Dimari.Atmosphere
{
    /// <summary>
    /// Аудиосистема леса и озвучки главного героя:
    /// Автоматически загружает:
    /// 1) Голосовую озвучку героя из Assets/Dimari/Audio/Voice/
    /// 2) Студийные звуки леса и шагов из «Nature - Essentials» и «Footsteps - Essentials»
    /// Поддерживает настройку громкости из меню (Общая громкость, Озвучка, Природа и шаги).
    /// </summary>
    public class ProceduralForestAudio : MonoBehaviour
    {
        public static ProceduralForestAudio Instance { get; private set; }

        [Header("Громкость (настраивается в меню Параметры)")]
        [Range(0f, 1f)] public float masterVolume = 1.0f;
        [Range(0f, 1f)] public float voiceVolume = 1.0f;
        [Range(0f, 1f)] public float windVolume = 0.55f;
        [Range(0f, 1f)] public float birdsVolume = 0.50f;
        [Range(0f, 1f)] public float footstepVolume = 0.85f;

        [Header("Голосовая озвучка главного героя (загружается автоматически)")]
        public AudioClip voiceAwakeningIntro;
        public AudioClip voiceQuestBackpack;
        public AudioClip voiceQuestTablet;
        public AudioClip voiceQuestRogovo;

        [Header("Студийные звуки из Asset Store (заполняются автоматически)")]
        public List<AudioClip> forestAmbienceLoops = new List<AudioClip>();
        public List<AudioClip> birdChirpClips = new List<AudioClip>();
        public List<AudioClip> grassWalkClips = new List<AudioClip>();
        public List<AudioClip> grassRunClips = new List<AudioClip>();
        public List<AudioClip> rockWalkClips = new List<AudioClip>();
        public List<AudioClip> jumpLandClips = new List<AudioClip>();

        private AudioSource _windSource;
        private AudioSource _secondaryAmbienceSource;
        private AudioSource _sfxSource;
        private AudioSource _voiceSource;

        private AudioClip[] _fallbackFootstepClips;
        private AudioClip[] _fallbackBirdClips;
        private AudioClip _breathClip;
        private AudioClip _interactClip;

        private float _birdTimer = 2.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Загружаем сохранённые настройки громкости
            masterVolume = PlayerPrefs.GetFloat("Dimari_MasterVol", 1.0f);
            voiceVolume = PlayerPrefs.GetFloat("Dimari_VoiceVol", 1.0f);
            float envVol = PlayerPrefs.GetFloat("Dimari_EnvVol", 0.7f);
            windVolume = envVol * 0.8f;
            birdsVolume = envVol * 0.7f;
            footstepVolume = Mathf.Clamp01(envVol * 1.15f);

            EnsureAudioListenerActiveAndUnmuted();

#if UNITY_EDITOR
            AutoLoadClipsFromAssetDatabase();
#endif

            _windSource = gameObject.AddComponent<AudioSource>();
            _windSource.loop = true;
            _windSource.spatialBlend = 0f;
            _windSource.playOnAwake = false;
            _windSource.volume = windVolume * masterVolume;

            _secondaryAmbienceSource = gameObject.AddComponent<AudioSource>();
            _secondaryAmbienceSource.loop = true;
            _secondaryAmbienceSource.spatialBlend = 0f;
            _secondaryAmbienceSource.playOnAwake = false;
            _secondaryAmbienceSource.volume = windVolume * 0.75f * masterVolume;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.spatialBlend = 0f;
            _sfxSource.playOnAwake = false;

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.loop = false;
            _voiceSource.spatialBlend = 0f;
            _voiceSource.playOnAwake = false;
            _voiceSource.priority = 0; // Максимальный приоритет для голоса героя
            _voiceSource.volume = voiceVolume * masterVolume;

            GenerateFallbackClips();

            if (forestAmbienceLoops != null && forestAmbienceLoops.Count > 0 && forestAmbienceLoops[0] != null)
            {
                _windSource.clip = forestAmbienceLoops[0];
                _windSource.Play();

                if (forestAmbienceLoops.Count > 1 && forestAmbienceLoops[1] != null)
                {
                    _secondaryAmbienceSource.clip = forestAmbienceLoops[1];
                    _secondaryAmbienceSource.Play();
                }
            }
            else
            {
                _windSource.clip = CreateForestWindClip(8f);
                _windSource.Play();
            }
        }

        public void ApplyVolumeSettings(float master, float voice, float environment)
        {
            masterVolume = Mathf.Clamp01(master);
            voiceVolume = Mathf.Clamp01(voice);
            float env = Mathf.Clamp01(environment);

            windVolume = env * 0.8f;
            birdsVolume = env * 0.7f;
            footstepVolume = Mathf.Clamp01(env * 1.15f);

            AudioListener.volume = masterVolume;

            if (_windSource != null) _windSource.volume = windVolume;
            if (_secondaryAmbienceSource != null) _secondaryAmbienceSource.volume = windVolume * 0.75f;
            if (_voiceSource != null) _voiceSource.volume = voiceVolume;

            PlayerPrefs.SetFloat("Dimari_MasterVol", masterVolume);
            PlayerPrefs.SetFloat("Dimari_VoiceVol", voiceVolume);
            PlayerPrefs.SetFloat("Dimari_EnvVol", env);
            PlayerPrefs.Save();
        }

        public void PlayAwakeningIntroVoice()
        {
            if (_voiceSource == null) return;

#if UNITY_EDITOR
            if (voiceAwakeningIntro == null)
            {
                voiceAwakeningIntro = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dimari/Audio/Voice/Voice_Awakening_Intro.wav");
            }
#endif
            if (voiceAwakeningIntro != null)
            {
                _voiceSource.Stop();
                _voiceSource.clip = voiceAwakeningIntro;
                _voiceSource.volume = voiceVolume;
                _voiceSource.Play();
            }
        }

        public void PlayQuestVoiceClip(string questStepId)
        {
            if (_voiceSource == null) return;

            AudioClip clipToPlay = null;
            if (questStepId == "backpack" || questStepId == "find_backpack") clipToPlay = voiceQuestBackpack;
            else if (questStepId == "tablet" || questStepId == "find_tablet") clipToPlay = voiceQuestTablet;
            else if (questStepId == "rogovo_exit") clipToPlay = voiceQuestRogovo;

            if (clipToPlay != null)
            {
                _voiceSource.Stop();
                _voiceSource.clip = clipToPlay;
                _voiceSource.volume = voiceVolume;
                _voiceSource.Play();
            }
        }

        public void StopVoice()
        {
            if (_voiceSource != null && _voiceSource.isPlaying)
            {
                _voiceSource.Stop();
            }
        }

        private void EnsureAudioListenerActiveAndUnmuted()
        {
            AudioListener.volume = masterVolume;
            AudioListener.pause = false;

#if UNITY_EDITOR
            EditorUtility.audioMasterMute = false;
#endif

            AudioListener[] listeners = FindObjectsByType<AudioListener>();
            bool hasActiveListener = false;
            foreach (AudioListener l in listeners)
            {
                if (l != null && l.enabled && l.gameObject.activeInHierarchy)
                {
                    if (!hasActiveListener)
                    {
                        hasActiveListener = true;
                    }
                    else
                    {
                        l.enabled = false;
                    }
                }
            }

            if (!hasActiveListener)
            {
                Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    AudioListener listener = cam.GetComponent<AudioListener>();
                    if (listener == null) listener = cam.gameObject.AddComponent<AudioListener>();
                    listener.enabled = true;
                }
                else
                {
                    gameObject.AddComponent<AudioListener>();
                }
            }
        }

#if UNITY_EDITOR
        public int AutoLoadClipsFromAssetDatabase()
        {
            // Загружаем файлы озвучки героя
            voiceAwakeningIntro = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dimari/Audio/Voice/Voice_Awakening_Intro.wav");
            voiceQuestBackpack = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dimari/Audio/Voice/Voice_Quest_Backpack.wav");
            voiceQuestTablet = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dimari/Audio/Voice/Voice_Quest_Tablet.wav");
            voiceQuestRogovo = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dimari/Audio/Voice/Voice_Quest_Rogovo.wav");

            forestAmbienceLoops.Clear();
            birdChirpClips.Clear();
            grassWalkClips.Clear();
            grassRunClips.Clear();
            rockWalkClips.Clear();
            jumpLandClips.Clear();

            int totalAssigned = 0;
            if (voiceAwakeningIntro != null) totalAssigned++;
            if (voiceQuestBackpack != null) totalAssigned++;
            if (voiceQuestTablet != null) totalAssigned++;
            if (voiceQuestRogovo != null) totalAssigned++;

            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip");

            foreach (string guid in audioGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                // Пропускаем папку голосовой озвучки при сборе звуков природы
                if (lower.Contains("/voice/") || fileName.StartsWith("voice_"))
                    continue;

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                if ((lower.Contains("ambiance") || lower.Contains("ambience") || lower.Contains("nature") ||
                     fileName.Contains("forest") || fileName.Contains("wind") || fileName.Contains("breeze") || fileName.Contains("trees")) &&
                    !lower.Contains("footstep") && clip.length > 3.0f)
                {
                    if (fileName.Contains("forest") || fileName.Contains("wind"))
                        forestAmbienceLoops.Insert(0, clip);
                    else
                        forestAmbienceLoops.Add(clip);

                    totalAssigned++;
                    continue;
                }

                if (fileName.Contains("bird") || fileName.Contains("chirp") || fileName.Contains("sparrow") || fileName.Contains("crow"))
                {
                    birdChirpClips.Add(clip);
                    totalAssigned++;
                    continue;
                }

                if (fileName.Contains("jump") || fileName.Contains("land"))
                {
                    if (lower.Contains("grass") || lower.Contains("leaves") || lower.Contains("dirty") || !lower.Contains("water"))
                    {
                        jumpLandClips.Add(clip);
                        totalAssigned++;
                        continue;
                    }
                }

                if ((lower.Contains("rock") || lower.Contains("stone") || lower.Contains("gravel")) &&
                    (lower.Contains("footstep") || fileName.Contains("step") || fileName.Contains("walk") || lower.Contains("run")))
                {
                    rockWalkClips.Add(clip);
                    totalAssigned++;
                    continue;
                }

                bool isForestFloor = lower.Contains("grass") || lower.Contains("leaf") || lower.Contains("leaves") ||
                                     lower.Contains("dirtyground") || lower.Contains("dirt") || lower.Contains("mud");
                if (isForestFloor && (lower.Contains("footstep") || fileName.Contains("step") || fileName.Contains("walk") || fileName.Contains("run")))
                {
                    if (fileName.Contains("run") || fileName.Contains("sprint"))
                    {
                        grassRunClips.Add(clip);
                    }
                    else
                    {
                        grassWalkClips.Add(clip);
                    }
                    totalAssigned++;
                    continue;
                }
            }

            if (grassWalkClips.Count == 0)
            {
                foreach (string guid in audioGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    if (fileName.Contains("footstep") || fileName.Contains("step"))
                    {
                        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        if (clip != null)
                        {
                            grassWalkClips.Add(clip);
                            totalAssigned++;
                        }
                    }
                }
            }

            return totalAssigned;
        }
#endif

        private void Update()
        {
            _birdTimer -= Time.deltaTime;
            if (_birdTimer <= 0f)
            {
                _birdTimer = Random.Range(4.0f, 9.0f);
                if (birdsVolume > 0.01f)
                {
                    if (birdChirpClips != null && birdChirpClips.Count > 0)
                    {
                        AudioClip clip = birdChirpClips[Random.Range(0, birdChirpClips.Count)];
                        if (clip != null)
                        {
                            _sfxSource.pitch = Random.Range(0.95f, 1.05f);
                            _sfxSource.PlayOneShot(clip, birdsVolume * Random.Range(0.85f, 1.15f));
                            return;
                        }
                    }

                    if (_fallbackBirdClips != null && _fallbackBirdClips.Length > 0)
                    {
                        AudioClip clip = _fallbackBirdClips[Random.Range(0, _fallbackBirdClips.Length)];
                        _sfxSource.pitch = 1f;
                        _sfxSource.PlayOneShot(clip, birdsVolume * Random.Range(0.85f, 1.15f));
                    }
                }
            }
        }

        public void PlayFootstep(bool isRunning, bool isOnRock = false)
        {
            float vol = footstepVolume * (isRunning ? 1.15f : 0.9f);
            _sfxSource.pitch = Random.Range(0.93f, 1.07f);

            if (isOnRock && rockWalkClips != null && rockWalkClips.Count > 0)
            {
                AudioClip rockClip = rockWalkClips[Random.Range(0, rockWalkClips.Count)];
                if (rockClip != null)
                {
                    _sfxSource.PlayOneShot(rockClip, vol);
                    return;
                }
            }

            if (isRunning && grassRunClips != null && grassRunClips.Count > 0)
            {
                AudioClip runClip = grassRunClips[Random.Range(0, grassRunClips.Count)];
                if (runClip != null)
                {
                    _sfxSource.PlayOneShot(runClip, vol);
                    return;
                }
            }

            if (grassWalkClips != null && grassWalkClips.Count > 0)
            {
                AudioClip walkClip = grassWalkClips[Random.Range(0, grassWalkClips.Count)];
                if (walkClip != null)
                {
                    _sfxSource.PlayOneShot(walkClip, vol);
                    return;
                }
            }

            if (_fallbackFootstepClips != null && _fallbackFootstepClips.Length > 0)
            {
                AudioClip clip = _fallbackFootstepClips[Random.Range(0, _fallbackFootstepClips.Length)];
                _sfxSource.PlayOneShot(clip, vol);
            }
        }

        public void PlayJumpLand()
        {
            if (jumpLandClips != null && jumpLandClips.Count > 0)
            {
                AudioClip clip = jumpLandClips[Random.Range(0, jumpLandClips.Count)];
                if (clip != null)
                {
                    _sfxSource.pitch = Random.Range(0.95f, 1.05f);
                    _sfxSource.PlayOneShot(clip, footstepVolume * 1.15f);
                    return;
                }
            }
            PlayFootstep(true, false);
        }

        public void PlayWakingBreath()
        {
            if (_breathClip != null)
            {
                _sfxSource.pitch = 1f;
                _sfxSource.PlayOneShot(_breathClip, 0.75f);
            }
        }

        public void PlayInteractSound()
        {
            if (_interactClip != null)
            {
                _sfxSource.pitch = 1f;
                _sfxSource.PlayOneShot(_interactClip, 0.65f);
            }
        }

        private void GenerateFallbackClips()
        {
            _fallbackFootstepClips = new AudioClip[4];
            for (int i = 0; i < 4; i++)
            {
                _fallbackFootstepClips[i] = CreateGrassFootstepClip(0.24f, 1000 + i * 337);
            }

            _fallbackBirdClips = new AudioClip[3];
            _fallbackBirdClips[0] = CreateBirdChirpClip(1.2f, 2400f, 3100f);
            _fallbackBirdClips[1] = CreateBirdChirpClip(1.4f, 1950f, 2650f);
            _fallbackBirdClips[2] = CreateBirdChirpClip(1.1f, 2800f, 3400f);

            _breathClip = CreateBreathClip(2.2f);
            _interactClip = CreateDiscoveryChimeClip(0.9f);
        }

        private AudioClip CreateForestWindClip(float durationSeconds)
        {
            int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            float[] data = new float[sampleCount];

            float lowPass1 = 0f;
            float lowPass2 = 0f;
            System.Random rng = new System.Random(42);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);

                float gust = 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f / durationSeconds)
                                  + 0.25f * Mathf.Sin(t * Mathf.PI * 4f / durationSeconds);

                lowPass1 += 0.05f * (white - lowPass1);
                lowPass2 += 0.02f * (lowPass1 - lowPass2);

                float leavesRustle = (lowPass1 - lowPass2) * 2.4f;
                float sample = (lowPass2 * 1.2f + leavesRustle * 0.9f) * gust * 0.85f;
                data[i] = Mathf.Clamp(sample, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("ProceduralForestWind", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateGrassFootstepClip(float durationSeconds, int seed)
        {
            int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(seed);

            float filter = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float norm = (float)i / sampleCount;
                float env = Mathf.Sin(norm * Mathf.PI) * Mathf.Exp(-norm * 3.2f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                filter += 0.22f * (noise - filter);

                float thud = Mathf.Sin(norm * 95f * Mathf.PI * 2f * durationSeconds) * Mathf.Exp(-norm * 12f) * 0.55f;
                float crunch = (noise * 0.5f + filter * 0.7f) * env;
                data[i] = Mathf.Clamp((thud + crunch) * 0.95f, -0.98f, 0.98f);
            }

            AudioClip clip = AudioClip.Create("GrassFootstep_" + seed, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateBirdChirpClip(float durationSeconds, float freqStart, float freqEnd)
        {
            int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            float[] data = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float norm = (float)i / sampleCount;

                float note1 = Mathf.Exp(-Mathf.Pow((norm - 0.25f) * 8f, 2f));
                float note2 = Mathf.Exp(-Mathf.Pow((norm - 0.62f) * 7f, 2f));
                float env = (note1 + note2 * 0.85f) * 0.45f;

                float vibrato = Mathf.Sin(t * 28f * Mathf.PI * 2f) * 90f;
                float freq = Mathf.Lerp(freqStart, freqEnd, (norm * 2.3f) % 1f) + vibrato;
                phase += freq / sampleRate;

                data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * env;
            }

            AudioClip clip = AudioClip.Create("ForestBird", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateBreathClip(float durationSeconds)
        {
            int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(777);
            float lp = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float norm = (float)i / sampleCount;
                float env = Mathf.Sin(norm * Mathf.PI) * Mathf.Exp(-norm * 1.4f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.08f * (noise - lp);
                data[i] = Mathf.Clamp(lp * env * 0.65f, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("WakeBreath", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateDiscoveryChimeClip(float durationSeconds)
        {
            int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float norm = (float)i / sampleCount;
                float env = Mathf.Exp(-norm * 4.2f) * Mathf.Clamp01(norm * 25f);
                float s = (Mathf.Sin(t * 440f * Mathf.PI * 2f) + 0.6f * Mathf.Sin(t * 659.25f * Mathf.PI * 2f)) * 0.45f;
                data[i] = s * env;
            }

            AudioClip clip = AudioClip.Create("DiscoveryChime", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
