using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xyla
{
    public sealed partial class QuestAtlasController
    {
        static bool HasTerm(string value, string term) => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        static bool WebStudyIncludes(AtlasPart part, string study)
        {
            string n = part.name;
            bool hepaticSegment = part.system == "venous" && n.StartsWith("Hepatovenous segment ", StringComparison.OrdinalIgnoreCase);
            switch (study)
            {
                case "heart":
                    if (part.system == "muscular") return HasTerm(n, "papillary muscle");
                    return part.system == "cardiac" && AtlasTissueProfile.Kind(part) != AtlasSurfaceKind.CerebrospinalSpace;
                case "valves": return part.system == "cardiac" && (HasTerm(n, "leaflet") || HasTerm(n, "cusp"));
                case "digestive": return part.system == "digestive" || hepaticSegment;
                case "hepatobiliary":
                    return hepaticSegment || (part.system == "digestive" && (HasTerm(n,"liver") || HasTerm(n,"gallbladder") ||
                        HasTerm(n,"biliary") || HasTerm(n,"hepatic") || HasTerm(n,"cystic duct")));
                case "arteries": return part.system == "arterial";
                case "coronary": return part.system == "arterial" && (HasTerm(n,"coronary") || HasTerm(n,"interventricular branch"));
                case "veins": return part.system == "venous" && !hepaticSegment;
                case "portal": return part.system == "venous" && HasTerm(n,"portal vein");
                default: return false;
            }
        }

        public Bounds WebVisibleBounds()
        {
            Bounds bounds = default;
            bool first = true;
            for (int i = 0; i < data.parts.Length; i++)
            {
                if (!IsPartVisible(i)) continue;
                Bounds partBounds = data.parts[i].bounds;
                if (first) { bounds = partBounds; first = false; }
                else bounds.Encapsulate(partBounds);
            }
            if (first) bounds = new Bounds(Vector3.up * .9f, new Vector3(.6f, 1.8f, .4f));
            return WorldStudyBounds(bounds);
        }

        Bounds WorldStudyBounds(Bounds local)
        {
            Vector3 e = local.extents;
            var world = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                world.Encapsulate(transform.TransformPoint(local.center + new Vector3(
                    (corner & 1) == 0 ? -e.x : e.x, (corner & 2) == 0 ? -e.y : e.y, (corner & 4) == 0 ? -e.z : e.z)));
            return world;
        }

        public void PrepareWebStudyColliders() => BeginBuildSystem(ActiveSystemId);

        public bool ApplyWebStudy(string study, bool touch, out Bounds framing)
        {
            framing = default;
            int first = Array.FindIndex(data.parts, p => WebStudyIncludes(p, study));
            if (first < 0) return false;
            RecordUndo("abrir vista de estudio");
            Array.Clear(visibleSystems, 0, visibleSystems.Length);
            explicitInteractiveParts.Clear();
            showAllVisuals = false;
            string primary = study == "heart" || study == "valves" ? "cardiac" :
                study == "digestive" || study == "hepatobiliary" ? "digestive" :
                study == "arteries" || study == "coronary" ? "arterial" : "venous";
            activeSystem = Array.IndexOf(AtlasSystems.Ids, primary);
            bool hasBounds = false;
            for (int i = 0; i < data.parts.Length; i++)
            {
                AtlasPart part = data.parts[i];
                bool include = WebStudyIncludes(part, study);
                excludedParts[i] = !include;
                if (!include) continue;
                visibleSystems[Array.IndexOf(AtlasSystems.Ids, part.system)] = true;
                if (part.system != primary) explicitInteractiveParts.Add(i);
                // The opening view of digestion is the abdomen. Oral and oesophageal structures remain available.
                if (study == "digestive" && (HasTerm(part.name,"gland") || HasTerm(part.name,"tongue") || HasTerm(part.name,"esophagus"))) continue;
                if (!hasBounds) { framing = part.bounds; hasBounds = true; }
                else framing.Encapsulate(part.bounds);
            }
            framing = WorldStudyBounds(hasBounds ? framing : data.parts[first].bounds);
            if (!touch) foreach (int i in explicitInteractiveParts) EnsureLearningPiece(i);
            ApplyBatchState();
            UpdateAvailability();
            // Touch tests the shared meshes and creates only the individual piece that is touched.
            if (touch) { StopAllCoroutines(); building = false; }
            else BeginBuildSystem(primary);
            UpdateStatus();
            return true;
        }

        public void WebInspect(int index)
        {
            if (psychologyPanel && psychologyPanel.HandlePieceSelected(index)) return;
            if (Learning != null && Learning.HandlePieceSelected(index)) return;
            ShowLearningPart(index);
        }

        public void ClearWebFact() => informationPanel.ClearSelection();

        // Test triangles only at a tap or the start of a hold. Bounding boxes alone overlap heavily in vascular trees.
        struct PickCandidate
        {
            public int index;
            public Ray localRay;
            public float scale, near;
        }
        readonly List<PickCandidate> touchCandidates = new List<PickCandidate>();
        readonly List<Vector3> touchVertices = new List<Vector3>();
        readonly List<int> touchIndices = new List<int>();

        public int PickWebSurface(Ray ray, float limit, out float hitDistance)
        {
            touchCandidates.Clear();
            Matrix4x4 assembled = transform.worldToLocalMatrix;
            for (int i = 0; i < data.parts.Length; i++)
            {
                if (!IsPartVisible(i)) continue;
                AtlasPart part = data.parts[i];
                Matrix4x4 inverse = assembled;
                if (pieces[i] && pieces[i].Detached)
                    inverse = Matrix4x4.Translate(part.bounds.center) * pieces[i].transform.worldToLocalMatrix;
                Vector3 direction = inverse.MultiplyVector(ray.direction);
                float scale = direction.magnitude;
                if (scale < .00001f) continue;
                Ray local = new Ray(inverse.MultiplyPoint3x4(ray.origin), direction / scale);
                if (!part.bounds.IntersectRay(local, out float entry) || entry > limit * scale) continue;
                touchCandidates.Add(new PickCandidate { index = i, localRay = local, scale = scale, near = Mathf.Max(0, entry) / scale });
            }
            touchCandidates.Sort((a,b) => a.near.CompareTo(b.near));
            int best = -1;
            hitDistance = limit;
            foreach (var candidate in touchCandidates)
            {
                if (candidate.near > hitDistance) break;
                Mesh mesh = data.parts[candidate.index].mesh;
                mesh.GetVertices(touchVertices);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    mesh.GetTriangles(touchIndices, sub);
                    for (int t = 0; t < touchIndices.Count; t += 3)
                    {
                        Vector3 a = touchVertices[touchIndices[t]];
                        Vector3 e1 = touchVertices[touchIndices[t+1]] - a, e2 = touchVertices[touchIndices[t+2]] - a;
                        Vector3 p = Vector3.Cross(candidate.localRay.direction, e2);
                        float det = Vector3.Dot(e1, p);
                        if (Mathf.Abs(det) < 1e-12f) continue;
                        Vector3 origin = candidate.localRay.origin - a;
                        float u = Vector3.Dot(origin, p) / det;
                        if (u < 0 || u > 1) continue;
                        Vector3 q = Vector3.Cross(origin, e1);
                        float v = Vector3.Dot(candidate.localRay.direction, q) / det;
                        if (v < 0 || u + v > 1) continue;
                        float d = Vector3.Dot(e2, q) / det / candidate.scale;
                        if (d < 0 || d >= hitDistance) continue;
                        hitDistance = d;
                        best = candidate.index;
                    }
                }
            }
            return best;
        }
    }
}
