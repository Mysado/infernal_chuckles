using DG.Tweening;
using Score;
using Sisus.Init;
using UnityEngine;

public class PlayerController2 : MonoBehaviour<InputManager, ComboController>
{
    [SerializeField] private int maxHealth;
    [SerializeField] private DamageDealer spear;
    private int currentHealth;
    private InputManager inputManager;
    private ComboController comboController;
    private Sequence sequence;
    
    protected override void Init(InputManager inputManager, ComboController comboController)
    {
        this.inputManager = inputManager;
        this.comboController = comboController;
    }
    void Start()
    {
        currentHealth = maxHealth;
        inputManager.OnLeftAttack += InputManager_OnLeftAttack;
        inputManager.OnRightAttack += InputManager_OnRightAttack;
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
            comboController.IncreaseComboCounter();
        else
            comboController.ResetComboCounter();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
            TakeDamage();
    }

    private void TakeDamage()
    {
        currentHealth--;
        if (currentHealth <= 0)
            Time.timeScale = 0;
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
