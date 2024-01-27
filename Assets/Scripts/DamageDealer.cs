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
    public bool Attack(AttackPosition attackPosition)
    {
        if (IsStunned)
        {
            animator.Play("ImpArmature|ImpIStun",0,0);
            return false;
        }
        animator.Play("ImpArmature|ImpIAttack",0,0);
        raycastHits = Physics.RaycastAll(transform.position, transform.right,attackRange);
        var enemyDamaged = false;
        foreach (var hit in raycastHits)
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                var enemy = hit.collider.GetComponent<EnemyController>();
                if (enemy.CanMove && !enemyDamaged)
                {
                    DealDamage(enemy, attackPosition);
                    enemyDamaged = true;
                }

                if (enemy.CanMove && enemyDamaged)
                {
                    enemy.KnockBack();
                }
            }
        }
        StunPlayer();
        return false;
    }

    private void DealDamage(EnemyController enemy, AttackPosition attackPosition)
    {
        enemy.TakeDamage(attackPosition);
    }

    private void StunPlayer()
    {
        animator.Play("ImpArmature|ImpIStun",0,0);
        IsStunned = true;
        DOTween.Sequence().PrependInterval(0.5f).AppendCallback(() => IsStunned = false);
    }
}
