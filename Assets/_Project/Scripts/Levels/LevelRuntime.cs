using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Resultado de un nivel terminado (lo que necesita la pantalla de resultado).</summary>
public class LevelOutcome
{
    public LevelDefinition level;
    public bool won;
    /// <summary>Motivo legible: "¡Meta cumplida!", "Caíste en combate"...</summary>
    public string reason;
    public LevelScore score;
    public int kills;
    public float timeSeconds;
    public float damageTaken;
    public int seed;
}

/// <summary>
/// Controla el nivel dentro de la escena de juego. NO hay que ponerlo en la escena: se crea solo al cargar la
/// escena de juego (la que tiene <see cref="DifficultyDirector"/>).
///
/// Hace:
///  1. Aplica el nivel elegido en <see cref="LevelSession"/>: dificultad, terreno procedural (semilla + bioma)
///     y, en los niveles de jefe, la ronda en la que aparece La Llorona.
///  2. Verifica la condicion de victoria (kills, tiempo o jefe) y la derrota (muerte del jugador).
///  3. Calcula el puntaje (<see cref="LevelScoring"/>) y lo guarda con <c>Backend.Service.SubmitLevelResult</c>.
///
/// Para la UI (HUD y pantalla de resultado), usar los eventos estaticos y las propiedades:
/// <code>
/// LevelRuntime.LevelFinished += outcome => MostrarResultado(outcome);   // gano/perdio + puntaje
/// LevelRuntime.ResultSaved   += resp    => MostrarRecord(resp);         // guardado OK: record, desbloqueo...
/// LevelRuntime.ResultSaveFailed += err  => MostrarError(err.Message);   // boton -> LevelRuntime.Instance.RetrySave()
/// hudText.text = LevelRuntime.Instance.GoalProgressText;                 // "Enemigos 12/40"
/// </code>
/// Mientras nadie se suscriba a <see cref="LevelFinished"/>, se muestra una pantalla de resultado basica de
/// respaldo (<see cref="LevelResultFallbackUI"/>) para poder probar todo antes de tener las pantallas finales.
/// </summary>
public class LevelRuntime : MonoBehaviour
{
    public static LevelRuntime Instance { get; private set; }

    /// <summary>El nivel empezo (despues de configurar dificultad y terreno).</summary>
    public static event Action<LevelRuntime> LevelStarted;
    /// <summary>El nivel termino (victoria o derrota). El juego ya esta en pausa (timeScale 0).</summary>
    public static event Action<LevelOutcome> LevelFinished;
    /// <summary>El resultado se guardo en el backend.</summary>
    public static event Action<LevelResultResponse> ResultSaved;
    /// <summary>No se pudo guardar (sin sesion, sin internet...). Ofrecer <see cref="RetrySave"/>.</summary>
    public static event Action<Result> ResultSaveFailed;

    /// <summary>Apaga los paneles viejos de victoria (modo infinito) y game over de la escena.</summary>
    public static bool DisableLegacyEndScreens = true;
    /// <summary>Texto basico de la meta arriba de la pantalla. La UI final lo pone en false.</summary>
    public static bool ShowFallbackHud = true;

    [Tooltip("Segundos (tiempo real) entre que termina el nivel y se abre el resultado (deja ver la muerte).")]
    [SerializeField] private float endDelay = 1.2f;

    public LevelDefinition Definition { get; private set; }
    public BiomeSettings Biome { get; private set; }
    public int Seed { get; private set; }
    public int Kills { get; private set; }
    public float Elapsed { get; private set; }
    public float DamageTaken { get; private set; }
    public bool Finished { get; private set; }
    public LevelOutcome Outcome { get; private set; }

    public bool IsSaving { get; private set; }
    public LevelResultResponse SavedResponse { get; private set; }
    public Result LastSaveError { get; private set; }

    /// <summary>Avance hacia la meta 0..1 (para una barra en el HUD).</summary>
    public float GoalProgress01 { get; private set; }
    /// <summary>Texto del avance, por ejemplo "Enemigos 12/40" o "Sobrevive 01:10 / 02:00".</summary>
    public string GoalProgressText { get; private set; } = "";

    private Health playerHealth;
    private NightTimer nightTimer;
    private float lastHealth;

    // ================================================================ arranque automatico

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null) return;

        bool isGameScene = scene.name == LevelSession.GameSceneName ||
                           FindAnyObjectByType<DifficultyDirector>(FindObjectsInactive.Include) != null;
        if (!isGameScene) return;

        new GameObject("[LevelRuntime]").AddComponent<LevelRuntime>();
    }

    // ================================================================ ciclo de vida

    private void Awake()
    {
        Instance = this;

        LevelSession.EnsureLevelSelected();
        Definition = LevelSession.Current;
        Seed = LevelSession.RunSeed;
        Biome = LevelCatalog.GenerateBiome(Definition.biome.theme, Seed);

        // 1. Dificultad del nivel (la aplica DifficultyDirector cada frame).
        LevelModifiers.Set(Definition);

        // 2. Terreno procedural con la semilla de esta partida.
        InfiniteTilemapFloor floor = FindAnyObjectByType<InfiniteTilemapFloor>();
        if (floor != null) floor.Configure(Biome, Seed);
        else Debug.LogWarning("[Nivel] No hay InfiniteTilemapFloor en la escena: el terreno no cambia por nivel.");

        // 3. Jefe: solo en los niveles de jefe.
        nightTimer = FindAnyObjectByType<NightTimer>();
        if (nightTimer != null)
        {
            if (Definition.IsBossLevel) nightTimer.ConfigureForLevel(Definition.goalTarget, Definition.goalTarget * 45f);
            else nightTimer.DisableForLevel();
        }

        if (DisableLegacyEndScreens)
        {
            foreach (var v in FindObjectsByType<VictoryFlow>(FindObjectsInactive.Include)) v.enabled = false;
            foreach (var g in FindObjectsByType<GameOverFlow>(FindObjectsInactive.Include)) g.enabled = false;
        }

        // Controles tactiles (solo en celular).
        TouchControls.CreateIfNeeded();

        UpdateGoalProgress();
        Debug.Log($"[Nivel {Definition.number}] {Definition.name} | meta: {Definition.GoalText} | bioma {Biome.theme} semilla {Seed} " +
                  $"(agua {Biome.waterLevel:0.00}, pantano {Biome.swampThreshold:0.00}, roca {Biome.rockThreshold:0.00}, escala {Biome.landScale:0.000})");
    }

    private void OnEnable()
    {
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.BossDefeated += OnBossDefeated;
    }

    private void OnDisable()
    {
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.BossDefeated -= OnBossDefeated;
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent(out playerHealth))
        {
            lastHealth = playerHealth.Current;
            playerHealth.Changed += OnPlayerHealthChanged;
            playerHealth.Died += OnPlayerDied;
        }
        else
        {
            Debug.LogWarning("[Nivel] No se encontro el jugador (tag Player con Health): no se detectara la derrota.");
        }

        LevelStarted?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.Changed -= OnPlayerHealthChanged;
            playerHealth.Died -= OnPlayerDied;
        }
        PauseControl.Pop(this);
        if (Instance == this) Instance = null;
        LevelModifiers.Reset();
    }

    private void Update()
    {
        if (Finished) return;

        Elapsed += Time.deltaTime;   // 0 mientras el juego esta en pausa o con cartas abiertas
        UpdateGoalProgress();

        if (Definition.goal == LevelGoal.KillCount && Kills >= Definition.goalTarget) Finish(true, "¡Meta cumplida!");
        else if (Definition.goal == LevelGoal.SurviveTime && Elapsed >= Definition.goalTarget) Finish(true, "¡Sobreviviste!");
    }

    // ================================================================ eventos del juego

    private void OnEnemyKilled(GameObject enemy)
    {
        if (!Finished) Kills++;
    }

    private void OnBossDefeated()
    {
        if (!Finished && Definition.goal == LevelGoal.DefeatBoss) Finish(true, "¡Venciste a La Llorona!");
    }

    private void OnPlayerHealthChanged(float current, float max)
    {
        if (!Finished && current < lastHealth) DamageTaken += lastHealth - current;
        lastHealth = current;
    }

    private void OnPlayerDied()
    {
        if (!Finished) Finish(false, "Caíste en combate");
    }

    // ================================================================ final del nivel

    private void UpdateGoalProgress()
    {
        switch (Definition.goal)
        {
            case LevelGoal.KillCount:
                GoalProgress01 = Mathf.Clamp01(Kills / (float)Definition.goalTarget);
                GoalProgressText = $"Enemigos {Mathf.Min(Kills, Definition.goalTarget)}/{Definition.goalTarget}";
                break;

            case LevelGoal.SurviveTime:
                GoalProgress01 = Mathf.Clamp01(Elapsed / Definition.goalTarget);
                GoalProgressText = $"Sobrevive {LevelDefinition.FormatTime(Elapsed)} / {LevelDefinition.FormatTime(Definition.goalTarget)}";
                break;

            case LevelGoal.DefeatBoss:
                bool bossOut = nightTimer != null && nightTimer.BossSpawned;
                int round = DifficultyDirector.Instance != null ? DifficultyDirector.Instance.Round : 1;
                GoalProgress01 = bossOut ? 1f : Mathf.Clamp01((round - 1) / (float)Mathf.Max(1, Definition.goalTarget - 1));
                GoalProgressText = bossOut ? "¡Vence a La Llorona!" : $"Ronda {Mathf.Min(round, Definition.goalTarget)}/{Definition.goalTarget}";
                break;
        }
    }

    private void Finish(bool won, string reason)
    {
        Finished = true;
        UpdateGoalProgress();

        // Se congela la accion: sin control del jugador ni mas enemigos.
        if (playerHealth != null)
        {
            if (playerHealth.TryGetComponent(out PlayerMovement move)) move.enabled = false;
            if (playerHealth.TryGetComponent(out MacheteAttack attack)) attack.enabled = false;
        }
        foreach (var spawner in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Exclude)) spawner.enabled = false;

        float hpFraction = playerHealth != null && playerHealth.Max > 0f ? playerHealth.Current / playerHealth.Max : 0f;
        LevelScore score = LevelScoring.Compute(Definition, won, Kills, Elapsed, hpFraction);

        Outcome = new LevelOutcome
        {
            level = Definition,
            won = won,
            reason = reason,
            score = score,
            kills = Kills,
            timeSeconds = Elapsed,
            damageTaken = DamageTaken,
            seed = Seed
        };

        Debug.Log($"[Nivel {Definition.number}] {(won ? "VICTORIA" : "DERROTA")} ({reason}) | {score.total} pts " +
                  $"(kills {score.killPoints} + victoria {score.victoryBonus} + rapidez {score.speedBonus} + vida {score.healthBonus}) x{score.multiplier:0.0} | " +
                  $"tiempo {LevelDefinition.FormatTime(Elapsed)} | kills {Kills} | daño {DamageTaken:0}");

        StartCoroutine(EndSequence());
    }

    private IEnumerator EndSequence()
    {
        // Se guarda de una vez (en paralelo a la animacion de muerte / celebracion).
        SaveResult();

        yield return new WaitForSecondsRealtime(endDelay);
        PauseControl.Push(this);   // PauseControl es el dueno de timeScale: asi las cartas no reanudan detras

        if (LevelFinished == null) gameObject.AddComponent<LevelResultFallbackUI>();
        LevelFinished?.Invoke(Outcome);
    }

    /// <summary>Reintenta guardar el resultado (boton de la pantalla de resultado si fallo).</summary>
    public void RetrySave()
    {
        if (Outcome != null && !IsSaving && SavedResponse == null) SaveResult();
    }

    private async void SaveResult()
    {
        IsSaving = true;
        LastSaveError = null;

        var request = new LevelResultRequest
        {
            level = Definition.number,
            won = Outcome.won,
            score = Outcome.score.total,
            kills = Outcome.kills,
            timeSeconds = Outcome.timeSeconds,
            damageTaken = Outcome.damageTaken,
            seed = Outcome.seed
        };

        Result<LevelResultResponse> r;
        try
        {
            if (Backend.Service.IsLoggedIn) r = await Backend.Service.SubmitLevelResult(request);
            else if (GuestSession.IsActive) r = GuestSession.Submit(request);   // progreso local del invitado
            else r = Result<LevelResultResponse>.Fail(ResultCode.NoSession, "Inicia sesión para guardar tu puntaje.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            r = Result<LevelResultResponse>.Fail(ResultCode.Unknown, "Error inesperado al guardar.");
        }

        IsSaving = false;
        if (r.Ok)
        {
            SavedResponse = r.Data;
            ResultSaved?.Invoke(r.Data);
        }
        else
        {
            LastSaveError = r;
            Debug.LogWarning($"[Nivel] No se guardó el resultado: {r.Message}");
            ResultSaveFailed?.Invoke(r);
        }
    }

    // ================================================================ HUD de respaldo

    private GUIStyle hudStyle;

    private void OnGUI()
    {
        if (!ShowFallbackHud || Finished || Definition == null) return;

        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }
        hudStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.035f);

        string text = $"Nivel {Definition.number}: {Definition.name}\n{GoalProgressText}";
        var rect = new Rect(0, Screen.height * 0.09f, Screen.width, Screen.height * 0.12f);
        var shadow = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
        Color c = hudStyle.normal.textColor;
        hudStyle.normal.textColor = Color.black;
        GUI.Label(shadow, text, hudStyle);
        hudStyle.normal.textColor = c;
        GUI.Label(rect, text, hudStyle);
    }
}
