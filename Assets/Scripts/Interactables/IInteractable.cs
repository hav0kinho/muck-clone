using UnityEngine;

namespace MuckClone.Interactables
{
    /// <summary>
    /// Interface para qualquer objeto interagível no mundo (Baús, Forjas, Bancadas, Portais).
    /// </summary>
    public interface IInteractable
    {
        string GetInteractionPrompt();
        void Interact(GameObject player);
    }
}
