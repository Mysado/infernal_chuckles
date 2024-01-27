using System;
using Sisus.Init;
using UnityEngine;

namespace DevilSystem
{
    [Service(typeof(DevilController), FindFromScene = true)]
    public class DevilController : MonoBehaviour
    {
        [SerializeField] private Devil devil;
        public event Action DevilController_StopLaugh;

        private void Awake()
        {
            devil.Devil_StopLaugh += StopLaugh;
        }

        public void UpdateDevilStateDependingOnExperienceFill(float fillPercentage)
        {
            devil.UpdateAnimationBlendShape(fillPercentage);
            devil.LerpPosition(fillPercentage);
            if (fillPercentage >= 1)
            {
                MakeBoom();
                StartLaugh();
            }
        }

        private void MakeBoom()
        {
            //kill all enemies and make them not spawn for duration of laugh
        }

        private void StartLaugh()
        {
            devil.StartLaughAnim();
            //Open Level Up Panel on Anim end
        }

        public void StopLaugh()
        {
            DevilController_StopLaugh?.Invoke();
        }
    }
}
