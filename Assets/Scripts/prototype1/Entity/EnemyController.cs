namespace Entity
{
    using UnityEngine;

    public class EnemyController : EntityController
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private int hp;
        
        private Transform target;

        protected void Awake()
        {
            target = FindObjectOfType<PlayerController2>()?.transform; // ( ͡° ͜ʖ ͡°) shhhh
            initialized = true;
        }

        public void Damage()
        {
            Destroy(gameObject);
        }
        
        public void TakeDamage()
        {
            hp--;
            if(hp <= 0)
                Destroy(gameObject);
        }
        
        protected override void Move()
        {
            var distance = target.position.x - transform.position.x;
            
            if (Mathf.Abs(distance) <= stopDistance)
                return;
            
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
        }
    }
}