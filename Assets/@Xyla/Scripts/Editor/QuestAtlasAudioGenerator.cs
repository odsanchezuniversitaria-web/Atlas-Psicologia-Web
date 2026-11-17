using System.IO;
using UnityEditor;
using UnityEngine;

namespace Xyla.Editor
{
    public static class QuestAtlasAudioGenerator
    {
        [MenuItem("Atlas Humano Quest/Generar sonido de selección")]
        public static void Generate()
        {
            const int rate = 44100;
            const int count = 3528; // 80 ms, short attack and smooth decay.
            const string path = "Assets/@Xyla/Resources/AtlasSelection.wav";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)rate;
                    float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 55f)
                        * Mathf.Clamp01((0.08f - t) / 0.012f);
                    float sample = (Mathf.Sin(2f * Mathf.PI * 720f * t)
                        + 0.18f * Mathf.Sin(2f * Mathf.PI * 1080f * t)) * envelope * 0.5f;
                    writer.Write((short)(sample * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
