using UnityEngine;
using UnityEngine.UI;
namespace Xyla {
[RequireComponent(typeof(Button))]
public sealed class QuestHapticButton : MonoBehaviour {
 Button button;
 void Awake(){button=GetComponent<Button>();button.onClick.AddListener(Confirm);}
 void Confirm(){QuestAtlasHaptics.Play(AtlasHapticCue.Button);}
 void OnDestroy(){if(button)button.onClick.RemoveListener(Confirm);}
}}
