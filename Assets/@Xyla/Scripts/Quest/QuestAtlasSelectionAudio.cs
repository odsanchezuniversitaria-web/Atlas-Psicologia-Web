using UnityEngine;

namespace Xyla
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class QuestAtlasSelectionAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip clip;
        float nextSound;
        public bool Muted { get; set; }
        public int PlayedCount { get; private set; }

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.28f;
            clip = Resources.Load<AudioClip>("AtlasLabAudio/Select");
            if (clip == null) throw new System.InvalidOperationException("Falta el sonido AtlasSelection.");
        }

        public void PlaySelection()
        {
            if (Muted || Time.unscaledTime < nextSound) return;
            nextSound = Time.unscaledTime + 0.065f;
            source.PlayOneShot(clip);
            PlayedCount++;
        }
    }
}
