using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System;

namespace Starter.Shooter
{
	/// <summary>
	/// Handles player connections (spawning of Player instances).
	/// </summary>
	public sealed class GameManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
	{
		public Player PlayerPrefab;

		[Networked]
		public PlayerRef BestHunter { get; set; }
		
		// NUEVO: Variables networked para sincronizar el mejor jugador en todos los clientes
		[Networked, Capacity(24), OnChangedRender(nameof(OnBestHunterDataChanged))]
		public string BestHunterNickname { get; set; }
		
		[Networked, OnChangedRender(nameof(OnBestHunterDataChanged))]
		public int BestHunterKills { get; set; }
		
		public Player LocalPlayer { get; private set; }

		// Evento para notificar cambios en el mejor jugador (ahora se dispara en TODOS los clientes)
		public event Action<string, int> OnBestHunterChanged;

		private List<Player> _players = new(32);
		private SpawnPoint[] _spawnPoints;

		public override void Spawned()
		{
			_spawnPoints = FindObjectsOfType<SpawnPoint>();
			
			// Inicializar valores networked
			if (HasStateAuthority)
			{
				BestHunterNickname = string.Empty;
				BestHunterKills = 0;
			}
		}

		public override void FixedUpdateNetwork()
		{
			// Solo el servidor calcula el mejor jugador
			if (!HasStateAuthority)
				return;

			BestHunter = PlayerRef.None;
			int bestHunterKills = 0;
			Player bestHunterPlayer = null;

			for (int i = 0; i < _players.Count; i++)
			{
				var player = _players[i];

				if (player.KCC.Position.y < -15f)
				{
					// Player fell, let's kill him
					player.Health.TakeHit(1000);
				}

				if (player.Health.IsFinished)
				{
					player.Respawn(GetSpawnPosition());
				}

				// Calculate the best hunter (ahora usa PlayerKills en lugar de ChickenKills)
				if (player.Health.IsAlive && player.PlayerKills > bestHunterKills)
				{
					bestHunterKills = player.PlayerKills;
					BestHunter = player.Object.InputAuthority;
					bestHunterPlayer = player;
				}
			}

			// Actualizar las variables networked (esto se sincroniza automáticamente a todos los clientes)
			if (bestHunterPlayer != null && bestHunterKills > 0)
			{
				// Hay un mejor jugador con al menos 1 kill
				BestHunterNickname = bestHunterPlayer.Nickname;
				BestHunterKills = bestHunterKills;
				Debug.Log($"[GameManager] (Server) Mejor jugador actualizado: {BestHunterNickname} ({BestHunterKills} kills)");
			}
			else
			{
				// No hay mejor jugador (0 kills o nadie)
				BestHunterNickname = string.Empty;
				BestHunterKills = 0;
				Debug.Log("[GameManager] (Server) No hay mejor jugador (reset)");
			}
		}

		/// <summary>
		/// Callback que se ejecuta en TODOS los clientes cuando cambian las variables networked del mejor jugador
		/// </summary>
		private void OnBestHunterDataChanged()
		{
			// Este método se ejecuta en TODOS los clientes (incluido el servidor)
			OnBestHunterChanged?.Invoke(BestHunterNickname, BestHunterKills);
			
			Debug.Log($"[GameManager] (Client) Mejor jugador actualizado en UI: {BestHunterNickname} ({BestHunterKills} kills)");
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			// Clear the reference because UI can try to access it even after despawn
			LocalPlayer = null;
		}

		public override void Render()
		{
			// Prepare LocalPlayer property that can be accessed from UI
			if (LocalPlayer == null || LocalPlayer.Object == null || LocalPlayer.Object.IsValid == false)
			{
				var playerObject = Runner.GetPlayerObject(Runner.LocalPlayer);
				LocalPlayer = playerObject != null ? playerObject.GetComponent<Player>() : null;
			}
		}

		public void PlayerJoined(PlayerRef playerRef)
		{
			if (HasStateAuthority == false)
				return;

			var player = Runner.Spawn(PlayerPrefab, GetSpawnPosition(), Quaternion.identity, playerRef);
			Runner.SetPlayerObject(playerRef, player.Object);

			// This list is state authority only,
			// so it is valid to have this list non-networked
			_players.Add(player);
		}

		public void PlayerLeft(PlayerRef playerRef)
		{
			if (HasStateAuthority == false)
				return;

			int index = _players.FindIndex(t => t.Object.InputAuthority == playerRef);
			if (index >= 0)
			{
				// Resetear kills del jugador que se desconecta
				_players[index].ResetPlayerKills();
				
				Runner.Despawn(_players[index].Object);
				_players.RemoveAt(index);
				
				Debug.Log($"[GameManager] Jugador {playerRef} desconectado");
			}
			
			// NUEVO: Resetear las kills de TODOS los jugadores restantes
			// Esto asegura que el juego 1v1 empiece de cero cuando alguien se desconecta
			for (int i = 0; i < _players.Count; i++)
			{
				if (_players[i] != null)
				{
					_players[i].ResetPlayerKills();
					Debug.Log($"[GameManager] Reseteando kills del jugador restante: {_players[i].Nickname}");
				}
			}

			// Resetear el mejor jugador (esto se sincronizará automáticamente a todos los clientes)
			BestHunter = PlayerRef.None;
			BestHunterNickname = string.Empty;
			BestHunterKills = 0;
			
			Debug.Log("[GameManager] Mejor jugador reseteado por desconexión");
		}

		private Vector3 GetSpawnPosition()
		{
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                return new Vector3(-22f, 1.5f, 0f);
            }

            var spawnPoint = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];
            var randomPositionOffset = UnityEngine.Random.insideUnitCircle * spawnPoint.Radius;
            return spawnPoint.transform.position + new Vector3(randomPositionOffset.x, 0f, randomPositionOffset.y);
		}
	}
}
