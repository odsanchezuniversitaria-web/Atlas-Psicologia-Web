using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xyla
{
    public static class QuestAtlasInformation
    {
        [Serializable] public sealed class Definition
        {
            public string id, kind, description, function, reference;
            public bool general;
        }
        [Serializable] public sealed class Mapping { public string source, definition; }
        [Serializable] public sealed class Catalogue
        {
            public string version, attribution;
            public Definition[] definitions;
            public Mapping[] entries;
        }

        static readonly Dictionary<string, Definition> entries = Load();
        public static int Count => entries.Count;

        static Dictionary<string, Definition> Load()
        {
            TextAsset resource = Resources.Load<TextAsset>("AtlasPartInformation");
            if (resource == null) throw new InvalidOperationException("Falta el catálogo de fichas anatómicas.");
            Catalogue catalogue = JsonUtility.FromJson<Catalogue>(resource.text);
            var definitions = new Dictionary<string, Definition>(StringComparer.Ordinal);
            foreach (Definition definition in catalogue.definitions) definitions.Add(definition.id, definition);
            var result = new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase);
            foreach (Mapping mapping in catalogue.entries) result.Add(mapping.source, definitions[mapping.definition]);
            return result;
        }

        public static Definition Get(string source)
        {
            if (!entries.TryGetValue(source, out Definition definition))
                throw new InvalidOperationException("Falta la ficha anatómica: " + source);
            return definition;
        }
    }
}
