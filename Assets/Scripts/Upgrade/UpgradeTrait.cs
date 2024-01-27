using Microsoft.Unity.VisualStudio.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Upgrades { POINTSMODIFIER, LAVAWAVE}

[CreateAssetMenu(fileName = "New Buildings", menuName = "Building")]
public class UpgradeTrait : ScriptableObject
{
    public string name;
    public string description;
    public Sprite icon;
    public int cost;
    public int costToNextUpgrade;
    public int levelUpgrade;
    public int pointsModifier;
    public Upgrades upgrades;
}
