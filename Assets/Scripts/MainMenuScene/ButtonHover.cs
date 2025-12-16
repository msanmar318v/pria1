using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Symbols")]
    [SerializeField] private RectTransform leftSymbol;
    [SerializeField] private RectTransform rightSymbol;

    [Header("Text")]
    [SerializeField] private RectTransform buttonText;
    [SerializeField] private float textScaleMultiplier = 1.1f;

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 15f;
    [SerializeField] private float moveDistance = 8f;
    [SerializeField] private float scaleSpeed = 10f;

    [Header("Audio")]
    [SerializeField] private AudioSource hoverSound;

    private CanvasGroup leftCanvas;
    private CanvasGroup rightCanvas;

    private Vector2 leftStartPos;
    private Vector2 rightStartPos;
    private Vector3 textStartScale;

    private float targetAlpha = 0f;
    private float targetTextScale = 1f;
    private bool isHovered = false;

    private Button button;

    private void Awake()
    {
        leftCanvas = leftSymbol.GetComponent<CanvasGroup>();
        rightCanvas = rightSymbol.GetComponent<CanvasGroup>();

        leftStartPos = leftSymbol.anchoredPosition;
        rightStartPos = rightSymbol.anchoredPosition;
        textStartScale = buttonText.localScale;

        leftCanvas.alpha = 0f;
        rightCanvas.alpha = 0f;

        button = GetComponent<Button>();
    }

    private void Update()
    {
        // Fade
        leftCanvas.alpha = Mathf.Lerp(leftCanvas.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        rightCanvas.alpha = Mathf.Lerp(rightCanvas.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        // Move
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

        // Scale text
        buttonText.localScale = Vector3.Lerp(
            buttonText.localScale,
            textStartScale * targetTextScale,
            Time.deltaTime * scaleSpeed
        );
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
        if (isHovered) return;

        isHovered = true;
        targetAlpha = 1f;
        targetTextScale = textScaleMultiplier;

        if (hoverSound != null)
            hoverSound.Play();
    }

    private void DeactivateHover()
    {
        isHovered = false;
        targetAlpha = 0f;
        targetTextScale = 1f;
    }
}