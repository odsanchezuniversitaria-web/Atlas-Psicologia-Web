using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Xyla {
public sealed class AtlasWebProgress : MonoBehaviour {
 static readonly JObject progress=new JObject {["schemaVersion"]=1,["missions"]=new JArray(),["milestones"]=new JArray(),["reports"]=new JObject(),["flags"]=new JObject(),["poses"]=new JArray()};
 public static JObject Capture(){return (JObject)progress.DeepClone();}
 public static void Restore(JObject value){foreach(var name in new[]{"missions","milestones","reports","flags","poses"})progress[name]=value[name]?.DeepClone()??(name=="reports"||name=="flags"?(JToken)new JObject():new JArray());}
 public static int GetInt(string name,int fallback=0){if(!AtlasWebPlatform.Active)return PlayerPrefs.GetInt(name,fallback);return ((JObject)progress["flags"]).Value<int?>(name)??fallback;}
 public static void SetInt(string name,int value){if(!AtlasWebPlatform.Active){PlayerPrefs.SetInt(name,value);return;}progress["flags"][name]=value;Save();}
 public static void Complete(string id,JObject report=null){
  if(!AtlasWebPlatform.Active)return;var missions=(JArray)progress["missions"];if(!System.Linq.Enumerable.Contains(missions.Values<string>(),id))missions.Add(id);
  if(report!=null)progress["reports"][id]=report.DeepClone();Save();
 }
 public static void Milestone(string id){if(!AtlasWebPlatform.Active)return;var ids=(JArray)progress["milestones"];if(!System.Linq.Enumerable.Contains(ids.Values<string>(),id))ids.Add(id);Save();}
 public static void SetPoses(JArray poses){progress["poses"]=poses;Save();}
 public static void Save(){if(AtlasWebPlatform.Active)AtlasWebPlatform.Save(Capture());else PlayerPrefs.Save();}
 public void RestoreBrowser(string json){var value=JObject.Parse(json);AtlasWebPlatform.Profile=value.Value<string>("profileKey")??"practice";AtlasWebPlatform.Evaluated=value.Value<bool?>("evaluated")??false;Restore(value["checkpoint"] as JObject??new JObject());}
}}
