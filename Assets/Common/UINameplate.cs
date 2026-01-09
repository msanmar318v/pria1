using TMPro;
using UnityEngine;


public class UINameplate : MonoBehaviour
{
    public TextMeshProUGUI NicknameText;

    private Transform _cameraTransform;

    public void SetNickname(string nickname)
    {
        NicknameText.text = nickname;
    }

    private void Awake()
    {
        _cameraTransform = Camera.main.transform;
        NicknameText.text = string.Empty;
    }

    private void LateUpdate()
    {
        // Rotate nameplate toward camera
        transform.rotation = _cameraTransform.rotation;
    }
}

