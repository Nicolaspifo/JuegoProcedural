using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Backend FALSO para disenar y probar pantallas sin internet ni Firebase. Se comporta igual que el real
/// (mismas validaciones, mismas reglas de progresion, mismos mensajes), con un retraso simulado y, si se
/// configura, errores de red al azar para probar los estados de error.
///
/// Las cuentas se guardan en PlayerPrefs del dispositivo, asi que tambien "recuerda" la sesion al reabrir.
/// Trae jugadores inventados para que el ranking no se vea vacio.
/// Activar/desactivar: casilla "Use Mock" en Resources/FirebaseSettings.
/// </summary>
public class MockBackend : IBackendService
{
    private const string DbKey = "apdm.mock.db";
    private const string SessionKey = "apdm.mock.session";

    [Serializable]
    private class MockAccount
    {
        public string email;
        public string password;   // solo para el Mock local; el backend real nunca guarda contrasenas
        public UserProfile profile;
    }

    [Serializable]
    private class MockDb
    {
        public List<MockAccount> accounts = new List<MockAccount>();
    }

    private readonly float delay;
    private readonly float errorChance;
    private MockDb db;
    private MockAccount current;

    public event Action<UserProfile> ProfileChanged;

    public bool IsLoggedIn => current != null;
    public UserProfile CurrentUser => current?.profile?.Clone();

    public MockBackend(FirebaseSettings settings)
    {
        delay = settings != null ? settings.mockDelay : 0.6f;
        errorChance = settings != null ? settings.mockNetworkErrorChance : 0f;
        LoadDb();
    }

    // ================================================================ sesion

    public async Task<Result> TryRestoreSession()
    {
        if (!await SimulateNetwork()) return NetworkFail();

        string uid = PlayerPrefs.GetString(SessionKey, "");
        MockAccount acc = string.IsNullOrEmpty(uid) ? null : db.accounts.Find(a => a.profile.uid == uid);
        if (acc == null) return Result.Fail(ResultCode.NoSession, "No hay una sesión guardada.");

        SetCurrent(acc);
        return Result.Success($"Bienvenido de nuevo, {acc.profile.displayName}.");
    }

    public async Task<Result> Register(string email, string password, string displayName)
    {
        email = email?.Trim();
        displayName = displayName?.Trim();

        Result valid = BackendValidation.ValidateDisplayName(displayName);
        if (valid.Ok) valid = BackendValidation.ValidateEmail(email);
        if (valid.Ok) valid = BackendValidation.ValidatePassword(password);
        if (!valid.Ok) return valid;

        if (!await SimulateNetwork()) return NetworkFail();

        if (FindByEmail(email) != null)
            return Result.Fail(ResultCode.EmailInUse, "Ese correo ya está registrado. Inicia sesión.");

        var acc = new MockAccount
        {
            email = email.ToLowerInvariant(),
            password = password,
            profile = UserProfile.CreateNew("mock-" + Guid.NewGuid().ToString("N").Substring(0, 12), displayName, email)
        };
        db.accounts.Add(acc);
        SaveDb();
        SetCurrent(acc);
        return Result.Success($"¡Cuenta creada! Bienvenido, {displayName}.");
    }

    public async Task<Result> Login(string email, string password)
    {
        email = email?.Trim();
        Result valid = BackendValidation.ValidateLogin(email, password);
        if (!valid.Ok) return valid;

        if (!await SimulateNetwork()) return NetworkFail();

        MockAccount acc = FindByEmail(email);
        if (acc == null || acc.password != password)
            return Result.Fail(ResultCode.WrongCredentials, "Correo o contraseña incorrectos.");

        SetCurrent(acc);
        return Result.Success($"Hola, {acc.profile.displayName}.");
    }

    public async Task<Result> SendPasswordReset(string email)
    {
        Result valid = BackendValidation.ValidateEmail(email);
        if (!valid.Ok) return valid;
        if (!await SimulateNetwork()) return NetworkFail();
        return Result.Success("Te enviamos un correo para restablecer la contraseña. (Mock: no se envía nada)");
    }

    public void Logout()
    {
        current = null;
        PlayerPrefs.DeleteKey(SessionKey);
        PlayerPrefs.Save();
        ProfileChanged?.Invoke(null);
    }

    // ================================================================ perfil y progreso

    public async Task<Result<UserProfile>> LoadProfile()
    {
        if (current == null) return Result<UserProfile>.Fail(ResultCode.NoSession, "Inicia sesión primero.");
        if (!await SimulateNetwork()) return Result<UserProfile>.From(NetworkFail());
        return Result<UserProfile>.Success(current.profile.Clone());
    }

    public async Task<Result<LevelResultResponse>> SubmitLevelResult(LevelResultRequest request)
    {
        if (current == null)
            return Result<LevelResultResponse>.Fail(ResultCode.NoSession, "Inicia sesión para guardar tu puntaje.");

        Result valid = ProgressRules.Validate(current.profile, request);
        if (!valid.Ok) return Result<LevelResultResponse>.From(valid);

        if (!await SimulateNetwork()) return Result<LevelResultResponse>.From(NetworkFail());

        LevelResultResponse response = ProgressRules.Apply(current.profile, request);
        SaveDb();
        ProfileChanged?.Invoke(current.profile.Clone());
        Debug.Log($"[Mock] Nivel {request.level} guardado: {(request.won ? "victoria" : "derrota")}, {request.score} pts, total {current.profile.totalScore}.");
        return Result<LevelResultResponse>.Success(response);
    }

    // ================================================================ ranking

    public async Task<Result<RankingResult>> GetRanking(int top = 20)
    {
        if (current == null) return Result<RankingResult>.Fail(ResultCode.NoSession, "Inicia sesión para ver el ranking.");
        if (!await SimulateNetwork()) return Result<RankingResult>.From(NetworkFail());

        var all = new List<UserProfile>();
        foreach (var a in db.accounts) all.Add(a.profile);
        all.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));

        var ranking = new RankingResult();
        for (int i = 0; i < all.Count; i++)
        {
            UserProfile p = all[i];
            var entry = new RankingEntry
            {
                rank = i + 1,
                uid = p.uid,
                displayName = p.displayName,
                totalScore = p.totalScore,
                maxUnlockedLevel = p.maxUnlockedLevel,
                levelsCompleted = p.levelsCompleted,
                isCurrentUser = p.uid == current.profile.uid
            };
            if (i < top) ranking.entries.Add(entry);
            if (entry.isCurrentUser)
            {
                ranking.me = entry;
                ranking.meInTop = i < top;
            }
        }
        return Result<RankingResult>.Success(ranking);
    }

    // ================================================================ utilidades de prueba

    /// <summary>Borra todas las cuentas falsas del dispositivo (menu contextual del panel de pruebas).</summary>
    public void ResetAllData()
    {
        PlayerPrefs.DeleteKey(DbKey);
        PlayerPrefs.DeleteKey(SessionKey);
        PlayerPrefs.Save();
        current = null;
        LoadDb();
        ProfileChanged?.Invoke(null);
    }

    // ================================================================ internos

    private void SetCurrent(MockAccount acc)
    {
        current = acc;
        PlayerPrefs.SetString(SessionKey, acc.profile.uid);
        PlayerPrefs.Save();
        ProfileChanged?.Invoke(acc.profile.Clone());
    }

    private MockAccount FindByEmail(string email)
    {
        string e = email.Trim().ToLowerInvariant();
        return db.accounts.Find(a => a.email == e);
    }

    /// <summary>Espera el retraso (en tiempo real: funciona aunque el juego este en pausa) y decide si falla.</summary>
    private async Task<bool> SimulateNetwork()
    {
        float until = Time.realtimeSinceStartup + delay;
        while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
        return UnityEngine.Random.value >= errorChance;
    }

    private static Result NetworkFail() =>
        Result.Fail(ResultCode.NetworkError, "Sin conexión. Revisa tu internet e inténtalo de nuevo. (Mock)");

    private void LoadDb()
    {
        string json = PlayerPrefs.GetString(DbKey, "");
        db = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<MockDb>(json);
        if (db == null || db.accounts == null)
        {
            db = new MockDb();
            SeedFakePlayers();
            SaveDb();
        }
    }

    private void SaveDb()
    {
        PlayerPrefs.SetString(DbKey, JsonUtility.ToJson(db));
        PlayerPrefs.Save();
    }

    // Rivales inventados para que el ranking tenga gente desde el principio.
    private void SeedFakePlayers()
    {
        string[] names = { "JaguarNegro", "LaPatasola", "Chiguirito", "CondorAndino", "RanaDorada", "ElMohan", "Arriera22", "Venadito" };
        var rng = new System.Random(1234);
        foreach (string n in names)
        {
            UserProfile p = UserProfile.CreateNew("bot-" + n.ToLowerInvariant(), n, "");
            int cleared = rng.Next(0, 8);
            for (int lvl = 1; lvl <= cleared + 1 && lvl <= UserProfile.MaxLevels; lvl++)
            {
                ProgressRules.Apply(p, new LevelResultRequest
                {
                    level = lvl,
                    won = lvl <= cleared,
                    score = rng.Next(300, 1500) * lvl,
                    kills = rng.Next(20, 120),
                    timeSeconds = rng.Next(90, 300)
                });
            }
            db.accounts.Add(new MockAccount { email = "", password = Guid.NewGuid().ToString(), profile = p });
        }
    }
}
