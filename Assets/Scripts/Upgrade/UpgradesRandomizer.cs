using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.iOS;

public class UpgradesRandomizer : MonoBehaviour
{
    [SerializeField] private GameObject[] allBuildingsPool;

    private void Start()
    {
        SpawnRandomizedUpgrades();
    }

    private void SpawnRandomizedUpgrades()
    {
        List<int> spawnedTraits = new List<int>();

        for (int i = 0; i < 3; i++)
        {
            int rand = RandomizeUpgrades(spawnedTraits);
            GameObject traitClone = Instantiate(allBuildingsPool[rand], transform.localPosition, Quaternion.identity);
            traitClone.transform.SetParent(GameObject.FindGameObjectWithTag("UpgradesPanel").transform, false);
            traitClone.SetActive(false);
        }
    }

    private int RandomizeUpgrades(List<int> spawnedTraits)
    {
        int rand = 0;
        bool traitIsSpawned = false;
        do
        {
            rand = Random.Range(0, allBuildingsPool.Length);

            if (!spawnedTraits.Contains(rand))
            {
                spawnedTraits.Add(rand);
                return rand;
            }

        } while (!traitIsSpawned);

        return rand;
        
    }
}
