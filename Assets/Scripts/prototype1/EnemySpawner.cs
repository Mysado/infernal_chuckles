using System.Collections.Generic;
using Entity;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private float spawnInterval;
    [SerializeField] private float spawnBoost;
    [SerializeField] private int spawnBoostThreshold;
    [SerializeField] private float spawnRange;
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
            Instantiate(enemy, point.position + new Vector3(Random.Range(spawnRange, -spawnRange), 0, 0), Quaternion.identity);
            spawnCounter++;
        }

        timer += Time.deltaTime;
    }
}
