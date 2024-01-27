using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.VFX;

public class UpgradesManager : MonoBehaviour
{
    [SerializeField] private int pointsToAdd;
    [SerializeField] private int pointsModifier;
    

    private UpgradeTrait trait;
    public TMP_Text pointsUI;
    public int points;

    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            AddPoints();
        }
    }

    private void AddPoints()
    {
        points += pointsToAdd * pointsModifier;
        pointsUI.text = points.ToString();
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

        points -= trait.cost;
        pointsUI.text = points.ToString();
    }

    private void ModifierUp(int traitModifier)
    {
        pointsModifier = traitModifier;
    }
    private void UpgradeLavaWave()
    {
        Debug.Log("Lava wave skill unlocked!");
    }

    
}
