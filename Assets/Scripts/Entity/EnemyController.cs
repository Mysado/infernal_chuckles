using DG.Tweening;
using ExperienceSystem;
using UnityEngine.UI;
using Sisus.Init;
using Unity.VisualScripting;

namespace Entity
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using ExperienceSystem;
    using Shields;
    using Sisus.Init;
    using UnityEngine;
    using Upgrade;
    using Random = UnityEngine.Random;

    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private int hp;
        [SerializeField] private List<GameObject> shields;
        [SerializeField] private Transform healthParent;
        [SerializeField] private Sprite healthSprite;
        [SerializeField] protected float speed;

        public ShieldType ShieldType{ get; private set; }
        public bool CanMove => canMove;

        private PlayerController2 target;
        private ExperienceController experienceController;
        private Transform targetTransform;
        private Rigidbody rigidbody;
        private Collider collider;
        private bool canMove;
        private bool initialized;
        private UpgradesManager upgradesManager;
        private List<Image> healths;

        public bool IsDead;
        
        protected void Update()
        {
            if (!initialized)
                return;
        
            Move();
        }
    
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

        public void Initialize(ExperienceController experienceController, bool shielded)
        {
            healths = new List<Image>();
            this.experienceController = experienceController;
            transform.rotation = Quaternion.Euler(0, transform.position.x > targetTransform.position.x ? 0 : 180, 0);
            for (int i = 0; i < hp; i++)
            {
                var sprite = new GameObject();
                var image = sprite.AddComponent<Image>();
                sprite.transform.parent = healthParent;
                sprite.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                image.sprite = healthSprite;
                healths.Add(image);
            }
            if (!shielded) 
                return;
           
            var shieldTypes = Enum.GetValues(typeof(ShieldType));
            ShieldType = (ShieldType)shieldTypes.GetValue(Random.Range(1, shieldTypes.Length));
            shields[(int)ShieldType - 1].SetActive(true);
        }
        
        public void TakeDamage(AttackPosition attackPosition)
        {
            rigidbody.AddForce(transform.right * Random.Range(5,10),ForceMode.Impulse);
            canMove = false;
            DOTween.Sequence().PrependInterval(1f).AppendCallback(() => canMove = true);
            
            if (IsTargetingShield(attackPosition))
                return;
            
            Destroy(healths[0].gameObject);
            healths.RemoveAt(0);
            hp--;
            if (hp <= 0)
            {
                IsDead = true;
                collider.enabled = false;
                rigidbody.DOJump(transform.right * 13 - (transform.up * 4), 7, 1, 1.5f);
                transform.DOShakeRotation(1.5f);
                experienceController.AddExperience(2);
                Destroy(gameObject, 2);
            }
        }
        
        public void KnockBack()
        {
            rigidbody.AddForce(transform.right * Random.Range(5,10),ForceMode.Impulse);
            canMove = false;
            DOTween.Sequence().PrependInterval(1f).AppendCallback(() => canMove = true);
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
        
        private void Move()
        {
            if(IsDead || !canMove)
                return;
            
            var distance = targetTransform.position.x - transform.position.x;

            if (Mathf.Abs(distance) <= stopDistance)
            {
                target.TakeDamage();
                Destroy(gameObject);
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
            Destroy(gameObject);
        }
    }
}