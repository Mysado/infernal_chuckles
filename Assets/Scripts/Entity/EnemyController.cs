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

        private PlayerController2 target;
        private Transform targetTransform;
        private Rigidbody rigidbody;
        private Collider collider;

        public bool IsDead;

        protected void Awake()
        {
            target = FindObjectOfType<PlayerController2>(); // ( ͡° ͜ʖ ͡°) shhhh
            targetTransform = target.transform;
            initialized = true;
            rigidbody = GetComponentInChildren<Rigidbody>();
            collider = GetComponentInChildren<Collider>();

        }

        public void Initialize(bool shielded)
        {
            transform.rotation = Quaternion.Euler(0, transform.position.x > targetTransform.position.x ? 0 : 180, 0);
            
            if (!shielded) 
                return;
           
            var shieldTypes = Enum.GetValues(typeof(ShieldType));
            ShieldType = (ShieldType)shieldTypes.GetValue(Random.Range(1, shieldTypes.Length));
            shields[ShieldType].SetActive(true);
        }
        
        public void TakeDamage(AttackPosition attackPosition)
        {
            if (IsTargetingShield(attackPosition))
                return;
            
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

        private bool IsTargetingShield(AttackPosition attackPosition)
        {
            if (attackPosition == AttackPosition.Head && ShieldType == ShieldType.Head)
                return true;
            if (attackPosition == AttackPosition.Body && ShieldType == ShieldType.Body)
                return true;
            if (attackPosition == AttackPosition.Legs && ShieldType == ShieldType.Legs)
                return true;
            
            return false;
        }
        
        protected override void Move()
        {
            var distance = targetTransform.position.x - transform.position.x;

            if (Mathf.Abs(distance) <= stopDistance)
            {
                target.TakeDamage();
                Destroy(this.gameObject);
            }


            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, speed * Time.deltaTime);
        }
    }
}