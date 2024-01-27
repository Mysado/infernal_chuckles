using UnityEngine;

public class EnemyMovementController : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private int hp;
    
    private Transform player;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position,player.position, speed * Time.deltaTime);
    }

    public void TakeDamage()
    {
        hp--;
        if(hp <= 0)
            Destroy(this.gameObject);
    }
}
