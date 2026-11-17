using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Xyla
{
    // Deterministic show/hide/only orders for whole systems, resolved on the headset.
    // Only an entire, unambiguous order matches: an unknown word means no change at all.
    public static class AtlasSystemCommands
    {
        public sealed class Command
        {
            public string[] SystemIds;
            public string Mode; // show, hide or only
            public string Description;
        }

        static readonly string[] All = AtlasSystems.Ids;
        static readonly Dictionary<string, string[]> Aliases = BuildAliases();

        static Dictionary<string, string[]> BuildAliases()
        {
            var map = new Dictionary<string, string[]>();
            void Add(string[] ids, params string[] words) { foreach (string w in words) map[Normalize(w)] = ids; }
            Add(new[] { "skeletal" }, "esqueleto", "esqueletico", "esqueletica", "oseo", "osea", "oseos", "oseas", "hueso", "huesos",
                "osamenta", "estructura osea", "armazon oseo", "osteologico", "osteologia", "osseo", "osio");
            Add(new[] { "muscular" }, "muscular", "musculo", "musculos", "musculatura", "tejido muscular", "miofascial", "miologia");
            Add(new[] { "cardiac" }, "corazon", "cardiaco", "cardiaca", "estructura cardiaca");
            Add(new[] { "arterial" }, "arterial", "arteria", "arterias", "red arterial", "circulacion arterial");
            Add(new[] { "venous" }, "venoso", "venosa", "vena", "venas", "red venosa", "circulacion venosa");
            Add(new[] { "sensory" }, "sensorial", "sensoriales", "sensitivo", "sentidos", "organos sensoriales", "organos de los sentidos",
                "ojo", "ojos", "oido", "oidos", "orejas");
            Add(new[] { "nervous" }, "nervioso", "nerviosa", "nervio", "nervios", "neurologico", "neural", "neuronas", "cerebro", "encefalo",
                "medula", "medula espinal", "nervioso central", "nervioso periferico", "snc", "snp");
            Add(new[] { "respiratory" }, "respiratorio", "respiratoria", "respiracion", "pulmon", "pulmones", "pulmonar", "bronquios",
                "vias respiratorias", "vias aereas", "traquea");
            Add(new[] { "digestive" }, "digestivo", "digestiva", "digestion", "gastrointestinal", "gastro intestinal", "tracto gastrointestinal",
                "intestino", "intestinos", "tripas", "estomago", "alimentario");
            Add(new[] { "urinary" }, "urinario", "urinaria", "renal", "excretor", "excretorio", "rinon", "rinones", "vejiga", "vias urinarias");
            Add(new[] { "lymphatic" }, "linfatico", "linfatica", "linfa", "ganglio", "ganglios", "ganglios linfaticos", "inmune", "inmunitario",
                "inmunologico", "defensas", "defensas del cuerpo");
            Add(new[] { "endocrine" }, "endocrino", "endocrina", "hormonal", "hormonas", "glandula", "glandulas", "glandular",
                "glandulas endocrinas", "tiroides");
            Add(new[] { "reproductive" }, "reproductor", "reproductora", "reproductivo", "reproductiva", "reproduccion", "sexual", "genital",
                "genitales", "organos genitales", "organos sexuales", "organos reproductores");
            Add(new[] { "integumentary" }, "tegumentario", "tegumentaria", "integumento", "piel", "cutaneo", "cutanea", "epidermis",
                "superficie corporal", "revestimiento corporal", "superficie");
            Add(new[] { "connective" }, "conectivo", "conectiva", "tejido conectivo", "conjuntivo", "tejido conjuntivo", "articular",
                "articulaciones", "fascia", "fascias", "tendon", "tendones", "ligamento", "ligamentos", "cartilago", "cartilagos");
            Add(new[] { "cardiac", "arterial", "venous" }, "circulatorio", "circulatoria", "cardiovascular", "circulacion",
                "circulacion sanguinea", "sangre");
            Add(new[] { "arterial", "venous" }, "vascular", "vasos sanguineos", "vasos", "arterias y venas");
            Add(new[] { "skeletal", "muscular", "connective" }, "locomotor", "musculoesqueletico", "musculo esqueletico", "osteomuscular");
            Add(new[] { "skeletal", "connective" }, "osteoarticular", "huesos y articulaciones");
            Add(new[] { "urinary", "reproductive" }, "genitourinario", "urogenital");
            Add(All, "todo", "todos", "todo el cuerpo", "todos los sistemas", "todos los aparatos", "cuerpo completo");
            return map;
        }

        static readonly string[] ShowVerbs = { "activa", "activar", "activame", "actives", "muestra", "muestrame", "mostrar", "mostrame",
            "muestres", "ensena", "ensename", "ensenar", "prende", "prender", "prendeme", "enciende", "encender", "enciendeme", "habilita",
            "habilitar", "quiero ver", "dejame ver", "ver", "visualiza", "visualizar", "pon", "ponme", "revela", "revelame", "despliega",
            "agrega", "anade", "incluye", "haz visible" };
        static readonly string[] HideVerbs = { "desactiva", "desactivar", "desactivame", "oculta", "ocultar", "ocultame", "esconde",
            "esconder", "escondeme", "apaga", "apagar", "apagame", "quita", "quitar", "quitame", "saca", "sacar", "sacame", "retira",
            "deshabilita", "haz invisible", "no muestres", "deja de mostrar" };
        static readonly string[] OnlyVerbs = { "deja solo", "deja solamente", "aisla", "enfoca" };
        static readonly string[] OnlyWords = { "solo", "solamente", "unicamente", "nada mas" };
        static readonly string[] Prefixes = { "oye atlas", "hola atlas", "atlas", "por favor", "puedes", "podrias", "quiero que",
            "necesito que", "me gustaria que", "ahora", "y" };

        public static bool TryParse(string text, out Command command)
        {
            command = null;
            string q = Normalize(text);
            if (q.Length == 0) return false;
            for (bool trimmed = true; trimmed;)
            {
                trimmed = false;
                foreach (string p in Prefixes)
                {
                    if (q.StartsWith(p + ", ")) { q = q.Substring(p.Length + 2); trimmed = true; }
                    else if (q.StartsWith(p + " ")) { q = q.Substring(p.Length + 1); trimmed = true; }
                }
            }
            if (q.EndsWith(" por favor")) q = q.Substring(0, q.Length - " por favor".Length);

            string mode = null, rest = null;
            foreach (var (verbs, kind) in new[] { (OnlyVerbs, "only"), (HideVerbs, "hide"), (ShowVerbs, "show") })
                foreach (string verb in verbs.OrderByDescending(v => v.Length))
                    if (q.StartsWith(verb + " ")) { mode = kind; rest = q.Substring(verb.Length + 1); goto found; }
            return false;
        found:
            foreach (string word in OnlyWords)
                if (rest.StartsWith(word + " ")) { if (mode == "show") mode = "only"; rest = rest.Substring(word.Length + 1); break; }
            foreach (string word in OnlyWords)
                if (rest.EndsWith(" " + word)) { if (mode == "show") mode = "only"; rest = rest.Substring(0, rest.Length - word.Length - 1); break; }

            var ids = new List<string>();
            foreach (string raw in rest.Replace(",", " y ").Split(new[] { " y ", " e ", " con ", " mas " }, StringSplitOptions.RemoveEmptyEntries))
            {
                string item = Noun(raw);
                if (item.Length == 0) continue;
                if (!Aliases.TryGetValue(item, out string[] found)) return false;
                foreach (string id in found) if (!ids.Contains(id)) ids.Add(id);
            }
            if (ids.Count == 0) return false;
            string names = string.Join(", ", ids.Select(AtlasSystems.Name));
            command = new Command
            {
                SystemIds = ids.ToArray(),
                Mode = mode,
                Description = mode == "hide" ? "Listo: oculté " + names + "."
                    : mode == "only" ? "Listo: ahora solo ves " + names + "."
                    : "Listo: muestro " + names + "."
            };
            return true;
        }

        static string Noun(string text)
        {
            string t = " " + text.Trim() + " ";
            foreach (string w in new[] { " el ", " la ", " los ", " las ", " lo ", " un ", " una ", " mi ", " me " })
                t = t.Replace(w, " ");
            t = t.Trim();
            foreach (string w in new[] { "sistemas ", "sistema ", "aparatos ", "aparato ", "conjunto ", "organos del ", "organos de " })
                if (t.StartsWith(w)) { t = t.Substring(w.Length); break; }
            foreach (string w in new[] { "de los ", "de las ", "de la ", "del ", "de " })
                if (t.StartsWith(w)) { t = t.Substring(w.Length); break; }
            foreach (string w in new[] { " del cuerpo humano", " del cuerpo", " humano", " humana", " completo", " completa", " entero" })
                if (t.EndsWith(w)) { t = t.Substring(0, t.Length - w.Length); break; }
            return t.Trim();
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string d = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var b = new StringBuilder(d.Length);
            foreach (char c in d)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                b.Append(char.IsLetterOrDigit(c) || c == ',' ? c : ' ');
            }
            return string.Join(" ", b.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Replace(" ,", ",");
        }
    }
}
