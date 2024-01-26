using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
        other.GetComponent<EnemyMovementController>().TakeDamage();
        other.GetComponent<Rigidbody>().AddForce(other.transform.right * 20,ForceMode.Impulse);
    }
}
