using UnityEngine;

public class KegController : MonoBehaviour
{
    [SerializeField] private ParticleSystem fire;
    
    private void OnTriggerEnter(Collider other)
    {
        fire.Play();
    }
}
