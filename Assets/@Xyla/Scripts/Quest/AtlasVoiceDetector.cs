using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xyla
{
    // Bounded voice segmentation. No network or Unity objects in the detector.
    public sealed class AtlasVoiceDetector
    {
        const int Rate = 16000, Frame = 320, MaxSamples = Rate * 15;
        readonly Queue<float> preRoll = new Queue<float>(Rate / 4);
        readonly List<float> recording = new List<float>(MaxSamples);
        float noise = .002f, quiet, speech;
        public bool Recording { get; private set; }
        public event Action Started;
        public event Action<float[]> Completed;

        public void Reset()
        {
            preRoll.Clear(); recording.Clear(); quiet = speech = 0; Recording = false;
        }
        public void Feed(float[] frame)
        {
            float sum = 0;
            foreach(float sample in frame) sum += sample * sample;
            float rms = Mathf.Sqrt(sum / frame.Length);
            float seconds = frame.Length / (float)Rate;
            bool voiced = rms > Mathf.Max(.007f, noise * 3.5f);
            if (!Recording)
            {
                foreach(float sample in frame) { if(preRoll.Count >= Rate/4) preRoll.Dequeue(); preRoll.Enqueue(sample); }
                if(!voiced) { noise = Mathf.Lerp(noise, Mathf.Min(rms,.02f), .025f); return; }
                Recording = true; recording.AddRange(preRoll); preRoll.Clear();
                speech = seconds; quiet = 0; Started?.Invoke();
            }
            else
            {
                recording.AddRange(frame);
                if(voiced) { speech += seconds; quiet = 0; } else quiet += seconds;
            }
            if(recording.Count >= MaxSamples || quiet >= .40f)
            {
                if(recording.Count > MaxSamples) recording.RemoveRange(MaxSamples, recording.Count - MaxSamples);
                var data = speech >= .18f ? recording.ToArray() : null;
                Reset();
                if(data != null) Completed?.Invoke(data);
            }
        }

        // Shortens every pause to at most 120 ms and trims silent edges, so a streaming
        // recognizer with 250 ms endpointing hears one continuous utterance.
        // Returns an empty array when the clip contains no speech.
        public static float[] CompactPauses(float[] samples, int maxGapFrames = 6)
        {
            int frames = samples.Length / Frame;
            if (frames == 0) return Array.Empty<float>();
            var rms = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float sum = 0;
                for (int i = f * Frame; i < (f + 1) * Frame; i++) sum += samples[i] * samples[i];
                rms[f] = Mathf.Sqrt(sum / Frame);
            }
            var sorted = (float[])rms.Clone(); Array.Sort(sorted);
            // Capped so a clip spoken end to end never treats quiet syllables as silence.
            float threshold = Mathf.Clamp(sorted[frames / 5] * 3f, .007f, .02f);
            var voiced = new bool[frames];
            for (int f = 0; f < frames; f++)
                if (rms[f] > threshold)
                    for (int k = Mathf.Max(0, f - 2); k <= Mathf.Min(frames - 1, f + 2); k++) voiced[k] = true;
            int first = Array.IndexOf(voiced, true);
            if (first < 0) return Array.Empty<float>();
            int last = Array.LastIndexOf(voiced, true);
            var output = new List<float>(samples.Length);
            int gap = 0;
            for (int f = Mathf.Max(0, first - 3); f <= Mathf.Min(frames - 1, last + maxGapFrames); f++)
            {
                if (voiced[f]) gap = 0; else if (++gap > maxGapFrames) continue;
                for (int i = f * Frame; i < (f + 1) * Frame; i++) output.Add(samples[i]);
            }
            return output.ToArray();
        }

        public static float Peak(float[] samples)
        {
            float peak = 0;
            foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            return peak;
        }

        public static byte[] Pcm16(float[] samples)
        {
            var bytes = new byte[samples.Length*2];
            for(int i=0;i<samples.Length;i++) {
                short value=(short)Mathf.RoundToInt(Mathf.Clamp(samples[i],-1,1)*32767);
                bytes[2*i]=(byte)value; bytes[2*i+1]=(byte)(value>>8);
            }
            return bytes;
        }
        public static float[] DecodePcm16(byte[] bytes)
        {
            if(bytes.Length%2!=0) throw new ArgumentException("Audio PCM inválido");
            var samples=new float[bytes.Length/2];
            for(int i=0;i<samples.Length;i++)samples[i]=(short)(bytes[i*2]|bytes[i*2+1]<<8)/32768f;
            return samples;
        }
    }
}
