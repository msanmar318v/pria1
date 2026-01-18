using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    public class PowerUp : NetworkBehaviour
    {
        [Header("Power Up Configuration")]
        [SerializeField] private float speedMultiplier = 1.5f;
        [SerializeField] private float duration = 5f;
        [SerializeField] private float respawnTime = 5f;
        
        [Header("Visual Components")]
        [SerializeField] private MeshRenderer meshRenderer; 
        [SerializeField] private Collider triggerCollider;  
        
        [Networked]
        private NetworkBool IsAvailable { get; set; }
        
        [Networked]
        private TickTimer RespawnTimer { get; set; }
        
        [Networked]
        private Vector3 NetworkedPosition { get; set; }
        
        [Networked]
        private Quaternion NetworkedRotation { get; set; }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                IsAvailable = true;
            }
            
            UpdateVisuals();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;

            if (!IsAvailable && RespawnTimer.Expired(Runner))
            {
                // Llamar al GameManager para que nos reposicione
                var gameManager = FindFirstObjectByType<GameManager>();
                if (gameManager != null)
                {
                    gameManager.RespawnPowerUp(this);
                }
                
                IsAvailable = true;
                RespawnTimer = default;
            }
        }
        
        public override void Render()
        {
            // Sincronizar la posición visual con la posición de red
            transform.position = NetworkedPosition;
            transform.rotation = NetworkedRotation;
            
            UpdateVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Solo el host procesa las colisiones
            if (!HasStateAuthority)
                return;
                
            if (!IsAvailable)
                return;

            var player = other.GetComponentInParent<Player>();
            if (player == null)
                return;

            // Aplicar el power-up al jugador
            player.ApplySpeedPowerUp(speedMultiplier, duration);

            // Desactivar el power-up
            IsAvailable = false;
            UpdateVisuals();
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnTime);
        }
        
        public void SetPosition(Vector3 position, Quaternion rotation)
        {
            if (!HasStateAuthority)
                return;
            
            NetworkedPosition = position;
            NetworkedRotation = rotation;
            transform.position = position;
            transform.rotation = rotation;
        }
        
        private void UpdateVisuals()
        {
            bool shouldBeVisible = IsAvailable;
            
            if (meshRenderer != null)
                meshRenderer.enabled = shouldBeVisible;
        
            if (triggerCollider != null)  
                triggerCollider.enabled = shouldBeVisible;
        }
    }
}