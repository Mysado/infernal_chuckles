using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sisus.Init;
using UnityEngine;
using UnityEngine.UI;

public class tutorialContoller : MonoBehaviour
{
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private GameObject firstTutorialPage;
    [SerializeField] private GameObject secondTutorialPage;
    [SerializeField] private Vector3 tutorialGoToPoint = new Vector3(0, -830,0);
    [SerializeField] private Vector3 tutorialHideToPoint = new Vector3(0,0,0);
    [SerializeField] private float tutorialPanelSpeed;

    [SerializeField] private MenuController menuController;
    
    public void StartTutorial()
    {
        LoadFirstTutorialPage();
        ShowTutorialPanel();
    }

    private void ShowTutorialPanel()
    {
        tutorialPanel.SetActive(true);
    }

    private void HideTutorialPanel()
    {
        LoadSecondTutorialPage();
    }

    public void FirstTutorialPageButtonClicked()
    {
        HideTutorialPanel();
    }

    private void LoadFirstTutorialPage()
    {
        firstTutorialPage.SetActive(true);
        secondTutorialPage.SetActive(false);
    }

    private void LoadSecondTutorialPage()
    {
        firstTutorialPage.SetActive(false);
        secondTutorialPage.SetActive(true);
    }

    public void SecondTutorialPageButtonClicked()
    {
        menuController.LoadGame();
    }
    
    
}
