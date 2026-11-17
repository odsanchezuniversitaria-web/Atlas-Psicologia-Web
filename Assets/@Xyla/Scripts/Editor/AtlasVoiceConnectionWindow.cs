using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Xyla.Editor
{
    public sealed class AtlasVoiceConnectionWindow : EditorWindow
    {
        string endpoint = QuestAtlasVoiceAgent.DefaultServerUrl, appId = "", appSecret = "", token = "", message = "";
        bool local;

        [MenuItem("Atlas Humano Quest/Asistente IA/Configurar conexión")]
        public static void Open() { GetWindow<AtlasVoiceConnectionWindow>("Asistente Atlas").minSize = new Vector2(560,410); }

        void OnEnable()
        {
            if(File.Exists(QuestAtlasVoiceAgent.ConnectionPath)) {
                var config=JsonUtility.FromJson<AtlasVoiceConnection>(File.ReadAllText(QuestAtlasVoiceAgent.ConnectionPath));
                endpoint=config.serverUrl;appId=config.appId;appSecret=config.appSecret;
                token=config.deviceToken;local=config.allowLocalHttp;
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Conexión del asistente de anatomía",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("OpenRouter y Deepgram permanecen en Railway. Unity inicia sesión con las credenciales de la aplicación y renueva su JWT automáticamente.",MessageType.Info);
            endpoint=EditorGUILayout.TextField("Dirección de Atlas",endpoint);
            appId=EditorGUILayout.TextField("ID de aplicación",appId);
            appSecret=EditorGUILayout.PasswordField("Secreto de aplicación",appSecret);
            token=EditorGUILayout.PasswordField("Token legado (opcional)",token);
            local=EditorGUILayout.Toggle("Permitir red local de pruebas",local);
            if(GUILayout.Button("Usar backend de Railway")) {
                endpoint=QuestAtlasVoiceAgent.DefaultServerUrl;local=false;
                message="Backend remoto seleccionado. Completa las credenciales y pulsa Guardar.";
            }
            if(GUILayout.Button("Usar vinculación de este PC")) {
                string path=Path.GetFullPath("VoiceServer/device-token.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if(!File.Exists(path)) {
                    var bytes=new byte[48];
                    using(var generator=RandomNumberGenerator.Create())generator.GetBytes(bytes);
                    File.WriteAllText(path,Convert.ToBase64String(bytes));
                }
                token=File.ReadAllText(path).Trim();
                endpoint="http://127.0.0.1:8765";local=true;
                message="Vinculación local preparada. Pulsa Guardar.";
            }
            if(GUILayout.Button("Guardar conexión del Editor")) {
                try {
                    var config=new AtlasVoiceConnection{serverUrl=endpoint.Trim(),appId=appId.Trim(),appSecret=appSecret,
                        deviceToken=token.Trim(),allowLocalHttp=local};
                    QuestAtlasVoiceAgent.ValidateConnection(config);
                    Directory.CreateDirectory(Application.persistentDataPath);
                    File.WriteAllText(QuestAtlasVoiceAgent.ConnectionPath,JsonUtility.ToJson(config,true));
                    message="Conexión guardada fuera de Assets. Entra en Play > Asistente > Activar voz.";
                }catch(Exception ex){message=ex.Message;}
            }
            if(GUILayout.Button("Abrir instrucciones"))EditorUtility.RevealInFinder(Path.GetFullPath("VoiceServer/README.md"));
            EditorGUILayout.HelpBox("Las credenciales se guardan fuera de Assets. Para las gafas usa VoiceServer/Vincular-Railway.ps1.",MessageType.None);
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
        }
    }
}
