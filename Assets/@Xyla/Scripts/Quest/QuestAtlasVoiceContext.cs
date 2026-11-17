using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Oculus.Interaction;
using UnityEngine;

namespace Xyla
{
    public sealed class QuestAtlasVoiceContext : MonoBehaviour
    {
        QuestAtlasController owner;
        RayInteractor[] rays;
        readonly Dictionary<int, QuestAtlasPiece> pieces = new Dictionary<int, QuestAtlasPiece>();
        readonly Dictionary<RayInteractable, QuestAtlasPiece> rayPieces = new Dictionary<RayInteractable, QuestAtlasPiece>();

        public void Initialize(QuestAtlasController atlas)
        {
            owner = atlas;
            rays = FindObjectsByType<RayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        public void Register(QuestAtlasPiece piece, RayInteractable target)
        {
            pieces[piece.Index] = piece;
            rayPieces[target] = piece;
        }

        public void RegisterWeb(QuestAtlasPiece piece){pieces[piece.Index]=piece;}
        readonly List<int> recentPointed = new List<int>();
        readonly List<int> livePointed = new List<int>();
        float lastPointedAt = -10f;
        const float PointingGraceSeconds = .65f;
        void LateUpdate() { RefreshPointing(); }

        void RefreshPointing()
        {
            if(AtlasWebPlatform.Active){var target=AtlasWebRuntime.Current.Pointed;recentPointed.Clear();if(target&&owner.IsPartVisible(target.Index))recentPointed.Add(target.Index);return;}
            livePointed.Clear();
            bool otherTarget = false;
            foreach(var ray in rays)
            {
                if(!ray || !ray.isActiveAndEnabled ||
                    (ray.State!=InteractorState.Hover && ray.State!=InteractorState.Select)) continue;
                var target=ray.Interactable;
                if(!target || !target.isActiveAndEnabled)continue;
                if(Vector3.Distance(ray.Origin,ray.End)>ray.MaxRayLength+.01f)continue;
                if(!rayPieces.TryGetValue(target,out var piece)) {otherTarget=true;continue;}
                if(!piece || !piece.isActiveAndEnabled || !owner.IsPartVisible(piece.Index))continue;
                if(!livePointed.Contains(piece.Index))livePointed.Add(piece.Index);
            }
            if(livePointed.Count>0)
            {
                recentPointed.Clear();recentPointed.AddRange(livePointed);lastPointedAt=Time.unscaledTime;
            }
            else if(otherTarget || Time.unscaledTime-lastPointedAt>PointingGraceSeconds) recentPointed.Clear();
        }

        public bool HasCurrentTarget()
        {
            RefreshPointing();
            foreach(int index in recentPointed)
                if(pieces.TryGetValue(index,out var piece) && piece && piece.isActiveAndEnabled && owner.IsPartVisible(index))return true;
            foreach(var piece in pieces.Values)
                if(piece && piece.isActiveAndEnabled && piece.IsHeld && owner.IsPartVisible(piece.Index))return true;
            return false;
        }

public bool TryResolvePrimaryTarget(JObject snapshot, out AtlasPart part)
        {
            part = null;
            if (snapshot == null) return false;
            var held = snapshot["held"] as JArray;
            var pointed = snapshot["pointed"] as JArray;
            JArray candidates = held != null && held.Count > 0 ? held : pointed;
            if (candidates == null || candidates.Count != 1) return false;
            return TryResolvePart(candidates[0].Value<string>("id"), out part);
        }

        public bool TryResolvePart(string id, out AtlasPart part)
        {
            part = null;
            if (owner == null || owner.data == null || owner.data.parts == null || string.IsNullOrWhiteSpace(id))
                return false;
            for (int i = 0; i < owner.data.parts.Length; i++)
            {
                AtlasPart candidate = owner.data.parts[i];
                if (candidate != null && string.Equals(candidate.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    part = candidate;
                    return true;
                }
            }
            return false;
        }


        public JObject Capture()
        {
            var held = new JArray();
            foreach (var piece in pieces.Values)
                if (piece && piece.isActiveAndEnabled && piece.IsHeld && owner.IsPartVisible(piece.Index))
                    held.Add(Entry(piece));
            RefreshPointing();
            var pointed = new JArray();
            foreach(int index in recentPointed)
                if(pieces.TryGetValue(index,out var piece) && piece && piece.isActiveAndEnabled && owner.IsPartVisible(index))
                    pointed.Add(Entry(piece));
            var visibility = new JObject();
            var examples = new JArray();
            bool partial=false;
            for(int si=0;si<AtlasSystems.Ids.Length;si++)
            {
                int count=0,total=0;
                for(int p=0;p<owner.data.parts.Length;p++)
                    if(owner.data.parts[p].system==AtlasSystems.Ids[si]) {total++;if(owner.IsPartVisible(p))count++;}
                visibility[AtlasSystems.Ids[si]]=count;
                if(count>0&&count<total)partial=true;
            }
            if(partial)
                for(int p=0;p<owner.data.parts.Length && examples.Count<12;p++)
                    if(owner.IsPartVisible(p))examples.Add(owner.data.parts[p].id);
            var visible = new JArray();
            for (int i = 0; i < AtlasSystems.Ids.Length; i++)
                if (owner.IsSystemVisible(i)) visible.Add(AtlasSystems.Ids[i]);
            return new JObject {
                ["snapshot_id"] = Guid.NewGuid().ToString("N"),
                ["captured_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["held"] = held, ["pointed"] = pointed,
                ["visible_systems"] = visible, ["active_system"] = owner.ActiveSystemId,
                ["visibility_counts"] = visibility, ["visible_examples"] = examples,
                ["species"] = "human"
            };
        }

        JObject Entry(QuestAtlasPiece piece)
        {
            var part = owner.data.parts[piece.Index];
            return new JObject { ["id"] = part.id, ["name"] = AtlasSpanish.Name(part.name) };
        }
    }
}
