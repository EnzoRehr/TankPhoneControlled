using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuFinal : MonoBehaviour
{
    [Header("=== SCENE TO LOAD ===")]
    public int sceneIndex = 1;

    public void PlayGame()
    {
        SceneManager.LoadScene(sceneIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit!");
    }
}