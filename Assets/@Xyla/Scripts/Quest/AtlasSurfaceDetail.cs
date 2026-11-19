using System;
using UnityEngine;

namespace Xyla
{
    // Codes are shared with the surface shader. The mesh name describes the exposed structure.
    public static class AtlasSurfaceDetail
    {
        public const byte Smooth = 0, Muscle = 1, Collagen = 2, Vessel = 3, Organ = 4,
            Mucosa = 5, Cartilage = 6, Nerve = 7, Enamel = 8, Skin = 9,
            Myocardium = 10, Bone = 11, Brain = 12, OuterWall = 14, Liver = 18;

        static bool Has(string label, string text) =>
            label.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

        // The catalogue groups liver territories by their drainage system. A branch draining a segment is still a vessel.
        static bool IsLiverSegment(string label)
        {
            const string prefix = "Hepatovenous segment ";
            if (label.Length <= prefix.Length || !label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            for (int i = prefix.Length; i < label.Length; i++)
                if ("IVXivx".IndexOf(label[i]) < 0) return false;
            return true;
        }

        public static byte Code(string system, string label)
        {
            if (string.IsNullOrEmpty(label)) return Smooth;

            switch (system)
            {
                case "muscular":
                    return Has(label, "papillary muscle") ? Myocardium : Muscle;

                case "skeletal":
                    if (Has(label, "gingiva")) return Mucosa;
                    if (Has(label, "tooth")) return Enamel;
                    if (Has(label, "cartilage") || Has(label, "intervertebral disk")) return Cartilage;
                    if (Has(label, "iliotibial tract")) return Collagen;
                    if (Has(label, "fibularis") || Has(label, "tibialis") ||
                        Has(label, "subscapularis") || Has(label, "levator scapulae")) return Muscle;
                    return Bone;

                case "connective":
                    if (Has(label, "tensor fasciae latae")) return Muscle;
                    if (Has(label, "trochlea of")) return Cartilage;
                    return Collagen;

                case "arterial": return Vessel;
                case "venous": return IsLiverSegment(label) ? Liver : Vessel;

                case "cardiac":
                    if (Has(label, "wall of") || Has(label, "papillary muscle")) return Myocardium;
                    if (Has(label, "cusp") || Has(label, "leaflet")) return Collagen;
                    // This system also contains the geometry of blood and CSF spaces.
                    return Smooth;

                case "nervous":
                    if (Has(label, "tentorium")) return Collagen;
                    if (Has(label, "nerve") || Has(label, "ganglion")) return Nerve;
                    if (Has(label, "gyrus") || Has(label, "lobule") || Has(label, "lobe") ||
                        Has(label, "insula") || Has(label, "cerebellum")) return Brain;
                    // Preserve distinct deep nuclei and smooth white matter. The canal and aqueduct are spaces.
                    return Smooth;

                case "respiratory":
                    if (Has(label, "concha")) return Bone;
                    if (Has(label, "constrictor") || Has(label, "pharyngeus")) return Muscle;
                    if (Has(label, "cartilage") || Has(label, "epiglottis") ||
                        Has(label, "trachea") || Has(label, "main bronchus")) return Cartilage;
                    // A bronchial tree models an external airway wall, with no exposed alveoli.
                    return Has(label, "bronch") ? OuterWall : Smooth;

                case "digestive":
                    if (Has(label, "tongue")) return Mucosa;
                    if (Has(label, "duct") || Has(label, "biliary")) return OuterWall;
                    if (Has(label, "gland") || Has(label, "lobe of liver") || Has(label, "pancreas")) return Organ;
                    // Whole viscera expose serosa or adventitia; the lumen's folds are internal.
                    return OuterWall;

                case "urinary":
                    return Has(label, "kidney") ? Organ : OuterWall;

                case "endocrine":
                case "lymphatic":
                    return Organ;

                case "reproductive":
                    if (Has(label, "testis") || Has(label, "prostate") ||
                        Has(label, "seminal vesicle") || Has(label, "epididymis")) return Organ;
                    if (Has(label, "corpus cavern") || Has(label, "corpus spongios")) return Collagen;
                    return Has(label, "deferent duct") ? OuterWall : Smooth;

                case "integumentary":
                    if (Has(label, "skin")) return Skin;
                    if (Has(label, "lip")) return Mucosa;
                    return Smooth;

                case "sensory":
                    if (Has(label, "lacrimal bone")) return Bone;
                    if (Has(label, "external ear")) return Skin;
                    if (Has(label, "gland") || Has(label, "choroid plexus")) return Organ;
                    if (Has(label, "tendinous") || Has(label, "ligament") ||
                        Has(label, "retinaculum") || Has(label, "tarsal plate")) return Collagen;
                    if (Has(label, "canaliculus") || Has(label, "lacrimal sac") || Has(label, "nasolacrimal duct")) return OuterWall;
                    // The lens, cornea, and vitreous require an optically smooth surface.
                    return Smooth;

                default: return Smooth;
            }
        }

        public static Color32 Tint(byte code, Color32 fallback, string system, string label)
        {
            if (string.IsNullOrEmpty(label)) return fallback;
            byte a = fallback.a;
            switch (code)
            {
                case Liver:
                    Color32 liverTint = AtlasTissueProfile.Color(AtlasSurfaceKind.Liver, "digestive");
                    liverTint.a = a;
                    return liverTint;
                case Vessel:
                    // Retain the atlas colour convention for identification at the scale of thin branches.
                    return system == "venous" ? new Color32(121, 91, 121, a) : new Color32(181, 78, 68, a);
                case Brain: return new Color32(202, 171, 159, a);
                case Muscle: return new Color32(161, 72, 65, a);
                case Myocardium: return new Color32(133, 58, 58, a);
                case Collagen:
                    if (system == "reproductive") return new Color32(193, 165, 155, a);
                    if (system == "cardiac") return new Color32(220, 200, 178, a);
                    return new Color32(212, 205, 184, a);
                case Cartilage:
                    if (Has(label, "epiglottis")) return new Color32(198, 155, 153, a);
                    if (Has(label, "trachea") || Has(label, "bronchus")) return new Color32(201, 170, 158, a);
                    return new Color32(217, 207, 186, a);
                case Bone: return new Color32(221, 209, 178, a);
                case Nerve: return new Color32(219, 202, 158, a);
                case Enamel: return new Color32(235, 228, 205, a);
                case Mucosa:
                    if (Has(label, "gingiva")) return new Color32(191, 117, 115, a);
                    if (Has(label, "tongue")) return new Color32(176, 99, 101, a);
                    return fallback;
                case Organ:
                    if (Has(label, "spleen")) return new Color32(124, 66, 78, a);
                    if (Has(label, "thymus")) return new Color32(199, 155, 142, a);
                    if (Has(label, "adrenal")) return new Color32(202, 166, 106, a);
                    if (Has(label, "pineal") || Has(label, "pituitary")) return new Color32(169, 128, 122, a);
                    if (Has(label, "choroid plexus")) return new Color32(157, 96, 98, a);
                    if (Has(label, "lacrimal gland")) return new Color32(200, 158, 145, a);
                    if (Has(label, "sublingual") || Has(label, "submandibular")) return new Color32(200, 166, 141, a);
                    if (Has(label, "testis")) return new Color32(217, 204, 186, a);
                    return fallback;
                case Smooth:
                    if (system == "nervous")
                    {
                        if (Has(label, "canal") || Has(label, "aqueduct") || Has(label, "fossa"))
                            return new Color32(170, 184, 188, a);
                        if (Has(label, "white matter") || Has(label, "tract") || Has(label, "commissure") ||
                            Has(label, "capsule") || Has(label, "fornix") || Has(label, "callosum") ||
                            Has(label, "stria") || Has(label, "chiasm")) return new Color32(221, 209, 184, a);
                        return new Color32(180, 151, 144, a);
                    }
                    return fallback;
                case OuterWall:
                    if (system == "digestive")
                    {
                        if (Has(label, "stomach")) return new Color32(194, 138, 134, a);
                        if (Has(label, "jejunum") || Has(label, "duodenum")) return new Color32(196, 145, 131, a);
                        if (Has(label, "ileum") || Has(label, "ileocecal")) return new Color32(202, 157, 143, a);
                        if (Has(label, "mesentery") || Has(label, "mesocolon") || Has(label, "mesoappendix")) return new Color32(210, 186, 144, a);
                        if (Has(label, "colon") || Has(label, "rectum") || Has(label, "appendix")) return new Color32(178, 133, 138, a);
                        if (Has(label, "biliary") || Has(label, "hepatic duct") ||
                            Has(label, "duct of caudate") || Has(label, "cystic duct")) return new Color32(164, 159, 116, a);
                        if (Has(label, "pancreatic duct")) return new Color32(198, 180, 153, a);
                        if (Has(label, "taenia")) return new Color32(194, 149, 138, a);
                    }
                    return fallback;
                default: return fallback;
            }
        }
    }
}
