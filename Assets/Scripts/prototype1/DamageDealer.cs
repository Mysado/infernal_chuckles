using System.Collections;
using System.Collections.Generic;
using Entity;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [SerializeField] private Collider collider;
    [SerializeField] private MeshRenderer renderer;
    [SerializeField] private Animator animator;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetActive(bool value)
    {
        collider.enabled = value;
        //renderer.enabled = value;
    }

    public void Attack()
    {
        animator.Play("ImpArmature|ImpIAttack",0,0);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            DealDamage(other.gameObject);
        }
    }

    private void DealDamage(GameObject other)
    {
        other.GetComponent<EnemyController>().TakeDamage();
        other.GetComponent<Rigidbody>().AddForce(other.transform.right * 20,ForceMode.Impulse);
    }
}
