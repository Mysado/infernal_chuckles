using System;
using DG.Tweening;
using Entity;
using UnityEngine;

public enum AttackPosition
{
    Body,
    Legs,
    Head
}
public class DamageDealer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float attackRange;

    public bool IsStunned;
    private RaycastHit[] raycastHits;
    private float cooldownTimer;


    private void Update()
    {
        cooldownTimer += Time.deltaTime;
    }

    public bool Attack(AttackPosition attackPosition)
    {
        if (IsStunned)
        {
            animator.Play("ImpArmature|ImpIStun",0,0);
            return false;
        }

        switch (attackPosition)
        {
            case AttackPosition.Body:
                animator.Play("ImpArmature|ImpIAttack",0,0);
                break;
            case AttackPosition.Legs:
                animator.Play("ImpArmature|ImpIAttack3",0,0);
                break;
            case AttackPosition.Head:
                animator.Play("ImpArmature|ImpIAttack4",0,0);
                break;
        }
        
        raycastHits = Physics.RaycastAll(transform.position, transform.right,attackRange);
        System.Array.Sort(raycastHits, (x,y) => x.distance.CompareTo(y.distance));
        var enemyDamaged = false;
        foreach (var hit in raycastHits)
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                var enemy = hit.collider.GetComponent<EnemyController>();
                if (enemy.CanMove)
                {
                    DealDamage(enemy, attackPosition);
                    enemyDamaged = true;
                }
            }
        }

        if (enemyDamaged)
            return true;
        
        StunPlayer();
        return false;
    }

    private void DealDamage(EnemyController enemy, AttackPosition attackPosition)
    {
        enemy.TakeDamage(attackPosition);
    }

    private void StunPlayer()
    {
        cooldownTimer = 0;
        //animator.Play("ImpArmature|ImpIStun",0,0);
        IsStunned = true;
        DOTween.Sequence().PrependInterval(0.5f).AppendCallback(() => IsStunned = false);
    }
}
