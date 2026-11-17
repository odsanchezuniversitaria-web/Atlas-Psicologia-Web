using System;
using UnityEngine;

namespace Xyla
{
    [Serializable] public class AtlasPart
    {
        public string id, name, conceptId, system;
        public Mesh mesh;
        public Bounds bounds;
    }
    [Serializable] public class AtlasConcept { public string id, name; public string[] elements; }
    [Serializable] public class AtlasBatch { public string system; public Mesh mesh; }
    public class AtlasData : ScriptableObject
    {
        public AtlasPart[] parts;
        public AtlasConcept[] concepts;
        public AtlasBatch[] batches;
        public Shader shader;
        public int triangles;
        public string attribution;
    }
    public static class AtlasSystems
    {
        public static readonly string[] Ids = { "skeletal","muscular","cardiac","sensory","arterial","venous","nervous","respiratory","digestive","urinary","lymphatic","endocrine","reproductive","integumentary","connective" };
        public static readonly string[] Names = { "Esqueleto","Músculos","Corazón","Órganos sensoriales","Arterias","Venas","Sistema nervioso","Respiratorio","Digestivo","Urinario","Linfático","Endocrino","Reproductor","Superficie corporal","Tejido conectivo" };
        public static readonly string[] Colors = { "#e2d9ba","#a85b50","#b96760","#b0c8ce","#c05245","#527c9f","#d8b565","#b98991","#b8916b","#b47961","#879f7c","#c5a09a","#bda098","#ba9b7d","#aec3bb" };
        public static string Name(string id) { int i = Array.IndexOf(Ids,id); return i < 0 ? id : Names[i]; }
        public static Color ColorFor(string id) { int i=Array.IndexOf(Ids,id); ColorUtility.TryParseHtmlString(Colors[Mathf.Max(0,i)],out Color c); return c; }
    }
}
