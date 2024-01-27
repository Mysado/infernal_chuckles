namespace Entity
{
    using System.Collections.Generic;
    using UnityEngine;

    public class EnemyController : EntityController
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private int hp;
        [SerializeField] private GameObject shield;
        [SerializeField] private List<float> shieldHeights;
        [SerializeField] private float shieldOffsetX;
        
        private Transform target;

        protected void Awake()
        {
            target = FindObjectOfType<PlayerController2>()?.transform; // ( ͡° ͜ʖ ͡°) shhhh
            initialized = true;
        }

        public void Initialize(bool shielded)
        {
            if (shielded)
            {
                /*shield.SetActive(true);

                var direction = target.position.x - transform.position.x;

                shield.transform.localPosition = new Vector3(direction * shieldOffsetX,
                    shieldHeights[Random.Range(0, shieldHeights.Count)], 0);*/
            }
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