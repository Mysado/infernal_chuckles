using System.Collections.Generic;
using Entity;
using ExperienceSystem;
using Sisus.Init;
using UnityEngine;
using Upgrade;

public class EnemySpawner : MonoBehaviour<ExperienceController>
{
    [SerializeField] private float spawnInterval;
    [SerializeField] private float spawnBoost;
    [SerializeField] private float minSpawnInterval;
    [SerializeField] private int spawnBoostThreshold;
    [SerializeField] private float spawnRange;
    [SerializeField] private float spawnHeight;
    [SerializeField] private List<Transform> spawnPoints;
    [SerializeField] private UpgradesManager upgradesManager;
    [SerializeField] private ExperienceController experienceController;
    [SerializeField] private List<EnemyController> enemies;

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
            if (timer >= Mathf.Max(minSpawnInterval, spawnInterval - (spawnCounter / spawnBoostThreshold) * spawnBoost))
            {
                timer = 0;
                var point = spawnPoints[Random.Range(0, spawnPoints.Count)];
                var newEnemy = Instantiate(GetEnemyType(), point.position + new Vector3(Random.Range(spawnRange, -spawnRange), 0, 0), Quaternion.identity);
                newEnemy.Initialize(experienceController, true);
                spawnCounter++;
            }

            timer += Time.deltaTime;
        }
    }

    private EnemyController GetEnemyType()
    {
        return enemies[Random.Range(0, enemies.Count)];
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
