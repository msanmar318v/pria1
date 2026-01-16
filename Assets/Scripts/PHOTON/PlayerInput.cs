using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
	public enum EInputButton
	{
		Jump,
		Fire,
		Dash,
	}

	public struct GameplayInput : INetworkInput
	{
		public Vector2 LookRotation;
		public Vector2 MoveDirection;
		public NetworkButtons Buttons;
	}

	public sealed class PlayerInput : NetworkBehaviour, IBeforeUpdate, IAfterTick
	{
		[Networked]
		public NetworkButtons PreviousButtons { get; private set; }
		public Vector2 LookRotation => _input.LookRotation;

		private GameplayInput _input;

		public override void Spawned()
		{
			if (HasInputAuthority == false)
				return;

			var networkEvents = Runner.GetComponent<NetworkEvents>();
			networkEvents.OnInput.AddListener(OnInput);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (runner == null)
				return;

			var networkEvents = runner.GetComponent<NetworkEvents>();
			if (networkEvents != null)
			{
				networkEvents.OnInput.RemoveListener(OnInput);
			}
		}

		public void ResetLookRotation()
		{
			_input.LookRotation = Vector2.zero;
		}

		void IBeforeUpdate.BeforeUpdate()
		{
			if (HasInputAuthority == false)
				return;

			// NUEVO: Verificar si el juego ha terminado
			var gameManager = FindFirstObjectByType<GameManager>();
			bool isGameOver = gameManager != null && gameManager.IsGameOver;

			// Si el juego ha terminado o el cursor no está bloqueado, no procesar input
			if (isGameOver || Cursor.lockState != CursorLockMode.Locked)
			{
				_input.MoveDirection = default;
				// NUEVO: No actualizar la rotación de la cámara
				return;
			}

			_input.LookRotation += new Vector2(Input.GetAxisRaw("Mouse X"), -Input.GetAxisRaw("Mouse Y"));
			_input.LookRotation.y = Mathf.Clamp(_input.LookRotation.y, -90f, 90f);

			var moveDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
			_input.MoveDirection = moveDirection.normalized;

			_input.Buttons.Set(EInputButton.Fire, Input.GetButton("Fire1"));
			_input.Buttons.Set(EInputButton.Jump, Input.GetButton("Jump"));
			
			_input.Buttons.Set(EInputButton.Dash, Input.GetKey(KeyCode.LeftShift));
		}

		void IAfterTick.AfterTick()
		{
			if (GetInput(out GameplayInput input))
			{
				PreviousButtons = input.Buttons;
			}
		}

		private void OnInput(NetworkRunner runner, NetworkInput networkInput)
		{
			networkInput.Set(_input);
		}
	}
}
