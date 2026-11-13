using System;
using System.Collections.Generic;

/// <summary>Codigos de resultado para que la UI pueda reaccionar sin comparar textos.</summary>
public enum ResultCode
{
    Ok,
    NoSession,          // no hay sesion guardada: ir al login
    InvalidInput,       // validacion local (campos vacios, contrasena corta...)
    EmailInUse,
    InvalidEmail,
    WeakPassword,
    WrongCredentials,
    UserDisabled,
    TooManyAttempts,
    SessionExpired,     // hay que volver a iniciar sesion
    LevelLocked,
    NetworkError,       // sin internet o servidor caido: mostrar boton Reintentar
    PermissionDenied,   // reglas de Firestore
    NotConfigured,      // falta configurar FirebaseSettings
    Unknown
}

/// <summary>Resultado de una operacion sin datos. <see cref="Message"/> ya esta en espanol para mostrar.</summary>
public class Result
{
    public bool Ok { get; protected set; }
    public ResultCode Code { get; protected set; }
    public string Message { get; protected set; }

    /// <summary>True si conviene mostrar un boton Reintentar (problema de red).</summary>
    public bool CanRetry => Code == ResultCode.NetworkError;

    protected Result(bool ok, ResultCode code, string message)
    {
        Ok = ok;
        Code = code;
        Message = message ?? "";
    }

    public static Result Success(string message = "") => new Result(true, ResultCode.Ok, message);
    public static Result Fail(ResultCode code, string message) => new Result(false, code, message);

    public override string ToString() => Ok ? $"OK {Message}" : $"ERROR {Code}: {Message}";
}

/// <summary>Resultado con datos (<see cref="Data"/> es null si fallo).</summary>
public class Result<T> : Result
{
    public T Data { get; private set; }

    private Result(bool ok, ResultCode code, string message, T data) : base(ok, code, message) => Data = data;

    public static Result<T> Success(T data, string message = "") => new Result<T>(true, ResultCode.Ok, message, data);
    public new static Result<T> Fail(ResultCode code, string message) => new Result<T>(false, code, message, default);
    public static Result<T> From(Result other) => new Result<T>(false, other.Code, other.Message, default);
}

/// <summary>Progreso del jugador en un nivel concreto.</summary>
[Serializable]
public class LevelProgress
{
    public int level;
    public int bestScore;
    public bool completed;
    public int attempts;
    /// <summary>Mejor tiempo (segundos) en una partida GANADA; 0 si nunca lo gano.</summary>
    public float bestTime;
}

/// <summary>Perfil del usuario: lo que muestran el menu principal y el selector de niveles.</summary>
[Serializable]
public class UserProfile
{
    public const int MaxLevels = 10;

    public string uid;
    public string displayName;
    public string email;              // solo local, no se publica en el ranking
    public int totalScore;            // suma de los mejores puntajes de cada nivel
    public int maxUnlockedLevel = 1;  // 1..10
    public int levelsCompleted;
    public int gamesPlayed;
    public int totalKills;
    public List<LevelProgress> levels = new List<LevelProgress>();

    public bool IsUnlocked(int level) => level >= 1 && level <= maxUnlockedLevel && level <= MaxLevels;
    public bool IsCompleted(int level) => GetLevel(level)?.completed ?? false;
    public int BestScore(int level) => GetLevel(level)?.bestScore ?? 0;

    /// <summary>Progreso 0..1 para barras ("Nivel X/10").</summary>
    public float Progress01 => levelsCompleted / (float)MaxLevels;

    public LevelProgress GetLevel(int level)
    {
        if (levels == null) return null;
        for (int i = 0; i < levels.Count; i++)
            if (levels[i].level == level) return levels[i];
        return null;
    }

    public LevelProgress GetOrCreateLevel(int level)
    {
        LevelProgress p = GetLevel(level);
        if (p != null) return p;
        if (levels == null) levels = new List<LevelProgress>();
        p = new LevelProgress { level = level };
        levels.Add(p);
        levels.Sort((a, b) => a.level.CompareTo(b.level));
        return p;
    }

    public UserProfile Clone()
    {
        var copy = (UserProfile)MemberwiseClone();
        copy.levels = new List<LevelProgress>();
        if (levels != null)
            foreach (var l in levels)
                copy.levels.Add(new LevelProgress
                {
                    level = l.level, bestScore = l.bestScore, completed = l.completed,
                    attempts = l.attempts, bestTime = l.bestTime
                });
        return copy;
    }

    public static UserProfile CreateNew(string uid, string displayName, string email) =>
        new UserProfile { uid = uid, displayName = displayName, email = email, maxUnlockedLevel = 1 };
}

/// <summary>Lo que envia el juego al terminar un nivel.</summary>
[Serializable]
public class LevelResultRequest
{
    public int level;
    public bool won;
    public int score;
    public int kills;
    public float timeSeconds;
    public float damageTaken;
    /// <summary>Semilla procedural con la que se genero la partida (queda en el historial).</summary>
    public int seed;
}

/// <summary>Respuesta al guardar un nivel: lo que muestra la pantalla de resultado.</summary>
[Serializable]
public class LevelResultResponse
{
    public int level;
    public bool won;
    public int levelScore;
    public int previousBest;
    public bool isNewRecord;
    /// <summary>Nivel recien desbloqueado (0 si no se desbloqueo ninguno).</summary>
    public int unlockedLevel;
    public int newTotalScore;
    public int scoreGained;           // cuanto subio el puntaje total
    public UserProfile profile;
}

/// <summary>Una fila del ranking.</summary>
[Serializable]
public class RankingEntry
{
    public int rank;
    public string uid;
    public string displayName;
    public int totalScore;
    public int maxUnlockedLevel;
    public int levelsCompleted;
    public bool isCurrentUser;
}

/// <summary>Ranking: top N y la posicion del usuario actual (aunque no este en el top).</summary>
[Serializable]
public class RankingResult
{
    public List<RankingEntry> entries = new List<RankingEntry>();
    /// <summary>Fila del usuario actual (null si no hay sesion).</summary>
    public RankingEntry me;
    /// <summary>True si el usuario actual ya aparece dentro de <see cref="entries"/>.</summary>
    public bool meInTop;
}
