using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SoundChecker : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [Button]
    private void PlayAudio()
    {
        audioSource.Play();
    }
}
