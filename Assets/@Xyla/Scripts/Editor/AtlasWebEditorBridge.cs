using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Xyla.Editor {
[InitializeOnLoad]
public static class AtlasWebEditorBridge {
 static AtlasWebEditorBridge(){EditorApplication.delayCall+=StartBridge;}
 static void StartBridge(){
  if(!Application.dataPath.Replace('\\','/').StartsWith("D:/1a Atlas Psicologia/",StringComparison.OrdinalIgnoreCase))return;
  foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
   var host=assembly.GetType("MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost");
   var ports=assembly.GetType("MCPForUnity.Editor.Helpers.PortManager");
   if(host==null||ports==null)continue;
   bool running=(bool)host.GetProperty("IsRunning").GetValue(null);int port=(int)host.GetMethod("GetCurrentPort").Invoke(null,null);
   if(running&&port==6418)return;
   if(running)host.GetMethod("Stop").Invoke(null,null);
   ports.GetMethod("SetPreferredPort").Invoke(null,new object[]{6418});
   host.GetMethod("Start").Invoke(null,null);return;
  }
 }
}}
