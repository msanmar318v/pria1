using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine.Rendering;
using UnityEngine.Events;

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

        [Header("Ammo Setup")]
        [Tooltip("Munición máxima del cargador")]
        public int MaxAmmoPerClip = 6;
        
        [Tooltip("Tiempo mínimo entre disparos en segundos")]
        public float FireRate = 0.1f;
        
        [Tooltip("Tiempo de recarga en segundos")]
        public float ReloadTime = 3f;

        [Header("Ammo Events")]
        [Tooltip("Evento que se dispara cuando cambia la munición (parámetro: currentAmmo)")]
        public UnityEvent<int> OnAmmoChanged;

        [Header("Animation Setup")]
        public Transform ChestTargetPosition;
        public Transform ChestBone; // Ultimo Spine del jugador
        [Tooltip("Asigna todos los huesos de la columna desde Spine1 hasta Spine5")]
        public Transform[] SpineBones; // Array con Spines del jugador
        [Tooltip("Cuánto afecta la rotación de la cámara a cada hueso (0 = nada, 1 = completamente)")]
        [Range(0f, 1f)]
        public float SpineInfluenceMultiplier = 0.6f;
        [Tooltip("Límite máximo de rotación del IK de la columna en grados")]
        [Range(0f, 70f)]
        public float MaxSpineRotationAngle = 45f;
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

        [Header("Respawn Settings")]
        [Tooltip("Frames a esperar después del respawn antes de reactivar IK de columna")]
        public int RespawnSafetyFrames = 3;

        [Header("Sounds")]
        public AudioSource FireSound;
        public AudioSource FootstepSound;
        public AudioClip JumpAudioClip;
        public AudioClip LandAudioClip;
        public AudioClip ReloadAudioClip;

        [Header("VFX")]
        public ParticleSystem DustParticles;

        [Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
        public string Nickname { get; set; }
        [Networked, HideInInspector]
        public int ChickenKills { get; set; }

        // Ammo System - Networked Variables
        [Networked, HideInInspector, OnChangedRender(nameof(OnCurrentAmmoChangedCallback))]
        public int CurrentAmmo { get; set; }
        [Networked, HideInInspector]
        public NetworkBool IsReloading { get; set; }

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
        [Networked]
        private TickTimer _fireRateTimer { get; set; }
        [Networked]
        private TickTimer _reloadTimer { get; set; }

        // Animation IDs
        private int _animIDSpeedX;
        private int _animIDSpeedZ;
        private int _animIDMoveSpeedZ;
        private int _animIDGrounded;
        private int _animIDPitch;
        private int _animIDShoot;
        private int _animIDJumping;
        private int _animIDSpeed;

        private int _visibleFireCount;

        private Quaternion[] _spineAnimatorRotations;

        private Vector3 _previousKCCPosition;
        private Vector3 _filteredHeadOffset;

        // Variables para prevenir el bug de rotación infinita durante respawn
        private bool _isRespawning;
        private int _respawnFrameCounter;

        // Audio
        private AudioSource _reloadAudioSource;

        public void Respawn(Vector3 position)
        {
            ChickenKills = 0;
            Health.Revive();

            KCC.SetActive(true);
            KCC.SetPosition(position);
            
            // Resetear completamente la rotación del transform del jugador
            transform.rotation = Quaternion.identity;
            
            // Resetear completamente la rotación del KCC (mirar hacia adelante horizontal)
            KCC.SetLookRotation(0f, 0f);
            
            // Si tiene autoridad de input, resetear el Input también
            if (HasInputAuthority && Input != null)
            {
                Input.ResetLookRotation();
            }

            _moveVelocity = Vector3.zero;
            _jumpTimer = TickTimer.None;
            _isPlayingJumpAnimation = false;
            _jumpRequested = false;
            _previousKCCPosition = position;
            _filteredHeadOffset = Vector3.zero;
            
            // Resetear sistema de munición
            CurrentAmmo = MaxAmmoPerClip;
            IsReloading = false;
            _fireRateTimer = TickTimer.None;
            _reloadTimer = TickTimer.None;
            
            // Resetear todos los elementos del HUD
            ResetHUDElements();
            
            // Activar el flag de respawn para prevenir actualizaciones de IK
            _isRespawning = true;
            _respawnFrameCounter = 0;
            
            // IMPORTANTE: Resetear ANTES de llamar a ResetSpineRotations
            // para asegurar que el Animator está en estado neutral
            if (Animator != null)
            {
                Animator.SetFloat(_animIDPitch, 0f);
                Animator.Update(0f);
            }
            
            // Resetear las rotaciones de los huesos de la columna a su estado neutral
            ResetSpineRotations();
            
            // Resetear la rotación del CameraPivot y CameraHandle inmediatamente
            if (CameraPivot != null)
            {
                CameraPivot.rotation = Quaternion.identity;
                CameraPivot.localRotation = Quaternion.identity;
                
                if (CameraHandle != null)
                {
                    CameraHandle.rotation = Quaternion.identity;
                    CameraHandle.localRotation = Quaternion.identity;
                }
            }
        }

        /// <summary>
        /// Resetea todos los elementos del HUD para sincronizarlos con el estado del jugador.
        /// Se llama durante el respawn para prevenir desincronizaciones.
        /// </summary>
        private void ResetHUDElements()
        {
            // Solo ejecutar para el jugador local
            if (!HasInputAuthority)
                return;

            // Resetear munición en el HUD
            // Forzar la invocación del evento para actualizar la UI inmediatamente
            OnAmmoChanged?.Invoke(MaxAmmoPerClip);
            
            // Resetear vida en el HUD
            if (Health != null)
            {
                int healthPercentage = Health.GetHealthPercentage();
                Health.OnHealthChanged?.Invoke(Health.CurrentHealth, Health.InitialHealth, healthPercentage);
            }
            
            Debug.Log($"[Player] HUD reseteado - Munición: {MaxAmmoPerClip}, Vida: {Health.CurrentHealth}/{Health.InitialHealth}");
            
            // TODO: Añadir aquí futuros elementos del HUD cuando se implementen:
            // - Kills: OnKillsChanged?.Invoke(0);
            // - Otros elementos del HUD...
        }

        private void ResetSpineRotations()
        {
            // Capturar y resetear las rotaciones neutrales de los huesos
            if (SpineBones != null && SpineBones.Length > 0)
            {
                if (_spineAnimatorRotations == null)
                {
                    _spineAnimatorRotations = new Quaternion[SpineBones.Length];
                }
                
                for (int i = 0; i < SpineBones.Length; i++)
                {
                    if (SpineBones[i] != null)
                    {
                        // Resetear a rotación local por defecto
                        SpineBones[i].localRotation = Quaternion.identity;
                        _spineAnimatorRotations[i] = Quaternion.identity;
                    }
                }
            }
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                // Inicializar munición al máximo
                CurrentAmmo = MaxAmmoPerClip;
                IsReloading = false;
            }

            if (HasInputAuthority)
            {
                RPC_SetNickname(PlayerPrefs.GetString("PlayerName"));
            }

            OnNicknameChanged();
            _visibleFireCount = _fireCount;

            if (HasInputAuthority)
            {
                for (int i = 0; i < HeadRenderers.Length; i++)
                {
                    HeadRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

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

                KCC.Settings.ForcePredictedLookRotation = true;
            }

            if (Animator != null)
            {
                Animator.updateMode = AnimatorUpdateMode.Normal;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Animator.applyRootMotion = false;
                Animator.stabilizeFeet = false;
            }

            _previousKCCPosition = KCC.Position;
            _isRespawning = false;
            _respawnFrameCounter = 0;

            // Crear AudioSource para el sonido de recarga
            if (ReloadAudioClip != null)
            {
                _reloadAudioSource = gameObject.AddComponent<AudioSource>();
                _reloadAudioSource.clip = ReloadAudioClip;
                _reloadAudioSource.playOnAwake = false;
                _reloadAudioSource.spatialBlend = 1f; // 3D sound
                _reloadAudioSource.minDistance = 1f;
                _reloadAudioSource.maxDistance = 20f;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // CRÍTICO: Verificar CurrentHealth además de IsAlive
            if (Health.IsAlive && Health.CurrentHealth > 0 && GetInput<GameplayInput>(out var input))
            {
                ProcessInput(input, Input.PreviousButtons);
            }
            else
            {
                MovePlayer(Vector3.zero, 0f);
            }

            // Gestión de sistema de recarga
            if (IsReloading)
            {
                if (_reloadTimer.Expired(Runner))
                {
                    // Recarga completada
                    CurrentAmmo = MaxAmmoPerClip;
                    IsReloading = false;
                    _reloadTimer = TickTimer.None;
                }
            }
            else
            {
                // Verificar si necesita recargar automáticamente
                if (CurrentAmmo <= 0 && !_reloadTimer.IsRunning)
                {
                    StartReload();
                }
            }

            if (_jumpRequested && KCC.IsGrounded && !_jumpTimer.IsRunning)
            {
                _jumpRequested = false;
                _isPlayingJumpAnimation = true;
                if (JumpDelay > 0)
                {
                    _jumpTimer = TickTimer.CreateFromSeconds(Runner, JumpDelay);
                }
                else
                {
                    _isJumping = true;
                    KCC.Move(_moveVelocity, JumpImpulse);
                }
            }
            if (_jumpTimer.IsRunning && _jumpTimer.Expired(Runner))
            {
                _isJumping = true;
                KCC.Move(_moveVelocity, JumpImpulse);
                _jumpTimer = TickTimer.None;
            }
            if (KCC.IsGrounded && _isJumping)
            {
                if (KCC.RealVelocity.y <= 0.1f)
                {
                    _isJumping = false;
                    _isPlayingJumpAnimation = false;
                }
            }
            HitboxRoot.HitboxRootActive = Health.IsAlive && Health.CurrentHealth > 0;
            KCC.SetActive(Health.IsAlive && Health.CurrentHealth > 0);
        }

        public override void Render()
        {
            // No procesar input ni rotaciones si el jugador está muerto o muriendo
            if (HasInputAuthority && Health.CurrentHealth > 0)
            {
                // CORRECCIÓN: Invertir el orden - SetLookRotation espera (pitch, yaw)
                // Input.LookRotation.x = Yaw, Input.LookRotation.y = Pitch
                // Pasamos un Vector2(Pitch, Yaw) invirtiendo el orden
                KCC.SetLookRotation(new Vector2(Input.LookRotation.y, Input.LookRotation.x), -90f, 90f);
            }
            
            var moveSpeed = transform.InverseTransformVector(KCC.RealVelocity);
            float totalSpeed = new Vector2(moveSpeed.x, moveSpeed.z).magnitude;

            Animator.SetFloat(_animIDSpeedX, moveSpeed.x);
            Animator.SetFloat(_animIDSpeedZ, moveSpeed.z);
            Animator.SetFloat(_animIDSpeed, totalSpeed);
            Animator.SetBool(_animIDGrounded, KCC.IsGrounded);

            // Solo actualizar pitch si está vivo
            if (Health.CurrentHealth > 0)
            {
                Animator.SetFloat(_animIDPitch, KCC.GetLookRotation(true, false).x, 0.01f, Time.deltaTime);
            }
            else
            {
                Animator.SetFloat(_animIDPitch, 0f, 0.01f, Time.deltaTime);
            }
            
            Animator.SetBool(_animIDJumping, _isPlayingJumpAnimation);

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
            // CRÍTICO: Verificar TANTO Health.IsAlive COMO CurrentHealth directamente
            // para prevenir que el delay de interpolación cause código residual
            if (Health.IsAlive == false || Health.CurrentHealth <= 0)
            {
                // Resetear los huesos de la columna cuando está muerto
                // para prevenir que rotaciones residuales se queden atrapadas
                if (SpineBones != null && _spineAnimatorRotations != null)
                {
                    for (int i = 0; i < SpineBones.Length; i++)
                    {
                        if (SpineBones[i] != null)
                        {
                            SpineBones[i].localRotation = Quaternion.identity;
                            _spineAnimatorRotations[i] = Quaternion.identity;
                        }
                    }
                }
                
                // Resetear también el CameraPivot
                if (CameraPivot != null)
                {
                    CameraPivot.localRotation = Quaternion.identity;
                }
                
                // Resetear el KCC LookRotation para prevenir acumulación
                if (HasInputAuthority)
                {
                    KCC.SetLookRotation(0f, 0f);
                }
                
                return;
            }

            // Gestión del estado de respawn
            if (_isRespawning)
            {
                _respawnFrameCounter++;
                
                // Después de los frames de seguridad, desactivar el flag
                if (_respawnFrameCounter >= RespawnSafetyFrames)
                {
                    _isRespawning = false;
                    _respawnFrameCounter = 0;
                    
                    // Al finalizar el respawn, forzar una captura limpia de rotaciones
                    if (Animator != null)
                    {
                        Animator.Update(0f);
                    }
                    CaptureAnimatorRotations();
                }
                
                // Durante el respawn, mantener todo en estado neutral
                if (CameraPivot != null)
                {
                    // Forzar rotación neutral durante frames de seguridad
                    Quaternion neutralRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                    CameraPivot.rotation = neutralRotation;
                }
                
                // No ejecutar IK ni actualizaciones de cámara durante respawn
                return;
            }

            CaptureAnimatorRotations();

            var lookRotation = KCC.GetLookRotation(true, false);

            // Validación adicional: verificar que los valores de rotación son razonables
            if (float.IsNaN(lookRotation.x) || float.IsNaN(lookRotation.y) ||
                Mathf.Abs(lookRotation.x) > 360f || Mathf.Abs(lookRotation.y) > 360f)
            {
                Debug.LogWarning($"[Player] Rotación inválida detectada: {lookRotation}. Reseteando...");
                KCC.SetLookRotation(0f, 0f);
                return;
            }

            ApplySpineIK(lookRotation.x);

            UpdateCameraPivotTransform(lookRotation);

            if (HasInputAuthority)
            {
                Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
            }
        }

        private void UpdateCameraPivotTransform(Vector2 pitchRotation)
        {
            if (HeadBone == null)
                return;

            Vector3 targetHeadPosition = HeadBone.position;
            Vector3 positionDelta = targetHeadPosition - (CameraPivot.position - HeadBone.TransformDirection(CameraOffset));

            float deltaMagnitude = positionDelta.magnitude;
            float smoothSpeed;
            if (deltaMagnitude > 0.05f)
            {
                smoothSpeed = 100f;
            }
            else if (deltaMagnitude > VerticalMovementThreshold)
            {
                smoothSpeed = 40f;
            }
            else
            {
                smoothSpeed = OscillationSmoothSpeed;
            }
            Vector3 targetPosition = targetHeadPosition + HeadBone.TransformDirection(CameraOffset);
            CameraPivot.position = Vector3.Lerp(CameraPivot.position, targetPosition, Time.deltaTime * smoothSpeed);
            
            // Usar la rotación Y del transform directamente (ya sincronizado por KCC)
            Quaternion baseRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            Quaternion pitchRotationQuat = Quaternion.Euler(pitchRotation.x, 0, 0);
            CameraPivot.rotation = baseRotation * pitchRotationQuat;

            _previousKCCPosition = KCC.Position;
        }

        private void ApplySpineIK(float pitchAngle)
        {
            if (SpineBones == null || SpineBones.Length == 0 || _spineAnimatorRotations == null)
            {
                return;
            }

            // Clampear el ángulo de pitch para prevenir valores extremos
            pitchAngle = Mathf.Clamp(pitchAngle, -MaxSpineRotationAngle, MaxSpineRotationAngle);
            
            for (int i = 0; i < SpineBones.Length; i++)
            {
                if (SpineBones[i] == null)
                    continue;

                float normalizedIndex = (float)(i + 1) / SpineBones.Length;
                float influence = normalizedIndex * SpineInfluenceMultiplier;
                float additionalRotation = pitchAngle * influence;

                Quaternion animatorRotation = _spineAnimatorRotations[i];

                Vector3 localRight = SpineBones[i].parent != null
                    ? SpineBones[i].parent.InverseTransformDirection(transform.right)
                    : Vector3.right;

                Quaternion ikRotation = Quaternion.AngleAxis(additionalRotation, localRight);

                SpineBones[i].localRotation = animatorRotation * ikRotation;
            }
        }

        private void CaptureAnimatorRotations()
        {
            if (SpineBones == null || _spineAnimatorRotations == null)
                return;

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
            // CORRECCIÓN: Invertir el orden - SetLookRotation espera (pitch, yaw)
            // Input.LookRotation.x = Yaw, Input.LookRotation.y = Pitch
            KCC.SetLookRotation(new Vector2(input.LookRotation.y, input.LookRotation.x), -90f, 90f);
            
            var moveDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * WalkSpeed;

            if (input.Buttons.WasPressed(previousButtons, EInputButton.Jump))
            {
                // Solo procesar el salto si:
                // 1. El jugador está en el suelo
                // 2. No hay una solicitud de salto pendiente
                // 3. No hay un timer de salto activo
                // 4. No está ya saltando
                // 5. No está reproduciendo la animación de salto
                if (KCC.IsGrounded &&
                    !_jumpRequested &&
                    !_jumpTimer.IsRunning &&
                    !_isJumping &&
                    !_isPlayingJumpAnimation)
                {
                    _jumpRequested = true;
                }
                // Si alguna de las condiciones falla, ignorar completamente la solicitud
            }

            MovePlayer(desiredMoveVelocity, 0f);

            if (input.Buttons.WasPressed(previousButtons, EInputButton.Fire))
            {
                TryFire();
            }
        }

        private void MovePlayer(Vector3 desiredMoveVelocity, float jumpImpulse)
        {
            KCC.SetGravity(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity);

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
            {
                acceleration = KCC.IsGrounded ? GroundDeceleration : AirDeceleration;
            }
            else
            {
                acceleration = KCC.IsGrounded ? GroundAcceleration : AirAcceleration;
            }

            _moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

            KCC.Move(_moveVelocity, jumpImpulse);
        }

        private void TryFire()
        {
            // Verificar si puede disparar
            if (IsReloading)
            {
                Debug.Log("[Player] No se puede disparar mientras se recarga");
                return;
            }

            if (CurrentAmmo <= 0)
            {
                Debug.Log("[Player] Sin munición, iniciando recarga automática");
                return; // La recarga automática se maneja en FixedUpdateNetwork
            }

            if (_fireRateTimer.IsRunning && !_fireRateTimer.Expired(Runner))
            {
                Debug.Log("[Player] Debe esperar entre disparos (Fire Rate)");
                return;
            }

            // Disparar
            Fire();
            
            // Decrementar munición
            CurrentAmmo--;
            
            // Establecer cooldown de disparo
            _fireRateTimer = TickTimer.CreateFromSeconds(Runner, FireRate);
        }

        private void StartReload()
        {
            IsReloading = true;
            _reloadTimer = TickTimer.CreateFromSeconds(Runner, ReloadTime);
            
            // Reproducir sonido de recarga (solo en el cliente local)
            if (HasInputAuthority && _reloadAudioSource != null && ReloadAudioClip != null)
            {
                _reloadAudioSource.PlayOneShot(ReloadAudioClip);
            }
            
            Debug.Log($"[Player] Iniciando recarga - Duración: {ReloadTime}s");
        }

        private void Fire()
        {
            _hitPosition = Vector3.zero;

            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;
            if (Runner.LagCompensation.Raycast(CameraHandle.position, CameraHandle.forward, 200f,
                    Object.InputAuthority, out var hit, HitMask, hitOptions, QueryTriggerInteraction.Ignore) == true)
            {
                var health = hit.Hitbox != null ? hit.Hitbox.Root.GetComponent<Health>() : null;
                if (health != null && health.TakeHit(1))
                {
                    if (health.IsAlive == false)
                    {
                        ChickenKills += health.GetComponent<Chicken>() != null ? 1 : -10;
                    }
                }
                _hitPosition = hit.Point;
                _hitNormal = hit.Normal;
            }
            _fireCount++;
        }

        private void ShowFireEffects()
        {
            if (_visibleFireCount < _fireCount)
            {
                FireSound.PlayOneShot(FireSound.clip);
                MuzzleParticle.Play();
                Animator.SetTrigger(_animIDShoot);

                if (_hitPosition != Vector3.zero)
                {
                    Instantiate(ImpactPrefab, _hitPosition, Quaternion.LookRotation(_hitNormal));
                }
            }

            _visibleFireCount = _fireCount;
        }

        private void OnCurrentAmmoChangedCallback()
        {
            // Este método se ejecuta cuando CurrentAmmo cambia (NetworkBehaviour callback)
            Debug.Log($"[Player] Munición actualizada: {CurrentAmmo}/{MaxAmmoPerClip}");
            
            // Disparar el UnityEvent solo para el jugador local
            if (HasInputAuthority)
            {
                OnAmmoChanged?.Invoke(CurrentAmmo);
            }
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeedX = Animator.StringToHash("SpeedX");
            _animIDSpeedZ = Animator.StringToHash("SpeedZ");
            _animIDSpeed = Animator.StringToHash("Speed");
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
        }

        private void OnNicknameChanged()
        {
            if (HasInputAuthority)
                return;

            Nameplate.SetNickname(Nickname);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetNickname(string nickname)
        {
            Nickname = nickname;
        }
    }
}