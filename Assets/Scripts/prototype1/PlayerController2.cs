using DG.Tweening;
using Sisus.Init;
using UnityEngine;

public class PlayerController2 : MonoBehaviour<InputManager>
{
    [SerializeField] private int maxHealth;
    [SerializeField] private DamageDealer spear;
    private int currentHealth;
    private InputManager inputManager;
    private Sequence sequence;
    
    protected override void Init(InputManager inputManager)
    {
        this.inputManager = inputManager;
    }
    void Start()
    {
        currentHealth = maxHealth;
        inputManager.Input.Player.AttackLeft.performed += crt => AttackLeft();
        inputManager.Input.Player.AttackRight.performed += crt => AttackRight();
    }

    private void AttackLeft()
    {
        spear.transform.localRotation = new Quaternion(0, 1, 0, 0);
        spear.Attack(AttackPosition.middle);
        Debug.Log("left");
    }
    private void AttackRight()
    {
        spear.transform.localRotation = new Quaternion(0, 0, 0, 1);
        spear.Attack(AttackPosition.middle);
        Debug.Log("right");
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
