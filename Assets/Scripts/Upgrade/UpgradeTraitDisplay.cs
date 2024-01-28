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
        [SerializeField] private TMP_Text cost;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text levelText;

        private ScoreController scoreController;
        private SoundManager soundManager;

        public BuildingType buildingTemplateType;
        public UpgradesManager upgradesManager;
        public int level;
    
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
            cost.text = buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].cost.ToString();
            cost.color = Color.red;

            if(scoreController.Score >= buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].cost)
            {
                cost.color = Color.green;
            }

            level = buildingTemplate.buildings[upgradesManager.BuildingUpgrades[buildingTemplateType]].level;
            if(level == 1)
            {
                levelText.text = "I";
            }else if(level == 2)
            {
                levelText.text = "II";
            }
            else
            {
                levelText.text = "III";
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
