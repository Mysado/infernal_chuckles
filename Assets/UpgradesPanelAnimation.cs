using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Upgrade;

public class UpgradesPanelAnimation : MonoBehaviour
{
    [SerializeField] private Transform traitPanel;
    [SerializeField] private Transform sparksParticle;
    [SerializeField] private GameObject revealEffect;

    private void Start()
    {
        traitPanel.DOLocalMoveY(0, 1.5f);
        sparksParticle.DOMoveY(0, 1.5f).OnComplete(() => ActiveTraits());

        
    }

    private void ActiveTraits()
    {
        foreach (Transform child in transform)
        {
            GameObject go = child.gameObject;
            go.SetActive(true);
            child.DOScale(1.1f, 0.5f).OnComplete(() => child.DOScale(1f, 0.5f));
            revealEffect.gameObject.SetActive(true);
        }
    }

}
