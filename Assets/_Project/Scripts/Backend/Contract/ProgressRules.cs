using UnityEngine;

/// <summary>
/// Reglas de progresion compartidas por <see cref="MockBackend"/> y <see cref="FirebaseBackend"/>, para que los
/// dos se comporten exactamente igual:
///  - El puntaje de un nivel solo cuenta su MEJOR intento (repetir un nivel no infla el ranking).
///  - Puntaje total = suma de los mejores puntajes de los 10 niveles.
///  - Ganar el nivel N (el ultimo desbloqueado) desbloquea el N+1.
/// </summary>
public static class ProgressRules
{
    /// <summary>Comprueba que el resultado se pueda guardar (nivel valido y desbloqueado).</summary>
    public static Result Validate(UserProfile profile, LevelResultRequest request)
    {
        if (request == null) return Result.Fail(ResultCode.InvalidInput, "Resultado vacío.");
        if (request.level < 1 || request.level > UserProfile.MaxLevels)
            return Result.Fail(ResultCode.InvalidInput, $"El nivel {request.level} no existe.");
        if (profile != null && !profile.IsUnlocked(request.level))
            return Result.Fail(ResultCode.LevelLocked, $"El nivel {request.level} todavía está bloqueado.");
        if (request.score < 0) return Result.Fail(ResultCode.InvalidInput, "Puntaje inválido.");
        return Result.Success();
    }

    /// <summary>Aplica el resultado sobre el perfil (lo modifica) y devuelve lo que debe mostrar la UI.</summary>
    public static LevelResultResponse Apply(UserProfile profile, LevelResultRequest request)
    {
        int oldTotal = profile.totalScore;

        LevelProgress lp = profile.GetOrCreateLevel(request.level);
        int previousBest = lp.bestScore;
        bool newRecord = request.score > previousBest;

        lp.attempts++;
        if (newRecord) lp.bestScore = request.score;
        if (request.won)
        {
            lp.completed = true;
            if (lp.bestTime <= 0f || request.timeSeconds < lp.bestTime) lp.bestTime = request.timeSeconds;
        }

        int unlocked = 0;
        if (request.won && request.level >= profile.maxUnlockedLevel && request.level < UserProfile.MaxLevels)
        {
            profile.maxUnlockedLevel = request.level + 1;
            unlocked = profile.maxUnlockedLevel;
        }

        profile.gamesPlayed++;
        profile.totalKills += Mathf.Max(0, request.kills);
        Recalculate(profile);

        return new LevelResultResponse
        {
            level = request.level,
            won = request.won,
            levelScore = request.score,
            previousBest = previousBest,
            isNewRecord = newRecord,
            unlockedLevel = unlocked,
            newTotalScore = profile.totalScore,
            scoreGained = profile.totalScore - oldTotal,
            profile = profile.Clone()
        };
    }

    /// <summary>Recalcula los campos derivados (puntaje total y niveles completados).</summary>
    public static void Recalculate(UserProfile profile)
    {
        int total = 0, completed = 0;
        if (profile.levels != null)
            foreach (var l in profile.levels)
            {
                total += l.bestScore;
                if (l.completed) completed++;
            }
        profile.totalScore = total;
        profile.levelsCompleted = completed;
        profile.maxUnlockedLevel = Mathf.Clamp(profile.maxUnlockedLevel, 1, UserProfile.MaxLevels);
    }
}
