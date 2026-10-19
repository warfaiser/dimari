using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dimari.Cinematics;

namespace Dimari.Editor
{
    [InitializeOnLoad]
    public static class DonsportMaybeBabySetup
    {
        const string AudioMeta =
            "AudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n" +
            "    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 1\n" +
            "    quality: 1\n    conversionMode: 0\n    preloadAudioData: 0\n  platformSettingOverrides: {}\n" +
            "  forceToMono: 0\n  normalize: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 1\n  userData:\n" +
            "  assetBundleName:\n  assetBundleVariant:\n";
        static bool audioMetasChecked;
        static void RepairAudioMetas()
        {
            if (audioMetasChecked) return;
            audioMetasChecked = true;
            int repaired = 0;
            foreach (string meta in Directory.GetFiles("Assets/Dimari/Resources", "*.wav.meta"))
            {
                string text = File.ReadAllText(meta);
                Match version = Regex.Match(text, @"AudioImporter:\s*.*?serializedVersion:\s*(\d+)", RegexOptions.Singleline);
                if (version.Success && int.Parse(version.Groups[1].Value) >= 6) continue;
                Match guid = Regex.Match(text, @"(?m)^guid:\s*([0-9a-fA-F]{32})");
                if (!guid.Success) continue;
                File.WriteAllText(meta, "fileFormatVersion: 2\nguid: " + guid.Groups[1].Value + "\n" + AudioMeta);
                repaired++;
            }
            if (repaired > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[Dimari] Исправлены метаданные AudioImporter у " + repaired + " WAV, GUID сохранены.");
            }
        }
        static DonsportMaybeBabySetup()
        {
            EditorApplication.delayCall += Auto;
            EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += Auto;
        }
        static void Auto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) { audioMetasChecked = false; return; }
            RepairAudioMetas();
            var story = Object.FindAnyObjectByType<DonsportReceptionCutscene>();
            if (story == null || story.maybeBaby == null) return;
            Ensure(story, false);
        }

        [MenuItem("Dimari/Донспорт — кат-сцена разговора с Мэйби Бэйби")]
        public static void Apply()
        {
            audioMetasChecked = false; RepairAudioMetas();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var story = Object.FindAnyObjectByType<DonsportReceptionCutscene>();
            if (story == null || story.maybeBaby == null)
            {
                EditorUtility.DisplayDialog("Мэйби Бэйби", "Сначала установите историю Донспорта и создайте посетителей зала.", "OK");
                return;
            }
            Ensure(story, true);
        }

        public static void Ensure(DonsportReceptionCutscene story, bool report)
        {
            if (story == null || story.maybeBaby == null) return;
            var cutscene = story.GetComponent<DonsportMaybeBabyCutscene>();
            bool changed = false;
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<DonsportMaybeBabyCutscene>(story.gameObject);
                changed = true;
            }
            if (cutscene.maybeBaby != story.maybeBaby)
            {
                Undo.RecordObject(cutscene, "Assign Мэйби Бэйби to conversation");
                cutscene.maybeBaby = story.maybeBaby;
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(cutscene);
                EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
                Debug.Log("[Dimari] Кат-сцена Мэйби Бэйби установлена в Scene. Сохраните Ctrl+S.", story);
            }
            if (report)
                EditorUtility.DisplayDialog("Мэйби Бэйби",
                    changed
                        ? "Разговор установлен в сцене: 6 реплик, планшет с фото и передача трусов. Сохраните Ctrl+S.\nВ Play кат-сцена начнётся сама, когда герой подойдёт к певице."
                        : "Разговор уже установлен в сцене. Сохраните Ctrl+S, если меняли объект.",
                    "OK");
        }
    }
}
