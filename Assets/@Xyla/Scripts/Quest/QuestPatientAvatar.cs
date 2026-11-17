using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Xyla
{
    public sealed class QuestPatientAvatar : MonoBehaviour
    {
        [Header("Cuerpo editable en la jerarquía")]
        public Transform head;
        public Transform torso;
        public Transform leftArm;
        public Transform rightArm;
        public Transform mouth;
        public Transform leftEye;
        public Transform rightEye;
        public Transform leftBrow;
        public Transform rightBrow;
        [Header("Movimiento")]
        [Range(0f, 12f)] public float headMotion = 5f;
        [Range(0f, 20f)] public float gestureMotion = 9f;
        [Range(0f, .05f)] public float breathing = .009f;
        [Range(1f, 8f)] public float blinkInterval = 4f;

        QuestPatientVoice voice;
        Vector3 torsoBase, mouthBase, leftEyeBase, rightEyeBase;
        Quaternion leftBase, rightBase, headBase;
        float nextBlink, blinkStarted = -100f, nextFrame, lastFrame, speechEnvelope;

        public void Initialize(QuestPatientVoice patientVoice)
        {
            voice = patientVoice;
            torsoBase = torso.localPosition;
            mouthBase = mouth.localScale;
            headBase = head.localRotation;
            leftEyeBase = leftEye.localScale; rightEyeBase = rightEye.localScale;
            leftBase = leftArm.localRotation;
            rightBase = rightArm.localRotation;
            nextBlink = Time.unscaledTime + blinkInterval;
        }

        void Update()
        {
            if (!voice || !voice.IsOpen || Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 1f / 30f;
            float t = Time.unscaledTime;
            float dt = lastFrame <= 0 ? 1f / 30f : Mathf.Min(.1f, t - lastFrame);
            lastFrame = t;
            float target = voice.Speaking ? Mathf.Clamp01((voice.SpeechLevel - .012f) * 2.2f) : 0f;
            speechEnvelope = Mathf.Lerp(speechEnvelope, target, 1f - Mathf.Exp(-dt / (target > speechEnvelope ? .045f : .085f)));
            float speaking = speechEnvelope;
            torso.localPosition = torsoBase + Vector3.up * (Mathf.Sin(t * 1.7f) * breathing);
            head.localRotation = headBase * Quaternion.Euler(Mathf.Sin(t * 1.23f) * headMotion * .45f,
                Mathf.Sin(t * .76f) * headMotion, speaking * Mathf.Sin(t * 2f) * 2.3f);
            leftArm.localRotation = leftBase * Quaternion.Euler(speaking * Mathf.Sin(t * 3f) * gestureMotion, 0, 0);
            rightArm.localRotation = rightBase * Quaternion.Euler(speaking * Mathf.Sin(t * 2.6f + 1f) * gestureMotion, 0, 0);
            mouth.localScale = new Vector3(mouthBase.x, mouthBase.y * (1f + speaking * 4f), mouthBase.z);
            float browAngle = voice.Mood == "guarded" ? 13f : voice.Mood == "relieved" ? -3f : 7f;
            leftBrow.localRotation = Quaternion.Euler(0, 0, browAngle);
            rightBrow.localRotation = Quaternion.Euler(0, 0, -browAngle);
            if (t >= nextBlink) { blinkStarted = t; nextBlink = t + blinkInterval + Mathf.Sin(t) * .9f; }
            float phase = (t - blinkStarted) / .24f;
            float closure = phase >= 0f && phase <= 1f ? Mathf.Sin(phase * Mathf.PI) : 0f;
            float eyeY = Mathf.Lerp(1f, .04f, closure);
            leftEye.localScale = new Vector3(leftEyeBase.x, leftEyeBase.y * eyeY, leftEyeBase.z);
            rightEye.localScale = new Vector3(rightEyeBase.x, rightEyeBase.y * eyeY, rightEyeBase.z);
        }

        public void PlaceForViewer()
        {
            Camera viewer = Camera.main;
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            transform.SetPositionAndRotation(viewer.transform.position + forward * 1.6f - right * .36f - Vector3.up * 1.22f,
                Quaternion.LookRotation(-forward, Vector3.up));
        }
    }

}
