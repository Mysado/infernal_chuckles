namespace Entity
{
    using UnityEngine;

    public class EnemyController : EntityController
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private float attackDistance;
        
        private Transform target;

        protected override void Awake()
        {
            base.Awake();
            target = FindObjectOfType<PlayerController>()?.transform; // ( ͡° ͜ʖ ͡°)
            initialized = true;
        }
        
        protected override void Move()
        {
            var distance = target.position.x - transform.position.x;
            
            if (Mathf.Abs(distance) <= stopDistance)
                return;
            
            var direction = target.position.x > transform.position.x ? 1 : -1;
            var position = transform.position;
            transform.position = new Vector3(position.x + direction * speed * Time.deltaTime, position.y, position.z);
        }
    }
}