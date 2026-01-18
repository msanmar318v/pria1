using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    public class PowerUp : NetworkBehaviour
    {
        [SerializeField]  float speedMultiplier = 1.5f; // 50% más velocidad
        [SerializeField] float duration = 5f;
        
        
        [Header("Setup")]
        public float RespawnTime = 5f;
        [Header("Visual")]
        [SerializeField] private MeshRenderer meshRenderer; 
        [SerializeField] private Collider triggerCollider;  

        [Header("Respawn Points")]
        public Transform[] SpawnPoints;
        
        
        [Networked]
        private NetworkBool _isAvailable { get; set; }
        [Networked]
        private TickTimer _respawnTimer { get; set; }
        

        public override void Spawned()
        {
            // Se ejecuta cuando Fusion "registra" el objeto
            _isAvailable = true;
            UpdateVisuals();                        
            Debug.Log("PowerUp Spawned, disponible: " + _isAvailable);
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority == false)
                return;

            if (_isAvailable == false && _respawnTimer.Expired(Runner))
            {
                Debug.Log("🔥 RESPAWN EJECUTADO!");
                // Mover a spawn aleatorio
                if (SpawnPoints != null && SpawnPoints.Length > 0)
                {
                    int index = Random.Range(0, SpawnPoints.Length);
                    Transform spawnPoint = SpawnPoints[index];
                    transform.position = spawnPoint.position + Vector3.up * 0.5f;
                    transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);  // rotación random
                }
                
                _isAvailable = true;
                UpdateVisuals();
                _respawnTimer = default;
            }
            
            // Debug para ver que FixedUpdateNetwork se ejecuta
            // Debug.Log("PowerUp FixedUpdateNetwork ejecutándose");
          
        }
        
        public override void Render()
        {
            // Se ejecuta cada frame en todos los clientes
            // Sincroniza los visuales con el estado networked
            UpdateVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Si no está disponible
            if (_isAvailable == false)
            {
                Debug.Log("PowerUp no disponible");
                return;
            }

            // Buscar Player
            var player = other.GetComponentInParent<Player>();
            if (player == null)
            {
                Debug.Log("No es un Player");
                return;
            }

            Debug.Log("✓ PLAYER DETECTADO! Desactivando power up");
            
            player.ApplySpeedPowerUp(speedMultiplier, duration);

            // Para desactivar el gameObject del power up
            _isAvailable = false;
            
            UpdateVisuals();
            _respawnTimer = TickTimer.CreateFromSeconds(Runner, RespawnTime);
            
            Debug.Log($"Respawn: Expired={_respawnTimer.Expired(Runner)} Remaining={_respawnTimer.RemainingTime(Runner)}");
        }
        
        private void UpdateVisuals()
        {
            if (meshRenderer != null)
                meshRenderer.enabled = _isAvailable;
        
            if (triggerCollider != null)  
                triggerCollider.enabled = _isAvailable;
        }
        
        
    }
}