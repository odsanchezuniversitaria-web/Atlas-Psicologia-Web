using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Xyla
{
    public sealed partial class QuestAtlasController
    {
        sealed class UndoSnapshot
        {
            public string label;
            public bool showAll;
            public int active;
            public bool[] systems, excluded;
            public Vector3 modelPosition, modelScale;
            public Quaternion modelRotation;
            public bool passthrough,effects,sounds,bone,menuVisible,infoVisible,avatarVisible;
            public readonly Dictionary<int,PiecePose> pieces=new Dictionary<int,PiecePose>();
        }
        struct PiecePose
        {
            public Vector3 position,scale; public Quaternion rotation; public bool detached;
        }
        readonly Stack<UndoSnapshot> undoHistory=new Stack<UndoSnapshot>();
        bool restoringUndo;
        public QuestAtlasLearning Learning {get;private set;}
        public bool CanUndo=>undoHistory.Count>0;
        public void RefreshLearningUI(){menu?.Refresh();}
        void InitializeLearningTools()
        {
            Learning=gameObject.AddComponent<QuestAtlasLearning>();
            Learning.Initialize(this,informationPanel,voiceContext);
        }
        public void RecordUndo(string label)
        {
            if(restoringUndo||visibleSystems==null||excludedParts==null)return;
            var snapshot=new UndoSnapshot{label=label,showAll=showAllVisuals,active=activeSystem,
                systems=(bool[])visibleSystems.Clone(),excluded=(bool[])excludedParts.Clone(),
                modelPosition=transform.position,modelRotation=transform.rotation,modelScale=transform.localScale,
                passthrough=Environment&&Environment.PassthroughEnabled,effects=Environment&&Environment.EffectsEnabled,
                sounds=Environment&&Environment.SoundsEnabled,bone=BoneTextureEnabled,
                menuVisible=menu&&menu.gameObject.activeSelf,infoVisible=informationPanel&&informationPanel.gameObject.activeSelf,
                avatarVisible=avatar&&avatar.gameObject.activeSelf};
            if(pieces!=null)for(int i=0;i<pieces.Length;i++)if(pieces[i])
                snapshot.pieces[i]=new PiecePose{position=pieces[i].transform.position,rotation=pieces[i].transform.rotation,
                    scale=pieces[i].transform.localScale,detached=pieces[i].Detached};
            undoHistory.Push(snapshot);
            while(undoHistory.Count>20)
            {
                var items=undoHistory.ToArray();undoHistory.Clear();
                for(int i=Mathf.Min(19,items.Length-1);i>=0;i--)undoHistory.Push(items[i]);
            }
            menu?.Refresh();
        }
        public bool UndoLast()
        {
            if(undoHistory.Count==0){UpdateStatus("No hay cambios para deshacer");return false;}
            var s=undoHistory.Pop();restoringUndo=true;
            try
            {
                showAllVisuals=s.showAll;activeSystem=s.active;
                Array.Copy(s.systems,visibleSystems,visibleSystems.Length);
                Array.Copy(s.excluded,excludedParts,excludedParts.Length);
                transform.SetPositionAndRotation(s.modelPosition,s.modelRotation);transform.localScale=s.modelScale;
                if(Environment){Environment.SetPassthrough(s.passthrough);Environment.SetEffects(s.effects);Environment.SetSounds(s.sounds);}
                SetBoneTexture(s.bone);if(menu)menu.gameObject.SetActive(s.menuVisible);if(informationPanel)informationPanel.gameObject.SetActive(s.infoVisible);if(avatar)avatar.gameObject.SetActive(s.avatarVisible);
                if(pieces!=null)for(int i=0;i<pieces.Length;i++)
                {
                    if(!pieces[i])continue;
                    if(s.pieces.TryGetValue(i,out var pose))pieces[i].RestorePose(pose.position,pose.rotation,pose.scale,pose.detached);
                    else pieces[i].ResetPiece();
                }
                ApplyBatchState();UpdateAvailability();BeginBuildSystem(ActiveSystemId);
                UpdateStatus("Deshecho: "+s.label);Environment.PlayConfirm();menu.Refresh();
                return true;
            }
            finally{restoringUndo=false;}
        }
        public QuestAtlasPiece EnsureLearningPiece(int index)
        {
            if(index<0||index>=data.parts.Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(pieces[index]==null)pieces[index]=CreatePiece(index);
            pieces[index].SetAppVisible(IsPartVisible(index));
            return pieces[index];
        }
        public int FindPartIndex(string id)=>Array.FindIndex(data.parts,p=>p.id==id);
        public void ShowLearningPart(int index)
        {
            EnsurePartVisible(index);var piece=EnsureLearningPiece(index);
            piece.SetInteractionAvailable(true);informationPanel.ShowPart(data.parts[index], piece.transform);
            selectedName=SpanishName(data.parts[index].name);ApplyBatchState();UpdateStatus();
        }
        public void HideLearningPart(int index)
        {
            excludedParts[index]=true;explicitInteractiveParts.Remove(index);
            ApplyBatchState();UpdateAvailability();
        }
        public void ShowLearningPart(string id)
        {
            int index=FindPartIndex(id);if(index<0)throw new ArgumentException("Pieza desconocida.");
            EnsurePartVisible(index);explicitInteractiveParts.Add(index);EnsureLearningPiece(index);
            ApplyBatchState();UpdateAvailability();
        }
        public void SetLearningInteractive(IEnumerable<int> indices)
        {
            foreach(int index in indices){explicitInteractiveParts.Add(index);EnsureLearningPiece(index);}
            UpdateAvailability();
        }
    }

    public sealed class QuestAtlasLearning : MonoBehaviour
    {
        enum LearningMode{Idle,Lesson,Exam,Dissection,TimeAttack,Detective,Survival}
        QuestAtlasController owner; QuestAtlasInformationPanel info; QuestAtlasVoiceContext context; Transform head;
        QuestAtlasGameConfiguration gameSettings;
        readonly List<int> lesson=new List<int>(),exam=new List<int>(),removed=new List<int>();
        readonly List<QuestAtlasPiece> highlighted=new List<QuestAtlasPiece>();
        readonly List<GameObject> labels=new List<GameObject>();
        LearningMode mode; int step,examIndex,score,attempts,lives,streak,bestStreak; float highlightUntil,timeRemaining;
        public string Status{get;private set;}="Listo para aprender";
        public string Progress{get;private set;}="Elige Tutor, Examen o Disección";
        public bool ExamActive=>mode==LearningMode.Exam;
        public bool DissectionActive=>mode==LearningMode.Dissection;
        public bool GameActive=>mode==LearningMode.TimeAttack||mode==LearningMode.Detective||mode==LearningMode.Survival;

        public void PauseForPsychology()
        {
            CancelInvoke();
            mode = LearningMode.Idle;
            ClearNarration();
            Status = "Neuropsicología · misiones";
            Progress = "Selecciona una misión en el panel de psicología";
            owner.RefreshLearningUI();
        }

public void Initialize(QuestAtlasController atlas,QuestAtlasInformationPanel panel,QuestAtlasVoiceContext sceneContext)
        {
            owner=atlas;info=panel;context=sceneContext;
            head=Camera.main.transform;
            gameSettings=FindFirstObjectByType<QuestAtlasGameConfiguration>();
            if(gameSettings==null)throw new InvalidOperationException("Falta JUEGOS · Configuración editable en la escena.");
        }

        public string ExecuteCommand(string operation,string[] systems,string[] parts)
        {
            if(operation=="lesson_start")return StartLesson(systems,parts);
            if(operation=="exam_start")return StartExam(systems,parts);
            throw new ArgumentException("Modo de aprendizaje inválido.");
        }

        public string StartTimeAttack()
        {
            QuestAtlasTransitions.Current.Pulse();
            PrepareGamePool(gameSettings.contrarrelojPreguntas);
            mode=LearningMode.TimeAttack;examIndex=score=attempts=streak=bestStreak=0;timeRemaining=gameSettings.duracionContrarreloj;
            owner.FocusAnatomy(SystemsFor(exam),Array.Empty<string>(),"only");owner.SetLearningInteractive(exam);
            owner.OpenMenuPage(3);return PresentGameQuestion();
        }

        public string StartDetective()
        {
            QuestAtlasTransitions.Current.Pulse();
            PrepareGamePool(gameSettings.detectivePreguntas);
            mode=LearningMode.Detective;examIndex=score=attempts=streak=bestStreak=0;timeRemaining=0f;
            owner.FocusAnatomy(SystemsFor(exam),Array.Empty<string>(),"only");owner.SetLearningInteractive(exam);
            owner.OpenMenuPage(3);return PresentGameQuestion();
        }

        public string StartSurvival()
        {
            QuestAtlasTransitions.Current.Pulse();
            PrepareGamePool(gameSettings.supervivenciaPreguntas);
            mode=LearningMode.Survival;examIndex=score=attempts=streak=bestStreak=0;lives=gameSettings.vidasSupervivencia;timeRemaining=0f;
            owner.FocusAnatomy(SystemsFor(exam),Array.Empty<string>(),"only");owner.SetLearningInteractive(exam);
            owner.OpenMenuPage(3);return PresentGameQuestion();
        }

void PrepareGamePool(int limit)
        {
            exam.Clear();
            var representatives=new Dictionary<string,int>();
            for(int i=0;i<owner.data.parts.Length;i++)
            {
                string kind=QuestAtlasInformation.Get(owner.data.parts[i].name).id;
                if(!representatives.TryGetValue(kind,out int current)||
                    AtlasSpanish.Name(owner.data.parts[i].name).Length<AtlasSpanish.Name(owner.data.parts[current].name).Length)
                    representatives[kind]=i;
            }
            var candidates=new List<int>(representatives.Values);
            var random=new System.Random(AtlasWebProgress.GetInt("AtlasGameSession",0)+71);
            for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);int x=candidates[i];candidates[i]=candidates[j];candidates[j]=x;}
            for(int i=0;i<candidates.Count&&exam.Count<limit;i++)exam.Add(candidates[i]);
            AtlasWebProgress.SetInt("AtlasGameSession",AtlasWebProgress.GetInt("AtlasGameSession",0)+1);
        }

        string PresentGameQuestion()
        {
            if(examIndex>=exam.Count)return FinishGame("Reto completado");
            var part=owner.data.parts[exam[examIndex]];
            var definition=QuestAtlasInformation.Get(part.name);
            Status=mode==LearningMode.Detective
                ?"DETECTIVE · "+definition.description
                :(mode==LearningMode.TimeAttack?"CONTRARRELOJ":"SUPERVIVENCIA")+" · Busca: "+AtlasSpanish.Name(part.name);
            UpdateGameProgress();ClearNarration();owner.RefreshLearningUI();
            return mode==LearningMode.Detective?"Descubre la estructura: "+definition.description:"Busca "+AtlasSpanish.Name(part.name)+".";
        }

        void UpdateGameProgress()
        {
            if(mode==LearningMode.TimeAttack)Progress="Puntos "+score+" · tiempo "+Mathf.CeilToInt(timeRemaining)+" s · racha "+streak;
            else if(mode==LearningMode.Survival)Progress="Puntos "+score+" · vidas "+lives+" · mejor racha "+bestStreak;
            else Progress="Pistas resueltas "+score+" / "+exam.Count+" · intentos "+attempts;
        }

        string FinishGame(string reason)
        {
            if(examIndex>=exam.Count)AtlasWebProgress.Complete("game."+mode.ToString().ToLowerInvariant(),new JObject{["points"]=score,["attempts"]=attempts});
            string key="AtlasGameBest."+mode;
            int best=Mathf.Max(AtlasWebProgress.GetInt(key,0),score);AtlasWebProgress.SetInt(key,best);AtlasWebProgress.Save();
            Status=reason.ToUpperInvariant();Progress="Resultado "+score+" · récord "+best+" · mejor racha "+bestStreak;
            mode=LearningMode.Idle;owner.RefreshLearningUI();ClearNarration();
            return reason+". Obtuviste "+score+" puntos.";
        }

        public string StopGame()=>GameActive?FinishGame("Juego terminado"):"No hay un juego activo.";

        public string StartLesson(string[] systems,string[] partIds)
        {
            QuestAtlasTransitions.Current.Pulse();
            lesson.Clear();AddParts(lesson,partIds);
            if(lesson.Count==0)AddSystems(lesson,systems,8);
            if(lesson.Count==0)AddSystems(lesson,new[]{"skeletal"},8);
            mode=LearningMode.Lesson;step=0;
            var targetSystems=SystemsFor(lesson);
            owner.FocusAnatomy(targetSystems,Array.Empty<string>(),"only");owner.SetLearningInteractive(lesson);
            return PresentLesson();
        }
        public string NextLesson(){if(mode!=LearningMode.Lesson)return "Primero inicia una lección.";step=(step+1)%lesson.Count;return PresentLesson();}
        public string PreviousLesson(){if(mode!=LearningMode.Lesson)return "Primero inicia una lección.";step=(step-1+lesson.Count)%lesson.Count;return PresentLesson();}
        string PresentLesson()
        {
            int index=lesson[step];owner.ShowLearningPart(index);
            var part=owner.data.parts[index];var definition=QuestAtlasInformation.Get(part.name);
            string text=AtlasSpanish.Name(part.name)+". "+definition.description+" "+definition.function;
            Status="TUTOR · "+AtlasSpanish.Name(part.name);Progress="Paso "+(step+1)+" de "+lesson.Count;owner.RefreshLearningUI();
            ShowNarration(new[]{part.id},text,12f);owner.OpenMenuPage(3);return text;
        }

        public string StartExam(string[] systems,string[] partIds)
        {
            QuestAtlasTransitions.Current.Pulse();
            exam.Clear();AddParts(exam,partIds);if(exam.Count==0)AddSystems(exam,systems,10);
            if(exam.Count<2)AddSystems(exam,new[]{"skeletal"},10);
            var random=new System.Random(AtlasWebProgress.GetInt("AtlasExamSession",0)+17);
            for(int i=exam.Count-1;i>0;i--){int j=random.Next(i+1);int x=exam[i];exam[i]=exam[j];exam[j]=x;}
            AtlasWebProgress.SetInt("AtlasExamSession",AtlasWebProgress.GetInt("AtlasExamSession",0)+1);
            mode=LearningMode.Exam;examIndex=score=attempts=0;
            owner.FocusAnatomy(SystemsFor(exam),Array.Empty<string>(),"only");owner.SetLearningInteractive(exam);
            owner.OpenMenuPage(3);return PresentQuestion();
        }
        string PresentQuestion()
        {
            if(examIndex>=exam.Count)return FinishExam();
            string name=AtlasSpanish.Name(owner.data.parts[exam[examIndex]].name);
            Status="EXAMEN · Busca y selecciona: "+name;Progress="Aciertos "+score+" / "+exam.Count;owner.RefreshLearningUI();
            ClearNarration();return "Busca y selecciona "+name+".";
        }
        public string Hint()
        {
            if(mode!=LearningMode.Exam||examIndex>=exam.Count)return "No hay un examen activo.";
            var part=owner.data.parts[exam[examIndex]];var d=QuestAtlasInformation.Get(part.name);
            attempts++;Status="PISTA · "+d.description;owner.RefreshLearningUI();return "Pista: "+d.description;
        }
        public string StopExam()=>mode==LearningMode.Exam?FinishExam():"No hay un examen activo.";
        string FinishExam()
        {
            string system=exam.Count>0?owner.data.parts[exam[0]].system:"general";
            int best=Mathf.Max(AtlasWebProgress.GetInt("AtlasExamBest."+system,0),score);
            AtlasWebProgress.SetInt("AtlasExamBest."+system,best);AtlasWebProgress.Save();
            if(examIndex>=exam.Count)AtlasWebProgress.Complete("anatomy.exam",new JObject{["score_percent"]=exam.Count>0?100f*score/exam.Count:0,["attempts"]=attempts});
            mode=LearningMode.Idle;Status="EXAMEN COMPLETADO";Progress="Resultado "+score+" / "+exam.Count+" · mejor "+best;owner.RefreshLearningUI();
            return "Examen terminado. Lograste "+score+" de "+exam.Count+".";
        }

        public string ToggleDissection()
        {
            QuestAtlasTransitions.Current.Pulse();
            mode=mode==LearningMode.Dissection?LearningMode.Idle:LearningMode.Dissection;
            Status=mode==LearningMode.Dissection?"DISECCIÓN · selecciona una pieza para retirarla":"Disección detenida";
            Progress=removed.Count+" capas retiradas";owner.OpenMenuPage(3);return Status;
        }
        public string DissectPointed()
        {
            var snapshot=context.Capture();var group=(JArray)snapshot["pointed"];
            if(group.Count!=1)return "Señala una sola pieza hasta que se ilumine.";
            int index=owner.FindPartIndex(group[0].Value<string>("id"));return Remove(index);
        }
        string Remove(int index)
        {
            if(index<0||!owner.IsPartVisible(index))return "Esa pieza ya no está visible.";
            owner.RecordUndo("retirar "+AtlasSpanish.Name(owner.data.parts[index].name));owner.HideLearningPart(index);
            removed.Add(index);Status="CAPA RETIRADA · "+AtlasSpanish.Name(owner.data.parts[index].name);Progress=removed.Count+" capas retiradas";owner.RefreshLearningUI();
            return "Retiré "+AtlasSpanish.Name(owner.data.parts[index].name)+".";
        }
        public string RestoreLayer()
        {
            if(removed.Count==0)return "No hay capas retiradas.";
            int index=removed[removed.Count-1];removed.RemoveAt(removed.Count-1);
            owner.RecordUndo("recuperar capa");owner.ShowLearningPart(owner.data.parts[index].id);
            Status="CAPA RECUPERADA · "+AtlasSpanish.Name(owner.data.parts[index].name);Progress=removed.Count+" capas retiradas";owner.RefreshLearningUI();
            return "Recuperé "+AtlasSpanish.Name(owner.data.parts[index].name)+".";
        }
        public string Rebuild()
        {
            if(removed.Count==0)return "El modelo ya está reconstruido.";
            owner.RecordUndo("reconstruir disección");
            foreach(int index in removed)owner.ShowLearningPart(owner.data.parts[index].id);
            removed.Clear();Status="MODELO RECONSTRUIDO";Progress="0 capas retiradas";owner.RefreshLearningUI();return "Reconstruí las capas retiradas.";
        }
public bool HandlePieceSelected(int index)
        {
            if(mode==LearningMode.Dissection){Remove(index);return true;}
            bool selectionGame=mode==LearningMode.Exam||mode==LearningMode.TimeAttack||mode==LearningMode.Detective||mode==LearningMode.Survival;
            if(!selectionGame||examIndex>=exam.Count)return selectionGame;
            attempts++;
            if(index==exam[examIndex])
            {
                score++;streak++;bestStreak=Mathf.Max(bestStreak,streak);
                if(mode==LearningMode.TimeAttack)timeRemaining=Mathf.Min(gameSettings.duracionContrarreloj,timeRemaining+gameSettings.premioAcierto);
                Status="CORRECTO · "+AtlasSpanish.Name(owner.data.parts[index].name);
                ShowNarration(new[]{owner.data.parts[index].id},"Correcto",1.1f);examIndex++;
                Invoke(mode==LearningMode.Exam?nameof(AdvanceExam):nameof(AdvanceGame),1.15f);
            }
            else
            {
                streak=0;Status="NO ES "+AtlasSpanish.Name(owner.data.parts[index].name).ToUpperInvariant()+" · INTENTA OTRA VEZ";
                if(mode==LearningMode.TimeAttack)timeRemaining=Mathf.Max(0,timeRemaining-gameSettings.penalizacionError);
                if(mode==LearningMode.Survival&&--lives<=0){FinishGame("Sin vidas");return true;}
                if(mode==LearningMode.Exam&&attempts%2==0)Hint();
            }
            if(mode==LearningMode.Exam)Progress="Aciertos "+score+" / "+exam.Count;else UpdateGameProgress();
            owner.RefreshLearningUI();return true;
        }
        void AdvanceExam(){if(mode==LearningMode.Exam)PresentQuestion();}

        void AdvanceGame(){if(GameActive)PresentGameQuestion();}

        public void ShowNarration(string[] ids,string text,float seconds=8f)
        {
            ClearNarration();highlightUntil=Time.unscaledTime+seconds;
            for(int n=0;n<ids.Length&&n<3;n++)
            {
                int index=owner.FindPartIndex(ids[n]);if(index<0||!owner.IsPartVisible(index))continue;
                var piece=owner.EnsureLearningPiece(index);piece.SetNarrationHighlight(true);highlighted.Add(piece);
                labels.Add(CreateLabel(piece,AtlasSpanish.Name(owner.data.parts[index].name),n));
            }
        }
GameObject CreateLabel(QuestAtlasPiece piece,string value,int order)
        {
            var root=new GameObject("Etiqueta holográfica fija · "+value,typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=30;canvas.worldCamera=Camera.main;
            var panel=new GameObject("Cristal",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            panel.GetComponent<RectTransform>().sizeDelta=new Vector2(380,92);panel.GetComponent<Image>().color=new Color(.004f,.075f,.105f,.82f);
            var glow=new GameObject("Borde luminoso",typeof(RectTransform),typeof(Image));glow.transform.SetParent(root.transform,false);
            glow.GetComponent<RectTransform>().sizeDelta=new Vector2(392,104);glow.transform.SetAsFirstSibling();glow.GetComponent<Image>().color=new Color(.08f,.74f,.9f,.18f);
            var accentGo=new GameObject("Pulso lateral",typeof(RectTransform),typeof(Image));accentGo.transform.SetParent(root.transform,false);
            var accentRect=accentGo.GetComponent<RectTransform>();accentRect.sizeDelta=new Vector2(7,92);accentRect.anchoredPosition=new Vector2(-186,0);
            Color systemColor=AtlasSystems.ColorFor(owner.data.parts[piece.Index].system);accentGo.GetComponent<Image>().color=Color.Lerp(systemColor,new Color(.2f,1f,1f),.55f);
            var textGo=new GameObject("Nombre",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(panel.transform,false);
            var t=textGo.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=28;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.55f,.98f,1);t.text=value;t.raycastTarget=false;
            t.rectTransform.sizeDelta=new Vector2(350,76);
            Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            root.transform.localScale=Vector3.one*.0012f;
            root.transform.position=head.position+forward*1.08f+right*(.30f+order*.28f)+Vector3.up*(.24f-order*.13f);
            root.transform.rotation=Quaternion.LookRotation(root.transform.position-head.position,Vector3.up);
            var pointer=root.AddComponent<AtlasDottedPointer>();pointer.Initialize(root.transform,head,Vector3.zero);pointer.SetTarget(piece.transform,systemColor);
            root.AddComponent<AtlasLearningLabel>().Initialize(head,root.GetComponent<CanvasGroup>(),order);return root;
        }
        public void ClearNarration()
        {
            foreach(var p in highlighted)if(p)p.SetNarrationHighlight(false);highlighted.Clear();
            foreach(var l in labels)if(l)Destroy(l);labels.Clear();
        }
void Update()
        {
            if(labels.Count>0&&!owner.Voice.Speaking&&Time.unscaledTime>highlightUntil)ClearNarration();
            if(mode==LearningMode.TimeAttack)
            {
                timeRemaining=Mathf.Max(0,timeRemaining-Time.unscaledDeltaTime);
                if(timeRemaining<=0){FinishGame("Tiempo terminado");return;}
                if(Time.frameCount%15==0){UpdateGameProgress();owner.RefreshLearningUI();}
            }
        }
        void AddParts(List<int> list,string[] ids){foreach(string id in ids){int i=owner.FindPartIndex(id);if(i>=0&&!list.Contains(i))list.Add(i);}}
        void AddSystems(List<int> list,string[] systems,int limit)
        {
            foreach(string system in systems)for(int i=0;i<owner.data.parts.Length&&list.Count<limit;i++)
                if(owner.data.parts[i].system==system&&!list.Contains(i))list.Add(i);
        }
        string[] SystemsFor(List<int> list)
        {
            var systems=new List<string>();foreach(int i in list)if(!systems.Contains(owner.data.parts[i].system))systems.Add(owner.data.parts[i].system);
            return systems.ToArray();
        }
        void OnDestroy(){ClearNarration();}
    }

    public sealed class AtlasLearningLabel:MonoBehaviour
    {
        Transform head;CanvasGroup group;float phase;
public void Initialize(Transform viewer,CanvasGroup canvasGroup,int order)
        {
            head=viewer;group=canvasGroup;phase=order*1.7f;
        }
void LateUpdate()
        {
            if(!head)return;
            transform.rotation=Quaternion.LookRotation(transform.position-head.position,Vector3.up);
            if(group)group.alpha=.90f+Mathf.Sin(Time.unscaledTime*2.8f+phase)*.06f;
        }



    }
}
