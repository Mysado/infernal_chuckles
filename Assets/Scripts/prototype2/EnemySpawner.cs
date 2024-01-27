using Entity;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private float spawnInterval;
    [SerializeField] private float spawnRange;
    [SerializeField] private EnemyController enemy;

    private float timer;
    
    private void Update()
    {
        if (timer >= spawnInterval)
        {
            timer = 0;
            Instantiate(enemy, new Vector3(Random.Range(spawnRange, -spawnRange), 1.5f, 0), Quaternion.identity);
        }

        timer += Time.deltaTime;
    }
}
