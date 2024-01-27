using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace DevilSystem
{
    using Sound;

    public class Devil : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform initialPosition;
        [SerializeField] private Transform endPosition;
        [SerializeField] private float lerpDuration;
        [SerializeField] private SkinnedMeshRenderer renderer;
        
        public event Action Devil_StopLaugh;

        private Sequence devilSequence;

        public void UpdateAnimationBlendShape(float percentage)
        {
            renderer.SetBlendShapeWeight(1,100 - (percentage * 100));
        }

        public void LerpPosition(float percentage)
        {
            var newPosition = Vector3.Lerp(initialPosition.position, endPosition.position, percentage);
            devilSequence.Insert(0,transform.DOMove(newPosition, lerpDuration));
        }

        public void StartLaughAnim()
        {
            animator.SetTrigger("Laugh");
        }

        public void StopLaugh()
        {
            Devil_StopLaugh?.Invoke();
        }
    }
}
