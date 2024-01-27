using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.iOS;

public class UpgradesRandomizer : MonoBehaviour
{
    [SerializeField] private Transform[] buildingsPanelPosition;
    [SerializeField] private GameObject[] allBuildingsPool;

    private void Start()
    {
        RandomizeUpgrades();
    }

    private void RandomizeUpgrades()
    {
        for (int i = 0; i < buildingsPanelPosition.Length; i++)
        {
            int rand = Random.Range(0, allBuildingsPool.Length);
            GameObject traitClone = Instantiate(allBuildingsPool[rand], buildingsPanelPosition[i].localPosition, Quaternion.identity);
            traitClone.transform.SetParent(GameObject.FindGameObjectWithTag("UpgradesPanel").transform, false);
        }
    }
}
