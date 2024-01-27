using DG.Tweening;
using Sisus.Init;
using UnityEngine;

public class PlayerController2 : MonoBehaviour<InputManager>
{
    [SerializeField] private int maxHealth;
    [SerializeField] private DamageDealer spear;
    public InputActions input;
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
        spear.SetActive(false);
    }

    void Update()
    {

    }

    private void AttackLeft()
    {
        sequence.Kill();
        spear.SetActive(true);
        //var q = Quaternion.LookRotation((transform.position - transform.forward * 5) - transform.position); 
        //spear.transform.rotation = Quaternion.RotateTowards(spear.transform.rotation, q, 9999 * Time.deltaTime);
        Vector3 targetDirection = (transform.position - transform.forward * 5) - transform.position;
        var newDirection = Vector3.RotateTowards(spear.transform.position, targetDirection, 3, 3);
        //spear.transform.rotation = Quaternion.LookRotation(targetDirection);
        spear.transform.localRotation = new Quaternion(0, 1, 0, 0);
        spear.Attack();
        sequence = DOTween.Sequence().PrependInterval(0.1f).AppendCallback(() => 
        {
            spear.SetActive(false);
            //spear.transform.rotation = Quaternion.identity;

        });
        Debug.Log("left");
    }
    private void AttackRight()
    {
        sequence.Kill();
        spear.SetActive(true);
        //var q = Quaternion.LookRotation((transform.position + transform.forward * 5) - transform.position); 
        //spear.transform.rotation = Quaternion.RotateTowards(spear.transform.rotation, q, 9999 * Time.deltaTime);
        Vector3 targetDirection = (transform.position + transform.forward * 5) - transform.position;
        var newDirection = Vector3.RotateTowards(spear.transform.position, targetDirection, 3, 3);
        //spear.transform.rotation = Quaternion.LookRotation(targetDirection);
        spear.transform.localRotation = new Quaternion(0, 0, 0, 1);
        spear.Attack();
        sequence = DOTween.Sequence().PrependInterval(0.1f).AppendCallback(() =>
        {
            spear.SetActive(false);
            //spear.transform.rotation = Quaternion.identity;

        });
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
