using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starter
{
	public class UIGameMenu : MonoBehaviour
	{
		[Header("Configuración de Inicio")]
		[Tooltip("Especifica a qué modo de juego debe unirse el jugador")]
		public string GameModeIdentifier;
		public NetworkRunner RunnerPrefab;
		public int MaxPlayerCount = 8;

		[Header("Depuración")]
		[Tooltip("Para propósitos de depuración es posible forzar un juego individual")]
		public bool ForceSinglePlayer;

		[Header("Configuración de UI")]
		public CanvasGroup PanelGroup;
		public TMP_InputField RoomText;
		public TMP_InputField NicknameText;
		public TextMeshProUGUI StatusText;
		public GameObject StartGroup;
		public GameObject DisconnectGroup;

		[Header("Host Detection Settings")]
		[Tooltip("Intervalo en segundos para verificar la conexión con el host")]
		public float HostCheckInterval = 1f;

		private NetworkRunner _runnerInstance;
		private static string _shutdownStatus;
		private static bool _isHostDisconnected;
		private float _hostCheckTimer;
		private bool _isCheckingHost;

		public async void StartGame()
		{
			await Disconnect();

			PlayerPrefs.SetString("PlayerName", NicknameText.text);

			_runnerInstance = Instantiate(RunnerPrefab);

			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.AddListener(OnShutdown);

			var sceneInfo = new NetworkSceneInfo();
			sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

			var startArguments = new StartGameArgs()
			{
				GameMode = Application.isEditor && ForceSinglePlayer ? GameMode.Single : GameMode.AutoHostOrClient,
				SessionName = RoomText.text,
				PlayerCount = MaxPlayerCount,
				SessionProperties = new Dictionary<string, SessionProperty> {["GameMode"] = GameModeIdentifier},
				Scene = sceneInfo,
			};

			StatusText.text = startArguments.GameMode == GameMode.Single ? "Iniciando partida individual..." : "Conectando...";

			var startTask = _runnerInstance.StartGame(startArguments);
			await startTask;

			if (startTask.Result.Ok)
			{
				StatusText.text = "";
				PanelGroup.gameObject.SetActive(false);
				_isCheckingHost = true;
			}
			else
			{
				StatusText.text = $"Error de conexión: {startTask.Result.ShutdownReason}";
			}
		}

		public async void DisconnectClicked()
		{
			await Disconnect();
		}

		public async void BackToMenu()
		{
			await Disconnect();
			SceneManager.LoadScene(0);
		}

		public void TogglePanelVisibility()
		{
			if (PanelGroup.gameObject.activeSelf && _runnerInstance == null)
				return;

			PanelGroup.gameObject.SetActive(!PanelGroup.gameObject.activeSelf);
		}

		private void OnEnable()
		{
			Application.targetFrameRate = 60;

			var nickname = PlayerPrefs.GetString("PlayerName");
			if (string.IsNullOrEmpty(nickname))
			{
				nickname = "Jugador" + Random.Range(10000, 100000);
			}

			NicknameText.text = nickname;

			if (_shutdownStatus != null)
			{
				StatusText.text = _shutdownStatus;
				
				if (_isHostDisconnected)
				{
					StatusText.color = Color.red;
				}
			}
			else
			{
				StatusText.text = string.Empty;
				StatusText.color = Color.white;
			}
			
			_shutdownStatus = null;
			_isHostDisconnected = false;
		}

		private void Update()
		{
			// Verificar si el panel de Game Over está activo antes de procesar ESC
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
			{
				// Buscar el PlaySceneUIManager para verificar si el Game Over está activo
				var playSceneUI = FindFirstObjectByType<PlaySceneUIMannager>();
				if (playSceneUI != null && playSceneUI.IsGameOverPanelActive)
				{
					// Si el panel de Game Over está activo, no abrir el menú de escape
					return;
				}

				TogglePanelVisibility();
			}

			if (PanelGroup.gameObject.activeSelf)
			{
				StartGroup.SetActive(_runnerInstance == null);
				DisconnectGroup.SetActive(_runnerInstance != null);
				RoomText.interactable = _runnerInstance == null;
				NicknameText.interactable = _runnerInstance == null;

				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
			else
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}

			CheckHostConnection();
		}

		private void CheckHostConnection()
		{
			if (!_isCheckingHost || _runnerInstance == null)
				return;

			_hostCheckTimer += Time.deltaTime;

			if (_hostCheckTimer >= HostCheckInterval)
			{
				_hostCheckTimer = 0f;

				if (_runnerInstance.IsRunning && !_runnerInstance.IsServer && !_runnerInstance.IsSharedModeMasterClient)
				{
					if (!_runnerInstance.IsConnectedToServer)
					{
						_isCheckingHost = false;
						HandleHostDisconnection();
					}
				}
			}
		}

		private void HandleHostDisconnection()
		{
			_shutdownStatus = "Host desconectado";
			_isHostDisconnected = true;
			
			if (_runnerInstance != null)
			{
				var events = _runnerInstance.GetComponent<NetworkEvents>();
				if (events != null)
				{
					events.OnShutdown.RemoveListener(OnShutdown);
				}
			}

			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		public async Task Disconnect()
		{
			if (_runnerInstance == null)
				return;

			_isCheckingHost = false;

			StatusText.text = "Desconectando...";
			StatusText.color = Color.white;
			PanelGroup.interactable = false;

			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.RemoveListener(OnShutdown);

			await _runnerInstance.Shutdown();
			_runnerInstance = null;

			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		private void OnShutdown(NetworkRunner runner, ShutdownReason reason)
		{
			_isCheckingHost = false;

			if (reason == ShutdownReason.GameClosed || 
			    reason == ShutdownReason.HostMigration || 
			    reason == ShutdownReason.ConnectionTimeout ||
			    reason == ShutdownReason.ServerInRoom ||
			    reason == ShutdownReason.DisconnectedByPluginLogic)
			{
				_shutdownStatus = "Host desconectado";
				_isHostDisconnected = true;
			}
			else if (reason == ShutdownReason.Ok)
			{
				_shutdownStatus = string.Empty;
				_isHostDisconnected = false;
			}
			else
			{
				_shutdownStatus = $"Desconexión: {reason}";
				_isHostDisconnected = false;
			}

			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		private void OnDestroy()
		{
			_isCheckingHost = false;
		}
	}
}
