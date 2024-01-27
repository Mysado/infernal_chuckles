using System.Collections;
using System.Collections.Generic;
using Entity;
using UnityEngine;

public enum AttackPosition
{
    middle,
    low,
    high
}
public class DamageDealer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform lowAttack;
    [SerializeField] private Transform hightAttack;

    private RaycastHit[] raycastHits;
    public void Attack(AttackPosition attackPosition)
    {
        animator.Play("ImpArmature|ImpIAttack",0,0);
        Vector3 hitOrigin = default;
        switch (attackPosition)
        {
            case AttackPosition.middle:
                hitOrigin = transform.position;
                break;
            case AttackPosition.low:
                hitOrigin = lowAttack.position;
                break;
            case AttackPosition.high:
                hitOrigin = hightAttack.position;
                break;
        }
        raycastHits = Physics.RaycastAll(hitOrigin, transform.right,2);
        foreach (var hit in raycastHits)
        {
            if (hit.collider.CompareTag("Shield"))
            {
                break;
            }
            if (hit.collider.CompareTag("Enemy"))
            {
                DealDamage(hit.collider.gameObject);
                break;
            }
        }
    }

    private void DealDamage(GameObject other)
    {
        var enemy = other.GetComponent<EnemyController>();
        enemy.TakeDamage();
    }

    private void StunPlayer()
    {
        
    }
}
