using System.Collections;
using System.Collections.Generic;
using Entity;
using ExperienceSystem;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;
using UnityEngine.UIElements;
using Upgrade;

public class EnemySpawner : MonoBehaviour<ExperienceController>
{
    [SerializeField] private float spawnInterval;
    [SerializeField] private float spawnBoost;
    [SerializeField] private int spawnBoostThreshold;
    [SerializeField] private float spawnRange;
    [SerializeField] private float spawnHeight;
    [SerializeField] private List<Transform> spawnPoints;
    [SerializeField] private EnemyController enemy;
    [SerializeField] private UpgradesManager upgradesManager;
    [SerializeField] private List<Sprite> healths;
    [SerializeField] private ExperienceController experienceController;

    private float timer;
    private int spawnCounter;
    private bool isStoped;

    private void Start()
    {
        upgradesManager.FinishStage += StopGame;
        upgradesManager.StartStage += StartGame;
        timer = spawnInterval;
    }

    private void Update()
    {
        if (!isStoped)
        {
            if (timer >= spawnInterval - (spawnCounter / spawnBoostThreshold) * spawnBoost)
            {
                timer = 0;
                var point = spawnPoints[Random.Range(0, spawnPoints.Count)];
                var newEnemy = Instantiate(enemy, point.position + new Vector3(Random.Range(spawnRange, -spawnRange), 0, 0), Quaternion.identity);
                newEnemy.Initialize(experienceController, true, healths);
                spawnCounter++;
            }

            timer += Time.deltaTime;
        }
    }

    private void StopGame()
    {
        isStoped = true;
    }

    private void StartGame()
    {
        isStoped = false;
    }

    protected override void Init(ExperienceController experienceController)
    {
        this.experienceController = experienceController;
    }
}
