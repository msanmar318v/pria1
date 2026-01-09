using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starter
{
	/// <summary>
	/// Muestra el menú del juego, maneja la conexión/desconexión del jugador a la partida de red y el bloqueo del cursor.
	/// </summary>
	public class UIGameMenu : MonoBehaviour
	{
		[Header("Configuración de Inicio")]
		[Tooltip("Especifica a qué modo de juego debe unirse el jugador - ej. Platformer, ThirdPersonCharacter")]
		public string GameModeIdentifier;
		public NetworkRunner RunnerPrefab;
		public int MaxPlayerCount = 8;

		[Header("Depuración")]
		[Tooltip("Para propósitos de depuración es posible forzar un juego individual (inicia más rápido)")]
		public bool ForceSinglePlayer;

		[Header("Configuración de UI")]
		public CanvasGroup PanelGroup;
		public TMP_InputField RoomText;
		public TMP_InputField NicknameText;
		public TextMeshProUGUI StatusText;
		public GameObject StartGroup;
		public GameObject DisconnectGroup;

		private NetworkRunner _runnerInstance;
		private static string _shutdownStatus;

		public async void StartGame()
		{
			await Disconnect();

			PlayerPrefs.SetString("PlayerName", NicknameText.text);

			_runnerInstance = Instantiate(RunnerPrefab);

			// Añade un listener para desconexiones para poder manejar desconexiones inesperadas
			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.AddListener(OnShutdown);

			var sceneInfo = new NetworkSceneInfo();
			sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

			var startArguments = new StartGameArgs()
			{
				GameMode = Application.isEditor && ForceSinglePlayer ? GameMode.Single : GameMode.AutoHostOrClient,
				SessionName = RoomText.text,
				PlayerCount = MaxPlayerCount,
				// Necesitamos especificar una propiedad de sesión para que el matchmaking decida dónde quiere unirse el jugador.
				// De lo contrario, jugadores de la escena Platformer podrían conectarse al juego ThirdPersonCharacter, etc.
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
				return; // El panel no puede ocultarse si el juego no está en ejecución

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

			// Intenta cargar el estado de desconexión previo
			StatusText.text = _shutdownStatus != null ? _shutdownStatus : string.Empty;
			_shutdownStatus = null;
		}

		private void Update()
		{
			// Las teclas Enter/Esc se usan para bloquear/desbloquear el cursor en la vista del juego.
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
			{
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
		}

		public async Task Disconnect()
		{
			if (_runnerInstance == null)
				return;

			StatusText.text = "Desconectando...";
			PanelGroup.interactable = false;

			// Elimina el listener de desconexión ya que nos estamos desconectando deliberadamente
			var events = _runnerInstance.GetComponent<NetworkEvents>();
			events.OnShutdown.RemoveListener(OnShutdown);

			await _runnerInstance.Shutdown();
			_runnerInstance = null;

			// Es necesario restablecer los objetos de red de la escena, recarga toda la escena
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		private void OnShutdown(NetworkRunner runner, ShutdownReason reason)
		{
			// Ocurrió una desconexión inesperada (ej. el host se desconectó)

			// Guarda el estado en una variable estática, se usará en OnEnable después de cargar la escena
			_shutdownStatus = $"Desconexión: {reason}";
			Debug.LogWarning(_shutdownStatus);

			// Es necesario restablecer los objetos de red de la escena, recarga toda la escena
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}
	}
}
