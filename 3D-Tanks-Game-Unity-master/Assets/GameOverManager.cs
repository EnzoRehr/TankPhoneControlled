using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameOverManager : MonoBehaviour
{
    [Header("=== PLAYER ===")]
    public MegaTankHealth playerHealth;

    [Header("=== UI ===")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI waveReachedText;
    public Button restartButton;
    public Button mainMenuButton;

    private WaveManager waveManager;
    private bool gameOver = false;

    void Start()
    {
        waveManager = FindObjectOfType<WaveManager>();

        if (playerHealth != null)
            playerHealth.OnDeath += OnPlayerDied;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    void OnPlayerDied()
    {
        if (gameOver) return;
        gameOver = true;
        ShowGameOver("GAME OVER", Color.red);
    }

    void Update()
    {
        // Verifica daca playerul nu mai exista pe harta
        if (!gameOver && playerHealth == null)
        {
            gameOver = true;
            ShowGameOver("GAME OVER", Color.red);
        }

        // Verifica daca toate wave-urile s-au terminat
        if (!gameOver && waveManager != null && waveManager.waves.Length > 0)
        {
            if (waveManager.currentWave >= waveManager.waves.Length)
            {
                gameOver = true;
                ShowGameOver("VICTORIE!", Color.green);
            }
        }
    }

    void ShowGameOver(string title, Color color)
    {
        Time.timeScale = 0f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = color;
        }

        if (waveReachedText != null && waveManager != null)
            waveReachedText.text = $"Wave atins: {waveManager.currentWave}";

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartLevel);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}