using DevilSystem;
using Sisus.Init;
using Sound;

namespace Entity
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using ExperienceSystem;
    using Shields;
    using UnityEngine;
    using Upgrade;
    using Random = UnityEngine.Random;
    using DG.Tweening;
    using UnityEngine.UI;


    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private float stopDistance;
        [SerializeField] private int hp;
        [SerializeField] private List<GameObject> shields;
        [SerializeField] private Transform healthParent;
        [SerializeField] private Sprite healthSprite;
        [SerializeField] protected float speed;
        [SerializeField] protected Animator animator;
        [SerializeField] private GameObject dieParticle;
        [SerializeField] private SoundType deathSoundType;

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
        private SoundManager soundManager;
        private List<Image> healths;
        private Sequence attackSequence;

        public bool IsDead;
        private bool hasBrokenLegs = false;
        
        private static readonly int walk = Animator.StringToHash("Walk");
        private static readonly int attack = Animator.StringToHash("Attack");
        private static readonly int die = Animator.StringToHash("Die");
        private static readonly int knockback = Animator.StringToHash("Knockback");
        
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
            soundManager = FindAnyObjectByType<SoundManager>();
            upgradesManager.FinishStage += KillEmAll;
        }

        public void Initialize(ExperienceController experienceController, bool shielded, int health, int speed)
        {
            hp = Mathf.Min(4,health);
            this.speed = Mathf.Min(10, speed);
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

        public void TakeDamage(AttackPosition attackPosition, bool instaKill = false)
        {
            if(instaKill)
            {
                rigidbody.AddForce(transform.right * Random.Range(5, 10), ForceMode.Impulse);
                canMove = false;
                DOTween.Sequence().PrependInterval(1f).AppendCallback(() =>
                {
                    canMove = true;
                    animator.ResetTrigger(knockback);
                    animator.SetTrigger(walk);
                });

                Destroy(healths[0].gameObject);
                healths.RemoveAt(0);
                hp = 0;
                if (hp <= 0)
                {
                    soundManager.Play(deathSoundType, transform.position, 1.5f, 0.3f);
                    animator.SetTrigger(die);
                    IsDead = true;
                    rigidbody.DOJump(transform.right * 13 - (transform.up * 4), 7, 1, 1.5f)
                        .OnComplete(() => soundManager.Play(SoundType.EnemyDropToLava, transform.position, 0.65f));
                    transform.DOShakeRotation(1.5f);
                    experienceController.AddExperience(2);
                    Destroy(gameObject, 2);
                    return;
                }
                
            }
            if (!IsTargetingShield(attackPosition))
                return;
            
            animator.SetTrigger(knockback);
            attackSequence.Kill();
            attackSequence = null;
            rigidbody.AddForce(transform.right * Random.Range(5,10),ForceMode.Impulse);
            canMove = false;
            DOTween.Sequence().PrependInterval(1f).AppendCallback(() =>
            {
                canMove = true;
                animator.ResetTrigger(knockback);
                animator.SetTrigger(walk);
            });
            
            Destroy(healths[0].gameObject);
            healths.RemoveAt(0);
            hp--;
            if (hp <= 0)
            {
                soundManager.Play(deathSoundType,transform.position,1.5f);
                animator.SetTrigger(die);
                IsDead = true; 
                StartCoroutine(PlayDeathSound());
                rigidbody.DOJump(transform.right * 13 - (transform.up * 4), 7, 1, 1.5f);
                transform.DOShakeRotation(1.5f);
                experienceController.AddExperience(2);
                Destroy(gameObject, 2);
            }
            else
            {
                soundManager.Play(SoundType.EnemyDamaged,transform.position,0.8f);
            }
        }

        private IEnumerator PlayDeathSound()
        {
            yield return new WaitForSeconds(0.8f);
            soundManager.Play(SoundType.EnemyDropToLava, transform.position, 0.65f);
        }

        public void BrokenLegs(int duration)
        {
            if(hasBrokenLegs)
            {
                return;
            }

            speed = speed / 2f;
            DOTween.Sequence().AppendInterval(duration).AppendCallback(() => speed = speed * 2f);
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
            if(IsDead || !canMove || attackSequence != null)
                return;
            
            var distance = targetTransform.position.x - transform.position.x;

            if (Mathf.Abs(distance) <= stopDistance && attackSequence == null)
            {
                animator.SetTrigger(attack);
                attackSequence = DOTween.Sequence();
                attackSequence.AppendInterval(0.6f).AppendCallback(() =>
                {
                    target.TakeDamage();
                    IsDead = true;
                    Destroy(gameObject, 0.1f);
                    Instantiate(dieParticle, transform.position + new Vector3(0, 1, 0), Quaternion.identity);
                });
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
            animator.SetTrigger(walk);
        }

        private void KillEmAll()
        {
            upgradesManager.FinishStage -= KillEmAll;
            IsDead = true;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            upgradesManager.FinishStage -= KillEmAll;
        }
    }
}