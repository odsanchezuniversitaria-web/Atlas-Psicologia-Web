using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

namespace Xyla
{
    // Reads the PCM after LiveKit's generator filled it; never changes the audio.
    public sealed class QuestAudioEnvelope : MonoBehaviour
    {
        [Header("Lipsync · audio real")]
        [Range(1f, 30f)] public float gain = 12f;
        [Range(0f, .02f)] public float noiseFloor = .0015f;
        [Range(.05f, .4f)] public float freshnessSeconds = .18f;
        public float Rms => Volatile.Read(ref rms);
        public float Level
        {
            get
            {
                long stamp = Interlocked.Read(ref lastFrame);
                if (stamp == 0 || (Stopwatch.GetTimestamp()-stamp)/(double)Stopwatch.Frequency > freshnessSeconds) return 0f;
                return Mathf.Sqrt(Mathf.Clamp01((Rms-noiseFloor)*gain));
            }
        }
        float rms;
        long lastFrame;
        AudioSource source;
        Component probe;
        EventInfo audioEvent;
        Delegate callback;

        public void BindNativeStream(AudioSource output)
        {
            UnbindNativeStream();
            source = output;
            AttachProbe();
            AudioSettings.OnAudioConfigurationChanged += Rebind;
        }
        void AttachProbe()
        {
            // LiveKit recreates this generator after an audio-device change.
            Component found = null;
            foreach (var component in source.GetComponents<MonoBehaviour>())
                if (component.GetType().FullName == "LiveKit.AudioProbe") found = component;
            if (!found) throw new InvalidOperationException("No se encontró la salida PCM de LiveKit para lipsync.");
            probe = found;
            audioEvent = probe.GetType().GetEvent("AudioRead", BindingFlags.Instance | BindingFlags.Public);
            if (audioEvent == null) throw new InvalidOperationException("El SDK LiveKit cambió su evento PCM.");
            callback = Delegate.CreateDelegate(audioEvent.EventHandlerType, this, nameof(CaptureNativeAudio));
            // The generator subscribed first. This observer sees the final PCM.
            audioEvent.AddEventHandler(probe, callback);
        }
        void DetachProbe()
        {
            if (probe && audioEvent != null && callback != null) audioEvent.RemoveEventHandler(probe, callback);
            probe = null; audioEvent = null; callback = null;
        }
        void Rebind(bool deviceChanged)
        {
            DetachProbe();
            ResetLevel();
            AttachProbe();
        }
        [Preserve]
        public void CaptureNativeAudio(float[] data, int channels, int sampleRate)
        {
            double energy = 0;
            for (int i=0;i<data.Length;i++) energy += data[i]*data[i];
            Volatile.Write(ref rms, data.Length == 0 ? 0f : (float)Math.Sqrt(energy/data.Length));
            Interlocked.Exchange(ref lastFrame, Stopwatch.GetTimestamp());
        }
        public void ResetLevel() { Volatile.Write(ref rms, 0f); Interlocked.Exchange(ref lastFrame, 0); }
        public void UnbindNativeStream()
        {
            AudioSettings.OnAudioConfigurationChanged -= Rebind;
            DetachProbe(); source = null; ResetLevel();
        }
        void OnDestroy() { UnbindNativeStream(); }
    }
}
