using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeTraitDisplay : MonoBehaviour
{
    [SerializeField] private UpgradeTrait buildingTemplate;

    [SerializeField] private TMP_Text name;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text cost;
    [SerializeField] private TMP_Text levelUpgrade;
    [SerializeField] private Button upgradeButton;

    public UpgradesManager upgradesManager;
    private void Start()
    {
        upgradesManager = FindFirstObjectByType<UpgradesManager>();
        name.text = buildingTemplate.name;
        description.text = buildingTemplate.description;
        icon.sprite = buildingTemplate.icon;
        cost.text = buildingTemplate.cost.ToString();
        cost.color = Color.red;
        levelUpgrade.text = buildingTemplate.levelUpgrade.ToString();

        if(upgradesManager.points >= buildingTemplate.cost)
        {
            cost.color = Color.green;
        }

        upgradeButton.onClick.AddListener(()=>upgradesManager.Upgrade(buildingTemplate));  
        
    }
}
