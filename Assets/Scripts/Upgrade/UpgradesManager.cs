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
        [SerializeField] private PlayerController2 playerController;
        [SerializeField] private CanvasGroup fireWallIcon;
        [SerializeField] private CanvasGroup whipIcon;
        [SerializeField] private CanvasGroup breakLegsIcon;

        private UpgradeTrait trait;
        public ScoreController scoreController;

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
                    UpgradeLavaWave(trait);
                    break;
                case BuildingType.SATANTHRONE:
                    ModifierUp(trait);
                    break;
                case BuildingType.WHIPPINGTORTURE:
                    UpgradeWhip(trait);
                    break;
                case BuildingType.TORTUREWHEEL:
                    UpgradeWheelOfTorture(trait);
                    break;
                case BuildingType.SOULSSUCKER:
                    UpgradeSoulSucker(trait);
                    break;
                default:
                    break;
            }
            playerController.ChangeHP();
            scoreController.DeductScorePoints(trait.buildings[buildingUpgrades[trait.buildingType]].cost);
            buildingUpgrades[trait.buildingType]++;
            StartStage?.Invoke();
        }

        private void ModifierUp(UpgradeTrait trait)
        {
            scoreController.ScoreModifier = trait.buildings[buildingUpgrades[trait.buildingType]].pointsModifier;
        }
        private void UpgradeLavaWave(UpgradeTrait trait)
        {
            playerController.hasFireBall = true;
            playerController.fireBallCooldown = trait.buildings[buildingUpgrades[trait.buildingType]].fireBallCooldown;
            fireWallIcon.alpha = 1;
        }
        private void UpgradeWheelOfTorture(UpgradeTrait trait)
        {
            playerController.hasBreakingLegs = true;
            playerController.breakLegsCooldown = trait.buildings[buildingUpgrades[trait.buildingType]].breakingLegsCooldown;
            breakLegsIcon.alpha = 1;
        }
        private void UpgradeWhip(UpgradeTrait trait)
        {
            playerController.hasWhip = true;
            playerController.whipCooldown = trait.buildings[buildingUpgrades[trait.buildingType]].whipCooldown;
            playerController.instaKillValue = trait.buildings[buildingUpgrades[trait.buildingType]].whipInstaKillValue;
            whipIcon.alpha = 1;
        }
        private void UpgradeSoulSucker(UpgradeTrait trait)
        {
            playerController.hasBreakingLegs = true;
            playerController.MaxHealth = trait.buildings[buildingUpgrades[trait.buildingType]].increaseMaxHP;
        }
    }
}
