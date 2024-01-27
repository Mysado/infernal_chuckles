namespace Entity
{
    using System;
    using UnityEngine;

    public class ComfortZoneController : MonoBehaviour
    {
        public event Action<Collider> OnTriggerEntered;
        public event Action<Collider> OnTriggerExited;
        
        private void OnTriggerEnter(Collider other)
        {
            OnTriggerEntered?.Invoke(other);
        }
        
        private void OnTriggerExit(Collider other)
        {
            OnTriggerExited?.Invoke(other);
        }
    }
}