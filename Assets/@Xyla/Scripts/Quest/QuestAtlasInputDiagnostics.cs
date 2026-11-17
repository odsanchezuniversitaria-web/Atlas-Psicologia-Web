using UnityEngine;
using Oculus.Interaction;

namespace Xyla
{
    public sealed class QuestAtlasInputDiagnostics : MonoBehaviour
    {
        OVRCameraRig rig;
        HandVisual[] hands;
        RayInteractor[] rays;
        float nextCheck;
        string previousState;

        void Start()
        {
            rig = GetComponent<OVRCameraRig>();
            hands = GetComponentsInChildren<HandVisual>(true);
            rays = GetComponentsInChildren<RayInteractor>(true);
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            int visibleHands = 0;
            int availableRays = 0;
            foreach (HandVisual hand in hands)
                if (hand.name.StartsWith("Building Block") && hand.IsVisible) visibleHands++;
            foreach (RayInteractor ray in rays)
                if (ray.name == "ControllerRayInteractor" && ray.State != InteractorState.Disabled) availableRays++;
            bool leftTracked = OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch);
            bool rightTracked = OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
            string state = "connected=" + OVRInput.GetConnectedControllers() +
                " tracked=" + leftTracked + "/" + rightTracked +
                " hands=" + visibleHands + " controllerRays=" + availableRays;
            if (state == previousState) return;
            previousState = state;
            Debug.Log("[Atlas Input] " + state + " origin=" + rig.transform.position);
        }
    }
}