using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Starter;
using Starter.Shooter;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PlaySceneUIMannager : MonoBehaviour
{
    [System.Serializable]
    public class ButtonHoverSettings
    {
        [Tooltip("El botón al que se aplicará la animación")]
        public Button button;
        
        [Tooltip("Dirección del movimiento al hacer hover")]
        public HoverDirection direction = HoverDirection.Left;
        
        [Tooltip("Distancia de movimiento específica para este botón (deja en 0 para usar el valor global)")]
        public float customMoveDistance = 0f;
        
        [HideInInspector]
        public RectTransform hoverExtension;
    }

    public enum HoverDirection
    {
        Left,
        Right
    }

    [Header("Buttons Configuration")]
    [SerializeField]
    private List<ButtonHoverSettings> buttonsWithHover = new List<ButtonHoverSettings>();

    [Header("Global Hover Animation Settings")]
    [Tooltip("Distancia de movimiento por defecto para todos los botones")]
    public float defaultHoverMoveDistance = 20f;
    
    [Tooltip("Duración de la animación en segundos")]
    public float hoverAnimationDuration = 0.1f;

    [Header("HUD - Health")]
    [Tooltip("Icono de vida del jugador")]
    public Image healthIcon;
    
    [Tooltip("Texto que muestra la vida actual")]
    public TextMeshProUGUI healthText;

    [Header("HUD - Ammo")]
    [Tooltip("Imagen del cargador (se cambiará el sprite según las balas)")]
    public Image ammoImage;
    
    [Tooltip("Array de sprites del cargador (índice 0 = 0 balas, índice 6 = 6 balas)")]
    public Sprite[] ammoSprites = new Sprite[7];
    
    [Tooltip("Texto que muestra las balas actuales")]
    public TextMeshProUGUI ammoText;

    [Header("HUD - Local Kills")]
    [Tooltip("Imagen/icono de kills locales")]
    public Image localKillsIcon;
    
    [Tooltip("Texto que muestra las kills totales del jugador")]
    public TextMeshProUGUI localKillsText;

    [Header("HUD - Best Player")]
    [Tooltip("Imagen/icono del mejor jugador")]
    public Image bestPlayerIcon;
    
    [Tooltip("Texto que muestra el nombre del mejor jugador")]
    public TextMeshProUGUI bestPlayerNameText;
    
    [Tooltip("Texto que muestra las kills del mejor jugador")]
    public TextMeshProUGUI bestPlayerKillsText;

    [Header("Game References")]
    [Tooltip("Referencia al GameManager (se buscará automáticamente si no se asigna)")]
    public GameManager gameManager;

    private Dictionary<RectTransform, Coroutine> activeAnimations = new Dictionary<RectTransform, Coroutine>();
    private Dictionary<RectTransform, bool> isHovering = new Dictionary<RectTransform, bool>();
    private bool _isSubscribed = false;

    private void Start()
    {
        foreach (var buttonSettings in buttonsWithHover)
        {
            if (buttonSettings.button != null)
            {
                AddHoverAnimation(buttonSettings);
            }
        }

        // Buscar GameManager si no está asignado
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }
    }

    private void Update()
    {
        // Suscribirse a los eventos del jugador local cuando esté disponible
        if (gameManager != null && gameManager.LocalPlayer != null && 
            gameManager.LocalPlayer.HasInputAuthority && !_isSubscribed)
        {
            SubscribeToPlayerEvents(gameManager.LocalPlayer);
            _isSubscribed = true;
        }
    }

    #region HUD Update Methods

    /// <summary>
    /// Suscribe el UI Manager a los eventos del jugador local
    /// </summary>
    private void SubscribeToPlayerEvents(Player player)
    {
        // Suscribirse al evento de munición
        player.OnAmmoChanged.AddListener(OnAmmoChanged);
        
        // Inicializar la UI con los valores actuales
        OnAmmoChanged(player.CurrentAmmo);
        
        // TODO: Suscribirse a otros eventos cuando se implementen
        // player.Health.OnHealthChanged += OnHealthChanged;
        // player.OnKillsChanged += OnKillsChanged;
        // gameManager.OnBestHunterChanged += OnBestHunterChanged;
        
        Debug.Log("[PlaySceneUIManager] Suscrito a eventos del jugador local");
    }

    /// <summary>
    /// Actualiza la visualización de la vida del jugador
    /// </summary>
    /// <param name="currentHealth">Vida actual</param>
    /// <param name="maxHealth">Vida máxima</param>
    public void OnHealthChanged(int currentHealth, int maxHealth)
    {
        // TODO: Implementar actualización de vida
        // - Actualizar healthText.text con formato "currentHealth / maxHealth"
        // - Cambiar color del icono o texto si la vida es baja
        // - Opcional: animación de daño recibido
    }

    /// <summary>
    /// Actualiza la visualización de munición del jugador
    /// </summary>
    /// <param name="currentAmmo">Balas actuales en el cargador</param>
    public void OnAmmoChanged(int currentAmmo)
    {
        // Actualizar el sprite del cargador
        if (ammoImage != null && ammoSprites != null && ammoSprites.Length == 7)
        {
            int spriteIndex = Mathf.Clamp(currentAmmo, 0, 6);
            ammoImage.sprite = ammoSprites[spriteIndex];
        }
        
        // Actualizar el texto de munición
        if (ammoText != null)
        {
            ammoText.text = currentAmmo.ToString();
        }
        
        Debug.Log($"[PlaySceneUIManager] HUD actualizado - Munición: {currentAmmo}");
    }

    /// <summary>
    /// Actualiza el contador de kills locales del jugador
    /// </summary>
    /// <param name="kills">Número de kills</param>
    public void OnKillsChanged(int kills)
    {
        // TODO: Implementar actualización de kills locales
        // - Actualizar localKillsText.text con el valor de kills
        // - Opcional: animación de incremento
    }

    /// <summary>
    /// Actualiza la información del mejor jugador
    /// </summary>
    /// <param name="playerName">Nombre del mejor jugador</param>
    /// <param name="kills">Kills del mejor jugador</param>
    public void OnBestHunterChanged(string playerName, int kills)
    {
        // TODO: Implementar actualización del mejor jugador
        // - Actualizar bestPlayerNameText.text con playerName
        // - Actualizar bestPlayerKillsText.text con kills
        // - Opcional: resaltar si el mejor jugador eres tú
    }

    #endregion

    #region Hover Animation Methods

    private void AddHoverAnimation(ButtonHoverSettings settings)
    {
        GameObject buttonObject = settings.button.gameObject;
        
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        Vector2 originalPosition = rectTransform.anchoredPosition;
        
        isHovering[rectTransform] = false;
        
        float moveDistance = settings.customMoveDistance > 0 ? settings.customMoveDistance : defaultHoverMoveDistance;
        float directionMultiplier = settings.direction == HoverDirection.Left ? -1f : 1f;

        GameObject hoverExtension = new GameObject("HoverExtension");
        hoverExtension.transform.SetParent(buttonObject.transform);
        hoverExtension.transform.localScale = Vector3.one;
        
        RectTransform extensionRect = hoverExtension.AddComponent<RectTransform>();
        extensionRect.anchorMin = new Vector2(0, 0);
        extensionRect.anchorMax = new Vector2(1, 1);
        extensionRect.offsetMin = Vector2.zero;
        extensionRect.offsetMax = Vector2.zero;
        extensionRect.anchoredPosition = Vector2.zero;
        
        hoverExtension.transform.SetAsFirstSibling();
        
        Image invisibleImage = hoverExtension.AddComponent<Image>();
        invisibleImage.color = new Color(0, 0, 0, 0);
        invisibleImage.raycastTarget = true;
        
        settings.hoverExtension = extensionRect;
        
        EventTrigger buttonTrigger = buttonObject.GetComponent<EventTrigger>();
        if (buttonTrigger == null)
        {
            buttonTrigger = buttonObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry buttonEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        buttonEnter.callback.AddListener((data) => { 
            OnHoverEnter(rectTransform, extensionRect, originalPosition, moveDistance * directionMultiplier); 
        });
        buttonTrigger.triggers.Add(buttonEnter);

        EventTrigger.Entry buttonExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        buttonExit.callback.AddListener((data) => { 
            OnHoverExit(rectTransform, extensionRect, originalPosition); 
        });
        buttonTrigger.triggers.Add(buttonExit);

        EventTrigger extensionTrigger = hoverExtension.AddComponent<EventTrigger>();

        EventTrigger.Entry extensionEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        extensionEnter.callback.AddListener((data) => { 
            OnHoverEnter(rectTransform, extensionRect, originalPosition, moveDistance * directionMultiplier); 
        });
        extensionTrigger.triggers.Add(extensionEnter);

        EventTrigger.Entry extensionExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        extensionExit.callback.AddListener((data) => { 
            OnHoverExit(rectTransform, extensionRect, originalPosition); 
        });
        extensionTrigger.triggers.Add(extensionExit);

        EventTrigger.Entry extensionClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        extensionClick.callback.AddListener((data) => {
            settings.button.onClick.Invoke();
        });
        extensionTrigger.triggers.Add(extensionClick);
    }

    private void OnHoverEnter(RectTransform rectTransform, RectTransform extensionRect, Vector2 originalPosition, float moveDistance)
    {
        isHovering[rectTransform] = true;
        
        if (activeAnimations.ContainsKey(rectTransform) && activeAnimations[rectTransform] != null)
        {
            StopCoroutine(activeAnimations[rectTransform]);
        }
        
        Vector2 targetPosition = originalPosition + new Vector2(moveDistance, 0);
        
        float extensionDistance = -moveDistance;
        extensionRect.offsetMin = new Vector2(extensionDistance < 0 ? extensionDistance : 0, 0);
        extensionRect.offsetMax = new Vector2(extensionDistance > 0 ? extensionDistance : 0, 0);
        
        activeAnimations[rectTransform] = StartCoroutine(AnimatePosition(rectTransform, targetPosition));
    }

    private void OnHoverExit(RectTransform rectTransform, RectTransform extensionRect, Vector2 originalPosition)
    {
        isHovering[rectTransform] = false;
        
        StartCoroutine(DelayedHoverExit(rectTransform, extensionRect, originalPosition));
    }

    private IEnumerator DelayedHoverExit(RectTransform rectTransform, RectTransform extensionRect, Vector2 originalPosition)
    {
        yield return null;
        
        if (!isHovering[rectTransform])
        {
            if (activeAnimations.ContainsKey(rectTransform) && activeAnimations[rectTransform] != null)
            {
                StopCoroutine(activeAnimations[rectTransform]);
            }
            
            extensionRect.offsetMin = Vector2.zero;
            extensionRect.offsetMax = Vector2.zero;
            
            activeAnimations[rectTransform] = StartCoroutine(AnimatePosition(rectTransform, originalPosition));
        }
    }

    private IEnumerator AnimatePosition(RectTransform rectTransform, Vector2 targetPosition)
    {
        Vector2 startPosition = rectTransform.anchoredPosition;
        float elapsedTime = 0f;

        while (elapsedTime < hoverAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / hoverAnimationDuration);
            
            t = 1 - (1 - t) * (1 - t);
            
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        rectTransform.anchoredPosition = targetPosition;
    }

    #endregion

    public async void GoBack()
    {
        var uiGameMenu = FindObjectOfType<UIGameMenu>();
        
        if (uiGameMenu != null)
        {
            await uiGameMenu.Disconnect();
        }
        else
        {
            Debug.LogWarning("No se encontró UIGameMenu en la escena");
        }
        
        SceneManager.LoadScene("MainMenuScene");
    }

    private void OnDestroy()
    {
        // Desuscribirse de eventos al destruir
        if (_isSubscribed && gameManager != null && gameManager.LocalPlayer != null)
        {
            gameManager.LocalPlayer.OnAmmoChanged.RemoveListener(OnAmmoChanged);
            
            // TODO: Desuscribirse de otros eventos cuando se implementen
            // gameManager.LocalPlayer.Health.OnHealthChanged -= OnHealthChanged;
            // gameManager.LocalPlayer.OnKillsChanged -= OnKillsChanged;
            // gameManager.OnBestHunterChanged -= OnBestHunterChanged;
        }
    }
}