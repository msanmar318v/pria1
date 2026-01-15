using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System;

namespace Starter.Shooter
{
	public sealed class GameManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
	{
		public Player PlayerPrefab;

		[Networked]
		public PlayerRef BestHunter { get; set; }
		
		[Networked, Capacity(24), OnChangedRender(nameof(OnBestHunterDataChanged))]
		public string BestHunterNickname { get; set; }
		
		[Networked, OnChangedRender(nameof(OnBestHunterDataChanged))]
		public int BestHunterKills { get; set; }
		
		public Player LocalPlayer { get; private set; }

		public event Action<string, int> OnBestHunterChanged;

		private List<Player> _players = new(32);
		private SpawnPoint[] _spawnPoints;

		public override void Spawned()
		{
			_spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
			
			if (HasStateAuthority)
			{
				BestHunterNickname = string.Empty;
				BestHunterKills = 0;
			}
		}

		public override void FixedUpdateNetwork()
		{
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
					player.Health.TakeHit(1000);
				}

				if (player.Health.IsFinished)
				{
					player.Respawn(GetSpawnPosition());
				}

				if (player.Health.IsAlive && player.PlayerKills > bestHunterKills)
				{
					bestHunterKills = player.PlayerKills;
					BestHunter = player.Object.InputAuthority;
					bestHunterPlayer = player;
				}
			}

			if (bestHunterPlayer != null && bestHunterKills > 0)
			{
				BestHunterNickname = bestHunterPlayer.Nickname;
				BestHunterKills = bestHunterKills;
			}
			else
			{
				BestHunterNickname = string.Empty;
				BestHunterKills = 0;
			}
		}

		private void OnBestHunterDataChanged()
		{
			OnBestHunterChanged?.Invoke(BestHunterNickname, BestHunterKills);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			LocalPlayer = null;
		}

		public override void Render()
		{
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

			_players.Add(player);
		}

		public void PlayerLeft(PlayerRef playerRef)
		{
			if (HasStateAuthority == false)
				return;

			int index = _players.FindIndex(t => t.Object.InputAuthority == playerRef);
			if (index >= 0)
			{
				_players[index].ResetPlayerKills();
				
				Runner.Despawn(_players[index].Object);
				_players.RemoveAt(index);
			}
			
			for (int i = 0; i < _players.Count; i++)
			{
				if (_players[i] != null)
				{
					_players[i].ResetPlayerKills();
				}
			}

			BestHunter = PlayerRef.None;
			BestHunterNickname = string.Empty;
			BestHunterKills = 0;
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
