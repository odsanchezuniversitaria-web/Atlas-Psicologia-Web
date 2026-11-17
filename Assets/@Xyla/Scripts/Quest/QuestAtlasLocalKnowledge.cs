using System;
using System.Globalization;
using System.Text;

namespace Xyla
{
    public enum AtlasLocalQuestionIntent
    {
        Unknown,
        Identification,
        Function,
        IdentificationAndFunction
    }

    // Knowledge that is packaged in Resources and works without a network connection.
    public static class QuestAtlasLocalKnowledge
    {
        public static bool TryAnswer(AtlasPart part, string question, out string answer)
        {
            answer = "";
            if (part == null) return false;
            AtlasLocalQuestionIntent intent = DetectIntent(question);
            if (intent == AtlasLocalQuestionIntent.Unknown) return false;
            answer = BuildAnswer(part, intent);
            return true;
        }

        public static string BuildOfflineAnswer(AtlasPart part)
        {
            return part == null
                ? "Señala o toma una pieza para consultar su ficha anatómica sin conexión."
                : BuildAnswer(part, AtlasLocalQuestionIntent.IdentificationAndFunction);
        }

public static string GetOfflineVoiceKey(AtlasPart part)
        {
            if (part == null) return "";
            return QuestAtlasInformation.Get(part.name).id;
        }


        public static string BuildAnswer(AtlasPart part, AtlasLocalQuestionIntent intent)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            QuestAtlasInformation.Definition info = QuestAtlasInformation.Get(part.name);
            string name = AtlasSpanish.Name(part.name);
            switch (intent)
            {
                case AtlasLocalQuestionIntent.Identification:
                    return name + ". " + EnsureSentence(info.description);
                case AtlasLocalQuestionIntent.Function:
                    return name + ". " + EnsureSentence(info.function);
                default:
                    return name + ". " + EnsureSentence(info.description) + " Función: " + EnsureSentence(info.function);
            }
        }

        public static AtlasLocalQuestionIntent DetectIntent(string question)
        {
            string value = Normalize(question);
            if (string.IsNullOrEmpty(value)) return AtlasLocalQuestionIntent.Unknown;

            bool asksFunction =
                value.Contains("para que sirve") || value.Contains("que hace") ||
                value.Contains("cual es su funcion") || value.Contains("que funcion") ||
                value.Contains("funcion tiene") || value.Contains("utilidad") ||
                value.Contains("para que se usa") || value.Contains("que papel cumple");

            bool asksIdentity =
                value.Contains("que es esto") || value.Contains("que es eso") ||
                value.Contains("que es esta") || value.Contains("que es este") ||
                value.Contains("que parte es") || value.Contains("como se llama") ||
                value.Contains("identifica") || value.Contains("que organo") ||
                value.Contains("que hueso") || value.Contains("que musculo") ||
                value == "que es" || value == "que es la pieza";

            if (asksIdentity && asksFunction) return AtlasLocalQuestionIntent.IdentificationAndFunction;
            if (asksFunction) return AtlasLocalQuestionIntent.Function;
            if (asksIdentity) return AtlasLocalQuestionIntent.Identification;
            return AtlasLocalQuestionIntent.Unknown;
        }

        static string EnsureSentence(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0) return "";
            char last = value[value.Length - 1];
            return last == '.' || last == '?' || last == '!' ? value : value + ".";
        }

        static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            bool space = false;
            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    space = false;
                }
                else if (!space && builder.Length > 0)
                {
                    builder.Append(' ');
                    space = true;
                }
            }
            return builder.ToString().Trim();
        }
    }
}
