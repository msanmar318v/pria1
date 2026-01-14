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
        private int _animIDSpeed;

        private int _visibleFireCount;

        private Quaternion[] _spineAnimatorRotations;

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
        }

        public override void FixedUpdateNetwork()
        {
            if (Health.IsAlive && GetInput<GameplayInput>(out var input))
            {
                ProcessInput(input, Input.PreviousButtons);
            }
            else
            {
                MovePlayer(Vector3.zero, 0f);
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
            HitboxRoot.HitboxRootActive = Health.IsAlive;
            KCC.SetActive(Health.IsAlive);
        }

        public override void Render()
        {
            if (HasInputAuthority)
            {
                KCC.SetLookRotation(Input.LookRotation, -90f, 90f);
            }
            var moveSpeed = transform.InverseTransformVector(KCC.RealVelocity);
            float totalSpeed = new Vector2(moveSpeed.x, moveSpeed.z).magnitude;

            Animator.SetFloat(_animIDSpeedX, moveSpeed.x);
            Animator.SetFloat(_animIDSpeedZ, moveSpeed.z);
            Animator.SetFloat(_animIDSpeed, totalSpeed);
            Animator.SetBool(_animIDGrounded, KCC.IsGrounded);

            Animator.SetFloat(_animIDPitch, KCC.GetLookRotation(true, false).x, 0.01f, Time.deltaTime);
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
            if (Health.IsAlive == false)
                return;

            CaptureAnimatorRotations();

            var pitchRotation = KCC.GetLookRotation(true, false);

            ApplySpineIK(pitchRotation.x);

            UpdateCameraPivotTransform(pitchRotation);

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
            KCC.SetLookRotation(input.LookRotation, -90f, 90f);
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
                Fire();
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