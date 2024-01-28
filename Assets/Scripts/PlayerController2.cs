using DG.Tweening;
using Entity;
using Score;
using Sisus.Init;
using System.Linq;
using Sound;
using TMPro;
using UnityEngine;
using Unity.VisualScripting;

public class PlayerController2 : MonoBehaviour<InputManager, ComboController, ScoreController>
{
    [SerializeField] private int maxHealth;
    [SerializeField] private DamageDealer spear;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private GameObject barun;
     
    [SerializeField] private GameObject fireBallPrefab;
    [SerializeField] private Transform barunSpawnPoint;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private CanvasGroup fireWallIcon;
    [SerializeField] private CanvasGroup whipIcon;
    [SerializeField] private CanvasGroup breakLegsIcon;

    private int currentHealth;
    private InputManager inputManager;
    private ComboController comboController;
    private SoundManager soundManager;
    private ScoreController scoreController;

    private bool canFireBall = true;
    private bool canBreakLegs = true;
    private bool canUseWhip = true;

    public float fireBallCooldown;
    public float breakLegsCooldown;
    public float whipCooldown;
    public int instaKillValue;
    public int breakLegsDuration;
    public int MaxHealth { set => maxHealth = value; }

    public bool hasFireBall = false;
    public bool hasWhip = false;
    public bool hasBreakingLegs = false;

    
    protected override void Init(InputManager inputManager, ComboController comboController, ScoreController scoreController)
    {
        this.inputManager = inputManager;
        this.comboController = comboController;
        this.scoreController = scoreController;
    }
    void Start()
    {
        currentHealth = maxHealth;
        inputManager.OnLeftAttack += InputManager_OnLeftAttack;
        inputManager.OnRightAttack += InputManager_OnRightAttack;
        text.text = "HP: " + currentHealth;
        inputManager.CastFireball += InputManager_CastFireBall;
        inputManager.UseWhip += InputManager_UseWhip;
        inputManager.BreakLegs += InputManager_BreakLegs;
        text.text = "HP = " + currentHealth;
        soundManager = FindAnyObjectByType<SoundManager>();
    }

    private void RotateLeft()
    {
        spear.transform.localRotation = new Quaternion(0, 1, 0, 0);
    }
    private void RotateRight()
    {
        spear.transform.localRotation = new Quaternion(0, 0, 0, 1);
    }

    private void Attack(AttackPosition position)
    {
        soundManager.Play(SoundType.PlayerAttack,transform.position,0.8f);
        if (spear.Attack(position))
        {
            comboController.IncreaseComboCounter();
            scoreController.AddScorePoints(1);
        }        
        else
            comboController.ResetComboCounter();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
            TakeDamage();
    }

    public void TakeDamage()
    {
        currentHealth--;
        text.text = "HP: " + currentHealth;
        if (currentHealth <= 0)
        {
            text.text = "GameOver";
            enemySpawner.StopGame();
            var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            for (var i = 0; i < enemies.Length; i++)
                Destroy(enemies[i].gameObject);
            Instantiate(barun, barunSpawnPoint.position, barunSpawnPoint.rotation);
            Destroy(gameObject);
        }
        else
        {
            soundManager.Play(SoundType.PlayerDamaged,transform.position,1.2f);
        }
    }
    
    private void InputManager_OnRightAttack(AttackPosition attackPosition)
    {
        RotateRight();
        Attack(attackPosition);
    }

    private void InputManager_OnLeftAttack(AttackPosition attackPosition)
    {
        RotateLeft();
        Attack(attackPosition);
    }

    private void InputManager_CastFireBall()
    {
        if(hasFireBall)
        {
            if (canFireBall)
            {
                fireWallIcon.alpha = 0.2f;
                Instantiate(fireBallPrefab, transform.position, Quaternion.identity);
                GameObject clone = Instantiate(fireBallPrefab, transform.position, Quaternion.identity);
                clone.transform.Rotate(0, 180, 0);
                canFireBall = false;
                fireWallIcon.DOFade(0.5f, fireBallCooldown).OnComplete(() => fireWallIcon.alpha = 1);
                DOTween.Sequence().AppendInterval(fireBallCooldown).AppendCallback(() => canFireBall = true);
            }
        }
    }
    private void InputManager_UseWhip()
    {
        if (hasWhip)
        {
            if (canUseWhip)
            {
                whipIcon.alpha = 0.2f;
                RaycastHit[] raycastHitsRight;
                RaycastHit[] raycastHitsLeft;
                RaycastHit[] raycastHitsSum;
                raycastHitsRight = Physics.RaycastAll(transform.position, transform.right);
                raycastHitsLeft = Physics.RaycastAll(transform.position, -transform.right);

                raycastHitsSum = raycastHitsRight.Concat(raycastHitsLeft).ToArray();
                System.Array.Sort(raycastHitsSum, (x, y) => x.distance.CompareTo(y.distance));

                for (int i = 0; i < instaKillValue; i++)
                {
                    if (raycastHitsSum[i].collider.CompareTag("Enemy"))
                    {
                        GameObject enemy = raycastHitsSum[i].collider.gameObject;
                        Destroy(enemy);
                        comboController.IncreaseComboCounter();
                        scoreController.AddScorePoints(1);
                    }
                }
                canUseWhip = false;
                whipIcon.DOFade(0.5f, whipCooldown).OnComplete(() => whipIcon.alpha = 1);
                DOTween.Sequence().AppendInterval(whipCooldown).AppendCallback(() => canUseWhip = true);
            }
        }
        
    }
    private void InputManager_BreakLegs()
    {
        if (hasBreakingLegs)
        {
            if (canBreakLegs)
            {
                breakLegsIcon.alpha = 0.2f;
                RaycastHit[] raycastHitsRight;
                RaycastHit[] raycastHitsLeft;
                RaycastHit[] raycastHitsSum;
                raycastHitsRight = Physics.RaycastAll(transform.position, transform.right);
                raycastHitsLeft = Physics.RaycastAll(transform.position, -transform.right);

                raycastHitsSum = raycastHitsRight.Concat(raycastHitsLeft).ToArray();

                foreach (var hit in raycastHitsSum)
                {
                    if (hit.collider.CompareTag("Enemy"))
                    {
                        EnemyController enemy = hit.collider.gameObject.GetComponent<EnemyController>();
                        enemy.BrokenLegs(breakLegsDuration);
                    }
                }
                canBreakLegs = false;
                breakLegsIcon.DOFade(0.5f, breakLegsCooldown).OnComplete(() => breakLegsIcon.alpha = 1);
                DOTween.Sequence().AppendInterval(breakLegsCooldown).AppendCallback(() => canBreakLegs = true);
            }
        }
        
    }

    private void OnDestroy()
    {
        inputManager.OnLeftAttack -= InputManager_OnLeftAttack;
        inputManager.OnRightAttack -= InputManager_OnRightAttack;
    }

}
