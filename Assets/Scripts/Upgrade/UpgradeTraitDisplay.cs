using DG.Tweening;
using Score;
using Sisus.Init;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Upgrade
{
    public class UpgradeTraitDisplay : MonoBehaviour<ScoreController>
    {
        [SerializeField] private UpgradeTrait buildingTemplate;

        [SerializeField] private TMP_Text name;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text cost;
        [SerializeField] private Button upgradeButton;

        private ScoreController scoreController;

        public BuildingType buildingTemplateType;
        public UpgradesManager upgradesManager;
    
        protected override void Init(ScoreController scoreController)
        {
            this.scoreController = scoreController;
        }
    
        private void Start()
        {
            upgradesManager = FindFirstObjectByType<UpgradesManager>();
            buildingTemplateType = buildingTemplate.buildingType;
            name.text = buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].name;
            description.text = buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].description;
            icon.sprite = buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].icon;
            cost.text = buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].cost.ToString();
            cost.color = Color.red;

            if(scoreController.Score >= buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].cost)
            {
                cost.color = Color.green;
            }

            upgradeButton.onClick.AddListener(()=>upgradesManager.Upgrade(buildingTemplate));  
        
        }


    }
}
