using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Xyla
{
    public sealed class QuestAtlasButton : Button
    {
        public QuestAtlasMenu Menu { private get; set; }
        int pressedPointer = int.MinValue;

        protected override void OnDisable()
        {
            if (Menu != null && pressedPointer != int.MinValue) Menu.ReleasePress(pressedPointer);
            pressedPointer = int.MinValue;
            base.OnDisable();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            base.OnPointerDown(eventData);
            if (Menu.TryPress(eventData.pointerId))
            {
                pressedPointer = eventData.pointerId;
                QuestAtlasHaptics.Play(AtlasHapticCue.Button);
                onClick.Invoke();
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            Menu.ReleasePress(eventData.pointerId);
            pressedPointer = int.MinValue;
        }

        // Selection is acknowledged once on press.
        public override void OnPointerClick(PointerEventData eventData) { }
    }
}
