using System;
using DG.Tweening;
using Sisus.Init;
using UnityEngine;
using Upgrade;
using Random = UnityEngine.Random;

namespace DevilSystem
{
    [Service(typeof(DevilController), FindFromScene = true)]
    public class DevilController : MonoBehaviour
    {
        [SerializeField] private Devil devil;
        public event Action DevilController_ResetDevil;

        [SerializeField] private UpgradesManager upgradesManager;
        private void Awake()
        {
            devil.Devil_StopLaugh += OpenUpgradeMenu;
            upgradesManager.StartStage += ResetDevil;
            RockTheDevil();
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

        private void OpenUpgradeMenu()
        {
            upgradesManager.StartUpgrade();
        }

        private void ResetDevil()
        {
            DevilController_ResetDevil?.Invoke();
        }

        private void RockTheDevil()
        {
            var randomVector3 = new Vector3(Random.Range(-0.8f, 0.8f),Random.Range(-0.03f, 0.03f),Random.Range(-0.8f, 0.8f));
            var randomDuration = Random.Range(10, 15);
            transform.DOPunchPosition(randomVector3, randomDuration, vibrato:0,elasticity:1f).SetLoops(2, LoopType.Yoyo).OnComplete(() => RockTheDevil());
        }
    }
}
