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

    [Header("HUD Main Container")]
    [Tooltip("GameObject contenedor principal del HUD (se activará cuando el jugador spawnee)")]
    public GameObject hudMainContainer;

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

    [Header("HUD - Best Player Container")]
    [Tooltip("GameObject contenedor de todos los elementos del mejor jugador (se activará/desactivará)")]
    public GameObject bestPlayerContainer;

    [Header("Game References")]
    [Tooltip("Referencia al GameManager (se buscará automáticamente si no se asigna)")]
    public GameManager gameManager;

    private Dictionary<RectTransform, Coroutine> activeAnimations = new Dictionary<RectTransform, Coroutine>();
    private Dictionary<RectTransform, bool> isHovering = new Dictionary<RectTransform, bool>();
    private bool _isSubscribed = false;
    private bool _hudVisible = false;

    private void Start()
    {
        foreach (var buttonSettings in buttonsWithHover)
        {
            if (buttonSettings.button != null)
            {
                AddHoverAnimation(buttonSettings);
            }
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
        
        InitializeHealthIcons();
        HideBestPlayerUI();
        HideHUD();
    }

    private void Update()
    {
        if (gameManager != null && gameManager.LocalPlayer != null && 
            gameManager.LocalPlayer.HasInputAuthority && !_isSubscribed)
        {
            SubscribeToPlayerEvents(gameManager.LocalPlayer);
            SubscribeToGameManagerEvents();
            ShowHUD();
            _isSubscribed = true;
        }
    }

    #region HUD Visibility

    private void ShowHUD()
    {
        if (hudMainContainer != null && !_hudVisible)
        {
            hudMainContainer.SetActive(true);
            _hudVisible = true;
        }
    }

    private void HideHUD()
    {
        if (hudMainContainer != null)
        {
            hudMainContainer.SetActive(false);
            _hudVisible = false;
        }
    }

    #endregion

    #region HUD Update Methods

    private void InitializeHealthIcons()
    {
        if (healthBackgroundIcon != null)
        {
            Color bgColor = healthBackgroundIcon.color;
            bgColor.a = healthBackgroundAlpha;
            healthBackgroundIcon.color = bgColor;
            
            healthBackgroundIcon.type = Image.Type.Filled;
            healthBackgroundIcon.fillMethod = Image.FillMethod.Vertical;
            healthBackgroundIcon.fillOrigin = (int)Image.OriginVertical.Top;
            healthBackgroundIcon.fillAmount = 1f;
        }
        
        if (healthIcon != null)
        {
            Color fgColor = healthIcon.color;
            fgColor.a = healthForegroundAlpha;
            healthIcon.color = fgColor;
            
            healthIcon.type = Image.Type.Filled;
            healthIcon.fillMethod = Image.FillMethod.Vertical;
            healthIcon.fillOrigin = (int)Image.OriginVertical.Top;
            healthIcon.fillAmount = 1f;
        }
    }

    private void SubscribeToPlayerEvents(Player player)
    {
        player.OnAmmoChanged.AddListener(OnAmmoChanged);
        
        if (player.Health != null)
        {
            player.Health.OnHealthChanged.AddListener(OnHealthChanged);
            
            int healthPercentage = player.Health.GetHealthPercentage();
            OnHealthChanged(player.Health.CurrentHealth, player.Health.InitialHealth, healthPercentage);
        }
        
        player.OnPlayerKillsChanged.AddListener(OnKillsChanged);
        
        OnAmmoChanged(player.CurrentAmmo);
        OnKillsChanged(player.PlayerKills);
    }

    private void SubscribeToGameManagerEvents()
    {
        if (gameManager != null)
        {
            gameManager.OnBestHunterChanged += OnBestHunterChanged;
        }
    }

    public void OnHealthChanged(int currentHealth, int maxHealth, int healthPercentage)
    {
        if (healthIcon != null)
        {
            healthIcon.fillAmount = (float)healthPercentage / 100f;
        }
        
        if (healthText != null)
        {
            healthText.text = $"{healthPercentage}%";
            
            if (healthPercentage <= 25)
            {
                healthText.color = Color.red;
            }
            else if (healthPercentage <= 50)
            {
                healthText.color = Color.yellow;
            }
            else
            {
                healthText.color = Color.white;
            }
        }
    }

    public void OnAmmoChanged(int currentAmmo)
    {
        if (ammoImage != null && ammoSprites != null && ammoSprites.Length == 7)
        {
            int spriteIndex = Mathf.Clamp(currentAmmo, 0, 6);
            ammoImage.sprite = ammoSprites[spriteIndex];
        }
        
        if (ammoText != null)
        {
            ammoText.text = currentAmmo.ToString();
        }
    }

    public void OnKillsChanged(int kills)
    {
        if (localKillsText != null)
        {
            localKillsText.text = kills.ToString("D2");
        }
    }

    public void OnBestHunterChanged(string playerName, int kills)
    {
        if (string.IsNullOrEmpty(playerName) || kills <= 0)
        {
            HideBestPlayerUI();
            return;
        }

        ShowBestPlayerUI();

        if (bestPlayerNameText != null)
        {
            bestPlayerNameText.text = playerName;
        }

        if (bestPlayerKillsText != null)
        {
            bestPlayerKillsText.text = kills.ToString("D2");
        }

        if (gameManager != null && gameManager.LocalPlayer != null && 
            gameManager.LocalPlayer.Nickname == playerName)
        {
            if (bestPlayerNameText != null)
            {
                bestPlayerNameText.color = Color.yellow;
            }
        }
        else
        {
            if (bestPlayerNameText != null)
            {
                bestPlayerNameText.color = Color.white;
            }
        }
    }

    private void ShowBestPlayerUI()
    {
        if (bestPlayerContainer != null)
        {
            bestPlayerContainer.SetActive(true);
        }
    }

    private void HideBestPlayerUI()
    {
        if (bestPlayerContainer != null)
        {
            bestPlayerContainer.SetActive(false);
        }

        if (bestPlayerNameText != null)
        {
            bestPlayerNameText.text = string.Empty;
        }

        if (bestPlayerKillsText != null)
        {
            bestPlayerKillsText.text = "00";
        }
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
        var uiGameMenu = FindFirstObjectByType<UIGameMenu>();
        
        if (uiGameMenu != null)
        {
            await uiGameMenu.Disconnect();
        }
        
        SceneManager.LoadScene("MainMenuScene");
    }

    private void OnDestroy()
    {
        if (_isSubscribed && gameManager != null && gameManager.LocalPlayer != null)
        {
            gameManager.LocalPlayer.OnAmmoChanged.RemoveListener(OnAmmoChanged);
            gameManager.LocalPlayer.OnPlayerKillsChanged.RemoveListener(OnKillsChanged);
            
            if (gameManager.LocalPlayer.Health != null)
            {
                gameManager.LocalPlayer.Health.OnHealthChanged.RemoveListener(OnHealthChanged);
            }
            
            if (gameManager != null)
            {
                gameManager.OnBestHunterChanged -= OnBestHunterChanged;
            }
        }
    }
}