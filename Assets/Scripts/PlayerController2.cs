using DG.Tweening;
using Entity;
using Score;
using Sisus.Init;
using TMPro;
using UnityEngine;

public class PlayerController2 : MonoBehaviour<InputManager, ComboController, ScoreController>
{
    [SerializeField] private int maxHealth;
    [SerializeField] private DamageDealer spear;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private GameObject barun;
    [SerializeField] private Transform barunSpawnPoint;
    [SerializeField] private EnemySpawner enemySpawner;
    private int currentHealth;
    private InputManager inputManager;
    private ComboController comboController;
    private ScoreController scoreController;
    private Sequence sequence;
    
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
}
