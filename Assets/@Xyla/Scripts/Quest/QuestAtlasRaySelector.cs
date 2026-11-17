using System;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Xyla
{
    // Keeps one input and one target for the entire press. Sliding across the
    // panel while holding Grab or Trigger cannot start a second selection.
    public sealed class QuestAtlasRaySelector : MonoBehaviour, ISelector
    {
        [SerializeField] UnityEngine.Object controllerSource;
        [SerializeField] RayInteractor ray;
        IController controller;
        bool gripWasDown, triggerWasDown, selected, selectedWithGrip;
        public event Action WhenSelected = delegate { };
        public event Action WhenUnselected = delegate { };

        public void Configure(UnityEngine.Object source, RayInteractor interactor)
        {
            controllerSource = source;
            ray = interactor;
        }

        void Awake() { controller = controllerSource as IController; }

        void Start()
        {
            if (controller == null || ray == null)
                throw new InvalidOperationException("Falta configurar el selector del mando.");
        }

        void Update()
        {
            bool connected = controller.IsConnected;
            ProcessInput(connected && controller.IsButtonUsageAnyActive(ControllerButtonUsage.GripButton),
                connected && controller.IsButtonUsageAnyActive(ControllerButtonUsage.TriggerButton),
                ray.Interactable, ray.State != InteractorState.Disabled);
        }

        public void ProcessInput(bool grip, bool trigger, RayInteractable target, bool available)
        {
            if (selected)
            {
                if (!available || !(selectedWithGrip ? grip : trigger)) Release();
            }
            else if (available && target != null && Vector3.Distance(ray.Origin, ray.End) <= ray.MaxRayLength + 0.01f)
            {
                bool piece = target.PointableElement is Grabbable;
                bool pressed = piece ? grip && !gripWasDown : trigger && !triggerWasDown;
                if (pressed)
                {
                    selectedWithGrip = piece;
                    selected = true;
                    QuestAtlasHaptics.Play(piece ? AtlasHapticCue.Grab : AtlasHapticCue.Button, controller.Handedness == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
                    WhenSelected();
                }
            }
            gripWasDown = grip;
            triggerWasDown = trigger;
        }

        void Release()
        {
            if (!selected) return;
            selected = false;
            if (selectedWithGrip && isActiveAndEnabled) QuestAtlasHaptics.Play(AtlasHapticCue.Release, controller.Handedness == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
            WhenUnselected();
        }

        void OnDisable()
        {
            Release();
            // A held button must be released before selecting after reactivation.
            gripWasDown = triggerWasDown = true;
        }
    }
}
