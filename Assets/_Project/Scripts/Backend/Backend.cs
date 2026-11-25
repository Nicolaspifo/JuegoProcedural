using UnityEngine;

/// <summary>
/// Punto de acceso unico al backend: <c>Backend.Service</c>. Las pantallas usan siempre esto.
///
/// Ejemplo (pantalla de login):
/// <code>
/// async void OnLoginClicked()
/// {
///     SetLoading(true);
///     Result r = await Backend.Service.Login(emailInput.text, passwordInput.text);
///     SetLoading(false);
///     if (r.Ok) SceneManager.LoadScene("MenuPrincipal");
///     else errorText.text = r.Message;
/// }
/// </code>
/// </summary>
public static class Backend
{
    private static IBackendService service;

    public static IBackendService Service
    {
        get
        {
            if (service == null) service = CreateDefault();
            return service;
        }
    }

    /// <summary>True si se esta usando el Mock (datos falsos locales).</summary>
    public static bool IsMock => Service is MockBackend;

    /// <summary>Forzar una implementacion (pruebas). Normalmente no hace falta.</summary>
    public static void Use(IBackendService custom) => service = custom;

    private static IBackendService CreateDefault()
    {
        FirebaseSettings settings = FirebaseSettings.Load();

        if (settings == null)
        {
            Debug.LogWarning("[Backend] No se encontro Resources/FirebaseSettings: se usa el Mock.");
            return new MockBackend(null);
        }

        if (settings.useMock)
        {
            Debug.Log("[Backend] Usando MockBackend (datos falsos locales).");
            return new MockBackend(settings);
        }

        if (!settings.IsConfigured)
        {
            Debug.LogError("[Backend] FirebaseSettings no tiene Web API Key / Project ID: se usa el Mock.");
            return new MockBackend(settings);
        }

        Debug.Log($"[Backend] Usando Firebase (proyecto {settings.projectId}).");
        return new FirebaseBackend(settings);
    }

    // Si "Enter Play Mode Options" no recarga el dominio, los estaticos sobreviven: se reinicia al entrar en Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => service = null;
}
