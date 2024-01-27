using System.Collections;
using System.Collections.Generic;
using Entity;
using Sirenix.OdinInspector;
using UnityEngine;

public class EnemySpawner : SerializedMonoBehaviour
{
    [SerializeField] private float spawnInterval;
    [SerializeField] private float spawnBoost;
    [SerializeField] private int spawnBoostThreshold;
    [SerializeField] private float spawnRange;
    [SerializeField] private float spawnHeight;
    [SerializeField] private List<Transform> spawnPoints;
    [SerializeField] private EnemyController enemy;

    private float timer;
    private int spawnCounter;

    private void Start()
    {
        timer = spawnInterval;
    }

    private void Update()
    {
        if (timer >= spawnInterval - (spawnCounter / spawnBoostThreshold) * spawnBoost)
        {
            timer = 0;
            var point = spawnPoints[Random.Range(0, spawnPoints.Count)];
            var newEnemy = Instantiate(enemy, point.position + new Vector3(Random.Range(spawnRange, -spawnRange), 0, 0), Quaternion.identity);
            newEnemy.Initialize(true);
            spawnCounter++;
        }

        timer += Time.deltaTime;
    }
}
