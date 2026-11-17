using UnityEngine;

namespace Xyla
{
    // Audio-envelope animation: it follows the actual sound, not the transcript.
    // Authored bone rest poses are preserved; all amplitudes remain editable.
    public sealed class QuestPatientRealisticDriver : MonoBehaviour
    {
        [Header("Personaje y voz")]
        public Transform patientVisualRoot;
        public SkinnedMeshRenderer faceAndBody;
        public AudioSource speaker;
        [Header("Huesos editables")]
        public Transform headBone, spineBone, leftUpperArmBone, rightUpperArmBone;
        public Transform leftForearmBone, rightForearmBone;
        [Header("Postura sentada · medidas en metros sobre el suelo")]
        public bool seatedInConsultation = true;
        public Transform hipsBone, leftThighBone, rightThighBone, leftCalfBone, rightCalfBone;
        public Transform leftFootBone, rightFootBone, leftHandBone, rightHandBone;
        [Range(.35f,.6f)] public float seatSurfaceHeight = .45f;
        [Range(.06f,.2f)] public float pelvisAboveSeat = .18f;
        [Range(.08f,.25f)] public float feetHalfSpacing = .14f;
        [Range(.25f,.55f)] public float feetForward = .40f;
        [Range(.08f,.16f)] public float ankleAboveFloor = .12f;
        public Vector3 leftHandOnLap = new Vector3(-.12f,.64f,.22f);
        public Vector3 rightHandOnLap = new Vector3(.12f,.65f,.23f);
        [Range(-5f,8f)] public float seatedBackLeanDegrees = 2f;
        public Vector3 leftWristEuler, rightWristEuler;
        [Header("Expresión y movimiento")]
        [Range(0f,10f)] public float headYawDegrees = 4f;
        [Range(0f,18f)] public float gestureDegrees = 7f;
        [Range(0f,5f)] public float breathingDegrees = .6f;
        [Range(0f,5f)] public float idleGestureDegrees = .4f;
        [Range(0f,8f)] public float listeningNodDegrees = 2f;
        [Range(0f,15f)] public float relaxedShoulderDegrees = 0f;
        [Range(0f,45f)] public float relaxedElbowDegrees = 0f;
        [Range(0f,4f)] public float torsoShiftCentimeters = .4f;
        [Range(0f,100f)] public float mouthWeight = 95f;
        [Range(0f,100f)] public float blinkWeight = 100f;
        [Range(2f,8f)] public float secondsBetweenBlinks = 4f;
        [Range(15f,60f)] public float animationFps = 45f;
        public Vector3 leftGestureAxis = Vector3.right, rightGestureAxis = Vector3.right;
        public Vector3 leftElbowAxis = Vector3.right, rightElbowAxis = Vector3.right;
        [Header("Gestos de conversación")]
        [Range(0f,15f)] public float thinkingTiltDegrees = 2.5f;
        [Range(0f,30f)] public float greetingWaveDegrees = 7f;
        [Range(0f,8f)] public float speakingNodDegrees = 1.3f;
        public bool greetingGesture = true;
        [Header("Mirada hacia quien conversa")]
        public bool followViewer = true;
        [Range(0f,30f)] public float maximumGazeYaw = 18f;
        [Range(0f,20f)] public float maximumGazePitch = 10f;
        [Header("Boca y párpados")]
        public string mouthShape = "Hablar", leftBlinkShape = "ParpadeoIzq", rightBlinkShape = "ParpadeoDer";
        [Range(0f,.15f)] public float mouthNoiseFloor = .012f;
        [Range(.5f,5f)] public float mouthGain = 3.2f;
        [Range(.02f,.2f)] public float mouthAttackSeconds = .045f;
        [Range(.03f,.3f)] public float mouthReleaseSeconds = .085f;
        [Range(.15f,.4f)] public float blinkDuration = .24f;
        [HideInInspector] public int motionProfileVersion;

        QuestPatientVoice voice;
        Camera viewerCamera;
        Quaternion headRest, spineRest, leftRest, rightRest, leftElbowRest, rightElbowRest;
        Vector3 spinePosition;
        Transform[] authoredBones;
        Quaternion[] authoredRotations;
        Vector3[] authoredPositions;
        int mouthIndex = -1, leftBlinkIndex = -1, rightBlinkIndex = -1;
        readonly System.Random motion = new System.Random();
        float nextFrame, lastFrame, nextBlink, blinkStarted = -100f, speakingLevel;
        float nextPoseAt, headYaw, headPitch, headRoll, targetYaw, targetPitch, targetRoll;
        float torsoX, torsoZ, targetTorsoX, targetTorsoZ;
        float gestureStarted = -100f, gestureDuration = 2f, gestureSide;
        float nodStarted = -100f, openedAt;
        bool initialized, wasOpen, wasSpeaking;

        public void Initialize(QuestPatientVoice patientVoice)
        {
            voice = patientVoice;
            viewerCamera = Camera.main;
            ConfigureRestPose();
            Mesh mesh = faceAndBody.sharedMesh;
            mouthIndex = mesh.GetBlendShapeIndex(mouthShape);
            leftBlinkIndex = mesh.GetBlendShapeIndex(leftBlinkShape);
            rightBlinkIndex = mesh.GetBlendShapeIndex(rightBlinkShape);
            if (mouthIndex < 0 || leftBlinkIndex < 0 || rightBlinkIndex < 0)
                throw new System.InvalidOperationException("María necesita las expresiones de boca y ambos párpados; revisa sus nombres en el Inspector.");
            initialized = true;
            ResetMotion();
        }

        // Uses measured limb lengths and world-space directions, independent of FBX bone axes.
        // It only runs when entering/configuring the conversation, not once per frame.
        public void SetSeatedPose(bool seated, float surfaceHeight = .45f)
        {
            seatedInConsultation = seated;
            seatSurfaceHeight = surfaceHeight;
            ConfigureRestPose();
            if (initialized) ResetMotion();
        }

        void ConfigureRestPose()
        {
            if (authoredBones == null)
            {
                authoredBones = new[] { hipsBone,leftThighBone,rightThighBone,leftCalfBone,rightCalfBone,
                    leftFootBone,rightFootBone,leftHandBone,rightHandBone,headBone,spineBone,
                    leftUpperArmBone,rightUpperArmBone,leftForearmBone,rightForearmBone };
                authoredRotations = new Quaternion[authoredBones.Length];
                authoredPositions = new Vector3[authoredBones.Length];
                for (int i=0;i<authoredBones.Length;i++)
                    if (authoredBones[i])
                    {
                        authoredRotations[i]=authoredBones[i].localRotation;
                        authoredPositions[i]=authoredBones[i].localPosition;
                    }
            }
            for (int i=0;i<authoredBones.Length;i++)
                if (authoredBones[i])
                    authoredBones[i].SetLocalPositionAndRotation(authoredPositions[i],authoredRotations[i]);
            if (seatedInConsultation)
            {
                if (!hipsBone || !leftThighBone || !rightThighBone || !leftCalfBone || !rightCalfBone
                    || !leftFootBone || !rightFootBone || !leftHandBone || !rightHandBone)
                    throw new System.InvalidOperationException("La postura sentada de María necesita cadera, piernas, pies y manos vinculados en el Inspector.");
                Quaternion leftFootRotation=leftFootBone.rotation, rightFootRotation=rightFootBone.rotation;
                Vector3 pelvis=patientVisualRoot.InverseTransformPoint(hipsBone.position);
                pelvis.y=seatSurfaceHeight+pelvisAboveSeat;
                hipsBone.position=patientVisualRoot.TransformPoint(pelvis);
                spineBone.rotation=Quaternion.AngleAxis(-seatedBackLeanDegrees,patientVisualRoot.right)*spineBone.rotation;
                SolveTwoBone(leftThighBone,leftCalfBone,leftFootBone,
                    patientVisualRoot.TransformPoint(new Vector3(-feetHalfSpacing,ankleAboveFloor,feetForward)),
                    patientVisualRoot.forward);
                SolveTwoBone(rightThighBone,rightCalfBone,rightFootBone,
                    patientVisualRoot.TransformPoint(new Vector3(feetHalfSpacing,ankleAboveFloor,feetForward)),
                    patientVisualRoot.forward);
                leftFootBone.rotation=leftFootRotation; rightFootBone.rotation=rightFootRotation;
                SolveTwoBone(leftUpperArmBone,leftForearmBone,leftHandBone,
                    patientVisualRoot.TransformPoint(leftHandOnLap),-patientVisualRoot.right-patientVisualRoot.up);
                SolveTwoBone(rightUpperArmBone,rightForearmBone,rightHandBone,
                    patientVisualRoot.TransformPoint(rightHandOnLap),patientVisualRoot.right-patientVisualRoot.up);
                leftHandBone.localRotation*=Quaternion.Euler(leftWristEuler);
                rightHandBone.localRotation*=Quaternion.Euler(rightWristEuler);
            }
            headRest=headBone.localRotation; spineRest=spineBone.localRotation; spinePosition=spineBone.localPosition;
            leftRest=leftUpperArmBone.localRotation; rightRest=rightUpperArmBone.localRotation;
            leftElbowRest=leftForearmBone.localRotation; rightElbowRest=rightForearmBone.localRotation;
        }

        static void SolveTwoBone(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 bendHint)
        {
            Vector3 origin=upper.position;
            float a=Vector3.Distance(origin,lower.position), b=Vector3.Distance(lower.position,end.position);
            Vector3 direction=target-origin;
            float distance=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            direction.Normalize();
            Vector3 bend=Vector3.ProjectOnPlane(bendHint,direction).normalized;
            if (bend.sqrMagnitude<.001f) bend=Vector3.Cross(direction,Vector3.right).normalized;
            float along=(a*a-b*b+distance*distance)/(2f*distance);
            float perpendicular=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 joint=origin+direction*along+bend*perpendicular;
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,joint-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,target-lower.position)*lower.rotation;
        }

        public void PlaceForViewer()
        {
            Camera viewer = Camera.main;
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            patientVisualRoot.SetPositionAndRotation(
                viewer.transform.position + forward * 1.75f - right * .33f - Vector3.up * 1.57f,
                Quaternion.LookRotation(-forward, Vector3.up));
            ResetMotion();
        }

        void ResetMotion()
        {
            nextFrame = lastFrame = 0f; wasOpen = wasSpeaking = false;
            speakingLevel = headYaw = headPitch = headRoll = torsoX = torsoZ = 0f;
            targetYaw = targetPitch = targetRoll = targetTorsoX = targetTorsoZ = 0f;
            gestureStarted = nodStarted = blinkStarted = -100f;
            nextBlink = Time.unscaledTime + .8f + Random01() * 1.5f;
            nextPoseAt = Time.unscaledTime;
            if (initialized) RestorePose();
        }

        void RestorePose()
        {
            headBone.localRotation = headRest; spineBone.localRotation = spineRest;
            spineBone.localPosition = spinePosition;
            leftUpperArmBone.localRotation = leftRest; rightUpperArmBone.localRotation = rightRest;
            leftForearmBone.localRotation = leftElbowRest; rightForearmBone.localRotation = rightElbowRest;
            faceAndBody.SetBlendShapeWeight(mouthIndex, 0);
            faceAndBody.SetBlendShapeWeight(leftBlinkIndex, 0);
            faceAndBody.SetBlendShapeWeight(rightBlinkIndex, 0);
        }

        void OnDisable()
        {
            if (initialized) RestorePose();
            wasOpen = false;
        }

        void LateUpdate()
        {
            if (!initialized || !voice.IsOpen) return;
            float t = Time.unscaledTime;
            if (!wasOpen)
            {
                wasOpen = true; openedAt = t; nextPoseAt = t; nextBlink = t + .9f + Random01();
            }
            if (t < nextFrame) return;
            float dt = lastFrame <= 0 ? 1f / animationFps : Mathf.Min(.1f, t-lastFrame);
            lastFrame = t; nextFrame = t + 1f / Mathf.Max(15f, animationFps);
            bool speaking = voice.Speaking, listening = voice.UserSpeaking;
            float target = speaking ? Mathf.Clamp01((voice.SpeechLevel-mouthNoiseFloor)*mouthGain) : 0;
            float smoothing = 1f-Mathf.Exp(-dt/Mathf.Max(.02f, target > speakingLevel ? mouthAttackSeconds : mouthReleaseSeconds));
            speakingLevel = Mathf.Lerp(speakingLevel,target,smoothing);
            if (!speaking && speakingLevel < .003f) speakingLevel = 0;
            faceAndBody.SetBlendShapeWeight(mouthIndex, speakingLevel*mouthWeight);
            UpdateBlink(t);
            if (speaking && !wasSpeaking) nextPoseAt = t;
            wasSpeaking = speaking;
            if (t >= nextPoseAt) ChoosePose(t,speaking,listening);
            float blend = 1f-Mathf.Exp(-3.5f*dt);
            float gazeYaw=0f,gazePitch=0f;
            if (followViewer && viewerCamera)
            {
                Vector3 direction=patientVisualRoot.InverseTransformDirection(viewerCamera.transform.position-headBone.position);
                gazeYaw=Mathf.Clamp(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-maximumGazeYaw,maximumGazeYaw);
                gazePitch=Mathf.Clamp(-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg,
                    -maximumGazePitch,maximumGazePitch);
            }
            headYaw = Mathf.Lerp(headYaw,targetYaw+gazeYaw,blend);
            headPitch = Mathf.Lerp(headPitch,targetPitch+gazePitch,blend);
            headRoll = Mathf.Lerp(headRoll,targetRoll,blend);
            torsoX = Mathf.Lerp(torsoX,targetTorsoX,blend*.35f); torsoZ = Mathf.Lerp(torsoZ,targetTorsoZ,blend*.35f);
            float nod = Pulse(t-nodStarted,.85f)*(listening ? listeningNodDegrees : speakingNodDegrees);
            headBone.localRotation = headRest*Quaternion.Euler(headPitch+nod,headYaw,headRoll);
            spineBone.localRotation = spineRest*Quaternion.Euler(Mathf.Sin(t*1.2f)*breathingDegrees,torsoX*20f,torsoZ*20f);
            spineBone.localPosition = spinePosition + spineBone.parent.InverseTransformVector(
                patientVisualRoot.TransformVector(new Vector3(torsoX,0,torsoZ)));
            float gesture = Pulse(t-gestureStarted,gestureDuration)*(speaking ? 1f : .35f);
            float left = gestureSide < 0 ? gesture : 0, right = gestureSide > 0 ? gesture : 0;
            float greet = greetingGesture ? Pulse(t-openedAt,2.4f) : 0;
            leftUpperArmBone.localRotation = leftRest*Quaternion.AngleAxis(relaxedShoulderDegrees+left*gestureDegrees,leftGestureAxis);
            rightUpperArmBone.localRotation = rightRest*Quaternion.AngleAxis(-relaxedShoulderDegrees-right*gestureDegrees,rightGestureAxis);
            leftForearmBone.localRotation = leftElbowRest*Quaternion.AngleAxis(relaxedElbowDegrees+left*gestureDegrees*.7f,leftElbowAxis);
            rightForearmBone.localRotation = rightElbowRest*Quaternion.AngleAxis(-relaxedElbowDegrees-right*gestureDegrees*.7f-greet*greetingWaveDegrees,rightElbowAxis);
        }

        void UpdateBlink(float t)
        {
            if(t >= nextBlink)
            {
                blinkStarted=t;
                nextBlink=t+Mathf.Max(.6f, secondsBetweenBlinks*(.7f+Random01()*.6f));
                if(Random01()<.12f) nextBlink=t+blinkDuration+.12f;
            }
            float phase=(t-blinkStarted)/Mathf.Max(.15f,blinkDuration);
            float amount=phase < 0 || phase >= 1 ? 0 : phase < .3f
                ? Mathf.SmoothStep(0,1,phase/.3f) : phase < .45f ? 1 : 1-Mathf.SmoothStep(0,1,(phase-.45f)/.55f);
            faceAndBody.SetBlendShapeWeight(leftBlinkIndex,amount*blinkWeight);
            faceAndBody.SetBlendShapeWeight(rightBlinkIndex,amount*blinkWeight);
        }

        void ChoosePose(float t,bool speaking,bool listening)
        {
            nextPoseAt=t+(speaking ? 2.2f : 3f)+Random01()*2f;
            targetYaw=RandomSigned()*headYawDegrees*.65f;
            targetPitch=RandomSigned()*(speaking ? 1.2f : .7f);
            targetRoll=voice.WaitingForReply ? thinkingTiltDegrees*.6f : RandomSigned()*.6f;
            float shift=torsoShiftCentimeters*.01f;
            targetTorsoX=RandomSigned()*shift; targetTorsoZ=RandomSigned()*shift*.3f;
            if(speaking && Random01()>.25f)
            {
                gestureStarted=t; gestureDuration=1.7f+Random01()*.7f; gestureSide=Random01()>.5f ? 1 : -1;
            }
            if(listening || (speaking && Random01()>.6f)) nodStarted=t+.2f;
        }
        static float Pulse(float elapsed,float duration)
        {
            if(elapsed <= 0 || elapsed >= duration) return 0;
            float value=Mathf.Sin(Mathf.PI*elapsed/duration); return value*value;
        }
        float Random01() => (float)motion.NextDouble();
        float RandomSigned() => Random01()*2f-1f;
    }
}
