using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingType { LAVAPOOL, SATANTHRONE, TORTUREWHEEL, SOULSSUCKER, WHIPPINGTORTURE}

[CreateAssetMenu(fileName = "New Buildings", menuName = "Building")]
public class UpgradeTrait : SerializedScriptableObject
{
    public List<Building> buildings = new List<Building>();
    public BuildingType buildingType;

}

[Serializable]
public class Building
{
    public string name;
    public string description;
    public int cost;
    public int levelUpgrade;
    public int pointsModifier;
    public float fireBallCooldown;
    public float whipCooldown;
    public float breakingLegsCooldown;
    public int breakingLegsDuration;
    public int whipInstaKillValue;
    public int speedReduction;
    public int increaseMaxHP;
    public int level;
}
