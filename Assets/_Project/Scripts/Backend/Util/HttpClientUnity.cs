using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Respuesta HTTP simplificada.</summary>
public struct HttpResponse
{
    public long Code;
    public string Text;
    /// <summary>True si no hubo respuesta del servidor (sin internet, DNS, tiempo agotado...).</summary>
    public bool NetworkError;
    public string Error;

    public bool Ok => !NetworkError && Code >= 200 && Code < 300;
}

/// <summary>
/// Envoltorio de UnityWebRequest que se puede usar con async/await. Funciona en Android, iOS, Web y editor
/// (no usa hilos, asi que es seguro en WebGL). Las continuaciones vuelven al hilo principal de Unity.
/// </summary>
public static class HttpClientUnity
{
    public static bool LogRequests = true;
    public static int TimeoutSeconds = 15;

    public static async Task<HttpResponse> SendJson(string method, string url, string jsonBody, string bearerToken = null)
    {
        using var req = new UnityWebRequest(url, method);
        if (jsonBody != null)
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.SetRequestHeader("Content-Type", "application/json");
        }
        req.downloadHandler = new DownloadHandlerBuffer();
        if (!string.IsNullOrEmpty(bearerToken)) req.SetRequestHeader("Authorization", "Bearer " + bearerToken);
        req.timeout = TimeoutSeconds;

        if (LogRequests) Debug.Log($"[HTTP] {method} {StripKey(url)}");

        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        var tcs = new TaskCompletionSource<bool>();
        if (op.isDone) tcs.TrySetResult(true);
        else op.completed += _ => tcs.TrySetResult(true);
        await tcs.Task;

        var response = new HttpResponse
        {
            Code = req.responseCode,
            Text = req.downloadHandler != null ? req.downloadHandler.text : "",
            NetworkError = req.result == UnityWebRequest.Result.ConnectionError || req.responseCode == 0,
            Error = req.error
        };

        if (LogRequests)
        {
            if (response.Ok) Debug.Log($"[HTTP] {response.Code} {StripKey(url)}");
            else Debug.LogWarning($"[HTTP] {response.Code} {StripKey(url)} -> {req.error}\n{Truncate(response.Text)}");
        }
        return response;
    }

    // No ensuciar la consola con la API key (aunque no sea secreta).
    private static string StripKey(string url)
    {
        int k = url.IndexOf("key=", System.StringComparison.Ordinal);
        return k < 0 ? url : url.Substring(0, k) + "key=***";
    }

    private static string Truncate(string text) =>
        string.IsNullOrEmpty(text) || text.Length <= 600 ? text : text.Substring(0, 600) + "...";
}
