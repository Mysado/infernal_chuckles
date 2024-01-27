namespace prototype2
{
    using Entity;
    using UnityEngine;

    public class WeaponController : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Enemy"))
                other.GetComponent<EnemyController>().Damage();
        }
    }
}