using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xyla
{
    public sealed partial class QuestAtlasController
    {
        bool[] excludedParts;
        readonly HashSet<int> explicitInteractiveParts = new HashSet<int>();
        QuestAtlasAvatar avatar;
        Vector3 initialModelPosition, initialModelScale;
        Quaternion initialModelRotation;
        public QuestAtlasEnvironment Environment { get; private set; }

        void InitializeAppControls()
        {
            excludedParts = new bool[data.parts.Length];
            initialModelPosition = transform.position; initialModelRotation = transform.rotation;
            initialModelScale = transform.localScale;
        }
        public bool IsPartVisible(int index) => !excludedParts[index] && IsSystemVisible(Array.IndexOf(AtlasSystems.Ids, data.parts[index].system));
        void ClearPartMask(string system)
        {
            for (int i=0;i<data.parts.Length;i++) if(data.parts[i].system==system) excludedParts[i]=false;
        }
        void EnsurePartVisible(int index)
        {
            int si=Array.IndexOf(AtlasSystems.Ids,data.parts[index].system);
            if(!IsSystemVisible(si))
            {
                for(int i=0;i<data.parts.Length;i++) if(data.parts[i].system==data.parts[index].system) excludedParts[i]=true;
                visibleSystems[si]=true;
            }
            excludedParts[index]=false;
        }
        public void FocusAnatomy(string[] systemIds,string[] partIds,string mode)
        {
            QuestAtlasTransitions.Current.Pulse(true);
            RecordUndo("cambiar la vista anatómica");
            var systems=new HashSet<string>(systemIds); var ids=new HashSet<string>(partIds);
            if(mode!="only" && mode!="show" && mode!="hide") throw new ArgumentException("Modo inválido.");
            foreach(string id in systems) if(Array.IndexOf(AtlasSystems.Ids,id)<0) throw new ArgumentException("Sistema inválido.");
            foreach(string id in ids) if(Array.FindIndex(data.parts,p=>p.id==id)<0) throw new ArgumentException("Pieza inválida.");
            if(showAllVisuals) for(int i=0;i<visibleSystems.Length;i++) visibleSystems[i]=true;
            showAllVisuals=false;
            if(mode=="only") {Array.Clear(visibleSystems,0,visibleSystems.Length);explicitInteractiveParts.Clear();}
            // Showing one piece in a hidden system must not reveal every other piece.
            if(mode=="show")
                for(int si=0;si<visibleSystems.Length;si++)
                    if(!visibleSystems[si])
                        for(int i=0;i<data.parts.Length;i++) if(data.parts[i].system==AtlasSystems.Ids[si]) excludedParts[i]=true;
            int first=-1;
            for(int i=0;i<data.parts.Length;i++)
            {
                bool match=systems.Contains(data.parts[i].system)||ids.Contains(data.parts[i].id);
                int si=Array.IndexOf(AtlasSystems.Ids,data.parts[i].system);
                if(ids.Contains(data.parts[i].id)) {if(mode=="hide") explicitInteractiveParts.Remove(i);else explicitInteractiveParts.Add(i);}
                if(mode=="only") excludedParts[i]=!match;
                else if(match) excludedParts[i]=mode=="hide";
                if(match && mode!="hide") {visibleSystems[si]=true; if(first<0) first=si;}
            }
            // Recompute group flags so menus reflect actual visibility, including partial groups.
            for(int si=0;si<visibleSystems.Length;si++)
            {
                bool any=false;
                for(int i=0;i<data.parts.Length && !any;i++) any=data.parts[i].system==AtlasSystems.Ids[si]&&!excludedParts[i]&&visibleSystems[si];
                visibleSystems[si]=any;
            }
            if(first>=0) activeSystem=first;
            ApplyBatchState(); UpdateAvailability();
            // A focused list (psychology missions, for example) only needs its named pieces.
            // Building the entire nervous system here delayed the first interactive frame.
            bool focusedPiecesOnly = mode == "only" && systems.Count == 0 && ids.Count > 0;
            if (focusedPiecesOnly) { StopAllCoroutines(); building = false; }
            else BeginBuildSystem(ActiveSystemId);
            UpdateStatus();
            // Named pieces outside the active group remain directly grabbable.
            foreach(string id in ids)
            {
                int index=Array.FindIndex(data.parts,p=>p.id==id);
                if(!IsPartVisible(index)) continue;
                if(pieces[index]==null) pieces[index]=CreatePiece(index);
                pieces[index].SetAppVisible(true); pieces[index].SetInteractionAvailable(true);
            }
        }
        public void OpenMenuPage(int page) { ExitClinicalPresentation(); sceneMenu.Hide(); if (psychologyPanel.IsOpen) psychologyPanel.HideForAtlasNavigation(); menu.gameObject.SetActive(true); menu.ShowPage(page); }
        public void RestoreAtlasPanel() { ExitClinicalPresentation(); menu.gameObject.SetActive(true); }
        public void EnterClinicalPresentation() => clinicalPresentation.Enter(menu.gameObject,informationPanel.gameObject,statusText.GetComponentInParent<Canvas>(true).gameObject);
        public void ExitClinicalPresentation() => clinicalPresentation.Exit();
        public void OpenPsychology()
        {
            QuestAtlasTransitions.Current.Pulse();
            EnterClinicalPresentation();
            sceneMenu.Hide();
            menu.gameObject.SetActive(false);
            Environment.SetPassthrough(true);
            psychologyPanel.Open();
        }
        public void OpenPatientSimulation()
        {
            QuestAtlasTransitions.Current.Pulse();
            EnterClinicalPresentation();
            sceneMenu.Hide();
            psychologyPanel.HideForAtlasNavigation();
            menu.gameObject.SetActive(false);
            Environment.SetPassthrough(true);
            patientSimulation.Open();
        }
        public void SetSelectionSounds(bool enabled) {selectionAudio.Muted=!enabled;}
        public void ShowPanels(bool visible) {RecordUndo("cambiar paneles");menu.gameObject.SetActive(visible); informationPanel.gameObject.SetActive(visible);}
        public void ShowInformation(bool visible) {RecordUndo("cambiar ficha");informationPanel.gameObject.SetActive(visible);}
        public void ShowAvatar(bool visible) {RecordUndo("cambiar pantalla de Atlas");avatar.gameObject.SetActive(visible);}
        public void SetBoneTexture(bool enabled) {if(BoneTextureEnabled!=enabled) ToggleBoneTexture();}
        public void RestoreModelTransform()
        {
            RecordUndo("restaurar el modelo");
            transform.SetPositionAndRotation(initialModelPosition,initialModelRotation); transform.localScale=initialModelScale;
        }
        public void ScaleModel(float factor)
        {
            RecordUndo("cambiar tamaño del modelo");
            float current=transform.localScale.x/initialModelScale.x;
            transform.localScale=initialModelScale*Mathf.Clamp(current*factor,.35f,2f);
        }
        public void RotateModel(float degrees) {RecordUndo("girar el modelo");transform.Rotate(0,degrees,0,Space.World);}
        public bool ManipulatePart(string id,string operation,float value)
        {
            RecordUndo("transformar una pieza");
            int index=Array.FindIndex(data.parts,p=>p.id==id);
            if(index<0) throw new ArgumentException("Pieza desconocida.");
            if(pieces[index]==null) pieces[index]=CreatePiece(index);
            var piece=pieces[index];
            if(piece.IsHeld) return false;
            if(operation=="return") piece.ResetPiece();
            else
            {
                EnsurePartVisible(index); Detach(piece);
                if(operation=="scale")
                {
                    float target=Mathf.Clamp(piece.transform.localScale.x*value,escalaMinima,escalaMaxima);
                    piece.transform.localScale=Vector3.one*target;
                }
                else if(operation=="rotate") piece.transform.Rotate(0,value,0,Space.World);
                else throw new ArgumentException("Operación inválida.");
            }
            ApplyBatchState(); UpdateAvailability(); return true;
        }
        public static bool ValidControl(string operation,float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value)) return false;
            if(operation=="voice_volume") return value>=0&&value<=1;
            if(operation=="model_scale") return value>=.25f&&value<=2;
            if(operation=="model_rotate") return Mathf.Abs(value)<=180;
            return value==0 && Array.IndexOf(new[]{"home","systems","assistant","help","environment","panels_on","panels_off","info_on","info_off","avatar_on","avatar_off","reset","next_system","only_active","texture_on","texture_off","passthrough_on","passthrough_off","effects_on","effects_off","sounds_on","sounds_off","answer_next","answer_previous","voice_off","voice_louder","voice_quieter","model_restore","learning","psychology_open","psychology_tutorial","psychology_hint","psychology_next","psychology_close","patient_open","undo","lesson_next","lesson_previous","exam_hint","exam_stop","dissection_toggle","dissect_pointed","restore_layer","rebuild","stop_speaking","repeat_answer"},operation)>=0;
        }
        public void ControlApp(string operation,float value)
        {
            switch(operation)
            {
                case "home": OpenMenuPage(0); break; case "systems": OpenMenuPage(1); break;
                case "assistant": OpenMenuPage(2); break; case "learning": OpenMenuPage(3); break;
                case "psychology_open": OpenPsychology(); break;
                case "psychology_tutorial": if (!psychologyPanel.IsOpen) OpenPsychology(); psychologyPanel.OpenTutorial(); break;
                case "psychology_hint": if (psychologyPanel.IsOpen) psychologyPanel.ShowHint(); break;
                case "psychology_next": if (psychologyPanel.IsOpen) psychologyPanel.NextMission(); break;
                case "psychology_close": if (psychologyPanel.IsOpen) psychologyPanel.Close(); break;
                case "patient_open": OpenPatientSimulation(); break;
                case "help": OpenMenuPage(4); break; case "environment": OpenMenuPage(5); break;
                case "panels_on": ShowPanels(true); break; case "panels_off": ShowPanels(false); break;
                case "info_on": ShowInformation(true); break; case "info_off": ShowInformation(false); break;
                case "avatar_on": ShowAvatar(true); break; case "avatar_off": ShowAvatar(false); break;
                case "reset": ResetAtlas(); break; case "next_system": NextSystem(); break;
                case "only_active": ShowOnlyActiveSystem(); break;
                case "texture_on": SetBoneTexture(true); break; case "texture_off": SetBoneTexture(false); break;
                case "passthrough_on": RecordUndo("activar passthrough"); Environment.SetPassthrough(true); break; case "passthrough_off": RecordUndo("desactivar passthrough"); Environment.SetPassthrough(false); break;
                case "effects_on": RecordUndo("activar efectos"); Environment.SetEffects(true); break; case "effects_off": RecordUndo("desactivar efectos"); Environment.SetEffects(false); break;
                case "sounds_on": RecordUndo("activar sonidos"); Environment.SetSounds(true); break; case "sounds_off": RecordUndo("desactivar sonidos"); Environment.SetSounds(false); break;
                case "answer_next": menu.ChangeAnswerPage(1); break; case "answer_previous": menu.ChangeAnswerPage(-1); break;
                case "voice_off": Voice.MuteAfterReply=true; break;
                case "voice_volume": Voice.SetVoiceVolume(value); break;
                case "voice_louder": Voice.SetVoiceVolume(Voice.VoiceVolume+.15f); break;
                case "voice_quieter": Voice.SetVoiceVolume(Voice.VoiceVolume-.15f); break;
                case "model_scale": ScaleModel(value); break; case "model_rotate": RotateModel(value); break;
                case "model_restore": RestoreModelTransform(); break;
                case "undo": UndoLast(); break;
                case "lesson_next": Learning.NextLesson(); break; case "lesson_previous": Learning.PreviousLesson(); break;
                case "exam_hint": Learning.Hint(); break; case "exam_stop": Learning.StopExam(); break;
                case "dissection_toggle": Learning.ToggleDissection(); break; case "dissect_pointed": Learning.DissectPointed(); break;
                case "restore_layer": Learning.RestoreLayer(); break; case "rebuild": Learning.Rebuild(); break;
                case "stop_speaking": Voice.StopSpeaking(); break; case "repeat_answer": Voice.RepeatAnswer(); break;
                default: throw new ArgumentException("Control no disponible.");
            }
            Environment.PlayConfirm(); menu.Refresh();
        }
    }
}
