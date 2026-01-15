using Fusion;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace Starter.Shooter
{
	public class Health : NetworkBehaviour
	{
		[Header("Setup")]
		public int InitialHealth = 3;
		public float DeathTime;

		[Header("References")]
		public Transform ScalingRoot;
		public GameObject VisualRoot;
		public GameObject DeathRoot;

		[Header("Hit Visual Effect")]
		[Tooltip("Color del tinte cuando recibe daño")]
		public Color HitTintColor = new Color(1f, 0.3f, 0.3f, 1f);
		
		[Tooltip("Duración del efecto de tinte en segundos")]
		public float HitTintDuration = 0.3f;

		[Header("Health Events")]
		[Tooltip("Evento que se dispara cuando cambia la vida (parámetros: currentHealth, maxHealth, healthPercentage)")]
		public UnityEvent<int, int, int> OnHealthChanged;

		public bool IsAlive => CurrentHealth > 0;
		public bool IsFinished => IsAlive == false && _deathCooldown.Expired(Runner);

		[Networked, HideInInspector, OnChangedRender(nameof(OnCurrentHealthChangedCallback))]
		public int CurrentHealth { get; set; }
		[Networked]
		public NetworkBool IsInvulnerable { get; set; }
		[Networked]
		private TickTimer _deathCooldown { get; set; }

		private List<Renderer> _renderers = new List<Renderer>();
		private List<Material[]> _originalMaterials = new List<Material[]>();
		private float _hitTintTimer = 0f;
		private bool _isTinted = false;

		public bool TakeHit(int damage)
		{
			if (IsAlive == false)
				return false;
			
			if (IsInvulnerable)
				return false;

			CurrentHealth -= damage;

			if (IsAlive == false)
			{
				CurrentHealth = 0;
				_deathCooldown = TickTimer.CreateFromSeconds(Runner, DeathTime);
			}

			return true;
		}

		public void Revive()
		{
			CurrentHealth = InitialHealth;
			_deathCooldown = default;
		}

		public override void Spawned()
		{
			if (HasStateAuthority)
			{
				CurrentHealth = InitialHealth;
			}

			InitializeHitTintSystem();
		}

		public override void Render()
		{
			var interpolator = new NetworkBehaviourBufferInterpolator(this);
			bool isAlive = interpolator.Int(nameof(CurrentHealth)) > 0;

			VisualRoot.SetActive(isAlive);
			DeathRoot.SetActive(isAlive == false);

			UpdateHitTint();
		}

		private void InitializeHitTintSystem()
		{
			_renderers.Clear();
			_originalMaterials.Clear();

			if (VisualRoot != null)
			{
				Renderer[] allRenderers = VisualRoot.GetComponentsInChildren<Renderer>(true);
				
				foreach (Renderer renderer in allRenderers)
				{
					if (renderer.gameObject.layer == LayerMask.NameToLayer("FirstPersonOverlay"))
						continue;
					
					_renderers.Add(renderer);
					
					Material[] originalMats = new Material[renderer.materials.Length];
					for (int i = 0; i < renderer.materials.Length; i++)
					{
						originalMats[i] = new Material(renderer.materials[i]);
					}
					_originalMaterials.Add(originalMats);
					
					renderer.materials = originalMats;
				}
			}
		}

		private void UpdateHitTint()
		{
			if (!_isTinted)
				return;

			_hitTintTimer -= Time.deltaTime;

			if (_hitTintTimer <= 0f)
			{
				_isTinted = false;
				RestoreOriginalColors();
			}
			else
			{
				float t = _hitTintTimer / HitTintDuration;
				ApplyTint(Color.Lerp(Color.white, HitTintColor, t));
			}
		}

		private void ApplyTint(Color tintColor)
		{
			for (int i = 0; i < _renderers.Count; i++)
			{
				if (_renderers[i] == null)
					continue;

				Material[] materials = _renderers[i].materials;
				for (int j = 0; j < materials.Length; j++)
				{
					if (materials[j].HasProperty("_Color"))
					{
						Color originalColor = _originalMaterials[i][j].color;
						materials[j].color = originalColor * tintColor;
					}
					
					if (materials[j].HasProperty("_BaseColor"))
					{
						Color originalColor = _originalMaterials[i][j].GetColor("_BaseColor");
						materials[j].SetColor("_BaseColor", originalColor * tintColor);
					}
				}
			}
		}

		private void RestoreOriginalColors()
		{
			for (int i = 0; i < _renderers.Count; i++)
			{
				if (_renderers[i] == null)
					continue;

				Material[] materials = _renderers[i].materials;
				for (int j = 0; j < materials.Length; j++)
				{
					if (materials[j].HasProperty("_Color"))
					{
						materials[j].color = _originalMaterials[i][j].color;
					}
					
					if (materials[j].HasProperty("_BaseColor"))
					{
						materials[j].SetColor("_BaseColor", _originalMaterials[i][j].GetColor("_BaseColor"));
					}
				}
			}
		}

		private void StartHitTint()
		{
			_isTinted = true;
			_hitTintTimer = HitTintDuration;
			ApplyTint(HitTintColor);
		}

		public int GetHealthPercentage()
		{
			if (InitialHealth <= 0)
				return 0;
			
			float percentage = ((float)CurrentHealth / (float)InitialHealth) * 100f;
			return Mathf.RoundToInt(percentage);
		}

		private void OnCurrentHealthChangedCallback()
		{
			if (HasInputAuthority)
			{
				int healthPercentage = GetHealthPercentage();
				OnHealthChanged?.Invoke(CurrentHealth, InitialHealth, healthPercentage);
			}

			if (CurrentHealth <= 0)
			{
				return;
			}

			if (HasInputAuthority == false && ScalingRoot != null)
			{
				ScalingRoot.localScale = new Vector3(0.85f, 1.15f, 0.85f);
				StartHitTint();
			}
		}

		private void OnDestroy()
		{
			foreach (var materials in _originalMaterials)
			{
				if (materials != null)
				{
					foreach (var mat in materials)
					{
						if (mat != null)
							Destroy(mat);
					}
				}
			}
			
			_originalMaterials.Clear();
			_renderers.Clear();
		}
	}
}
