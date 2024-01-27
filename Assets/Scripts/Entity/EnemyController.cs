namespace Entity
{
    using System.Collections.Generic;
    using UnityEngine;

    public class EnemyController : EntityController
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private int hp;
        [SerializeField] private GameObject shield;
        /*[SerializeField] private List<float> shieldHeights;
        [SerializeField] private float shieldOffsetX;*/
        
        private Transform target;
        private Rigidbody rigidbody;
        private Collider collider;

        public bool IsDead;

        protected void Awake()
        {
            target = FindObjectOfType<PlayerController2>()?.transform; // ( ͡° ͜ʖ ͡°) shhhh
            initialized = true;
            rigidbody = GetComponentInChildren<Rigidbody>();
            collider = GetComponentInChildren<Collider>();

        }

        public void Initialize(bool shielded)
        {
            if (shielded)
                shield.SetActive(true);
        }
        
        public void TakeDamage()
        {
            hp--;
            rigidbody.AddForce(transform.right * 20,ForceMode.Impulse);
            if (hp <= 0)
            {
                IsDead = true;
                collider.enabled = false;
                rigidbody.AddForce(transform.right * 20,ForceMode.Impulse);
            }
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