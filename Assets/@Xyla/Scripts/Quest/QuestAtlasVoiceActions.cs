using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Xyla
{
    public sealed class QuestAtlasVoiceActions : MonoBehaviour
    {
        QuestAtlasController owner;
        Transform head;
        readonly Dictionary<string,string> completed = new Dictionary<string,string>();
        readonly Queue<string> completionOrder = new Queue<string>();

        public void Initialize(QuestAtlasController atlas)
        {
            owner = atlas;
            head=Camera.main.transform;
        }

        public void Validate(JObject batch)
        {
            if (batch.Value<long?>("expires_at") is not long expires || expires < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                || expires > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 150000)
                throw new InvalidOperationException("La orden de voz ha caducado. Repítela.");
            if (!(batch["actions"] is JArray actions) || actions.Count < 1 || actions.Count > 4)
                throw new InvalidOperationException("La orden de voz no es válida.");
            var ids = new HashSet<string>();
            int bringCount = 0;
            foreach (JObject action in actions)
            {
                string id = action.Value<string>("id");
                if (string.IsNullOrEmpty(id) || !Guid.TryParseExact(id, "N", out _) || !ids.Add(id))
                    throw new InvalidOperationException("Identificador de orden inválido.");
                string type = action.Value<string>("type");
                if (type == "set_system_visibility")
                {
                    if (!(action["system_ids"] is JArray systems) || systems.Count < 1 || systems.Count > AtlasSystems.Ids.Length
                        || action["visible"]?.Type != JTokenType.Boolean)
                        throw new InvalidOperationException("Sistema de la orden inválido.");
                    var distinct = new HashSet<string>();
                    foreach (var system in systems)
                        if (system.Type != JTokenType.String || Array.IndexOf(AtlasSystems.Ids, system.Value<string>()) < 0 || !distinct.Add(system.Value<string>()))
                            throw new InvalidOperationException("Sistema no encontrado en el atlas.");
                }
                else if (type == "bring_part")
                {
                    if (++bringCount > 1 || FindPart(action.Value<string>("part_id")) < 0)
                        throw new InvalidOperationException("Pieza no encontrada o demasiadas piezas.");
                }
                else if(type=="control_app")
                {
                    string op=action.Value<string>("operation"); float value=action.Value<float?>("value")??float.NaN;
                    if(!QuestAtlasController.ValidControl(op,value)) throw new InvalidOperationException("Control inválido.");
                }
                else if(type=="focus_anatomy")
                {
                    var systems=action["system_ids"] as JArray; var parts=action["part_ids"] as JArray;
                    string mode=action.Value<string>("mode");
                    if(systems==null||parts==null||systems.Count>15||parts.Count>owner.data.parts.Length||systems.Count+parts.Count==0
                        ||(mode!="only"&&mode!="show"&&mode!="hide")) throw new InvalidOperationException("Visibilidad inválida.");
                    foreach(var item in systems) if(item.Type!=JTokenType.String||Array.IndexOf(AtlasSystems.Ids,item.Value<string>())<0) throw new InvalidOperationException("Sistema inválido.");
                    foreach(var item in parts) if(item.Type!=JTokenType.String||FindPart(item.Value<string>())<0) throw new InvalidOperationException("Pieza inválida.");
                }
                else if(type=="learning_mode")
                {
                    string op=action.Value<string>("operation");var systems=action["system_ids"] as JArray;var parts=action["part_ids"] as JArray;
                    if((op!="lesson_start"&&op!="exam_start")||systems==null||parts==null||systems.Count>15||parts.Count>owner.data.parts.Length)
                        throw new InvalidOperationException("Aprendizaje inválido.");
                    foreach(var item in systems)if(item.Type!=JTokenType.String||Array.IndexOf(AtlasSystems.Ids,item.Value<string>())<0)throw new InvalidOperationException("Sistema inválido.");
                    foreach(var item in parts)if(item.Type!=JTokenType.String||FindPart(item.Value<string>())<0)throw new InvalidOperationException("Pieza inválida.");
                }
                else if(type=="manipulate_part")
                {
                    string op=action.Value<string>("operation"); float value=action.Value<float?>("value")??float.NaN;
                    if(FindPart(action.Value<string>("part_id"))<0 || float.IsNaN(value)||float.IsInfinity(value)
                        || !(op=="return"&&value==0 || op=="scale"&&value>=.25f&&value<=2 || op=="rotate"&&Mathf.Abs(value)<=180))
                        throw new InvalidOperationException("Transformación inválida.");
                }
                else throw new InvalidOperationException("Esta acción de voz no está disponible.");
            }
        }

        int FindPart(string id)
        {
            return Array.FindIndex(owner.data.parts, part => part.id == id);
        }

        public async Task<JArray> ExecuteAsync(JObject batch, CancellationToken token, Func<bool> allowed)
        {
            Validate(batch); // Validate the entire batch before modifying the scene.
            var receipts = new JArray();
            foreach (JObject action in (JArray)batch["actions"])
            {
                string id = action.Value<string>("id");
                string status;
                if (completed.TryGetValue(id, out status)) { }
                else
                {
                    status = "cancelled";
                    if (allowed() && !token.IsCancellationRequested)
                    {
                        try
                        {
                            if (action.Value<string>("type") == "set_system_visibility")
                            {
                                var systems = action["system_ids"].ToObject<string[]>();
                                owner.SetSystemsFromVoice(systems, action.Value<bool>("visible"));
                                status = "applied";
                            }
                            else if(action.Value<string>("type")=="control_app")
                            {owner.ControlApp(action.Value<string>("operation"),action.Value<float>("value"));status="applied";}
                            else if(action.Value<string>("type")=="focus_anatomy")
                            {owner.FocusAnatomy(action["system_ids"].ToObject<string[]>(),action["part_ids"].ToObject<string[]>(),action.Value<string>("mode"));owner.Environment.PlayConfirm();status="applied";}
                            else if(action.Value<string>("type")=="learning_mode")
                            {owner.Learning.ExecuteCommand(action.Value<string>("operation"),action["system_ids"].ToObject<string[]>(),action["part_ids"].ToObject<string[]>());status="applied";}
                            else if(action.Value<string>("type")=="manipulate_part")
                            {status=owner.ManipulatePart(action.Value<string>("part_id"),action.Value<string>("operation"),action.Value<float>("value"))?"applied":"held";}
                            else status = await BringAsync(action.Value<string>("part_id"), token, allowed);
                        }
                        catch (OperationCanceledException) { status = "cancelled"; }
                        catch (Exception ex) { status = "failed"; Debug.LogWarning("[AtlasVoice] Acción " + action.Value<string>("type") + " fallida: " + ex.GetType().Name + " · " + ex.Message); }
                    }
                    completed[id] = status;
                    completionOrder.Enqueue(id);
                    while (completionOrder.Count > 128) completed.Remove(completionOrder.Dequeue());
                }
                receipts.Add(new JObject { ["id"] = id, ["status"] = status });
            }
            return receipts;
        }

        async Task<string> BringAsync(string id, CancellationToken token, Func<bool> allowed)
        {
            owner.RecordUndo("acercar una pieza");
            var piece = owner.PreparePieceFromVoice(id);
            if (piece.IsHeld) return "held";
            Vector3 start = piece.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            float radius = Vector3.Scale(owner.data.parts[piece.Index].bounds.extents, piece.transform.lossyScale).magnitude;
            // Oversized pieces still need to arrive within comfortable reach.
            if (radius > .28f)
            {
                piece.transform.localScale *= .28f / radius;
                radius = .28f;
            }
            Vector3 target = head.position + forward * Mathf.Max(.48f, radius + .32f) - Vector3.up * .24f;
            target.y = Mathf.Max(target.y, radius + .06f);
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < .65f)
            {
                token.ThrowIfCancellationRequested();
                if (!allowed() || !piece || !piece.Detached) return "cancelled";
                if (piece.IsHeld) return "held";
                float t = Mathf.Clamp01((Time.unscaledTime - started) / .65f);
                piece.transform.position = Vector3.Lerp(start, target, t * t * (3f - 2f * t));
                await Task.Yield();
            }
            if (!allowed() || !piece || !piece.Detached) return "cancelled";
            if (piece.IsHeld) return "held";
            piece.transform.position = target;
            Physics.SyncTransforms();
            Debug.Log("[AtlasVoice] Pieza acercada: " + id + " · distancia " + Vector3.Distance(head.position, target).ToString("F2") + " m");
            owner.PlaySelectionSound();
            return "applied";
        }

        public string Describe(JArray actions, JArray receipts)
        {
            var lines = new List<string>();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = (JObject)actions[i];
                string status = receipts[i].Value<string>("status");
                if (status == "held") { lines.Add("La pieza está en tu mano."); continue; }
                if (status == "cancelled") { lines.Add("Acción cancelada."); continue; }
                if (status != "applied") { lines.Add("No pude completar una de las acciones."); continue; }
                if(action.Value<string>("type")=="control_app" || action.Value<string>("type")=="focus_anatomy" || action.Value<string>("type")=="manipulate_part" || action.Value<string>("type")=="learning_mode")
                {lines.Add(action.Value<string>("confirmation")??"Apliqué tu solicitud.");continue;}
                if (action.Value<string>("type") == "bring_part")
                    lines.Add("Te acerqué " + AtlasSpanish.Name(owner.data.parts[FindPart(action.Value<string>("part_id"))].name) + ".");
                else
                {
                    var names = new List<string>();
                    foreach (var system in (JArray)action["system_ids"]) names.Add(AtlasSystems.Name(system.Value<string>()));
                    lines.Add((action.Value<bool>("visible") ? "Activé " : "Desactivé ") + string.Join(", ", names) + ".");
                }
            }
            return string.Join(" ", lines);
        }
    }
}
