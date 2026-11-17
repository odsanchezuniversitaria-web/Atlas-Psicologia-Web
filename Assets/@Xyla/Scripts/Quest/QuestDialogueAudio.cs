using System;
using System.Threading.Tasks;
using UnityEngine;
namespace Xyla
{
    public static class QuestDialogueAudio
    {
        public static AudioClip Play(AudioSource source, byte[] pcm, string label)
        {
            if (pcm == null || pcm.Length < 2 || pcm.Length > 4000000 || pcm.Length % 2 != 0)
                throw new InvalidOperationException("El servidor no entregó audio PCM válido.");
            float[] samples = AtlasVoiceDetector.DecodePcm16(pcm);
            double energy = 0;
            foreach (float sample in samples) energy += sample * sample;
            double rms = Math.Sqrt(energy / samples.Length);
            if (rms < .00001) throw new InvalidOperationException("El servidor entregó audio silencioso.");
            source.enabled = true; source.mute = false; source.spatialBlend = 0; source.spatialize = false;
            source.outputAudioMixerGroup = null; source.loop = false; source.pitch = 1;
            source.priority = 0;
            var clip = AudioClip.Create(label, samples.Length, 1, 24000, false);
            clip.SetData(samples, 0);
            source.clip = clip; source.Play();
            Debug.Log("[Atlas Audio] " + label + " bytes=" + pcm.Length + " rms=" + rms.ToString("F4") +
                " active=" + source.gameObject.activeInHierarchy + " volume=" + source.volume +
                " listenerVolume=" + AudioListener.volume + " paused=" + AudioListener.pause);
            _ = CheckOutput(source, clip);
            return clip;
        }
        static async Task CheckOutput(AudioSource source, AudioClip clip)
        {
            await AtlasWebClock.Delay(500);
            if (!source || !clip || source.clip != clip) return;
            var data = new float[1024]; source.GetOutputData(data, 0);
            double sum=0; foreach(float value in data) sum+=value*value;
            Debug.Log("[Atlas Audio] outputRms=" + Math.Sqrt(sum/data.Length).ToString("F4") +
                " playing=" + source.isPlaying + " position=" + source.timeSamples +
                " listeners=" + UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
        }
    }
}