using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine.Rendering;
using UnityEngine.Events;

namespace Starter.Shooter
{
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
        [Tooltip("Delay en segundos antes de aplicar el impulso de salto")]
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
        [Tooltip("Evento que se dispara cuando cambia la munición")]
        public UnityEvent<int> OnAmmoChanged;

        [Header("Player Kills Events")]
        [Tooltip("Evento que se dispara cuando el jugador consigue una kill")]
        public UnityEvent<int> OnPlayerKillsChanged;

        [Header("Dash Setup")]
        [Tooltip("Velocidad del dash")]
        public float DashSpeed = 10f;
        [Tooltip("Duración del dash en segundos")]
        public float DashDuration = 0.25f;
        [Tooltip("Tiempo de cooldown entre dashes en segundos")]
        public float DashCooldown = 1.0f;

        [Header("Animation Setup")]
        public Transform ChestTargetPosition;
        public Transform ChestBone;
        [Tooltip("Asigna todos los huesos de la columna desde Spine1 hasta Spine5")]
        public Transform[] SpineBones;
        [Tooltip("Cuánto afecta la rotación de la cámara a cada hueso")]
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
        [Tooltip("Umbral mínimo de movimiento vertical para que la cámara siga")]
        public float VerticalMovementThreshold = 0.02f;
        [Tooltip("Reducción de movimientos horizontales")]
        [Range(0f, 1f)]
        public float HorizontalDampingStrength = 0.1f;
        [Tooltip("Velocidad de suavizado solo para oscilaciones pequeñas")]
        public float OscillationSmoothSpeed = 20f;

        [Header("Respawn Settings")]
        [Tooltip("Frames a esperar después del respawn antes de reactivar IK")]
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
        [Networked, HideInInspector, OnChangedRender(nameof(OnPlayerKillsChangedCallback))]
        public int PlayerKills { get; set; }

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
        
        // DASH: Variables de red para el dash
        [Networked]
        private float _dashTimer { get; set; }
        [Networked]
        private float _dashCooldownTimer { get; set; }
        [Networked]
        private NetworkBool _isInvulnerable { get; set; }

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
        private bool _isRespawning;
        private int _respawnFrameCounter;
        private AudioSource _reloadAudioSource;

        public void Respawn(Vector3 position)
        {
            ChickenKills = 0;
            Health.Revive();

            KCC.SetActive(true);
            KCC.SetPosition(position);
            
            transform.rotation = Quaternion.identity;
            KCC.SetLookRotation(0f, 0f);
            
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
            
            CurrentAmmo = MaxAmmoPerClip;
            IsReloading = false;
            _fireRateTimer = TickTimer.None;
            _reloadTimer = TickTimer.None;
            
            // DASH: Resetear variables del dash
            _dashTimer = 0f;
            _dashCooldownTimer = 0f;
            _isInvulnerable = false;
            
            ResetHUDElements();
            
            _isRespawning = true;
            _respawnFrameCounter = 0;
            
            if (Animator != null)
            {
                Animator.SetFloat(_animIDPitch, 0f);
                Animator.Update(0f);
            }
            
            ResetSpineRotations();
            
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

        private void ResetHUDElements()
        {
            if (!HasInputAuthority)
                return;

            OnAmmoChanged?.Invoke(MaxAmmoPerClip);
            
            if (Health != null)
            {
                int healthPercentage = Health.GetHealthPercentage();
                Health.OnHealthChanged?.Invoke(Health.CurrentHealth, Health.InitialHealth, healthPercentage);
            }
        }

        private void ResetSpineRotations()
        {
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
                CurrentAmmo = MaxAmmoPerClip;
                IsReloading = false;
                PlayerKills = 0;
                
                // DASH: Inicializar variables del dash
                _dashTimer = 0f;
                _dashCooldownTimer = 0f;
                _isInvulnerable = false;
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

            if (ReloadAudioClip != null)
            {
                _reloadAudioSource = gameObject.AddComponent<AudioSource>();
                _reloadAudioSource.clip = ReloadAudioClip;
                _reloadAudioSource.playOnAwake = false;
                _reloadAudioSource.spatialBlend = 1f;
                _reloadAudioSource.minDistance = 1f;
                _reloadAudioSource.maxDistance = 20f;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // DASH: Actualizar timers del dash
            if (_dashTimer > 0f)
            {
                _dashTimer -= Runner.DeltaTime;
                if (_dashTimer <= 0f)
                {
                    _dashTimer = 0f;
                    _isInvulnerable = false;
                    
                    if (Health != null)
                    {
                        Health.IsInvulnerable = false;
                    }
                }
            }

            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= Runner.DeltaTime;
                if (_dashCooldownTimer < 0f)
                    _dashCooldownTimer = 0f;
            }

            if (Health.IsAlive && Health.CurrentHealth > 0 && GetInput<GameplayInput>(out var input))
            {
                ProcessInput(input, Input.PreviousButtons);
            }
            else
            {
                MovePlayer(Vector3.zero, 0f);
            }

            if (IsReloading)
            {
                if (_reloadTimer.Expired(Runner))
                {
                    CurrentAmmo = MaxAmmoPerClip;
                    IsReloading = false;
                    _reloadTimer = TickTimer.None;
                }
            }
            else
            {
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
            if (HasInputAuthority && Health.CurrentHealth > 0)
            {
                KCC.SetLookRotation(new Vector2(Input.LookRotation.y, Input.LookRotation.x), -90f, 90f);
            }
            
            var moveSpeed = transform.InverseTransformVector(KCC.RealVelocity);
            float totalSpeed = new Vector2(moveSpeed.x, moveSpeed.z).magnitude;

            Animator.SetFloat(_animIDSpeedX, moveSpeed.x);
            Animator.SetFloat(_animIDSpeedZ, moveSpeed.z);
            Animator.SetFloat(_animIDSpeed, totalSpeed);
            Animator.SetBool(_animIDGrounded, KCC.IsGrounded);

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
            if (Health.IsAlive == false || Health.CurrentHealth <= 0)
            {
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
                
                if (CameraPivot != null)
                {
                    CameraPivot.localRotation = Quaternion.identity;
                }
                
                if (HasInputAuthority)
                {
                    KCC.SetLookRotation(0f, 0f);
                }
                
                return;
            }

            if (_isRespawning)
            {
                _respawnFrameCounter++;
                
                if (_respawnFrameCounter >= RespawnSafetyFrames)
                {
                    _isRespawning = false;
                    _respawnFrameCounter = 0;
                    
                    if (Animator != null)
                    {
                        Animator.Update(0f);
                    }
                    CaptureAnimatorRotations();
                }
                
                if (CameraPivot != null)
                {
                    Quaternion neutralRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                    CameraPivot.rotation = neutralRotation;
                }
                
                return;
            }

            CaptureAnimatorRotations();

            var lookRotation = KCC.GetLookRotation(true, false);

            if (float.IsNaN(lookRotation.x) || float.IsNaN(lookRotation.y) ||
                Mathf.Abs(lookRotation.x) > 360f || Mathf.Abs(lookRotation.y) > 360f)
            {
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
            KCC.SetLookRotation(new Vector2(input.LookRotation.y, input.LookRotation.x), -90f, 90f);
            
            var moveDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * WalkSpeed;

            if (input.Buttons.WasPressed(previousButtons, EInputButton.Jump))
            {
                if (KCC.IsGrounded &&
                    !_jumpRequested &&
                    !_jumpTimer.IsRunning &&
                    !_isJumping &&
                    !_isPlayingJumpAnimation)
                {
                    _jumpRequested = true;
                }
            }

            // DASH: Detectar input del dash
            if (input.Buttons.WasPressed(previousButtons, EInputButton.Dash))
            {
                TryStartDash(moveDirection);
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

            // DASH: Si estamos en dash, mantener la velocidad del dash
            if (_dashTimer > 0f)
            {
                KCC.Move(_moveVelocity, jumpImpulse: 0f);
                return;
            }

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

        // DASH: Método para iniciar el dash
        private void TryStartDash(Vector3 moveDirection)
        {
            // No puedes hacer dash si estás ya dashing, en cooldown o muerto
            if (_dashTimer > 0f || _dashCooldownTimer > 0f || Health.IsAlive == false)
                return;

            // Si no hay dirección de movimiento, dashear hacia adelante
            if (moveDirection.sqrMagnitude < 0.01f)
            {
                moveDirection = KCC.TransformRotation * Vector3.forward;
            }

            moveDirection.Normalize();

            // Activar dash
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown + DashDuration;
            _isInvulnerable = true;
            
            // Sincronizar invulnerabilidad con Health
            if (Health != null)
            {
                Health.IsInvulnerable = true;
            }

            // Aplicar velocidad de dash
            _moveVelocity = moveDirection * DashSpeed;
        }

        private void TryFire()
        {
            if (IsReloading || CurrentAmmo <= 0)
                return;

            if (_fireRateTimer.IsRunning && !_fireRateTimer.Expired(Runner))
                return;

            Fire();
            CurrentAmmo--;
            _fireRateTimer = TickTimer.CreateFromSeconds(Runner, FireRate);
        }

        private void StartReload()
        {
            IsReloading = true;
            _reloadTimer = TickTimer.CreateFromSeconds(Runner, ReloadTime);
            
            if (HasInputAuthority && _reloadAudioSource != null && ReloadAudioClip != null)
            {
                _reloadAudioSource.PlayOneShot(ReloadAudioClip);
            }
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
                        var targetPlayer = health.GetComponent<Player>();
                        
                        if (targetPlayer != null)
                        {
                            PlayerKills++;
                        }
                        else
                        {
                            ChickenKills += health.GetComponent<Chicken>() != null ? 1 : 0;
                        }
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
            if (HasInputAuthority)
            {
                OnAmmoChanged?.Invoke(CurrentAmmo);
            }
        }

        private void OnPlayerKillsChangedCallback()
        {
            if (HasInputAuthority)
            {
                OnPlayerKillsChanged?.Invoke(PlayerKills);
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

        public void ResetPlayerKills()
        {
            if (HasStateAuthority)
            {
                PlayerKills = 0;
            }
        }
    }
}