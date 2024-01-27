using Score;
using Sisus.Init;
using UnityEngine;

namespace Upgrade
{
    public class UpgradesManager : MonoBehaviour<ScoreController>
    {
        [SerializeField] private int pointsToAdd;

        private UpgradeTrait trait;
        private ScoreController scoreController;
    
        protected override void Init(ScoreController scoreController)
        {
            this.scoreController = scoreController;
        }
    
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                scoreController.AddScorePoints(pointsToAdd);
            }
        }

        public void Upgrade(UpgradeTrait traitToAssign)
        {
            trait = traitToAssign;

            switch (trait.upgrades)
            {
                case Upgrades.POINTSMODIFIER:
                    ModifierUp(trait.pointsModifier);
                    break;
                case Upgrades.LAVAWAVE:
                    UpgradeLavaWave();
                    break;
                default:
                    break;
            }

            scoreController.DeductScorePoints(trait.cost);
        }

        private void ModifierUp(int traitModifier)
        {
            scoreController.ScoreModifier = traitModifier;
        }
        private void UpgradeLavaWave()
        {
            Debug.Log("Lava wave skill unlocked!");
        }
    }
}
