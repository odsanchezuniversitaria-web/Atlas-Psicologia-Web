using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Xyla
{
    public enum AtlasFaceExpression { Rest, Ready, Listening, Thinking, Speaking, Happy, Question, Success, Concern, Wink, Surprise }

    public sealed class QuestAtlasAvatar : MonoBehaviour
    {
        QuestAtlasVoiceAgent voice;
        Transform head;
        Material face;
        Text caption, hint;
        Image statusDot;
        Image[] energyBars;
        Vector3 restingPosition;
        Vector3 restingScale;
        float nextFrame, lastFrame, blinkStart = -10f, nextBlink, speakingEnded = -10f;
        bool wasSpeaking;
        public AtlasFaceExpression CurrentExpression { get; private set; }
        public float MouthOpening { get; private set; }
#if UNITY_EDITOR
        float previewUntil;
        AtlasFaceExpression previewExpression;
        float previewMouth;
        public void Preview(AtlasFaceExpression expression, float mouth, float seconds = 3f)
        {
            previewExpression = expression; previewMouth = mouth;
            previewUntil = Time.unscaledTime + seconds;
        }
#endif
        public static QuestAtlasAvatar Create(QuestAtlasController owner, QuestAtlasVoiceAgent voice)
        {
            var go = new GameObject("Atlas · pantalla expresiva", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var avatar = go.AddComponent<QuestAtlasAvatar>();
            avatar.voice = voice;
            avatar.Build(owner);
            return avatar;
        }

        void Build(QuestAtlasController owner)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Título del atlas") root.SetActive(false);
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5; canvas.worldCamera = Camera.main;
            head=Camera.main.transform;
            GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
            var rect = GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1120,650);
            transform.position = owner.transform.position + new Vector3(0,2.17f,.72f);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * .00108f;

            var cyan = new Color(.04f,.84f,1f);
            Box("Carcasa",Vector2.zero,new Vector2(1144,674),new Color(.025f,.058f,.085f));
            Box("Borde luminoso",Vector2.zero,new Vector2(1126,656),new Color(.025f,.32f,.4f));
            Box("Pantalla",Vector2.zero,new Vector2(1116,646),new Color(.006f,.016f,.029f));
            Box("Línea superior",new Vector2(0,320),new Vector2(850,3),cyan);
            Label("Marca","A T L A S",new Vector2(-348,279),new Vector2(355,54),36,TextAnchor.MiddleLeft,Color.white);
            Label("Identidad","ASISTENTE DE ANATOMÍA",new Vector2(276,279),new Vector2(490,36),20,TextAnchor.MiddleRight,new Color(.40f,.76f,.83f));
            var screen = new GameObject("Rostro luminoso",typeof(RectTransform),typeof(RawImage));
            screen.transform.SetParent(transform,false);
            var image = screen.GetComponent<RawImage>();
            image.raycastTarget=false;
            face = new Material(Resources.Load<Material>("AtlasAssistantFace"));
            image.material=face; image.texture=Texture2D.whiteTexture;
            var screenRect=screen.GetComponent<RectTransform>();
            screenRect.sizeDelta=new Vector2(1040,448);screenRect.anchoredPosition=new Vector2(0,5);
            face.SetFloat("_Aspect",1040f/448f);
            caption=Label("Actividad","EN REPOSO",new Vector2(0,-244),new Vector2(960,45),28,TextAnchor.MiddleCenter,cyan);
            hint=Label("Ayuda","Activa la voz en el menú Asistente",new Vector2(0,-292),new Vector2(990,30),20,TextAnchor.MiddleCenter,new Color(.47f,.67f,.75f));
            statusDot=Box("Señal",new Vector2(-539,280),new Vector2(10,10),cyan);
            energyBars=new Image[7];
            for(int i=0;i<energyBars.Length;i++)
                energyBars[i]=Box("Pulso "+(i+1),new Vector2(-126+i*42,-205),new Vector2(13,18),new Color(.02f,.78f,1f,.72f));
            Box("Soporte posterior",new Vector2(0,-345),new Vector2(230,23),new Color(.035f,.09f,.13f));
            nextBlink=Time.unscaledTime+3.1f;lastFrame=Time.unscaledTime;
            restingPosition=transform.position;restingScale=transform.localScale;
        }

        Image Box(string name,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(transform,false);
            var r=go.GetComponent<RectTransform>();r.anchoredPosition=position;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        Text Label(string name,string value,Vector2 position,Vector2 size,int fontSize,TextAnchor alignment,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(transform,false);
            var r=go.GetComponent<RectTransform>();r.anchoredPosition=position;r.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text=value;text.fontSize=fontSize;text.alignment=alignment;text.color=color;text.raycastTarget=false;
            return text;
        }
        void Update()
        {
            float now=Time.unscaledTime;if(now<nextFrame)return;
            nextFrame=now+1f/30f;float dt=Mathf.Min(now-lastFrame,.1f);lastFrame=now;
            bool talking=voice.Speaking;
            if(wasSpeaking&&!talking)speakingEnded=now;
            wasSpeaking=talking;
            AtlasFaceExpression expression;
            string label,help;
            if(voice.HasError) { expression=AtlasFaceExpression.Concern;label="NECESITO TU AYUDA";help="Consulta el aviso en el menú Asistente"; }
            else if(talking) { expression=AtlasFaceExpression.Speaking;label="ATLAS HABLA";help="Escucha la explicación"; }
            else if(voice.PerformingAction) { expression=AtlasFaceExpression.Surprise;label="APLICANDO TU SOLICITUD";help="Estoy ajustando el atlas"; }
            else if(voice.Busy) { expression=AtlasFaceExpression.Thinking;label="PENSANDO";help="Estoy preparando tu respuesta"; }
            else if(voice.HearingSpeech) { expression=AtlasFaceExpression.Listening;label="TE ESCUCHO";help="Suelta el botón al terminar"; }
            else if(voice.AwaitingReply) { expression=AtlasFaceExpression.Question;label="TE ESCUCHO";help="Habla ahora"; }
            else if(!voice.Listening) { expression=AtlasFaceExpression.Rest;label="EN REPOSO";help="Activa la voz en el menú Asistente"; }
            else if(now-speakingEnded<1.7f||now-voice.LastActionAt<2f) { expression=AtlasFaceExpression.Success;label="LISTO";help="Puedes seguir explorando"; }
            else {
                float cycle=now%24f;
                expression=cycle>20f&&cycle<21f?AtlasFaceExpression.Wink:cycle>21f&&cycle<22.3f?AtlasFaceExpression.Happy:AtlasFaceExpression.Ready;
                label="AQUÍ ESTOY";help="Mantén A o X para preguntarme o pedirme una pieza";
            }
            float target=talking?voice.GetSpeechLevel():0f;
#if UNITY_EDITOR
            if(now<previewUntil) { expression=previewExpression;target=previewMouth;label=PreviewLabel(expression);help="ATLAS · EXPRESIONES"; }
#endif
            CurrentExpression=expression;
            MouthOpening=Mathf.Lerp(MouthOpening,target,1-Mathf.Exp(-dt*(target>MouthOpening?24f:13f)));
            if(now>=nextBlink) { blinkStart=now;nextBlink=now+Random.Range(3f,6.3f); }
            float phase=(now-blinkStart)/.17f;
            float blink=phase>=0&&phase<=1?Mathf.Sin(phase*Mathf.PI):0;
            Color tint=expression==AtlasFaceExpression.Success?new Color(1f,.60f,.24f)
                :expression==AtlasFaceExpression.Concern?new Color(1f,.57f,.24f)
                :new Color(.025f,.90f,1f);
            face.SetFloat("_Mood",(float)expression);face.SetFloat("_Talk",MouthOpening);
            face.SetFloat("_Blink",blink);face.SetFloat("_Clock",now);face.SetColor("_FaceColor",tint);
            if(caption.text!=label)caption.text=label;if(hint.text!=help)hint.text=help;
            caption.color=tint;statusDot.color=tint;
            statusDot.transform.localScale=Vector3.one*(1f+.32f*Mathf.Sin(now*4.5f));
            for(int i=0;i<energyBars.Length;i++)
            {
                float activity=talking?Mathf.Clamp01(.18f+MouthOpening*1.5f):voice.HearingSpeech?.72f:voice.Busy?.48f:.12f;
                float wave=.28f+.72f*Mathf.Abs(Mathf.Sin(now*(talking?9f:4f)+i*.83f));
                energyBars[i].rectTransform.sizeDelta=new Vector2(13,8+52*activity*wave);
                Color bar=tint;bar.a=.28f+.7f*activity;energyBars[i].color=bar;
            }
            float hover=Mathf.Sin(now*1.25f)*.008f;
            transform.position=restingPosition+Vector3.up*hover;
            float pulse=1f+(talking?.018f:voice.Busy?.009f:0f)*Mathf.Sin(now*5f);
            transform.localScale=restingScale*pulse;
            Vector3 direction=transform.position-head.position;direction.y=0;
            if(direction.sqrMagnitude>.05f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),dt*4f);
        }
#if UNITY_EDITOR
        static string PreviewLabel(AtlasFaceExpression e)
        {
            string[] names={"REPOSO","LISTO PARA AYUDAR","ESCUCHANDO","PENSANDO","HABLANDO","ALEGRÍA","DUDA","LOGRADO","AVISO","GUIÑO","SORPRESA"};
            return names[(int)e];
        }
#endif
        void OnDestroy() { if(face)Destroy(face); }
    }
}
