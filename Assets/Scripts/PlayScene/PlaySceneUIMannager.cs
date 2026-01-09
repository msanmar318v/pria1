using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Starter;
using System.Collections;
using System.Collections.Generic;

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

    private Dictionary<RectTransform, Coroutine> activeAnimations = new Dictionary<RectTransform, Coroutine>();
    private Dictionary<RectTransform, bool> isHovering = new Dictionary<RectTransform, bool>();

    private void Start()
    {
        foreach (var buttonSettings in buttonsWithHover)
        {
            if (buttonSettings.button != null)
            {
                AddHoverAnimation(buttonSettings);
            }
        }
    }

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
}