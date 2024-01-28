using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.iOS;
using Upgrade;

public class UpgradesRandomizer : MonoBehaviour
{
    [SerializeField] private Transform traitPanel;

    [SerializeField] private List<GameObject> allBuildingsPool;
    [SerializeField] private UpgradesManager upgradeManager;

    private List<GameObject> spawnedTraits;

    private void Start()
    {
        upgradeManager.FinishStage += ShowPanel;
        upgradeManager.StartStage += HidePanel;
        spawnedTraits = new List<GameObject>();
    }

    public void SpawnRandomizedUpgrades()
    {
        RemoveFullUpgradeTrait();

        List<int> removedTraitsFromPool = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            int rand = RandomizeUpgrades(removedTraitsFromPool);
            GameObject traitClone = Instantiate(allBuildingsPool[rand], transform.localPosition, Quaternion.identity);
            traitClone.transform.SetParent(GameObject.FindGameObjectWithTag("UpgradesPanel").transform, false);
            spawnedTraits.Add(traitClone);
        }
        ActiveTraits(spawnedTraits);
    }

    private int RandomizeUpgrades(List<int> spawnedTraits)
    {
        int rand = 0;
        bool traitIsSpawned = false;
        do
        {
            rand = Random.Range(0, allBuildingsPool.Count);

            if (!spawnedTraits.Contains(rand))
            {
                spawnedTraits.Add(rand);
                return rand;
            }

        } while (!traitIsSpawned);

        return rand;
        
    }

    public void ActiveTraits(List<GameObject> spawnedTraits)
    {
        foreach (GameObject trait in spawnedTraits)
        {
            trait.SetActive(true);
            trait.transform.DOScale(1.1f, 0.5f).OnComplete(() => trait.transform.DOScale(1f, 0.5f));
        }
    }
    private void ShowPanel()
    {
        DOTween.Sequence().AppendInterval(3).AppendCallback(() =>
            traitPanel.DOLocalMoveY(0, 1.5f).OnComplete(() => SpawnRandomizedUpgrades()));


    }
    public void HidePanel()
    {
        foreach(GameObject trait in spawnedTraits)
        {
            Destroy(trait);
        }
        spawnedTraits.Clear();
        traitPanel.DOLocalMoveY(819, 1.5f);
    }

    private void RemoveFullUpgradeTrait()
    {
        GameObject trait = allBuildingsPool.FirstOrDefault(x => x.GetComponent<UpgradeTraitDisplay>().level > 3);
        allBuildingsPool.Remove(trait);
    }
}
