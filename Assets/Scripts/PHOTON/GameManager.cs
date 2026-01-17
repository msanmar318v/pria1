using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System;
using System.Linq;

namespace Starter.Shooter
{
	public sealed class GameManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
	{
		[System.Serializable]
		public struct PlayerData : INetworkStruct
		{
			[Networked, Capacity(24)]
			public string Nickname { get; set; }
			
			[Networked]
			public int Kills { get; set; }
			
			[Networked]
			public PlayerRef PlayerRef { get; set; }
		}

		public Player PlayerPrefab;

		[Networked]
		public PlayerRef BestHunter { get; set; }
		
		[Networked, Capacity(24), OnChangedRender(nameof(OnBestHunterDataChanged))]
		public string BestHunterNickname { get; set; }
		
		[Networked, OnChangedRender(nameof(OnBestHunterDataChanged))]
		public int BestHunterKills { get; set; }
		
		[Networked, OnChangedRender(nameof(OnGameOverStateChanged))]
		public NetworkBool IsGameOver { get; set; }
		
		[Networked, Capacity(24)]
		public NetworkArray<PlayerData> NetworkedPlayerData => default;
		
		[Networked]
		public int PlayerCount { get; set; }
		
		public Player LocalPlayer { get; private set; }

		public event Action<string, int> OnBestHunterChanged;
		public event Action<string> OnGameOver;

		private List<Player> _players = new(32);
		private SpawnPoint[] _spawnPoints;
		private const int KILLS_TO_WIN = 10;

		public override void Spawned()
		{
			_spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
			
			if (HasStateAuthority)
			{
				BestHunterNickname = string.Empty;
				BestHunterKills = 0;
				IsGameOver = false;
				PlayerCount = 0;
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (!HasStateAuthority)
				return;

			if (IsGameOver)
				return;

			BestHunter = PlayerRef.None;
			int bestHunterKills = 0;
			Player bestHunterPlayer = null;

			PlayerCount = _players.Count;
			for (int i = 0; i < _players.Count && i < NetworkedPlayerData.Length; i++)
			{
				var player = _players[i];
				if (player != null && player.Object != null && player.Object.IsValid)
				{
					var playerData = new PlayerData
					{
						Nickname = player.Nickname,
						Kills = player.PlayerKills,
						PlayerRef = player.Object.InputAuthority
					};
					NetworkedPlayerData.Set(i, playerData);
				}
			}

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

				if (player.PlayerKills >= KILLS_TO_WIN && !IsGameOver)
				{
					IsGameOver = true;
					BestHunterNickname = player.Nickname;
					BestHunterKills = player.PlayerKills;
					return;
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

		private void OnGameOverStateChanged()
		{
			if (IsGameOver)
			{
				OnGameOver?.Invoke(BestHunterNickname);
			}
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

		[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
		public void RPC_RestartGame()
		{
			var playSceneUI = FindFirstObjectByType<PlaySceneUIMannager>();
			if (playSceneUI != null)
			{
				playSceneUI.HideGameOverPanel();
			}

			if (HasStateAuthority)
			{
				RestartGameInternal();
			}
		}

		private void RestartGameInternal()
		{
			if (!HasStateAuthority)
				return;

			IsGameOver = false;
			BestHunter = PlayerRef.None;
			BestHunterNickname = string.Empty;
			BestHunterKills = 0;

			for (int i = 0; i < _players.Count; i++)
			{
				var player = _players[i];
				if (player != null && player.Object != null && player.Object.IsValid)
				{
					player.ResetPlayerKills();
					player.Respawn(GetSpawnPosition());
				}
			}
		}

		public List<Player> GetAllPlayers()
		{
			return new List<Player>(_players);
		}

		public List<(string nickname, int kills)> GetNetworkedPlayerData()
		{
			var playerDataList = new List<(string nickname, int kills)>();
			
			for (int i = 0; i < PlayerCount && i < NetworkedPlayerData.Length; i++)
			{
				var data = NetworkedPlayerData[i];
				if (!string.IsNullOrEmpty(data.Nickname))
				{
					playerDataList.Add((data.Nickname, data.Kills));
				}
			}
			
			return playerDataList;
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
