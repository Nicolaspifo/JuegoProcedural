using UnityEngine;

/// <summary>
/// Guarda en el dispositivo lo minimo para recuperar la sesion al volver a abrir la app (el RefreshToken).
/// La contrasena NUNCA se guarda. Se borra al cerrar sesion.
/// </summary>
public static class SessionStore
{
    private const string KeyRefresh = "apdm.auth.refreshToken";
    private const string KeyUid = "apdm.auth.uid";
    private const string KeyEmail = "apdm.auth.email";
    private const string KeyName = "apdm.auth.displayName";

    public class Stored
    {
        public string RefreshToken;
        public string Uid;
        public string Email;
        public string DisplayName;
    }

    public static void Save(AuthSession session)
    {
        PlayerPrefs.SetString(KeyRefresh, session.RefreshToken ?? "");
        PlayerPrefs.SetString(KeyUid, session.Uid ?? "");
        PlayerPrefs.SetString(KeyEmail, session.Email ?? "");
        PlayerPrefs.SetString(KeyName, session.DisplayName ?? "");
        PlayerPrefs.Save();
    }

    public static Stored Load()
    {
        string refresh = PlayerPrefs.GetString(KeyRefresh, "");
        if (string.IsNullOrEmpty(refresh)) return null;
        return new Stored
        {
            RefreshToken = refresh,
            Uid = PlayerPrefs.GetString(KeyUid, ""),
            Email = PlayerPrefs.GetString(KeyEmail, ""),
            DisplayName = PlayerPrefs.GetString(KeyName, "")
        };
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyRefresh);
        PlayerPrefs.DeleteKey(KeyUid);
        PlayerPrefs.DeleteKey(KeyEmail);
        PlayerPrefs.DeleteKey(KeyName);
        PlayerPrefs.Save();
    }
}
