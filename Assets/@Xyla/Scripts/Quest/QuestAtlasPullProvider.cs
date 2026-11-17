using Oculus.Interaction;
using UnityEngine;

namespace Xyla
{
    public sealed class QuestAtlasPullProvider : MonoBehaviour, IMovementProvider
    {
        public IMovement CreateMovement() { return new PullMovement(); }

        public sealed class PullMovement : IMovement
        {
            public Pose Pose { get; private set; }
            public bool Stopped { get; private set; } = true;
            Pose source, target;
            Quaternion relativeRotation;
            float startedAt, distance;

            public void StopAndSetPose(Pose pose)
            {
                Pose = source = pose;
                Stopped = true;
            }

            public void MoveTo(Pose pose)
            {
                target = pose;
                source = Pose;
                distance = Mathf.Min(0.22f, Vector3.Distance(source.position, pose.position));
                relativeRotation = Quaternion.Inverse(pose.rotation) * source.rotation;
                startedAt = Time.unscaledTime;
                Stopped = false;
            }

            public void UpdateTarget(Pose pose) { target = pose; }
            public void Tick() { Advance(Time.unscaledTime, Time.unscaledDeltaTime); }

            public void Advance(float now, float deltaTime)
            {
                if (Stopped) return;
                Vector3 destination = target.position + target.forward * distance;
                Quaternion rotation = target.rotation * relativeRotation;
                float progress = Mathf.Clamp01((now - startedAt) / 0.38f);
                if (progress < 1f)
                {
                    float eased = progress * progress * (3f - 2f * progress);
                    Pose = new Pose(Vector3.Lerp(source.position, destination, eased),
                        Quaternion.Slerp(source.rotation, rotation, eased));
                }
                else
                {
                    float follow = 1f - Mathf.Exp(-28f * Mathf.Min(deltaTime, 0.05f));
                    Pose = new Pose(Vector3.Lerp(Pose.position, destination, follow),
                        Quaternion.Slerp(Pose.rotation, rotation, follow));
                }
            }
        }
    }
}
