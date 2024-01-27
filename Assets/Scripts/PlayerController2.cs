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
        inputManager.Input.Player.AttackLeft.performed += crt =>
        {
            RotateLeft();
            Attack(AttackPosition.middle);
        };
        inputManager.Input.Player.AttackRight.performed += crt =>
        {
            RotateRight();
            Attack(AttackPosition.middle);
        };
        inputManager.Input.Player.AttackLeftDown.performed += crt =>         
        {
            RotateLeft();
            Attack(AttackPosition.low);
        };
        inputManager.Input.Player.AttackRightDown.performed += crt =>         
        {
            RotateRight();
            Attack(AttackPosition.low);
        };
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
        }
        else
        {
            comboController.ResetComboCounter();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            TakeDamage();
            Destroy(other.gameObject);
        }
    }

    private void TakeDamage()
    {
        currentHealth--;
        if (currentHealth <= 0)
            Time.timeScale = 0;
    }
}
