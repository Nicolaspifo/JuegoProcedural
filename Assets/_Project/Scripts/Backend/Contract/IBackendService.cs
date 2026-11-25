using System;
using System.Threading.Tasks;

/// <summary>
/// CONTRATO entre las pantallas (UI) y el backend. Las pantallas SOLO hablan con esta interfaz, nunca con
/// Firebase directamente. Se obtiene con <c>Backend.Service</c>.
///
/// Hay dos implementaciones:
///  - <see cref="MockBackend"/>: datos falsos guardados en el dispositivo, con retraso y errores simulados.
///    Sirve para disenar y probar pantallas sin internet ni Firebase.
///  - <see cref="FirebaseBackend"/>: Firebase Authentication + Cloud Firestore por REST.
/// Cual se usa lo decide el asset <c>Resources/FirebaseSettings</c> (casilla "Use Mock").
///
/// Reglas para la UI:
///  - Todas las llamadas son asincronas (<c>await</c>): mostrar "Cargando..." y desactivar botones mientras tanto.
///  - Nunca lanzan excepciones por errores esperados: revisar <c>result.Ok</c> y mostrar <c>result.Message</c>
///    (ya viene en espanol, listo para pantalla).
/// </summary>
public interface IBackendService
{
    /// <summary>True si hay una sesion iniciada.</summary>
    bool IsLoggedIn { get; }

    /// <summary>Ultimo perfil cargado del usuario actual (null si no hay sesion). No hace llamadas de red.</summary>
    UserProfile CurrentUser { get; }

    /// <summary>Se dispara cuando cambia el perfil (login, nuevo puntaje...) o con null al cerrar sesion.</summary>
    event Action<UserProfile> ProfileChanged;

    /// <summary>
    /// Pantalla de inicio (splash): intenta recuperar la sesion guardada en el dispositivo.
    /// Ok = hay sesion y el perfil quedo cargado (ir al menu). No Ok con <see cref="Result.Code"/> = NoSession
    /// = ir al login. Cualquier otro codigo (por ejemplo sin internet) = mostrar error y boton Reintentar.
    /// </summary>
    Task<Result> TryRestoreSession();

    /// <summary>Crea la cuenta, inicia sesion y crea el perfil (puntaje 0, nivel 1 desbloqueado).</summary>
    Task<Result> Register(string email, string password, string displayName);

    /// <summary>Inicia sesion y carga el perfil.</summary>
    Task<Result> Login(string email, string password);

    /// <summary>Envia el correo de "olvide mi contrasena".</summary>
    Task<Result> SendPasswordReset(string email);

    /// <summary>Cierra la sesion y borra los datos guardados en el dispositivo.</summary>
    void Logout();

    /// <summary>Vuelve a leer el perfil desde el servidor (menu principal, selector de niveles).</summary>
    Task<Result<UserProfile>> LoadProfile();

    /// <summary>
    /// Guarda el resultado de un nivel: actualiza mejor puntaje, puntaje total, desbloquea el siguiente nivel si
    /// gano y deja registro de la partida. Lo llama <c>LevelRuntime</c> al terminar el nivel.
    /// </summary>
    Task<Result<LevelResultResponse>> SubmitLevelResult(LevelResultRequest request);

    /// <summary>Ranking general por puntaje total, mas la posicion del usuario actual.</summary>
    Task<Result<RankingResult>> GetRanking(int top = 20);
}
