using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private float sceneTransitionDelay = 0.45f;

    public void StartGame()
    {
        StartCoroutine(LoadSceneWithDelay("PlayScene"));
    }

    public void QuitGame()
    {
        StartCoroutine(QuitWithDelay());
    }

    private IEnumerator LoadSceneWithDelay(string sceneName)
    {
        // Esperar el delay (tiempo para que se reproduzca el audio y cambie el sprite)
        yield return new WaitForSeconds(sceneTransitionDelay);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator QuitWithDelay()
    {
        // Esperar el delay (tiempo para que se reproduzca el audio y cambie el sprite)
        yield return new WaitForSeconds(sceneTransitionDelay);
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}