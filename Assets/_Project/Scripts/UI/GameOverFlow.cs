using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameOverFlow : MonoBehaviour
{
    [SerializeField] Health playerHealth;
    [SerializeField] GameObject panel;
    [SerializeField] GameObject restartButton;   // el que queda seleccionado al abrir
    [SerializeField] GameObject quitButton;
    [SerializeField] string menuSceneName = "MenuPrincipal";

    [Header("Animacion de muerte")]
    [Tooltip("Segundos que se deja ver la animacion de muerte del jugador antes de abrir el Game Over.")]
    [SerializeField, Min(0f)] float deathAnimationDelay = 0.9f;
    [Tooltip("Scripts a apagar al morir (movimiento, ataque...). Si se deja vacio, busca PlayerMovement y MacheteAttack en el jugador.")]
    [SerializeField] Behaviour[] disableOnDeath;

    [Header("Puntaje final")]
    [Tooltip("Opcional: si se deja vacio, se busca solo en la escena.")]
    [SerializeField] RunScore runScore;
    [SerializeField] TMP_Text scoreText;

    bool isGameOver;

    void Awake()
    {
        Time.timeScale = 1f;   // empezar siempre en 1, sin importar como se cerro la partida anterior

        if (runScore == null) runScore = FindAnyObjectByType<RunScore>();

        if ((disableOnDeath == null || disableOnDeath.Length == 0) && playerHealth != null)
        {
            var found = new List<Behaviour>();
            if (playerHealth.TryGetComponent(out PlayerMovement move)) found.Add(move);
            if (playerHealth.TryGetComponent(out MacheteAttack attack)) found.Add(attack);
            disableOnDeath = found.ToArray();
        }
    }

    void Start()
    {
        // En la versión web no se puede cerrar el juego: se oculta el botón.
        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
            quitButton.SetActive(false);

        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;   // al salir de la escena o parar el Play, nunca dejar el 0
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
    }

    // Al morir: se apaga el control del jugador (queda quieto para la animacion de muerte) pero el mundo sigue
    // corriendo un instante, y despues se abre el panel de Game Over con el puntaje final.
    void OnPlayerDied()
    {
        if (isGameOver) return;
        isGameOver = true;

        foreach (var b in disableOnDeath)
            if (b != null) b.enabled = false;

        StartCoroutine(ShowGameOverAfterDelay());
    }

    IEnumerator ShowGameOverAfterDelay()
    {
        yield return new WaitForSeconds(deathAnimationDelay);
        ShowGameOver();
    }

    void ShowGameOver()
    {
        if (scoreText != null)
        {
            scoreText.text = runScore != null && runScore.Finished
                ? $"{(runScore.Won ? "VICTORIA" : "DERROTA")} · {runScore.Score} pts · {RunClock.Format(runScore.FinalTime)}"
                : "";
        }

        panel.SetActive(true);
        Time.timeScale = 0f;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(restartButton);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}