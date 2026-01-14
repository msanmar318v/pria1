using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine.Rendering;

namespace Starter.Shooter
{
    /// <summary>
    /// Main player scrip - controls player movement and animations.
    /// </summary>
    public sealed class Player : NetworkBehaviour
    {
        [Header("References")]
        public Health Health;
        public SimpleKCC KCC;
        public PlayerInput Input;
        public Animator Animator;
        public Transform CameraPivot;
        public Transform CameraHandle;
        public Transform ScalingRoot;
        public UINameplate Nameplate;
        public HitboxRoot HitboxRoot;
        public Renderer[] HeadRenderers;
        public GameObject[] FirstPersonOverlayObjects;

        [Header("Movement Setup")]
        public float WalkSpeed = 2f;
        public float JumpImpulse = 10f;
        public float UpGravity = 25f;
        public float DownGravity = 40f;

        [Header("Movement Accelerations")]
        public float GroundAcceleration = 55f;
        public float GroundDeceleration = 25f;
        public float AirAcceleration = 25f;
        public float AirDeceleration = 1.3f;

        [Header("Jump Animation Setup")]
        [Tooltip("Delay en segundos antes de aplicar el impulso de salto (para dar tiempo a la animación de agacharse)")]
        public float JumpDelay = 0.2f;
        [Tooltip("Si está activado, la animación de salto comenzará inmediatamente al pulsar salto")]
        public bool StartJumpAnimationEarly = true;

        [Header("Fire Setup")]
        public LayerMask HitMask;
        public GameObject ImpactPrefab;
        public ParticleSystem MuzzleParticle;

        [Header("Animation Setup")]
        public Transform ChestTargetPosition;
        public Transform ChestBone; // Spine5 (el último)
        [Tooltip("Asigna todos los huesos de la columna desde Spine1 hasta Spine5")]
        public Transform[] SpineBones; // Array con Spine1, Spine2, Spine3, Spine4, Spine5
        [Tooltip("Cuánto afecta la rotación de la cámara a cada hueso (0 = nada, 1 = completamente)")]
        [Range(0f, 1f)]
        public float SpineInfluenceMultiplier = 0.6f;
        [Tooltip("Límite máximo de rotación del IK de la columna en grados")]
        [Range(0f, 70f)]
        public float MaxSpineRotationAngle = 45f; // NUEVO PARÁMETRO
        [Tooltip("Hueso de la cabeza para posicionar el CameraPivot")]
        public Transform HeadBone;
        [Tooltip("Offset de la cámara respecto a la cabeza")]
        public Vector3 CameraOffset = new Vector3(0f, 0.1f, 0.05f);

        [Header("Camera Smoothing")]
        [Tooltip("Umbral mínimo de movimiento vertical para que la cámara siga (en metros)")]
        public float VerticalMovementThreshold = 0.02f;
        [Tooltip("Reducción de movimientos horizontales (0 = sin movimiento, 1 = movimiento completo)")]
        [Range(0f, 1f)]
        public float HorizontalDampingStrength = 0.1f;
        [Tooltip("Velocidad de suavizado solo para oscilaciones pequeñas")]
        public float OscillationSmoothSpeed = 20f;

        [Header("Sounds")]
        public AudioSource FireSound;
        public AudioSource FootstepSound;
        public AudioClip JumpAudioClip;
        public AudioClip LandAudioClip;

        [Header("VFX")]
        public ParticleSystem DustParticles;

        [Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
        public string Nickname { get; set; }
        [Networked, HideInInspector]
        public int ChickenKills { get; set; }

        [Networked]
        private Vector3 _moveVelocity { get; set; }
        [Networked, OnChangedRender(nameof(OnJumpingChanged))]
        private NetworkBool _isJumping { get; set; }
        [Networked]
        private Vector3 _hitPosition { get; set; }
        [Networked]
        private Vector3 _hitNormal { get; set; }
        [Networked]
        private int _fireCount { get; set; }
        [Networked]
        private TickTimer _jumpTimer { get; set; }
        [Networked]
        private NetworkBool _isPlayingJumpAnimation { get; set; }
        [Networked]
        private NetworkBool _jumpRequested { get; set; }

        // Animation IDs
        private int _animIDSpeedX;
        private int _animIDSpeedZ;
        private int _animIDMoveSpeedZ;
        private int _animIDGrounded;
        private int _animIDPitch;
        private int _animIDShoot;
        private int _animIDJumping;

        private int _visibleFireCount;
        
        // Cache para rotaciones del animator
        private Quaternion[] _spineAnimatorRotations;
        
        // Cache para filtrado de oscilaciones
        private Vector3 _previousKCCPosition;
        private Vector3 _filteredHeadOffset;

        public void Respawn(Vector3 position)
        {
            ChickenKills = 0;
            Health.Revive();

            KCC.SetActive(true);
            KCC.SetPosition(position);
            KCC.SetLookRotation(0f, 0f);

            _moveVelocity = Vector3.zero;
            _jumpTimer = TickTimer.None;
            _isPlayingJumpAnimation = false;
            _jumpRequested = false;
            _previousKCCPosition = position;
            _filteredHeadOffset = Vector3.zero;
        }

        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                // Sending player nickname that is saved in UIGameMenu
                RPC_SetNickname(PlayerPrefs.GetString("PlayerName"));
            }

            // In case the nickname is already changed,
            // we need to trigger the change manually
            OnNicknameChanged();

            // Reset visible fire count
            _visibleFireCount = _fireCount;

            if (HasInputAuthority)
            {
                // For input authority deactivate head renderers so they are not obstructing the view
                for (int i = 0; i < HeadRenderers.Length; i++)
                {
                    HeadRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

                // Some objects (e.g. weapon) are renderer with secondary Overlay camera.
                // This prevents weapon clipping into the wall when close to the wall.
                int overlayLayer = LayerMask.NameToLayer("FirstPersonOverlay");

                if (overlayLayer != -1)
                {
                    for (int i = 0; i < FirstPersonOverlayObjects.Length; i++)
                    {
                        if (FirstPersonOverlayObjects[i] != null)
                        {
                            FirstPersonOverlayObjects[i].layer = overlayLayer;
                        }
                    }
                }

                // Look rotation interpolation is skipped for local player.
                // Look rotation is set manually in Render.
                KCC.Settings.ForcePredictedLookRotation = true;
            }

            // Inicializar posición del KCC
            _previousKCCPosition = KCC.Position;
        }

        public override void FixedUpdateNetwork()
        {
            if (Health.IsAlive && GetInput<GameplayInput>(out var input))
            {
                ProcessInput(input, Input.PreviousButtons);
            }
            else
            {
                // Continue with KCC movement (e.g. fall) even
                // when player is dead or input is missing.
                MovePlayer(Vector3.zero, 0f);
            }

            // Procesar el salto solicitado
            if (_jumpRequested && KCC.IsGrounded && !_jumpTimer.IsRunning)
            {
                _jumpRequested = false;

                // PRIMERO: Activar la animación INMEDIATAMENTE
                _isPlayingJumpAnimation = true;

                // SEGUNDO: Configurar el timer para aplicar el impulso después del delay
                if (JumpDelay > 0)
                {
                    _jumpTimer = TickTimer.CreateFromSeconds(Runner, JumpDelay);
                }
                else
                {
                    // Sin delay, aplicar impulso inmediatamente
                    _isJumping = true;
                    KCC.Move(_moveVelocity, JumpImpulse);
                }
            }

            // Check if it's time to apply the jump impulse
            if (_jumpTimer.IsRunning && _jumpTimer.Expired(Runner))
            {
                // TERCERO: Aplicar el impulso real después del delay
                _isJumping = true;
                KCC.Move(_moveVelocity, JumpImpulse);
                _jumpTimer = TickTimer.None;
            }

            // CUARTO: Resetear la animación cuando aterrizamos
            if (KCC.IsGrounded && _isJumping)
            {
                // Solo resetear si ya hemos saltado (velocity cayendo)
                if (KCC.RealVelocity.y <= 0.1f)
                {
                    _isJumping = false;
                    _isPlayingJumpAnimation = false;
                }
            }

            // Disable collisions and hits when player is dead
            HitboxRoot.HitboxRootActive = Health.IsAlive;
            KCC.SetActive(Health.IsAlive);
        }

        public override void Render()
        {
            if (HasInputAuthority)
            {
                // Set look rotation for Render.
                KCC.SetLookRotation(Input.LookRotation, -90f, 90f);
            }

            // Transform velocity vector to local space.
            var moveSpeed = transform.InverseTransformVector(KCC.RealVelocity);

            Animator.SetFloat(_animIDSpeedX, moveSpeed.x, 0.1f, Time.deltaTime);
            Animator.SetFloat(_animIDSpeedZ, moveSpeed.z, 0.1f, Time.deltaTime);
            Animator.SetBool(_animIDGrounded, KCC.IsGrounded);
            Animator.SetFloat(_animIDPitch, KCC.GetLookRotation(true, false).x, 0.02f, Time.deltaTime);

            // Actualizar la animación de salto
            bool shouldBeJumping = _isPlayingJumpAnimation;
            Animator.SetBool(_animIDJumping, shouldBeJumping);

            FootstepSound.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
            ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 8f);

            var emission = DustParticles.emission;
            emission.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;

            ShowFireEffects();
        }

        private void Awake()
        {
            AssignAnimationIDs();
            InitializeSpineCache();
        }

        private void LateUpdate()
        {
            if (Health.IsAlive == false)
                return;

            // PASO 1: Capturar las rotaciones del Animator ANTES de modificarlas
            CaptureAnimatorRotations();

            // PASO 2: Aplicar IK a la columna vertebral
            var pitchRotation = KCC.GetLookRotation(true, false);
            ApplySpineIK(pitchRotation.x);

            // PASO 3: Actualizar posición Y ROTACIÓN del CameraPivot SIN lag de movimiento
            UpdateCameraPivotTransform(pitchRotation);

            // Only InputAuthority needs to update camera
            if (HasInputAuthority)
            {
                Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
            }
        }

        private void UpdateCameraPivotTransform(Vector2 pitchRotation)
        {
            if (HeadBone == null)
                return;

            // SOLUCIÓN SIMPLE: Seguir directamente la posición del HeadBone
            // pero con filtrado suave solo para oscilaciones de alta frecuencia
            Vector3 targetHeadPosition = HeadBone.position;

            // Calcular la velocidad de cambio de posición
            Vector3 positionDelta = targetHeadPosition - (CameraPivot.position - HeadBone.TransformDirection(CameraOffset));
            
            // Si el cambio es grande (IK/agacharse), seguirlo inmediatamente
            // Si es pequeño (oscilaciones de caminar), suavizarlo
            float deltamagnitude = positionDelta.magnitude;
            float smoothSpeed;
            
            if (deltamagnitude > 0.05f) // Movimiento grande (IK)
            {
                smoothSpeed = 50f; // Seguir muy rápido (casi instantáneo)
            }
            else if (deltamagnitude > VerticalMovementThreshold) // Movimiento mediano
            {
                smoothSpeed = 20f; // Seguir rápido
            }
            else // Oscilaciones pequeñas
            {
                smoothSpeed = OscillationSmoothSpeed; // Filtrar más
            }

            // POSICIÓN: Seguir la cabeza con suavizado adaptativo
            Vector3 targetPosition = targetHeadPosition + HeadBone.TransformDirection(CameraOffset);
            CameraPivot.position = Vector3.Lerp(CameraPivot.position, targetPosition, Time.deltaTime * smoothSpeed);

            // ROTACIÓN: usar directamente la rotación del transform + pitch del jugador
            Quaternion baseRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            Quaternion pitchRotationQuat = Quaternion.Euler(pitchRotation.x, 0, 0);
            CameraPivot.rotation = baseRotation * pitchRotationQuat;
            
            // Actualizar tracking
            _previousKCCPosition = KCC.Position;
}

private void ApplySpineIK(float pitchAngle)
{
    // Si no hay huesos configurados, salir
    if (SpineBones == null || SpineBones.Length == 0 || _spineAnimatorRotations == null)
    {
        return;
    }

    // USAR EL PARÁMETRO CONFIGURABLE
    pitchAngle = Mathf.Clamp(pitchAngle, -MaxSpineRotationAngle, MaxSpineRotationAngle);

    // Aplicar rotación progresiva a cada hueso de la columna
    for (int i = 0; i < SpineBones.Length; i++)
    {
        if (SpineBones[i] == null)
            continue;

        // Calcular influencia progresiva: los huesos superiores rotan más
        float normalizedIndex = (float)(i + 1) / SpineBones.Length;
        float influence = normalizedIndex * SpineInfluenceMultiplier;

        // Calcular la rotación adicional basada en el pitch
        float additionalRotation = pitchAngle * influence;
        
        // Usar la rotación capturada del Animator
        Quaternion animatorRotation = _spineAnimatorRotations[i];
        
        // Aplicar rotación en espacio local directamente
        Vector3 localRight = SpineBones[i].parent != null 
            ? SpineBones[i].parent.InverseTransformDirection(transform.right)
            : Vector3.right;
        
        Quaternion ikRotation = Quaternion.AngleAxis(additionalRotation, localRight);
        
        // Aplicar la rotación de forma estable
        SpineBones[i].localRotation = animatorRotation * ikRotation;
    }
}

        private void CaptureAnimatorRotations()
        {
            if (SpineBones == null || _spineAnimatorRotations == null)
                return;

            // Guardar la rotación actual (que viene del Animator) antes de aplicar IK
            for (int i = 0; i < SpineBones.Length; i++)
            {
                if (SpineBones[i] != null)
                {
                    _spineAnimatorRotations[i] = SpineBones[i].localRotation;
                }
            }
        }

        private void InitializeSpineCache()
        {
            if (SpineBones != null && SpineBones.Length > 0)
            {
                _spineAnimatorRotations = new Quaternion[SpineBones.Length];
            }
        }

        private void ProcessInput(GameplayInput input, NetworkButtons previousButtons)
        {
            KCC.SetLookRotation(input.LookRotation, -90f, 90f);

            // Calculate correct move direction from input (rotated based on latest KCC rotation)
            var moveDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * WalkSpeed;

            // Comparing current input buttons to previous input buttons - this prevents glitches when input is lost
            if (input.Buttons.WasPressed(previousButtons, EInputButton.Jump))
            {
                _jumpRequested = true;
            }

            // El impulso de salto ahora se aplica en FixedUpdateNetwork cuando el timer expira
            MovePlayer(desiredMoveVelocity, 0f);

            if (input.Buttons.WasPressed(previousButtons, EInputButton.Fire))
            {
                Fire();
            }
        }

        private void MovePlayer(Vector3 desiredMoveVelocity, float jumpImpulse)
        {
            // It feels better when the player falls quicker
            KCC.SetGravity(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity);

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
            {
                // No desired move velocity - we are stopping
                acceleration = KCC.IsGrounded ? GroundDeceleration : AirDeceleration;
            }
            else
            {
                acceleration = KCC.IsGrounded ? GroundAcceleration : AirAcceleration;
            }

            _moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

            KCC.Move(_moveVelocity, jumpImpulse);
        }

        private void Fire()
        {
            // Clear hit position in case nothing will be hit
            _hitPosition = Vector3.zero;

            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;

            // Whole projectile path and effects are immediately processed (= hitscan projectile)
            if (Runner.LagCompensation.Raycast(CameraHandle.position, CameraHandle.forward, 200f,
                    Object.InputAuthority, out var hit, HitMask, hitOptions, QueryTriggerInteraction.Ignore) == true)
            {
                // Deal damage
                var health = hit.Hitbox != null ? hit.Hitbox.Root.GetComponent<Health>() : null;
                if (health != null && health.TakeHit(1))
                {
                    if (health.IsAlive == false)
                    {
                        // Killing chicken grants 1 point, killing other player has -10 points penalty.
                        ChickenKills += health.GetComponent<Chicken>() != null ? 1 : -10;
                    }
                }

                // Save hit point to correctly show bullet path on all clients.
                // This however works only for single projectile per FUN and with higher fire cadence
                // some projectiles might not be fired on proxies because we save only the position
                // of the LAST hit.
                _hitPosition = hit.Point;
                _hitNormal = hit.Normal;
            }

            // In this example projectile count property (fire count) is used not only for weapon fire effects
            // but to spawn the projectile visuals themselves.
            _fireCount++;
        }

        private void ShowFireEffects()
        {
            // Notice we are not using OnChangedRender for fireCount property but instead
            // we are checking against a local variable and show fire effects only when visible
            // fire count is SMALLER. This prevents triggering false fire effects when
            // local player mispredicted fire (e.g. input got lost) and fireCount property got decreased.
            if (_visibleFireCount < _fireCount)
            {
                FireSound.PlayOneShot(FireSound.clip);
                MuzzleParticle.Play();
                Animator.SetTrigger(_animIDShoot);

                if (_hitPosition != Vector3.zero)
                {
                    // Impact gets destroyed automatically with DestroyAfter script
                    Instantiate(ImpactPrefab, _hitPosition, Quaternion.LookRotation(_hitNormal));
                }
            }

            _visibleFireCount = _fireCount;
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeedX = Animator.StringToHash("SpeedX");
            _animIDSpeedZ = Animator.StringToHash("SpeedZ");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDPitch = Animator.StringToHash("Pitch");
            _animIDShoot = Animator.StringToHash("Shoot");
            _animIDJumping = Animator.StringToHash("Jumping");
        }

        private void OnJumpingChanged()
        {
            if (_isJumping)
            {
                AudioSource.PlayClipAtPoint(JumpAudioClip, KCC.Position, 0.5f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(LandAudioClip, KCC.Position, 1f);
            }

            if (HasInputAuthority == false)
            {
                ScalingRoot.localScale = _isJumping ? new Vector3(0.5f, 1.5f, 0.5f) : new Vector3(1.25f, 0.75f, 1.25f);
            }
        }

        private void OnNicknameChanged()
        {
            if (HasInputAuthority)
                return; // Do not show nickname for local player

            Nameplate.SetNickname(Nickname);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetNickname(string nickname)
        {
            Nickname = nickname;
        }
    }
}