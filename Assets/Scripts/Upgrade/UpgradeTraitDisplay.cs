using DG.Tweening;
using Score;
using Sisus.Init;
using System.Collections;
using Sound;
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
        private SoundManager soundManager;
    
        protected override void Init(ScoreController scoreController)
        {
            this.scoreController = scoreController;
        }
    
        private void Start()
        {
            upgradesManager = FindFirstObjectByType<UpgradesManager>();
            soundManager = FindAnyObjectByType<SoundManager>();
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
            upgradeButton.onClick.AddListener(() => soundManager.Play(SoundType.ButtonClick,transform.position,1.5f));
        
        }

        public void OnButtonHover()
        {
            soundManager.Play(SoundType.ButtonHover,transform.position,0.65f);
        }


    }
}
