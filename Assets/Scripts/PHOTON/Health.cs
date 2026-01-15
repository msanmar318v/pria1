using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace Starter.Shooter
{
	/// <summary>
	/// A common component that represents entity health.
	/// It is used for both players and chickens.
	/// </summary>
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
		public Color HitTintColor = new Color(1f, 0.3f, 0.3f, 1f); // Rojo
		
		[Tooltip("Duración del efecto de tinte en segundos")]
		public float HitTintDuration = 0.3f;

		public bool IsAlive => CurrentHealth > 0;
		public bool IsFinished => IsAlive == false && _deathCooldown.Expired(Runner);

		[Networked, HideInInspector, OnChangedRender(nameof(OnCurrentHealthChanged))]
		public int CurrentHealth { get; set; }

		[Networked]
		private TickTimer _deathCooldown { get; set; }

		// Sistema de tinte de color
		private List<Renderer> _renderers = new List<Renderer>();
		private List<Material[]> _originalMaterials = new List<Material[]>();
		private float _hitTintTimer = 0f;
		private bool _isTinted = false;

		public bool TakeHit(int damage)
		{
			if (IsAlive == false)
				return false;

			CurrentHealth -= damage;

			if (IsAlive == false)
			{
				// Entity died, let's start death cooldown
				CurrentHealth = 0;
				_deathCooldown = TickTimer.CreateFromSeconds(Runner,  DeathTime);
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
				// Set initial health
				CurrentHealth = InitialHealth;
			}

			// Inicializar sistema de tinte
			InitializeHitTintSystem();
		}

		public override void Render()
		{
			// Use interpolated value when checking if entity is alive in Render.
			// This will ensure that death effects are played AFTER the death was "confirmed"
			// on the server in case of mispredictions (e.g. lost fire input) and also helps
			// with showing player visual at the correct position right away after respawn
			// (= player won't be visible before KCC teleport that is interpolated as well).
			var interpolator = new NetworkBehaviourBufferInterpolator(this);
			bool isAlive = interpolator.Int(nameof(CurrentHealth)) > 0;

			VisualRoot.SetActive(isAlive);
			DeathRoot.SetActive(isAlive == false);

			// Actualizar el efecto de tinte
			UpdateHitTint();
		}

		/// <summary>
		/// Inicializa el sistema de tinte capturando todos los renderers y sus materiales originales
		/// </summary>
		private void InitializeHitTintSystem()
		{
			_renderers.Clear();
			_originalMaterials.Clear();

			// Obtener todos los renderers del VisualRoot (excluyendo los que no queremos tintar)
			if (VisualRoot != null)
			{
				Renderer[] allRenderers = VisualRoot.GetComponentsInChildren<Renderer>(true);
				
				foreach (Renderer renderer in allRenderers)
				{
					// Filtrar renderers específicos si es necesario (ej: partículas, UI, etc.)
					if (renderer.gameObject.layer == LayerMask.NameToLayer("FirstPersonOverlay"))
						continue; // Saltar renderers de primera persona
					
					_renderers.Add(renderer);
					
					// Guardar los materiales originales (crear copias para no modificar los assets)
					Material[] originalMats = new Material[renderer.materials.Length];
					for (int i = 0; i < renderer.materials.Length; i++)
					{
						// Crear una copia del material para no modificar el asset original
						originalMats[i] = new Material(renderer.materials[i]);
					}
					_originalMaterials.Add(originalMats);
					
					// Asignar las copias al renderer
					renderer.materials = originalMats;
				}
			}
		}

		/// <summary>
		/// Actualiza el efecto de tinte interpolando entre el color de hit y el color original
		/// </summary>
		private void UpdateHitTint()
		{
			if (!_isTinted)
				return;

			// Decrementar el timer
			_hitTintTimer -= Time.deltaTime;

			if (_hitTintTimer <= 0f)
			{
				// Efecto terminado, restaurar colores originales
				_isTinted = false;
				RestoreOriginalColors();
			}
			else
			{
				// Interpolar el color de vuelta al original
				float t = _hitTintTimer / HitTintDuration;
				ApplyTint(Color.Lerp(Color.white, HitTintColor, t));
			}
		}

		/// <summary>
		/// Aplica un tinte de color a todos los materiales
		/// </summary>
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
						// Obtener el color original
						Color originalColor = _originalMaterials[i][j].color;
						// Aplicar el tinte multiplicando por el color de hit
						materials[j].color = originalColor * tintColor;
					}
					
					// Si el material usa el shader estándar o URP Lit, también modificar _BaseColor
					if (materials[j].HasProperty("_BaseColor"))
					{
						Color originalColor = _originalMaterials[i][j].GetColor("_BaseColor");
						materials[j].SetColor("_BaseColor", originalColor * tintColor);
					}
				}
			}
		}

		/// <summary>
		/// Restaura los colores originales de todos los materiales
		/// </summary>
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

		/// <summary>
		/// Inicia el efecto de tinte rojo
		/// </summary>
		private void StartHitTint()
		{
			_isTinted = true;
			_hitTintTimer = HitTintDuration;
			ApplyTint(HitTintColor);
		}

		private void OnCurrentHealthChanged()
		{
			if (CurrentHealth <= 0)
				return; // Just health reset

			if (HasInputAuthority == false && ScalingRoot != null)
			{
				// Show hit reaction by simple scale. Scaling root
				// scale is lerped back to one in the Player script.
				ScalingRoot.localScale = new Vector3(0.85f, 1.15f, 0.85f);
				
				// Aplicar efecto de tinte rojo
				StartHitTint();
			}
		}

		private void OnDestroy()
		{
			// Limpiar las copias de materiales para evitar memory leaks
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
