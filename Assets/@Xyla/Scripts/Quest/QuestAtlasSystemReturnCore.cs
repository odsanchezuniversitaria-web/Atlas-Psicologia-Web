using UnityEngine;

namespace Xyla
{
    // Invisible physical core for one anatomical system. It ignores rays and
    // accepts only detached pieces that belong to its own system.
    public sealed class QuestAtlasSystemReturnCore : MonoBehaviour
    {
        string systemId;

        public void Initialize(string id)
        {
            systemId = id;
            gameObject.layer = 2; // Ignore Raycast
        }

        void OnTriggerEnter(Collider other)
        {
            QuestAtlasPiece piece = other.GetComponentInParent<QuestAtlasPiece>();
            if (piece == null || piece.SystemId != systemId || !piece.Detached) return;
            piece.RequestReturnToOrigin();
        }
    }
}