using Sisus.Init;
using UnityEngine;

namespace DevilSystem
{
    [Service(typeof(DevilController), FindFromScene = true)]
    public class DevilController : MonoBehaviour
    {
        [SerializeField] private Devil devil;

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
        }
    }
}
