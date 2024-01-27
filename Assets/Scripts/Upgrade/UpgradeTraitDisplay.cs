using Score;
using Sisus.Init;
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
        [SerializeField] private TMP_Text levelUpgrade;
        [SerializeField] private Button upgradeButton;

        private ScoreController scoreController;
    
        public UpgradesManager upgradesManager;
    
        protected override void Init(ScoreController scoreController)
        {
            this.scoreController = scoreController;
        }
    
        private void Start()
        {
            upgradesManager = FindFirstObjectByType<UpgradesManager>();
            name.text = buildingTemplate.name;
            description.text = buildingTemplate.description;
            icon.sprite = buildingTemplate.icon;
            cost.text = buildingTemplate.cost.ToString();
            cost.color = Color.red;
            levelUpgrade.text = buildingTemplate.levelUpgrade.ToString();

            if(scoreController.Score >= buildingTemplate.cost)
            {
                cost.color = Color.green;
            }

            upgradeButton.onClick.AddListener(()=>upgradesManager.Upgrade(buildingTemplate));  
        
        }
    }
}
