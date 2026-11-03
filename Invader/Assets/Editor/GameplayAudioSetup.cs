using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class GameplayAudioSetup
    {
        private const string DirectoryPath = "Assets/Audio/Gameplay";
        private const int    SampleRate    = 22050;

        [MenuItem("Unity Invader/Configure Gameplay Audio")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring gameplay audio.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            Directory.CreateDirectory(DirectoryPath);
            var pattern = new AudioClip[4];
            for (int i = 0; i < pattern.Length; i++)
            {
                float frequency = 150f - i * 18f;
                pattern[i] = CreateClip($"Fleet{i + 1}", 0.09f, frequency, frequency * 0.7f, false, false);
            }
            var shot          = CreateClip("PlayerShot", 0.14f, 1300f, 350f, false, false);
            var invaderDefeat = CreateClip("InvaderDefeat", 0.16f, 320f, 80f, true, false);
            var playerDeath   = CreateClip("PlayerDeath", 0.45f, 260f, 45f, true, false);
            var ufoFlight     = CreateClip("UfoFlight", 0.4f, 600f, 600f, false, true);
            var ufoDefeat     = CreateClip("UfoDefeat", 0.28f, 800f, 100f, true, false);

            var scene     = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var driver    = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include).Single();
            var player    = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsInactive.Include).Single();
            var ufo       = UnityEngine.Object.FindObjectsByType<Ufo>(FindObjectsInactive.Include).Single();
            var formation = UnityEngine.Object.FindObjectsByType<InvaderFormation>(FindObjectsInactive.Include).Single();
            var roots     = scene.GetRootGameObjects().Where(inRoot => inRoot.name == "Gameplay Audio").ToArray();
            if (roots.Length > 1)
            {
                throw new InvalidOperationException("Multiple gameplay audio roots.");
            }
            var root  = roots.Length == 1 ? roots[0] : new GameObject("Gameplay Audio", typeof(GameplayAudio));
            var audio = root.GetComponent<GameplayAudio>();
            if (audio == null)
            {
                throw new InvalidOperationException("Gameplay Audio root requires GameplayAudio.");
            }
            var data = new SerializedObject(audio);
            data.FindProperty("_driver").objectReferenceValue = driver;
            data.FindProperty("_player").objectReferenceValue = player;
            data.FindProperty("_ufo").objectReferenceValue    = ufo;
            SetSource(data, root.transform, "_movement", "Movement", pattern[0], false, 0.25f);
            SetSource(data, root.transform, "_shot", "Player Shot", shot, false, 0.5f);
            SetSource(data, root.transform, "_invaderDefeat", "Invader Defeat", invaderDefeat, false, 0.35f);
            SetSource(data, root.transform, "_playerDeath", "Player Death", playerDeath, false, 0.65f);
            SetSource(data, root.transform, "_ufoFlight", "UFO Flight", ufoFlight, true, 0.25f);
            SetSource(data, root.transform, "_ufoDefeat", "UFO Defeat", ufoDefeat, false, 0.6f);
            var clips = data.FindProperty("_movementPattern");
            if (clips.arraySize == 0)
            {
                clips.arraySize = pattern.Length;
                for (int i = 0; i < pattern.Length; i++)
                {
                    clips.GetArrayElementAtIndex(i).objectReferenceValue = pattern[i];
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var formationData = new SerializedObject(formation);
            formationData.FindProperty("_audio").objectReferenceValue = audio;
            formationData.ApplyModifiedPropertiesWithoutUndo();
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            if (listeners.Length != 1 || listeners[0].enabled == false)
            {
                throw new InvalidOperationException("Arcade must have exactly one enabled AudioListener; inspect the camera configuration.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save gameplay audio.");
            }
            AssetDatabase.SaveAssets();
            ComponentGameplayVerification.Verify();
            Debug.Log("GAMEPLAY_AUDIO_SAVED: nine original WAV clips, six sources, owner references and one listener. No listening test performed.");
        }

        private static void SetSource(SerializedObject inData, Transform inParent, string inProperty,
            string inName, AudioClip inClip, bool inLoop, float inVolume)
        {
            var child = inParent.Find(inName);
            if (child == null)
            {
                child = new GameObject(inName, typeof(AudioSource)).transform;
                child.SetParent(inParent, false);
                var newSource = child.GetComponent<AudioSource>();
                newSource.clip   = inClip;
                newSource.volume = inVolume;
                newSource.pitch  = 1f;
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null || source.clip == null)
            {
                throw new InvalidOperationException($"Audio channel {inName} requires a clip and AudioSource.");
            }
            source.playOnAwake  = false;
            source.loop         = inLoop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            inData.FindProperty(inProperty).objectReferenceValue = source;
        }

        private static AudioClip CreateClip(string inName, float inDuration, float inStartFrequency,
            float inEndFrequency, bool inNoise, bool inLoop)
        {
            string path = $"{DirectoryPath}/{inName}.wav";
            if (File.Exists(path) == false)
            {
                // Original analytic oscillator/noise signals. No ROM or external audio data.
                int count   = Mathf.RoundToInt(SampleRate * inDuration);
                var samples = new float[count];
                var random  = new System.Random(1729);
                double phase = 0.0;
                for (int i = 0; i < count; i++)
                {
                    float t        = i / (float)SampleRate;
                    float progress = i / (float)(count - 1);
                    double frequency = inLoop ? inStartFrequency + 70.0 * Math.Sin(2.0 * Math.PI * 5.0 * t) :
                        Mathf.Lerp(inStartFrequency, inEndFrequency, progress);
                    double tone  = Math.Sin(phase) + 0.2 * Math.Sin(phase * 2.0);
                    double noise = inNoise ? (random.NextDouble() * 2.0 - 1.0) * 0.35 : 0.0;
                    float envelope = inLoop ? 1f : Mathf.Min(1f, t / 0.006f) * Mathf.Pow(1f - progress, 1.2f);
                    samples[i] = (float)((tone + noise) * envelope * 0.3);
                    phase += 2.0 * Math.PI * frequency / SampleRate;
                }
                var clip = AudioClip.Create(inName, count, 1, SampleRate, false);
                try
                {
                    if (clip.SetData(samples, 0) == false)
                    {
                        throw new InvalidOperationException($"Could not generate {inName}.");
                    }
                    WriteWave(path, samples);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
            AssetDatabase.ImportAsset(path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType          = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData   = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono           = true;
            importer.loadInBackground      = false;
            importer.SaveAndReimport();
            var result = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (result == null || result.samples <= 0 || result.channels != 1)
            {
                throw new InvalidOperationException($"Could not import generated WAV: {path}");
            }
            return result;
        }

        private static void WriteWave(string inPath, float[] inSamples)
        {
            using var writer = new BinaryWriter(File.Create(inPath));
            int bytes = inSamples.Length * sizeof(short);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(bytes);
            foreach (float sample in inSamples)
            {
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }
    }
}
