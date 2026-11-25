using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Datos de sesion que devuelve Firebase Authentication.</summary>
public class AuthSession
{
    public string Uid;
    public string Email;
    public string DisplayName;
    public string IdToken;        // va en "Authorization: Bearer" de Firestore; dura 1 hora
    public string RefreshToken;   // sirve para pedir un IdToken nuevo sin volver a pedir la contrasena
    public float ExpiresAtRealtime;
}

/// <summary>
/// Firebase Authentication por REST (Identity Toolkit). Documentacion:
/// https://firebase.google.com/docs/reference/rest/auth
/// </summary>
public class FirebaseAuthApi
{
    private const string IdentityUrl = "https://identitytoolkit.googleapis.com/v1/accounts:";
    private const string TokenUrl = "https://securetoken.googleapis.com/v1/token?key=";

    private readonly string apiKey;

    public FirebaseAuthApi(string apiKey) => this.apiKey = apiKey;

    public Task<Result<AuthSession>> SignUp(string email, string password) =>
        PostAuth("signUp", new Dictionary<string, object>
        {
            ["email"] = email,
            ["password"] = password,
            ["returnSecureToken"] = true
        });

    public Task<Result<AuthSession>> SignIn(string email, string password) =>
        PostAuth("signInWithPassword", new Dictionary<string, object>
        {
            ["email"] = email,
            ["password"] = password,
            ["returnSecureToken"] = true
        });

    /// <summary>Guarda el nombre visible en la cuenta de Auth (se ve en la consola de Firebase).</summary>
    public async Task<Result> UpdateDisplayName(string idToken, string displayName)
    {
        string body = MiniJson.Serialize(new Dictionary<string, object>
        {
            ["idToken"] = idToken,
            ["displayName"] = displayName,
            ["returnSecureToken"] = false
        });
        HttpResponse r = await HttpClientUnity.SendJson("POST", IdentityUrl + "update?key=" + apiKey, body);
        return r.Ok ? Result.Success() : FirebaseErrors.ToResult(r);
    }

    public async Task<Result> SendPasswordReset(string email)
    {
        string body = MiniJson.Serialize(new Dictionary<string, object>
        {
            ["requestType"] = "PASSWORD_RESET",
            ["email"] = email
        });
        HttpResponse r = await HttpClientUnity.SendJson("POST", IdentityUrl + "sendOobCode?key=" + apiKey, body);
        return r.Ok ? Result.Success("Te enviamos un correo para restablecer la contraseña.") : FirebaseErrors.ToResult(r);
    }

    /// <summary>Cambia un RefreshToken por un IdToken nuevo (sesion persistente entre aperturas de la app).</summary>
    public async Task<Result<AuthSession>> Refresh(string refreshToken)
    {
        string body = MiniJson.Serialize(new Dictionary<string, object>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        HttpResponse r = await HttpClientUnity.SendJson("POST", TokenUrl + apiKey, body);
        if (!r.Ok) return FirebaseErrors.ToResult<AuthSession>(r);

        var data = MiniJson.DeserializeObject(r.Text);
        var session = new AuthSession
        {
            Uid = FirestoreConvert.GetString(data, "user_id"),
            IdToken = FirestoreConvert.GetString(data, "id_token"),
            RefreshToken = FirestoreConvert.GetString(data, "refresh_token"),
            ExpiresAtRealtime = ExpiresAt(FirestoreConvert.GetString(data, "expires_in", "3600"))
        };
        return Result<AuthSession>.Success(session);
    }

    private async Task<Result<AuthSession>> PostAuth(string action, Dictionary<string, object> payload)
    {
        HttpResponse r = await HttpClientUnity.SendJson("POST", IdentityUrl + action + "?key=" + apiKey, MiniJson.Serialize(payload));
        if (!r.Ok) return FirebaseErrors.ToResult<AuthSession>(r);

        var data = MiniJson.DeserializeObject(r.Text);
        var session = new AuthSession
        {
            Uid = FirestoreConvert.GetString(data, "localId"),
            Email = FirestoreConvert.GetString(data, "email"),
            DisplayName = FirestoreConvert.GetString(data, "displayName"),
            IdToken = FirestoreConvert.GetString(data, "idToken"),
            RefreshToken = FirestoreConvert.GetString(data, "refreshToken"),
            ExpiresAtRealtime = ExpiresAt(FirestoreConvert.GetString(data, "expiresIn", "3600"))
        };
        return Result<AuthSession>.Success(session);
    }

    private static float ExpiresAt(string expiresInSeconds)
    {
        float seconds = float.TryParse(expiresInSeconds, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float s) ? s : 3600f;
        return UnityEngine.Time.realtimeSinceStartup + seconds;
    }
}
