using DG.Tweening;
using Entity;
using Score;
using Sisus.Init;
using System.Linq;
using TMPro;
using UnityEngine;

public class PlayerController2 : MonoBehaviour<InputManager, ComboController, ScoreController>
{
    [SerializeField] private int maxHealth;
    [SerializeField] private DamageDealer spear;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private GameObject barun;
    [SerializeField] private float fireBallCooldown;
    [SerializeField] private float breakLegsCooldown;
    [SerializeField] private float whipCooldown;
    [SerializeField] private GameObject fireBallPrefab;
    [SerializeField] private int instaKillValue;
    [SerializeField] private Transform barunSpawnPoint;
    [SerializeField] private EnemySpawner enemySpawner;

    private int currentHealth;
    private InputManager inputManager;
    private ComboController comboController;
    private ScoreController scoreController;
    private Sequence sequence;

    private bool canFireBall = true;
    private bool canBreakLegs = true;
    private bool canUseWhip = true;
    private bool hasFireBall = true;
    private bool hasWhip = true;
    private bool hasBreakingLegs = true;

    
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
    }

    private void Update()
    {
        
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
                Instantiate(fireBallPrefab, transform.position, Quaternion.identity);
                GameObject clone = Instantiate(fireBallPrefab, transform.position, Quaternion.identity);
                clone.transform.Rotate(0, 180, 0);
                canFireBall = false;
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
                        enemy.BrokenLegs();
                    }
                }
                canBreakLegs = false;
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
