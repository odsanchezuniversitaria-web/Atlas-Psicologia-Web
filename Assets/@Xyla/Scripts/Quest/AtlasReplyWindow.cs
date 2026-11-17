namespace Xyla
{
    public sealed class AtlasReplyWindow
    {
        public float Deadline { get; private set; }
        public bool ReplyStarted { get; private set; }
        public void Open(float now, float seconds)
        {
            Deadline = now + UnityEngine.Mathf.Clamp(seconds, 0f, 25f);
            ReplyStarted = false;
        }
        public bool IsOpen(float now) => now < Deadline;
        public void Capture(float now) { ReplyStarted = IsOpen(now); }
        public bool Consume() { bool reply = ReplyStarted; Cancel(); return reply; }
        public void Cancel() { Deadline = 0f; ReplyStarted = false; }
    }
}
