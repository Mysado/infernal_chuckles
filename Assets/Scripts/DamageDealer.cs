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
        foreach (var hit in raycastHits)
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                DealDamage(hit.collider.gameObject, attackPosition);
                return true;
            }
        }
        StunPlayer();
        return false;
    }

    private void DealDamage(GameObject other, AttackPosition attackPosition)
    {
        var enemy = other.GetComponent<EnemyController>();
        enemy.TakeDamage(attackPosition);
    }

    private void StunPlayer()
    {
        animator.Play("ImpArmature|ImpIStun",0,0);
        IsStunned = true;
        DOTween.Sequence().PrependInterval(0.5f).AppendCallback(() => IsStunned = false);
    }
}
