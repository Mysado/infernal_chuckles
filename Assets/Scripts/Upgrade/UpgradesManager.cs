using System;
using Score;
using Sisus.Init;
using System.Collections.Generic;
using UnityEngine;

namespace Upgrade
{
    public class UpgradesManager : MonoBehaviour<ScoreController>
    {
        [SerializeField] private UpgradesRandomizer upgradesRandomizer;

        private UpgradeTrait trait;
        private ScoreController scoreController;

        public Action FinishStage;
        public Action StartStage;

        private Dictionary<BuildingType, int> buildingUpgrades = new Dictionary<BuildingType, int>()
        {
            {BuildingType.LAVAPOOL, 0},
            {BuildingType.SATANTHRONE, 0},
            {BuildingType.TORTUREWHEEL, 0},
            {BuildingType.WHIPPINGTORTURE, 0},
            {BuildingType.SOULSSUCKER, 0}
        };

        public Dictionary<BuildingType, int> BuildingUpgrades {  get { return buildingUpgrades; } }

        protected override void Init(ScoreController scoreController)
        {
            this.scoreController = scoreController;
        }
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.R)) 
            {
                StartUpgrade();
            }
        }

        public void StartUpgrade()
        {
            FinishStage?.Invoke();
        }

        public void Upgrade(UpgradeTrait traitToAssign)
        {
            trait = traitToAssign;

            switch (trait.buildingType)
            {
                case BuildingType.LAVAPOOL:
                    ModifierUp(trait.buildings[buildingUpgrades[trait.buildingType]].pointsModifier);
                    break;
                case BuildingType.SATANTHRONE:
                    UpgradeLavaWave();
                    break;
                default:
                    break;
            }

            scoreController.DeductScorePoints(trait.buildings[buildingUpgrades[trait.buildingType]].cost);
            buildingUpgrades[trait.buildingType]++;
            StartStage?.Invoke();
        }

        private void ModifierUp(int traitModifier)
        {
            scoreController.ScoreModifier = traitModifier;
        }
        private void UpgradeLavaWave()
        {
            
        }
    }
}
