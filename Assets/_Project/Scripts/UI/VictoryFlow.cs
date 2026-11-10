using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class VictoryFlow : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] GameObject continueButton;   // queda seleccionado al abrir
    [SerializeField] Health playerHealth;         // opcional: evita ganar si el jugador ya murió
    [SerializeField] float showDelay = 1.5f;
    [SerializeField] string menuSceneName = "MenuPrincipal";

    [Header("Puntaje final")]
    [Tooltip("Opcional: si se deja vacio, se busca solo en la escena.")]
    [SerializeField] RunScore runScore;
    [SerializeField] TMP_Text scoreText;

    [Header("Modo infinito")]
    [Tooltip("Opcional: otros scripts a reactivar al continuar. Los EnemySpawner se reactivan solos")]
    [SerializeField] Behaviour[] enableOnEndless;

    public static bool IsEndless { get; private set; }

    bool shown;

    void Awake()
    {
        IsEndless = false;   // es estático: se reinicia al cargar la escena
        if (panel != null) panel.SetActive(false);
        if (runScore == null) runScore = FindAnyObjectByType<RunScore>();
    }

    void OnEnable()  => GameEvents.BossDefeated += HandleDefeated;
    void OnDisable() => GameEvents.BossDefeated -= HandleDefeated;

    void HandleDefeated()
    {
        if (shown || IsEndless || !isActiveAndEnabled) return;
        StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        yield return new WaitForSeconds(showDelay);              // deja ver la muerte y el café
        yield return new WaitUntil(() => Time.timeScale > 0f);   // espera si hay cartas abiertas

        if (playerHealth != null && playerHealth.Current <= 0f) yield break;
        Show();
    }

    void Show()
    {
        shown = true;

        if (scoreText != null)
        {
            scoreText.text = runScore != null && runScore.Finished
                ? $"{runScore.Score} pts · {RunClock.Format(runScore.FinalTime)}"
                : "";
        }

        panel.SetActive(true);
        Time.timeScale = 0f;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(continueButton);
    }

    public void ContinueEndless()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
        IsEndless = true;

        // Reactiva los spawners que se apagaron al aparecer el jefe, esten desactivados como objeto o solo
        // como script.
        foreach (var spawner in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include))
        {
            spawner.gameObject.SetActive(true);
            spawner.enabled = true;
        }

        // El director tambien se apaga durante la pelea del jefe (BossEncounter); sin reactivarlo, el modo
        // infinito spawneaba enemigos pero con la dificultad congelada en x1 para siempre. IsEndless ya quedo
        // en true arriba, asi que al reactivarse sigue escalando mas alla de la ultima ronda (ver DifficultyDirector).
        foreach (var director in FindObjectsByType<DifficultyDirector>(FindObjectsInactive.Include))
        {
            director.gameObject.SetActive(true);
            director.enabled = true;
        }

        foreach (var b in enableOnEndless)
            if (b != null) b.enabled = true;

        EventSystem.current.SetSelectedGameObject(null);
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

    [ContextMenu("Probar: mostrar victoria")]
    void TestShow() => Show();
}