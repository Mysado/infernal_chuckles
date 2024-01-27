﻿using DG.Tweening;

namespace Entity
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Shields;
    using UnityEngine;
    using Upgrade;
    using Random = UnityEngine.Random;

    public class EnemyController : EntityController
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private int hp;
        [SerializeField] private Dictionary<ShieldType, GameObject> shields;
        [SerializeField] private UpgradesManager upgradesManager;

        public ShieldType ShieldType{ get; private set; }

        private PlayerController2 target;
        private Transform targetTransform;
        private Rigidbody rigidbody;
        private Collider collider;
        private bool canMove;

        public bool IsDead;

        protected void Awake()
        {
            target = FindObjectOfType<PlayerController2>(); // ( ͡° ͜ʖ ͡°) shhhh
            targetTransform = target.transform;
            initialized = true;
            rigidbody = GetComponentInChildren<Rigidbody>();
            collider = GetComponentInChildren<Collider>();
            StartCoroutine(Initializator());
            upgradesManager = FindAnyObjectByType<UpgradesManager>();
            upgradesManager.FinishStage += KillEmAll;
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
            canMove = false;
            DOTween.Sequence().PrependInterval(1f).AppendCallback(() => canMove = true);
            hp--;
            rigidbody.AddForce(transform.right * 7,ForceMode.Impulse);
            if (hp <= 0)
            {
                IsDead = true;
                collider.enabled = false;
                rigidbody.DOJump(transform.right * 13 - (transform.up * 4), 7, 1, 1.5f);
                transform.DOShakeRotation(1.5f);
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
            if(IsDead || !canMove)
                return;
            
            var distance = targetTransform.position.x - transform.position.x;

            if (Mathf.Abs(distance) <= stopDistance)
            {
                target.TakeDamage();
                Destroy(this.gameObject);
            }
            
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, speed * Time.deltaTime);
        }

        private IEnumerator Initializator()
        {
            yield return new WaitForSeconds(2);
            while (rigidbody.velocity.y < -0.01f)
                yield return new WaitForEndOfFrame();

            canMove = true;
            rigidbody.useGravity = false;
        }

        private void KillEmAll()
        {
            upgradesManager.FinishStage -= KillEmAll;
            IsDead = true;
            Destroy(this.gameObject);
        }
    }
}