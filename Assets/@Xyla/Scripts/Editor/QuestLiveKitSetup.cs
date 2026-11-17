using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Xyla.Editor
{
    public static class QuestLiveKitSetup
    {
        public static void Ensure()
        {
            foreach (var controller in UnityEngine.Object.FindObjectsByType<QuestAtlasController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!controller.GetComponent<QuestAtlasVoiceAgent>()) controller.gameObject.AddComponent<QuestAtlasVoiceAgent>();
            }
            foreach (var voice in UnityEngine.Object.FindObjectsByType<QuestAtlasVoiceAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var transport = voice.GetComponent<QuestLiveKitVoice>();
                if (!transport) transport = voice.gameObject.AddComponent<QuestLiveKitVoice>();
                Author(transport, "Micrófono Atlas · LiveKit · editable");
                voice.liveKit = transport; EditorUtility.SetDirty(voice);
            }
            foreach (var voice in UnityEngine.Object.FindObjectsByType<QuestPatientVoice>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var transport = voice.GetComponent<QuestLiveKitVoice>();
                if (!transport) transport = voice.gameObject.AddComponent<QuestLiveKitVoice>();
                Author(transport, "Micrófono paciente · LiveKit · editable");
                voice.liveKit = transport; EditorUtility.SetDirty(voice);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        static void Author(QuestLiveKitVoice transport, string name)
        {
            if (!transport.microphoneHost)
            {
                var host = new GameObject(name);
                host.transform.SetParent(transport.transform, false);
                transport.microphoneHost = host;
            }
            EditorUtility.SetDirty(transport);
        }
    }
}
