using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class ButtonHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("HoverItems")]
    [SerializeField] private RectTransform leftSymbol;
    [SerializeField] private RectTransform rightSymbol;
    [SerializeField] private RectTransform backgroundPlankImage;

    [Header("Text")]
    [SerializeField] private RectTransform buttonText;
    [SerializeField] private float textScaleMultiplier = 1.1f;

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 15f;
    [SerializeField] private float moveDistance = 8f;
    [SerializeField] private float scaleSpeed = 10f;
    [SerializeField] private float plankDropDistance = 10f;
    [SerializeField] private float plankDropSpeed = 15f;

    [Header("Audio")]
    [SerializeField] private AudioSource hoverSound;
    [SerializeField] private AudioSource clickSound;

    [Header("Click Settings")]
    [SerializeField] private Sprite clickSprite;
    [SerializeField] private float delayBeforeAction = 0.5f;

    private CanvasGroup leftCanvas;
    private CanvasGroup rightCanvas;
    private CanvasGroup backgroundPlankImageCanvas;
    private Image backgroundPlankImageComponent;
    private Sprite originalSprite;

    private Vector2 leftStartPos;
    private Vector2 rightStartPos;
    private Vector2 plankCenterPos;
    private Vector2 plankHiddenPos;
    private Vector3 textStartScale;

    private float targetAlpha = 0f;
    private float targetTextScale = 1f;
    private bool isHovered = false;
    private bool isProcessingClick = false;

    private Button button;

    private void Awake()
    {
        leftCanvas = leftSymbol.GetComponent<CanvasGroup>();
        rightCanvas = rightSymbol.GetComponent<CanvasGroup>();
        backgroundPlankImageCanvas = backgroundPlankImage.GetComponent<CanvasGroup>();
        backgroundPlankImageComponent = backgroundPlankImage.GetComponent<Image>();

        if (backgroundPlankImageComponent != null)
        {
            originalSprite = backgroundPlankImageComponent.sprite;
        }

        leftStartPos = leftSymbol.anchoredPosition;
        rightStartPos = rightSymbol.anchoredPosition;
        plankCenterPos = backgroundPlankImage.anchoredPosition;
        plankHiddenPos = plankCenterPos + Vector2.up * plankDropDistance;

        textStartScale = buttonText.localScale;

        leftCanvas.alpha = 0f;
        rightCanvas.alpha = 0f;
        backgroundPlankImageCanvas.alpha = 0f;

        backgroundPlankImage.anchoredPosition = plankHiddenPos;

        button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private void Update()
    {
        if (isProcessingClick) return;

        leftCanvas.alpha = Mathf.Lerp(leftCanvas.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        rightCanvas.alpha = Mathf.Lerp(rightCanvas.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (isHovered)
        {
            backgroundPlankImageCanvas.alpha = Mathf.Lerp(backgroundPlankImageCanvas.alpha, targetAlpha, Time.deltaTime * plankDropSpeed);
        }
        else
        {
            backgroundPlankImageCanvas.alpha = 0f;
        }

        leftSymbol.anchoredPosition = Vector2.Lerp(
            leftSymbol.anchoredPosition,
            leftStartPos + Vector2.left * (1 - targetAlpha) * moveDistance,
            Time.deltaTime * fadeSpeed
        );

        rightSymbol.anchoredPosition = Vector2.Lerp(
            rightSymbol.anchoredPosition,
            rightStartPos + Vector2.right * (1 - targetAlpha) * moveDistance,
            Time.deltaTime * fadeSpeed
        );

        Vector2 targetPlankPos = isHovered ? plankCenterPos : plankHiddenPos;
        backgroundPlankImage.anchoredPosition = Vector2.Lerp(
            backgroundPlankImage.anchoredPosition,
            targetPlankPos,
            Time.deltaTime * plankDropSpeed
        );

        buttonText.localScale = Vector3.Lerp(
            buttonText.localScale,
            textStartScale * targetTextScale,
            Time.deltaTime * scaleSpeed
        );
    }

    private void OnButtonClick()
    {
        if (isProcessingClick) return;

        StartCoroutine(HandleClickSequence());
    }

    private IEnumerator HandleClickSequence()
    {
        isProcessingClick = true;

        if (clickSprite != null && backgroundPlankImageComponent != null)
        {
            backgroundPlankImageComponent.sprite = clickSprite;
        }

        if (clickSound != null)
        {
            clickSound.Play();
        }

        yield return new WaitForSeconds(delayBeforeAction);

        if (originalSprite != null && backgroundPlankImageComponent != null)
        {
            backgroundPlankImageComponent.sprite = originalSprite;
        }

        isProcessingClick = false;
    }

    // Mouse
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        ActivateHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        DeactivateHover();
    }

    // Keyboard / Gamepad
    public void OnSelect(BaseEventData eventData)
    {
        ActivateHover();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        DeactivateHover();
    }

    private void ActivateHover()
    {
        if (isHovered || isProcessingClick) return;

        isHovered = true;
        targetAlpha = 1f;
        targetTextScale = textScaleMultiplier;

        if (hoverSound != null)
            hoverSound.Play();
    }

    private void DeactivateHover()
    {
        if (isProcessingClick) return;

        isHovered = false;
        targetAlpha = 0f;
        targetTextScale = 1f;
    }
}