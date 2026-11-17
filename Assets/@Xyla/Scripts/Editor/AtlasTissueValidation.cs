using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Xyla.Editor
{
    public static class AtlasTissueValidation
    {
        [MenuItem("Atlas Humano Quest/Validar asignación anatómica de materiales")]
        public static void Run()
        {
            AtlasData atlas = AssetDatabase.LoadAssetAtPath<AtlasData>("Assets/@Xyla/Data/Atlas.asset");
            if (!atlas || atlas.parts == null) throw new InvalidOperationException("Falta Atlas.asset.");
            var byId = atlas.parts.ToDictionary(part => part.id);
            Assert(byId, "FJ2816", AtlasSurfaceKind.Liver);
            Assert(byId, "FJ3145", AtlasSurfaceKind.Kidney);
            Assert(byId, "FJ3147", AtlasSurfaceKind.Kidney);
            Assert(byId, "FJ2810", AtlasSurfaceKind.Skin);
            Assert(byId, "FJ2813", AtlasSurfaceKind.Hair);
            Assert(byId, "FJ2428", AtlasSurfaceKind.Myocardium);
            Assert(byId, "FJ2417", AtlasSurfaceKind.CardiacValve);
            Assert(byId, "FJ1730", AtlasSurfaceKind.CerebrospinalSpace);
            Assert(byId, "FJ2441", AtlasSurfaceKind.Airway);
            Assert(byId, "FJ1289", AtlasSurfaceKind.Cornea);
            Assert(byId, "FJ1297", AtlasSurfaceKind.Iris);
            Assert(byId, "FJ1317", AtlasSurfaceKind.Sclera);
            if (AtlasTissueProfile.Kind(byId["FJ3094"]) == AtlasSurfaceKind.Liver)
                throw new InvalidOperationException("Un conducto hepático recibió la textura del hígado.");
            var counts = new Dictionary<AtlasSurfaceKind, int>();
            int texturedInBatch = 0, texturedOnlyWhenGrabbed = 0;
            foreach (AtlasPart part in atlas.parts)
            {
                AtlasSurfaceKind kind = AtlasTissueProfile.Kind(part);
                counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
                string map = AtlasTissueProfile.TextureResource(kind);
                if (map == null) continue;
                if (map == AtlasTissueProfile.BatchTextureResource(part.system) ||
                    (part.system == "muscular" && map == "Myocardium")) texturedInBatch++;
                else texturedOnlyWhenGrabbed++;
            }
            if (counts[AtlasSurfaceKind.Liver] != 1 || counts[AtlasSurfaceKind.Kidney] != 2 ||
                counts[AtlasSurfaceKind.CerebrospinalSpace] != 5 || texturedOnlyWhenGrabbed != 0)
                throw new InvalidOperationException("El conteo de órganos o espacios cerebrales cambió inesperadamente.");
            Debug.Log("[Atlas Tissue Validation] " + atlas.parts.Length + " piezas; " +
                texturedInBatch + " con mapa en el atlas; " + texturedOnlyWhenGrabbed +
                " con mapa al agarrar; " + counts[AtlasSurfaceKind.Artery] + " arteriales; " +
                counts[AtlasSurfaceKind.Vein] + " venosas. Casos anatómicos verificados.");
        }

        static void Assert(Dictionary<string, AtlasPart> byId, string id, AtlasSurfaceKind expected)
        {
            AtlasSurfaceKind actual = AtlasTissueProfile.Kind(byId[id]);
            if (actual != expected)
                throw new InvalidOperationException(id + ": esperado " + expected + ", obtenido " + actual);
        }
    }
}
