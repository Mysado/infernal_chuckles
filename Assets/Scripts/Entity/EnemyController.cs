namespace Entity
{
    using System;
    using System.Collections.Generic;
    using Shields;
    using UnityEngine;
    using Random = UnityEngine.Random;

    public class EnemyController : EntityController
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private int hp;
        [SerializeField] private Dictionary<ShieldType, GameObject> shields;

        public ShieldType ShieldType{ get; private set; }

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
            transform.rotation = Quaternion.Euler(0, transform.position.x > target.position.x ? 0 : 180, 0);
            
            if (!shielded) 
                return;
           
            var shieldTypes = Enum.GetValues(typeof(ShieldType));
            ShieldType = (ShieldType)shieldTypes.GetValue(Random.Range(1, shieldTypes.Length));
            shields[ShieldType].SetActive(true);
        }
        
        public void TakeDamage(AttackPosition attackPosition)
        {
            hp--;
            rigidbody.AddForce(transform.right * 20,ForceMode.Impulse);
            if (hp <= 0)
            {
                IsDead = true;
                collider.enabled = false;
                rigidbody.AddForce(transform.right * 80,ForceMode.Impulse);
                Destroy(gameObject, 2);
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