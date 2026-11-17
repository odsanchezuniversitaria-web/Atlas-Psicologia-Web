using System;
using System.Collections.Generic;
using UnityEngine;
namespace Xyla {
public sealed class QuestAtlasClinicalPresentation : MonoBehaviour {
 [Header("Paneles exclusivos de anatomía · se ocultan en psicología")]
 public GameObject[] anatomyOnlyPanels=Array.Empty<GameObject>();
 readonly Dictionary<GameObject,bool> previous=new Dictionary<GameObject,bool>();
 public bool IsActive {get;private set;}
 public void Enter(params GameObject[] corePanels) {
  foreach(var panel in corePanels)Capture(panel);
  foreach(var panel in anatomyOnlyPanels)Capture(panel);
  IsActive=true;HideCaptured();
 }
 void Capture(GameObject panel) {
  if(!panel)throw new InvalidOperationException("Hay una referencia vacía entre los paneles de anatomía.");
  if(!previous.ContainsKey(panel))previous.Add(panel,panel.activeSelf);
 }
 void HideCaptured(){foreach(var item in previous)if(item.Key && item.Key.activeSelf)item.Key.SetActive(false);}
 void LateUpdate(){if(IsActive)HideCaptured();}
 public void Exit() {
  if(!IsActive)return;IsActive=false;
  foreach(var item in previous)if(item.Key)item.Key.SetActive(item.Value);
  previous.Clear();
 }
 void OnDisable()=>Exit();
}
}