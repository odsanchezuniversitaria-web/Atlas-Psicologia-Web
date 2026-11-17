using System;
using UnityEngine;

namespace Xyla
{
    [DisallowMultipleComponent]
    public sealed class QuestAtlasGameConfiguration : MonoBehaviour
    {
        [Header("Reglas generales")]
        [Min(2)] public int busquedaPreguntas = 10;
        [Min(2)] public int detectivePreguntas = 12;
        [Min(2)] public int contrarrelojPreguntas = 24;
        [Min(2)] public int supervivenciaPreguntas = 30;

        [Header("Contrarreloj")]
        [Min(10f)] public float duracionContrarreloj = 60f;
        [Min(0f)] public float premioAcierto = 2.5f;
        [Min(0f)] public float penalizacionError = 3f;

        [Header("Supervivencia")]
        [Min(1)] public int vidasSupervivencia = 3;

        [Header("Presentación")]
        public Color colorTutor = new Color(.16f,.86f,.91f);
        public Color colorDesafio = new Color(1f,.63f,.18f);
        public Color colorCorrecto = new Color(.18f,1f,.55f);
        public Color colorError = new Color(1f,.28f,.25f);

        public QuestAtlasGameModeDefinition[] modos;

        public QuestAtlasGameModeDefinition Find(string id)
        {
            if(modos == null) return null;
            foreach(var mode in modos)
                if(mode != null && string.Equals(mode.id,id,StringComparison.OrdinalIgnoreCase))
                    return mode;
            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class QuestAtlasGameModeDefinition : MonoBehaviour
    {
        public string id;
        public string titulo;
        [TextArea(2,5)] public string descripcion;
        public bool habilitado = true;
        [Min(2)] public int cantidadPreguntas = 10;
        [Min(0f)] public float duracionSegundos;
        [Min(0)] public int vidas;
    }
}
