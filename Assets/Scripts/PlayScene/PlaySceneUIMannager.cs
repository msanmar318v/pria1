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
    [Tooltip("Imagen de fondo de vida (alpha reducido, siempre visible)")]
    public Image healthBackgroundIcon;
    
    [Tooltip("Imagen principal de vida (alpha completo, se consume según la vida actual)")]
    public Image healthIcon;
    
    [Tooltip("Texto que muestra la vida actual")]
    public TextMeshProUGUI healthText;
    
    [Header("Health Visual Settings")]
    [Tooltip("Alpha de la imagen de fondo (vida perdida) - valor entre 0 y 1")]
    [Range(0f, 1f)]
    public float healthBackgroundAlpha = 0.4f;
    
    [Tooltip("Alpha de la imagen principal (vida actual) - valor entre 0 y 1")]
    [Range(0f, 1f)]
    public float healthForegroundAlpha = 1f;

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
        
        // Configurar los alphas iniciales de las imágenes de vida
        InitializeHealthIcons();
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
    /// Inicializa las imágenes de vida con los alphas correctos
    /// </summary>
    private void InitializeHealthIcons()
    {
        // Configurar imagen de fondo (siempre visible con alpha reducido)
        if (healthBackgroundIcon != null)
        {
            Color bgColor = healthBackgroundIcon.color;
            bgColor.a = healthBackgroundAlpha;
            healthBackgroundIcon.color = bgColor;
            
            // Asegurarse de que esté configurada como Filled
            healthBackgroundIcon.type = Image.Type.Filled;
            healthBackgroundIcon.fillMethod = Image.FillMethod.Vertical;
            healthBackgroundIcon.fillOrigin = (int)Image.OriginVertical.Top;
            healthBackgroundIcon.fillAmount = 1f; // Siempre al 100%
        }
        
        // Configurar imagen principal (se consume según la vida)
        if (healthIcon != null)
        {
            Color fgColor = healthIcon.color;
            fgColor.a = healthForegroundAlpha;
            healthIcon.color = fgColor;
            
            // Asegurarse de que esté configurada como Filled
            healthIcon.type = Image.Type.Filled;
            healthIcon.fillMethod = Image.FillMethod.Vertical;
            healthIcon.fillOrigin = (int)Image.OriginVertical.Top;
            healthIcon.fillAmount = 1f; // Empieza al 100%
        }
    }

    /// <summary>
    /// Suscribe el UI Manager a los eventos del jugador local
    /// </summary>
    private void SubscribeToPlayerEvents(Player player)
    {
        // Suscribirse al evento de munición
        player.OnAmmoChanged.AddListener(OnAmmoChanged);
        
        // Suscribirse al evento de vida
        if (player.Health != null)
        {
            player.Health.OnHealthChanged.AddListener(OnHealthChanged);
            
            // Inicializar la UI de vida con los valores actuales
            int healthPercentage = player.Health.GetHealthPercentage();
            OnHealthChanged(player.Health.CurrentHealth, player.Health.InitialHealth, healthPercentage);
        }
        
        // Suscribirse al evento de kills
        player.OnPlayerKillsChanged.AddListener(OnKillsChanged);
        
        // Inicializar la UI con los valores actuales
        OnAmmoChanged(player.CurrentAmmo);
        OnKillsChanged(player.PlayerKills); // Inicializar kills
        
        // TODO: Suscribirse a otros eventos cuando se implementen
        // gameManager.OnBestHunterChanged += OnBestHunterChanged;
        
        Debug.Log("[PlaySceneUIManager] Suscrito a eventos del jugador local");
    }

    /// <summary>
    /// Actualiza la visualización de la vida del jugador
    /// </summary>
    /// <param name="currentHealth">Vida actual</param>
    /// <param name="maxHealth">Vida máxima</param>
    /// <param name="healthPercentage">Porcentaje de vida (0-100)</param>
    public void OnHealthChanged(int currentHealth, int maxHealth, int healthPercentage)
    {
        // Actualizar la imagen principal de vida (se consume de arriba a abajo)
        if (healthIcon != null)
        {
            // fillAmount va de 0 (vacío) a 1 (lleno)
            healthIcon.fillAmount = (float)healthPercentage / 100f;
            
            Debug.Log($"[PlaySceneUIManager] FillAmount actualizado: {healthIcon.fillAmount} ({healthPercentage}%)");
        }
        
        // La imagen de fondo siempre permanece al 100% con alpha reducido
        // No es necesario actualizarla, siempre muestra la vida "máxima" con transparencia
        
        // Actualizar el texto de vida (mostrar porcentaje sin decimales)
        if (healthText != null)
        {
            healthText.text = $"{healthPercentage}%";
        }
        
        Debug.Log($"[PlaySceneUIManager] HUD de vida actualizado - {currentHealth}/{maxHealth} ({healthPercentage}%)");
        
        // Cambiar color del texto según el porcentaje de vida
        if (healthText != null)
        {
            if (healthPercentage <= 25)
            {
                healthText.color = Color.red; // Vida crítica
            }
            else if (healthPercentage <= 50)
            {
                healthText.color = Color.yellow; // Vida media
            }
            else
            {
                healthText.color = Color.white; // Vida normal
            }
        }
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
        // Actualizar el texto de kills con formato de dos dígitos (01, 02, ..., 10, etc.)
        if (localKillsText != null)
        {
            localKillsText.text = kills.ToString("D2"); // D2 = formato con 2 dígitos (01, 02, etc.)
        }
        
        Debug.Log($"[PlaySceneUIManager] HUD de kills actualizado: {kills:D2}");
        
        // Opcional: Animación visual cuando consigues una kill
        // StartCoroutine(AnimateKillIncrement());
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
            gameManager.LocalPlayer.OnPlayerKillsChanged.RemoveListener(OnKillsChanged);
            
            if (gameManager.LocalPlayer.Health != null)
            {
                gameManager.LocalPlayer.Health.OnHealthChanged.RemoveListener(OnHealthChanged);
            }
            
            // TODO: Desuscribirse de otros eventos cuando se implementen
            // gameManager.OnBestHunterChanged -= OnBestHunterChanged;
        }
    }
}