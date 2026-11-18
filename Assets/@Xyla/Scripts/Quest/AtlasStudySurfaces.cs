using System;
using UnityEngine;

namespace Xyla
{
    // All meshes already carry a stable catalogue coordinate in UV1, including loose pieces.
    internal sealed class AtlasStudySurfaces : IDisposable
    {
        public readonly Material Assembled;
        public readonly Material Loose;
        readonly Texture2D rows;
        readonly float boneStrength;

        public AtlasStudySurfaces(AtlasData data, Texture2D states)
        {
            byte[] frames = Resources.Load<TextAsset>("AtlasSurfaceFrames").bytes;
            byte[] tendons = Resources.Load<TextAsset>("AtlasDistalTendons").bytes;
            if (frames.Length != data.parts.Length * 6 || tendons.Length != data.parts.Length * 8)
                throw new InvalidOperationException("La tabla de superficies no corresponde al catálogo de piezas.");

            int width = states.width;
            rows = new Texture2D(width, 4, TextureFormat.RGBAHalf, false, true)
            {
                name = "Acabado y orientación por pieza", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[width * 4];
            for (int i = 0; i < data.parts.Length; i++)
            {
                AtlasPart part = data.parts[i];
                var kind = AtlasTissueProfile.Kind(part);
                byte code = AtlasSurfaceDetail.Code(part.system, part.name);
                Color tint = AtlasSurfaceDetail.Tint(code, AtlasTissueProfile.Color(kind, part.system), part.system, part.name);
                tint = tint.linear;
                Vector3 longAxis = Axis(frames, i * 6);
                Vector3 wideAxis = Axis(frames, i * 6 + 3);
                wideAxis = (wideAxis - Vector3.Dot(longAxis, wideAxis) * longAxis).normalized;
                Vector3 origin = part.bounds.center;
                float paleUntil = BitConverter.ToSingle(tendons, i * 8);
                float muscleFrom = BitConverter.ToSingle(tendons, i * 8 + 4);
                if (code == 1)
                {
                    if (Contains(part.name, "orbicularis")) code = 16;
                    else if (Contains(part.name, "pectoralis major") || Contains(part.name, "temporalis muscle"))
                    {
                        code = 15;
                        // Place the fan near the lateral humeral or inferior mandibular attachment.
                        Vector3 attachment = Contains(part.name, "pectoralis")
                            ? new Vector3(Mathf.Sign(origin.x), .24f, 0f) : Vector3.down;
                        if (Vector3.Dot(longAxis, attachment) > 0f) longAxis = -longAxis;
                        Vector3 extents = part.bounds.extents;
                        float reach = Vector3.Dot(extents, new Vector3(Mathf.Abs(longAxis.x), Mathf.Abs(longAxis.y), Mathf.Abs(longAxis.z)));
                        origin -= longAxis * reach;
                    }
                    else if (Contains(part.name, "rectus femoris")) code = 17;
                    else if (muscleFrom > paleUntil)
                    {
                        code = 20;
                        if (longAxis.y < 0f) longAxis = -longAxis;
                        pixels[3 * width + i] = new Color(paleUntil, muscleFrom, 0f, 0f);
                    }
                }
                if (code == 4 && kind == AtlasSurfaceKind.Liver) code = 18;
                if (code == 4 && kind == AtlasSurfaceKind.Kidney) code = 19;
                tint.a = code;
                pixels[i] = tint;
                pixels[width + i] = new Color(wideAxis.x, wideAxis.y, wideAxis.z, -Vector3.Dot(wideAxis, origin));
                pixels[2 * width + i] = new Color(longAxis.x, longAxis.y, longAxis.z, -Vector3.Dot(longAxis, origin));
            }
            rows.SetPixels(pixels);
            rows.Apply(false, true);
            Assembled = new Material(Resources.Load<Shader>("AtlasPartSurface")) { name = "Atlas · sistemas ensamblados" };
            Assembled.SetTexture("_PartData", states);
            Assembled.SetTexture("_SurfaceRows", rows);
            Assembled.SetTexture("_DetailMap", Resources.Load<Texture2D>("AtlasSurfaceDetail"));
            Bind("SkeletalMuscle", "_Muscle", "_TissueMap", "_TissueScale", "_TissueStrength");
            Bind("Myocardium", "_Heart", "_TissueMap", "_TissueScale", "_TissueStrength");
            Bind("KidneyTissue", "_Kidney", "_TissueMap", "_TissueScale", "_TissueStrength");
            Bind("LiverTissue", "_Liver", "_TissueMap", "_TissueScale", "_TissueStrength");
            Bind("SkinTissue", "_Skin", "_TissueMap", "_TissueScale", "_TissueStrength");
            Bind("CorticalBone", "_Bone", "_BoneMap", "_TextureScale", "_TextureStrength");
            Bind("BrainTissue", "_Brain", "_BrainMap", "_BrainScale", "_BrainStrength");
            boneStrength = Assembled.GetVector("_BoneParams").y;
            Loose = new Material(Assembled) { name = "Atlas · piezas en la mano" };
            Loose.EnableKeyword("ATLAS_LOOSE_PIECE");
        }

        static Vector3 Axis(byte[] values, int at)
        {
            return new Vector3(values[at] * (2f / 255f) - 1f, values[at + 1] * (2f / 255f) - 1f,
                values[at + 2] * (2f / 255f) - 1f).normalized;
        }

        static bool Contains(string label, string term) => label.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        void Bind(string resource, string property, string texture, string scale, string strength)
        {
            Material source = Resources.Load<Material>(resource);
            Assembled.SetTexture(property + "Map", source.GetTexture(texture));
            Assembled.SetVector(property + "Params", new Vector4(source.GetFloat(scale), source.GetFloat(strength), 0f, 0f));
        }

        public void SetBoneTexture(bool enabled)
        {
            var parameters = Assembled.GetVector("_BoneParams");
            parameters.y = enabled ? boneStrength : 0f;
            Assembled.SetVector("_BoneParams", parameters);
            Loose.SetVector("_BoneParams", parameters);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(Assembled);
            UnityEngine.Object.Destroy(Loose);
            UnityEngine.Object.Destroy(rows);
        }
    }
}
