using UnityEngine;

/// <summary>
/// Modo invitado: jugar sin cuenta. El progreso (niveles desbloqueados y récords) vive solo en memoria mientras
/// la app esta abierta: no se guarda en Firebase ni aparece en el ranking. Al iniciar sesion se descarta.
///
/// Las pantallas y los niveles usan <see cref="CurrentProfile"/> en lugar de <c>Backend.Service.CurrentUser</c>
/// para funcionar igual con cuenta o como invitado.
/// </summary>
public static class GuestSession
{
    public const string DisplayName = "Invitado";

    private static UserProfile profile;

    /// <summary>True si se esta jugando como invitado (y no hay sesion iniciada).</summary>
    public static bool IsActive => profile != null && !Backend.Service.IsLoggedIn;

    /// <summary>True si se pueden jugar niveles: con cuenta o como invitado.</summary>
    public static bool CanPlay => Backend.Service.IsLoggedIn || IsActive;

    /// <summary>Perfil del jugador actual: el de la cuenta, el del invitado o null si no hay ninguno.</summary>
    public static UserProfile CurrentProfile
    {
        get
        {
            UserProfile user = Backend.Service.CurrentUser;
            if (user != null) return user;
            return IsActive ? profile.Clone() : null;
        }
    }

    /// <summary>Entra como invitado. Si ya habia una partida de invitado, conserva su progreso.</summary>
    public static void Begin()
    {
        if (profile == null) profile = UserProfile.CreateNew("invitado", DisplayName, "");
        Debug.Log("[Invitado] Jugando sin cuenta: el progreso no se guarda en la nube.");
    }

    /// <summary>Descarta el progreso del invitado.</summary>
    public static void End() => profile = null;

    /// <summary>Aplica el resultado de un nivel al progreso local del invitado (mismas reglas que con cuenta).</summary>
    public static Result<LevelResultResponse> Submit(LevelResultRequest request)
    {
        if (!IsActive) return Result<LevelResultResponse>.Fail(ResultCode.NoSession, "Inicia sesión para guardar tu puntaje.");
        Result valid = ProgressRules.Validate(profile, request);
        if (!valid.Ok) return Result<LevelResultResponse>.From(valid);
        return Result<LevelResultResponse>.Success(ProgressRules.Apply(profile, request));
    }

    // Con la recarga de dominio desactivada los estaticos sobreviven entre Plays del editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => profile = null;
}
