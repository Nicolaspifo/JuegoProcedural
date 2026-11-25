using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Implementacion real del backend: Firebase Authentication (correo y contrasena) + Cloud Firestore, ambos por
/// su API REST con UnityWebRequest (funciona en Android y Web sin importar el SDK nativo).
///
/// Estructura en Firestore:
/// <code>
/// users/{uid}                      perfil + progreso (lo lee el ranking)
///   displayName       string
///   totalScore        int          suma de los mejores puntajes por nivel
///   maxUnlockedLevel  int          1..10
///   levelsCompleted   int
///   gamesPlayed       int
///   totalKills        int
///   levels            map          "1".."10" -> { bestScore, completed, attempts, bestTime }
///   createdAt / updatedAt  timestamp
/// users/{uid}/runs/{autoId}        historial: una entrada por partida jugada
///   level, won, score, kills, timeSeconds, damageTaken, seed, platform, playedAt
/// </code>
/// El correo NO se guarda en Firestore (el ranking es publico para los usuarios logueados).
/// </summary>
public class FirebaseBackend : IBackendService
{
    private const string UsersCollection = "users";
    private const float TokenRefreshMarginSeconds = 120f;

    private readonly FirebaseAuthApi auth;
    private readonly FirestoreApi db;

    private AuthSession session;
    private UserProfile profile;

    public event Action<UserProfile> ProfileChanged;

    public bool IsLoggedIn => session != null;
    public UserProfile CurrentUser => profile;

    public FirebaseBackend(FirebaseSettings settings)
    {
        auth = new FirebaseAuthApi(settings.webApiKey.Trim());
        db = new FirestoreApi(settings.projectId.Trim());
        HttpClientUnity.TimeoutSeconds = settings.requestTimeoutSeconds;
        HttpClientUnity.LogRequests = settings.logRequests;
    }

    // ================================================================ sesion

    public async Task<Result> TryRestoreSession()
    {
        if (session != null && profile != null) return Result.Success();

        SessionStore.Stored stored = SessionStore.Load();
        if (stored == null) return Result.Fail(ResultCode.NoSession, "No hay una sesión guardada.");

        Result<AuthSession> refreshed = await auth.Refresh(stored.RefreshToken);
        if (!refreshed.Ok)
        {
            // Token revocado o vencido: hay que volver a iniciar sesion. Si fue la red, se conserva para reintentar.
            if (!refreshed.CanRetry) SessionStore.Clear();
            return refreshed;
        }

        AuthSession s = refreshed.Data;
        s.Email = stored.Email;
        s.DisplayName = stored.DisplayName;
        StartSession(s);

        Result<UserProfile> loaded = await LoadProfile();
        return loaded.Ok ? Result.Success($"Bienvenido de nuevo, {profile.displayName}.") : loaded;
    }

    public async Task<Result> Register(string email, string password, string displayName)
    {
        email = email?.Trim();
        displayName = displayName?.Trim();

        Result valid = BackendValidation.ValidateDisplayName(displayName);
        if (valid.Ok) valid = BackendValidation.ValidateEmail(email);
        if (valid.Ok) valid = BackendValidation.ValidatePassword(password);
        if (!valid.Ok) return valid;

        Result<AuthSession> signUp = await auth.SignUp(email, password);
        if (!signUp.Ok) return signUp;

        AuthSession s = signUp.Data;
        s.DisplayName = displayName;
        StartSession(s);

        // El nombre en la cuenta de Auth es opcional (solo se ve en la consola): si falla no se bloquea el registro.
        Result named = await auth.UpdateDisplayName(s.IdToken, displayName);
        if (!named.Ok) Debug.LogWarning($"[Firebase] No se pudo guardar el nombre en Auth: {named.Message}");

        UserProfile created = UserProfile.CreateNew(s.Uid, displayName, email);
        var doc = ToDocument(created);
        doc["createdAt"] = DateTime.UtcNow;

        Result write = await WithToken(token => db.MergeDocument(UserPath(s.Uid), doc, token));
        if (!write.Ok) return write;   // la cuenta ya existe: al iniciar sesion se crea el perfil que falte

        SetProfile(created);
        Debug.Log($"[Firebase] Usuario registrado: {displayName} ({s.Uid})");
        return Result.Success($"¡Cuenta creada! Bienvenido, {displayName}.");
    }

    public async Task<Result> Login(string email, string password)
    {
        email = email?.Trim();
        Result valid = BackendValidation.ValidateLogin(email, password);
        if (!valid.Ok) return valid;

        Result<AuthSession> signIn = await auth.SignIn(email, password);
        if (!signIn.Ok) return signIn;

        StartSession(signIn.Data);

        Result<UserProfile> loaded = await LoadProfile();
        if (!loaded.Ok) return loaded;

        Debug.Log($"[Firebase] Sesión iniciada: {profile.displayName} ({profile.uid})");
        return Result.Success($"Hola, {profile.displayName}.");
    }

    public async Task<Result> SendPasswordReset(string email)
    {
        email = email?.Trim();
        Result valid = BackendValidation.ValidateEmail(email);
        if (!valid.Ok) return valid;
        return await auth.SendPasswordReset(email);
    }

    public void Logout()
    {
        string name = profile?.displayName;
        session = null;
        SessionStore.Clear();
        SetProfile(null);
        Debug.Log($"[Firebase] Sesión cerrada{(name != null ? $" ({name})" : "")}.");
    }

    // ================================================================ perfil y progreso

    public async Task<Result<UserProfile>> LoadProfile()
    {
        if (session == null) return Result<UserProfile>.Fail(ResultCode.NoSession, "Inicia sesión primero.");

        Result<UserProfile> read = await ReadOrCreateProfile();
        if (!read.Ok) return read;

        SetProfile(read.Data);
        return Result<UserProfile>.Success(profile.Clone());
    }

    public async Task<Result<LevelResultResponse>> SubmitLevelResult(LevelResultRequest request)
    {
        if (session == null)
            return Result<LevelResultResponse>.Fail(ResultCode.NoSession, "Inicia sesión para guardar tu puntaje.");

        // Siempre se parte del perfil del servidor (por si se jugo en otro dispositivo).
        Result<UserProfile> read = await ReadOrCreateProfile();
        if (!read.Ok) return Result<LevelResultResponse>.From(read);

        UserProfile fresh = read.Data;
        Result valid = ProgressRules.Validate(fresh, request);
        if (!valid.Ok) return Result<LevelResultResponse>.From(valid);

        LevelResultResponse response = ProgressRules.Apply(fresh, request);

        Result write = await WithToken(token => db.MergeDocument(UserPath(fresh.uid), ToDocument(fresh), token));
        if (!write.Ok) return Result<LevelResultResponse>.From(write);

        // Historial de partidas: si falla no se pierde el progreso (ya quedo guardado arriba).
        var run = new Dictionary<string, object>
        {
            ["level"] = request.level,
            ["won"] = request.won,
            ["score"] = request.score,
            ["kills"] = request.kills,
            ["timeSeconds"] = Math.Round(request.timeSeconds, 2),
            ["damageTaken"] = Math.Round(request.damageTaken, 2),
            ["seed"] = request.seed,
            ["platform"] = Application.platform.ToString(),
            ["playedAt"] = DateTime.UtcNow
        };
        Result history = await WithToken(token => db.AddDocument(UserPath(fresh.uid) + "/runs", run, token));
        if (!history.Ok) Debug.LogWarning($"[Firebase] No se guardo el historial de la partida: {history.Message}");

        SetProfile(fresh);
        Debug.Log($"[Firebase] Nivel {request.level} guardado: {(request.won ? "victoria" : "derrota")}, {request.score} pts, total {fresh.totalScore}.");
        return Result<LevelResultResponse>.Success(response);
    }

    // ================================================================ ranking

    public async Task<Result<RankingResult>> GetRanking(int top = 20)
    {
        if (session == null) return Result<RankingResult>.Fail(ResultCode.NoSession, "Inicia sesión para ver el ranking.");
        top = Mathf.Clamp(top, 1, 100);

        var query = await WithToken(token => db.QueryTop(UsersCollection, "totalScore", top, token));
        if (!query.Ok) return Result<RankingResult>.From(query);

        var ranking = new RankingResult();
        int rank = 0;
        foreach (var row in query.Data)
        {
            rank++;
            var entry = new RankingEntry
            {
                rank = rank,
                uid = row.Key,
                displayName = FirestoreConvert.GetString(row.Value, "displayName", "Jugador"),
                totalScore = FirestoreConvert.GetInt(row.Value, "totalScore"),
                maxUnlockedLevel = FirestoreConvert.GetInt(row.Value, "maxUnlockedLevel", 1),
                levelsCompleted = FirestoreConvert.GetInt(row.Value, "levelsCompleted"),
                isCurrentUser = row.Key == session.Uid
            };
            ranking.entries.Add(entry);
            if (entry.isCurrentUser)
            {
                ranking.me = entry;
                ranking.meInTop = true;
            }
        }

        // Si el usuario no esta en el top, su posicion = cuantos tienen MAS puntos que el + 1.
        if (ranking.me == null && profile != null)
        {
            var count = await WithToken(token => db.CountGreaterThan(UsersCollection, "totalScore", profile.totalScore, token));
            ranking.me = new RankingEntry
            {
                rank = count.Ok ? count.Data + 1 : 0,
                uid = profile.uid,
                displayName = profile.displayName,
                totalScore = profile.totalScore,
                maxUnlockedLevel = profile.maxUnlockedLevel,
                levelsCompleted = profile.levelsCompleted,
                isCurrentUser = true
            };
        }

        return Result<RankingResult>.Success(ranking);
    }

    // ================================================================ internos

    private static string UserPath(string uid) => $"{UsersCollection}/{uid}";

    private void StartSession(AuthSession s)
    {
        session = s;
        SessionStore.Save(s);
    }

    private void SetProfile(UserProfile p)
    {
        profile = p;
        ProfileChanged?.Invoke(p?.Clone());
    }

    /// <summary>Lee users/{uid}; si no existe (registro que fallo a medias) lo crea con valores iniciales.</summary>
    private async Task<Result<UserProfile>> ReadOrCreateProfile()
    {
        var read = await WithToken(token => db.GetDocument(UserPath(session.Uid), token));
        if (!read.Ok) return Result<UserProfile>.From(read);

        if (read.Data != null)
            return Result<UserProfile>.Success(FromDocument(session.Uid, read.Data, session.Email));

        string name = !string.IsNullOrEmpty(session.DisplayName) ? session.DisplayName : NameFromEmail(session.Email);
        UserProfile created = UserProfile.CreateNew(session.Uid, name, session.Email);
        var doc = ToDocument(created);
        doc["createdAt"] = DateTime.UtcNow;

        Result write = await WithToken(token => db.MergeDocument(UserPath(session.Uid), doc, token));
        if (!write.Ok) return Result<UserProfile>.From(write);
        return Result<UserProfile>.Success(created);
    }

    /// <summary>Obtiene un IdToken valido (lo renueva si esta por vencer).</summary>
    private async Task<Result<string>> GetValidToken(bool forceRefresh = false)
    {
        if (session == null) return Result<string>.Fail(ResultCode.NoSession, "Inicia sesión primero.");

        if (forceRefresh || Time.realtimeSinceStartup > session.ExpiresAtRealtime - TokenRefreshMarginSeconds)
        {
            Result<AuthSession> refreshed = await auth.Refresh(session.RefreshToken);
            if (!refreshed.Ok)
            {
                if (refreshed.Code == ResultCode.SessionExpired) Logout();
                return Result<string>.From(refreshed);
            }
            session.IdToken = refreshed.Data.IdToken;
            session.RefreshToken = refreshed.Data.RefreshToken;
            session.ExpiresAtRealtime = refreshed.Data.ExpiresAtRealtime;
            SessionStore.Save(session);
        }
        return Result<string>.Success(session.IdToken);
    }

    /// <summary>Ejecuta una llamada con token; si el servidor dice que vencio, renueva y reintenta una vez.</summary>
    private async Task<Result> WithToken(Func<string, Task<Result>> call)
    {
        Result<string> token = await GetValidToken();
        if (!token.Ok) return token;

        Result r = await call(token.Data);
        if (r.Code != ResultCode.SessionExpired) return r;

        token = await GetValidToken(forceRefresh: true);
        return token.Ok ? await call(token.Data) : token;
    }

    private async Task<Result<T>> WithToken<T>(Func<string, Task<Result<T>>> call)
    {
        Result<string> token = await GetValidToken();
        if (!token.Ok) return Result<T>.From(token);

        Result<T> r = await call(token.Data);
        if (r.Code != ResultCode.SessionExpired) return r;

        token = await GetValidToken(forceRefresh: true);
        return token.Ok ? await call(token.Data) : Result<T>.From(token);
    }

    private static string NameFromEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return "Jugador";
        int at = email.IndexOf('@');
        string n = at > 0 ? email.Substring(0, at) : email;
        if (n.Length < BackendValidation.MinNameLength) return "Jugador";   // las reglas exigen 3..16 caracteres
        return n.Length > BackendValidation.MaxNameLength ? n.Substring(0, BackendValidation.MaxNameLength) : n;
    }

    // ---------------------------------------------------------------- mapeo perfil <-> documento

    private static Dictionary<string, object> ToDocument(UserProfile p)
    {
        var levels = new Dictionary<string, object>();
        if (p.levels != null)
            foreach (LevelProgress l in p.levels)
                levels[l.level.ToString()] = new Dictionary<string, object>
                {
                    ["bestScore"] = l.bestScore,
                    ["completed"] = l.completed,
                    ["attempts"] = l.attempts,
                    ["bestTime"] = Math.Round(l.bestTime, 2)
                };

        return new Dictionary<string, object>
        {
            ["displayName"] = p.displayName,
            ["totalScore"] = p.totalScore,
            ["maxUnlockedLevel"] = p.maxUnlockedLevel,
            ["levelsCompleted"] = p.levelsCompleted,
            ["gamesPlayed"] = p.gamesPlayed,
            ["totalKills"] = p.totalKills,
            ["levels"] = levels,
            ["updatedAt"] = DateTime.UtcNow
        };
    }

    private static UserProfile FromDocument(string uid, Dictionary<string, object> d, string email)
    {
        var p = new UserProfile
        {
            uid = uid,
            email = email,
            displayName = FirestoreConvert.GetString(d, "displayName", "Jugador"),
            totalScore = FirestoreConvert.GetInt(d, "totalScore"),
            maxUnlockedLevel = FirestoreConvert.GetInt(d, "maxUnlockedLevel", 1),
            levelsCompleted = FirestoreConvert.GetInt(d, "levelsCompleted"),
            gamesPlayed = FirestoreConvert.GetInt(d, "gamesPlayed"),
            totalKills = FirestoreConvert.GetInt(d, "totalKills"),
            levels = new List<LevelProgress>()
        };

        Dictionary<string, object> levels = FirestoreConvert.GetMap(d, "levels");
        if (levels != null)
            foreach (var kv in levels)
            {
                if (!int.TryParse(kv.Key, out int n) || !(kv.Value is Dictionary<string, object> l)) continue;
                p.levels.Add(new LevelProgress
                {
                    level = n,
                    bestScore = FirestoreConvert.GetInt(l, "bestScore"),
                    completed = FirestoreConvert.GetBool(l, "completed"),
                    attempts = FirestoreConvert.GetInt(l, "attempts"),
                    bestTime = FirestoreConvert.GetFloat(l, "bestTime")
                });
            }
        p.levels.Sort((a, b) => a.level.CompareTo(b.level));
        ProgressRules.Recalculate(p);   // por si alguien edito el documento a mano
        return p;
    }
}
