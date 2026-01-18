using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Representa un punto de spawn para PowerUps en el entorno.
    /// </summary>
    public class PowerUpSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up);
        }
    }
}