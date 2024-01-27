using System.Collections;
using InputSystem;
using UnityEngine;

public class PlayerController : EntityController
{
    [SerializeField] private Collider weapon;
    [SerializeField] private Transform visualization;
    [SerializeField] private float attackDuration;
    
    private IInputManager inputManager;
    private Coroutine attackCoroutine;
    private readonly WaitForFixedUpdate waitForFixedUpdate = new();
    private WaitForSeconds waitForAttackDuration;
    private Vector3 weaponBasePos;

    public void Initialize(IInputManager inputManager)
    {
        this.inputManager = inputManager;
        inputManager.OnLeftAttack += InputManager_OnLeftAttack;
        inputManager.OnRightAttack += InputManager_OnRightAttack;
        waitForAttackDuration = new WaitForSeconds(attackDuration);
        weaponBasePos = weapon.transform.localPosition;
        initialized = true;
    }
    
    protected override void Move()
    {
        var position = transform.position;
        transform.position = new Vector3(position.x + inputManager.Horizontal * speed * Time.deltaTime, position.y, position.z);
    }
    
    private void InputManager_OnRightAttack()
    {
        visualization.rotation = Quaternion.Euler(new Vector3(0, 90, 0));
        PrepareAttack();
    }

    private void InputManager_OnLeftAttack()
    {   
        visualization.rotation = Quaternion.Euler(new Vector3(0, -90, 0));
        PrepareAttack();
    }

    private void PrepareAttack()
    {
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);
        
        attackCoroutine = StartCoroutine(Attack());
    }
    
    private IEnumerator Attack()
    {
        weapon.transform.localPosition = new Vector3(0, 100, 0);
        weapon.enabled = true;
        yield return waitForFixedUpdate;
        weapon.transform.localPosition = weaponBasePos;
        yield return waitForAttackDuration;
        weapon.enabled = false;
    }
}
