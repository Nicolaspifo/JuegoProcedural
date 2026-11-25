using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Que nivel se esta jugando y navegacion entre niveles. Las pantallas lo usan asi:
/// <code>
/// // Selector de niveles
/// bool desbloqueado = LevelSession.CanPlay(3);
/// LevelSession.Play(3);
///
/// // Pantalla de resultado
/// if (LevelSession.HasNextLevel) LevelSession.PlayNext();
/// LevelSession.Retry();
/// LevelSession.GoToMenu();
/// </code>
/// </summary>
public static class LevelSession
{
    /// <summary>Escena de juego (la que tiene al jugador, el spawner y el director de dificultad).</summary>
    public static string GameSceneName = "Principal";
    /// <summary>Escena a la que vuelve "Menu". La pantalla de menu/selector puede cambiarla.</summary>
    public static string MenuSceneName = "MenuPrincipal";

    /// <summary>True: cada intento genera un terreno distinto (misma tematica). False: siempre el mismo mapa.</summary>
    public static bool RandomizeEachAttempt = true;

    /// <summary>Nivel actual (1..10). 0 si todavia no se eligio ninguno.</summary>
    public static int CurrentLevelNumber { get; private set; }

    /// <summary>Semilla procedural de la partida actual.</summary>
    public static int RunSeed { get; private set; }

    public static LevelDefinition Current => LevelCatalog.Get(Mathf.Clamp(CurrentLevelNumber, 1, LevelCatalog.Count));

    /// <summary>True si el jugador actual (cuenta o invitado) tiene desbloqueado ese nivel (sin ninguno, solo el 1).</summary>
    public static bool CanPlay(int level)
    {
        if (level < 1 || level > LevelCatalog.Count) return false;
        UserProfile user = GuestSession.CurrentProfile;
        return user != null ? user.IsUnlocked(level) : level == 1;
    }

    /// <summary>Nivel mas alto que el usuario puede jugar (para un boton "Continuar").</summary>
    public static int HighestPlayableLevel
    {
        get
        {
            UserProfile user = GuestSession.CurrentProfile;
            return user != null ? Mathf.Clamp(user.maxUnlockedLevel, 1, LevelCatalog.Count) : 1;
        }
    }

    public static bool HasNextLevel => CurrentLevelNumber >= 1 && CanPlay(CurrentLevelNumber + 1);

    /// <summary>Carga la escena de juego con ese nivel. Devuelve false si esta bloqueado.</summary>
    public static bool Play(int level)
    {
        if (!CanPlay(level))
        {
            Debug.LogWarning($"[Niveles] El nivel {level} esta bloqueado.");
            return false;
        }
        Begin(level);
        SceneManager.LoadScene(GameSceneName);
        return true;
    }

    public static void Retry() => Play(Mathf.Max(1, CurrentLevelNumber));

    public static void PlayNext()
    {
        if (HasNextLevel) Play(CurrentLevelNumber + 1);
        else GoToMenu();
    }

    public static void GoToMenu()
    {
        PauseControl.Reset();
        SceneManager.LoadScene(MenuSceneName);
    }

    /// <summary>Si se dio Play directo en la escena de juego (editor), se arranca el nivel 1.</summary>
    internal static void EnsureLevelSelected()
    {
        if (CurrentLevelNumber < 1) Begin(1);
    }

    private static void Begin(int level)
    {
        CurrentLevelNumber = level;
        int baseSeed = LevelCatalog.BaseSeed(level);
        // Semilla acotada: InfiniteTilemapFloor suma la semilla a coordenadas float y valores enormes pierden precision.
        RunSeed = RandomizeEachAttempt ? (baseSeed + Random.Range(1, 50000)) % 100000 : baseSeed;
        PauseControl.Reset();   // devuelve Time.timeScale a 1 antes de cambiar de escena
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        CurrentLevelNumber = 0;
        RunSeed = 0;
    }
}
