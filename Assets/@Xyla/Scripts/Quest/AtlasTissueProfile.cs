using System;
using UnityEngine;

namespace Xyla
{
    public enum AtlasSurfaceKind
    {
        Default, Muscle, Myocardium, Liver, Kidney, Skin,
        CardiacValve, BloodChamber, CerebrospinalSpace,
        Airway, Cartilage, Epiglottis,
        Sclera, Iris, Cornea, Lens, Retina, Choroid, Vitreous,
        Hair, Lip, Pancreas, DigestiveWall, BileDuct, Gallbladder,
        UrinaryWall, Artery, Vein
    }

    public static class AtlasTissueProfile
    {
        static bool Has(string name, string fragment) => name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

        public static AtlasSurfaceKind Kind(AtlasPart part)
        {
            if (part == null || string.IsNullOrEmpty(part.name)) return AtlasSurfaceKind.Default;
            string name = part.name;
            switch (part.system)
            {
                case "muscular":
                    return Has(name, "papillary muscle") && Has(name, "ventricle")
                        ? AtlasSurfaceKind.Myocardium : AtlasSurfaceKind.Muscle;
                case "cardiac":
                    if (Has(name, "third ventricle") || Has(name, "fourth ventricle") ||
                        Has(name, "lateral ventricle") || Has(name, "interventricular foramen"))
                        return AtlasSurfaceKind.CerebrospinalSpace;
                    if (Has(name, "wall of") || Has(name, "wall of ventricle")) return AtlasSurfaceKind.Myocardium;
                    if (Has(name, "cusp") || Has(name, "leaflet")) return AtlasSurfaceKind.CardiacValve;
                    if (Has(name, "cavity")) return AtlasSurfaceKind.BloodChamber;
                    return AtlasSurfaceKind.Default;
                case "respiratory":
                    if (Has(name, "bronch") || Has(name, "trachea")) return AtlasSurfaceKind.Airway;
                    if (Has(name, "cartilage") || Has(name, "concha")) return AtlasSurfaceKind.Cartilage;
                    if (Has(name, "epiglottis")) return AtlasSurfaceKind.Epiglottis;
                    if (Has(name, "constrictor") || Has(name, "pharyngeus")) return AtlasSurfaceKind.Muscle;
                    return AtlasSurfaceKind.Default;
                case "digestive":
                    if (string.Equals(name, "Caudate lobe of liver", StringComparison.OrdinalIgnoreCase)) return AtlasSurfaceKind.Liver;
                    if (Has(name, "gallbladder")) return AtlasSurfaceKind.Gallbladder;
                    if (Has(name, "biliary") || Has(name, "hepatic duct") || Has(name, "cystic duct")) return AtlasSurfaceKind.BileDuct;
                    if (Has(name, "pancreas") || Has(name, "pancreatic")) return AtlasSurfaceKind.Pancreas;
                    if (Has(name, "stomach") || Has(name, "colon") || Has(name, "rectum") ||
                        Has(name, "ileum") || Has(name, "jejunum") || Has(name, "duodenum") || Has(name, "esophagus"))
                        return AtlasSurfaceKind.DigestiveWall;
                    return AtlasSurfaceKind.Default;
                case "urinary":
                    if (Has(name, "kidney")) return AtlasSurfaceKind.Kidney;
                    return AtlasSurfaceKind.UrinaryWall;
                case "sensory":
                    if (Has(name, "sclera")) return AtlasSurfaceKind.Sclera;
                    if (Has(name, "iris")) return AtlasSurfaceKind.Iris;
                    if (Has(name, "cornea")) return AtlasSurfaceKind.Cornea;
                    if (Has(name, "lens")) return AtlasSurfaceKind.Lens;
                    if (Has(name, "retina")) return AtlasSurfaceKind.Retina;
                    if (Has(name, "choroid") && !Has(name, "plexus")) return AtlasSurfaceKind.Choroid;
                    if (Has(name, "vitreous") || Has(name, "chamber of")) return AtlasSurfaceKind.Vitreous;
                    if (Has(name, "external ear")) return AtlasSurfaceKind.Skin;
                    return AtlasSurfaceKind.Default;
                case "integumentary":
                    if (string.Equals(name, "Skin", StringComparison.OrdinalIgnoreCase)) return AtlasSurfaceKind.Skin;
                    if (Has(name, "hair") || Has(name, "eyebrow")) return AtlasSurfaceKind.Hair;
                    if (Has(name, "lip")) return AtlasSurfaceKind.Lip;
                    return AtlasSurfaceKind.Default;
                case "arterial": return AtlasSurfaceKind.Artery;
                case "venous": return AtlasSurfaceKind.Vein;
                default: return AtlasSurfaceKind.Default;
            }
        }

        public static string TextureResource(AtlasSurfaceKind kind)
        {
            switch (kind)
            {
                case AtlasSurfaceKind.Muscle: return "SkeletalMuscle";
                case AtlasSurfaceKind.Myocardium: return "Myocardium";
                case AtlasSurfaceKind.Liver: return "LiverTissue";
                case AtlasSurfaceKind.Kidney: return "KidneyTissue";
                case AtlasSurfaceKind.Skin: return "SkinTissue";
                default: return null;
            }
        }

        public static string BatchTextureResource(string system)
        {
            switch (system)
            {
                case "muscular": case "respiratory": return "SkeletalMuscle";
                case "cardiac": return "Myocardium";
                case "digestive": return "LiverTissue";
                case "urinary": return "KidneyTissue";
                case "integumentary": case "sensory": return "SkinTissue";
                default: return null;
            }
        }

        public static Color Color(AtlasSurfaceKind kind, string system)
        {
            string hex;
            switch (kind)
            {
                case AtlasSurfaceKind.Muscle: hex = "#a7524e"; break;
                case AtlasSurfaceKind.Myocardium: hex = "#873539"; break;
                case AtlasSurfaceKind.Liver: hex = "#824441"; break;
                case AtlasSurfaceKind.Kidney: hex = "#965a50"; break;
                case AtlasSurfaceKind.Skin: hex = "#b88466"; break;
                case AtlasSurfaceKind.CardiacValve: hex = "#d9c5b7"; break;
                case AtlasSurfaceKind.BloodChamber: hex = "#7d3b43"; break;
                case AtlasSurfaceKind.CerebrospinalSpace: hex = "#a8b6bb"; break;
                case AtlasSurfaceKind.Airway: hex = "#bd999b"; break;
                case AtlasSurfaceKind.Cartilage: hex = "#d3cbbd"; break;
                case AtlasSurfaceKind.Epiglottis: hex = "#bb9096"; break;
                case AtlasSurfaceKind.Sclera: hex = "#e5e1d8"; break;
                case AtlasSurfaceKind.Iris: hex = "#705a48"; break;
                case AtlasSurfaceKind.Cornea: hex = "#b9ced2"; break;
                case AtlasSurfaceKind.Lens: hex = "#d9d9cb"; break;
                case AtlasSurfaceKind.Retina: hex = "#a66361"; break;
                case AtlasSurfaceKind.Choroid: hex = "#694345"; break;
                case AtlasSurfaceKind.Vitreous: hex = "#b8cbd0"; break;
                case AtlasSurfaceKind.Hair: hex = "#493b33"; break;
                case AtlasSurfaceKind.Lip: hex = "#b77676"; break;
                case AtlasSurfaceKind.Pancreas: hex = "#c1a68d"; break;
                case AtlasSurfaceKind.DigestiveWall: hex = "#b87e80"; break;
                case AtlasSurfaceKind.BileDuct: hex = "#a5a278"; break;
                case AtlasSurfaceKind.Gallbladder: hex = "#777c57"; break;
                case AtlasSurfaceKind.UrinaryWall: hex = "#bf9290"; break;
                case AtlasSurfaceKind.Artery: hex = "#b54b48"; break;
                case AtlasSurfaceKind.Vein: hex = "#657c99"; break;
                default: return AtlasSystems.ColorFor(system);
            }
            ColorUtility.TryParseHtmlString(hex, out Color result);
            return result;
        }

        public static float Smoothness(AtlasSurfaceKind kind)
        {
            switch (kind)
            {
                case AtlasSurfaceKind.Cornea:
                case AtlasSurfaceKind.Lens:
                case AtlasSurfaceKind.Vitreous: return .78f;
                case AtlasSurfaceKind.Iris:
                case AtlasSurfaceKind.Sclera:
                case AtlasSurfaceKind.CardiacValve:
                case AtlasSurfaceKind.Airway:
                case AtlasSurfaceKind.Artery:
                case AtlasSurfaceKind.Vein: return .52f;
                case AtlasSurfaceKind.Liver:
                case AtlasSurfaceKind.Kidney:
                case AtlasSurfaceKind.Gallbladder: return .39f;
                case AtlasSurfaceKind.Myocardium: return .3f;
                case AtlasSurfaceKind.Muscle: return .2f;
                case AtlasSurfaceKind.Skin:
                case AtlasSurfaceKind.Hair: return .12f;
                default: return .28f;
            }
        }
    }
}
