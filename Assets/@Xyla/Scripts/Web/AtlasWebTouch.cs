using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Xyla
{
    public sealed partial class AtlasWebRuntime
    {
        bool touchEnabled, orbitActive, savedCamera;
        Vector3 homePlayer, homeEye, pivot, grabOffset;
        Quaternion homeRotation, homeEyeRotation;
        float homePitch, homeNear, yaw, elevation, radius;
        Plane grabPlane;
        string studyId = "";
        Bounds studyBounds;

        void RememberCamera()
        {
            if (savedCamera) return;
            homePlayer = player.localPosition;
            homeRotation = player.localRotation;
            homeEye = view.transform.localPosition;
            homeEyeRotation = view.transform.localRotation;
            homePitch = pitch; homeNear = view.nearClipPlane;
            savedCamera = true;
        }

        void RestoreStudyCamera()
        {
            if (!savedCamera) return;
            EndTouchHold();
            player.SetLocalPositionAndRotation(homePlayer, homeRotation);
            view.transform.SetLocalPositionAndRotation(homeEye, homeEyeRotation);
            view.nearClipPlane = homeNear; pitch = homePitch;
            orbitActive = savedCamera = false;
            studyId = "";
        }

        bool TouchExploration()
        {
            if (InVR || atlas.patientSimulation.IsOpen)
            {
                RestoreStudyCamera();
                return false;
            }
            if (!touchEnabled) return false;
            if (!orbitActive) FrameStudy(atlas.WebVisibleBounds(), true);
            if (Pointed && !atlas.IsPartVisible(Pointed.Index)) SetTouchPoint(null);
            return true;
        }

        void FrameStudy(Bounds bounds, bool orbit)
        {
            RememberCamera();
            pivot = bounds.center; yaw = elevation = 0;
            float tangent = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            radius = Mathf.Clamp(bounds.extents.z + 1.25f * Mathf.Max(bounds.extents.y,
                bounds.extents.x / Mathf.Max(.25f, view.aspect)) / tangent, .09f, 6f);
            orbitActive = orbit;
            view.nearClipPlane = .012f;
            PlaceStudyCamera();
        }

        void PlaceStudyCamera()
        {
            Quaternion rotation = Quaternion.Euler(elevation, yaw, 0);
            Vector3 eye = pivot - rotation * Vector3.forward * radius;
            player.SetPositionAndRotation(new Vector3(eye.x,0,eye.z), Quaternion.Euler(0,yaw,0));
            view.transform.localPosition = new Vector3(0,eye.y,0);
            pitch = elevation;
            view.transform.localRotation = Quaternion.Euler(elevation,0,0);
        }

        void ShowWebStudy(string study)
        {
            if (InVR || atlas.patientSimulation.IsOpen) return;
            EndTouchHold(); SetTouchPoint(null); atlas.ClearWebFact();
            if (!atlas.ApplyWebStudy(study, touchEnabled, out Bounds framing)) return;
            studyId = study; studyBounds = framing; FrameStudy(framing, touchEnabled);
        }

        void SetTouchPoint(QuestAtlasPiece piece)
        {
            if (Pointed == piece) return;
            if (Pointed) Pointed.WebHover(false);
            Pointed = piece;
            if (Pointed) Pointed.WebHover(true);
        }

        int PickAt(Vector2 point, Vector2 tolerance, out Ray ray, out float distance)
        {
            ray = view.ViewportPointToRay(point);
            int index = atlas.PickWebSurface(ray, Mathf.Max(rayDistance,radius+2), out distance);
            if (index >= 0) return index;
            // A second chance for thin vessels, only if the centre of the fingertip missed.
            Vector2[] offsets = {Vector2.left,Vector2.right,Vector2.up,Vector2.down};
            foreach (Vector2 offset in offsets)
            {
                ray = view.ViewportPointToRay(point + Vector2.Scale(tolerance,offset));
                index = atlas.PickWebSurface(ray, Mathf.Max(rayDistance,radius+2), out distance);
                if (index >= 0) return index;
            }
            return -1;
        }

        public void TouchGesture(string json)
        {
            if (landing || !atlas || !atlas.Voice || InVR) return;
            var gesture = JObject.Parse(json);
            string kind = gesture.Value<string>("kind");
            if (kind == "enable") { touchEnabled = true; return; }
            if (kind == "cancel" || kind == "release") { EndTouchHold(); return; }
            if (atlas.patientSimulation.IsOpen) return;
            touchEnabled = true;
            if (!orbitActive) FrameStudy(atlas.WebVisibleBounds(),true);
            Vector2 uv = new Vector2(Mathf.Clamp01(gesture.Value<float>("x")),Mathf.Clamp01(gesture.Value<float>("y")));
            float dx = gesture.Value<float>("dx"), dy = gesture.Value<float>("dy");
            float ratio = Mathf.Clamp(gesture.Value<float?>("ratio") ?? 1f,.1f,10f);
            switch (kind)
            {
                case "frame":
                    FrameStudy(string.IsNullOrEmpty(studyId) ? atlas.WebVisibleBounds() : studyBounds,true);
                    break;
                case "orbit":
                    yaw -= dx * 180; elevation = Mathf.Clamp(elevation + dy * 150,-78,78);
                    PlaceStudyCamera(); break;
                case "pinch":
                    if (Held) Held.transform.localScale = Vector3.one * Mathf.Clamp(Held.transform.localScale.x*ratio,atlas.escalaMinima,atlas.escalaMaxima);
                    else
                    {
                        float height = 2f * radius * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
                        Vector3 anchored = view.transform.right * ((uv.x-.5f)*height*view.aspect) + view.transform.up*((uv.y-.5f)*height);
                        float next = Mathf.Clamp(radius/ratio,.065f,7f);
                        pivot += anchored * (1f - next/radius) - view.transform.right*(dx*height*view.aspect) + view.transform.up*(dy*height);
                        radius = next; PlaceStudyCamera();
                    }
                    break;
                case "tap":
                case "hold":
                    int index = PickAt(uv, new Vector2(gesture.Value<float>("tolX"),gesture.Value<float>("tolY")),out Ray ray,out float distance);
                    if (index < 0) { SetTouchPoint(null); atlas.ClearWebFact(); nextState=0; break; }
                    var piece = atlas.EnsureLearningPiece(index);
                    SetTouchPoint(piece);
                    if (kind=="tap") atlas.WebInspect(index);
                    else
                    {
                        piece.WebHold(true);
                        if (piece.Detached)
                        {
                            Held = piece;
                            Vector3 hit = ray.GetPoint(distance);
                            grabPlane = new Plane(view.transform.forward,hit);
                            grabOffset = Held.transform.position - hit;
                        }
                        else piece.WebHold(false);
                    }
                    nextState = 0;
                    break;
                case "drag":
                    if (!Held) break;
                    Ray dragRay = view.ViewportPointToRay(uv);
                    if (grabPlane.Raycast(dragRay,out float depth)) Held.transform.position = dragRay.GetPoint(depth) + grabOffset;
                    break;
            }
        }

        void EndTouchHold()
        {
            if (!orbitActive || !Held) return;
            Release();
            SavePoses();
            nextState=0;
        }
    }
}
