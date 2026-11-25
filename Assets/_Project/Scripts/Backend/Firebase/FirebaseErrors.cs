using System.Collections.Generic;

/// <summary>
/// Traduce las respuestas de error de Firebase a <see cref="Result"/> con mensajes en espanol para mostrar en
/// pantalla. Formato de Firebase: <c>{"error": {"code": 400, "message": "EMAIL_EXISTS", "status": "..."}}</c>.
/// </summary>
public static class FirebaseErrors
{
    public static Result<T> ToResult<T>(HttpResponse r) => Result<T>.From(ToResult(r));

    public static Result ToResult(HttpResponse r)
    {
        if (r.NetworkError)
            return Result.Fail(ResultCode.NetworkError, "Sin conexión. Revisa tu internet e inténtalo de nuevo.");

        string message = "", status = "";
        try
        {
            var root = MiniJson.Deserialize(r.Text);
            // runQuery a veces devuelve el error dentro de un array.
            if (root is List<object> list && list.Count > 0) root = list[0];
            if (root is Dictionary<string, object> obj && obj.TryGetValue("error", out object e) && e is Dictionary<string, object> err)
            {
                message = err.TryGetValue("message", out object m) ? m as string ?? "" : "";
                status = err.TryGetValue("status", out object s) ? s as string ?? "" : "";
            }
        }
        catch
        {
            // respuesta que no es JSON: se usa el codigo HTTP
        }

        // Firebase Auth manda codigos tipo "WEAK_PASSWORD : Password should be at least 6 characters".
        string code = message;
        int sep = code.IndexOf(' ');
        if (sep > 0) code = code.Substring(0, sep);

        switch (code)
        {
            case "EMAIL_EXISTS":
                return Result.Fail(ResultCode.EmailInUse, "Ese correo ya está registrado. Inicia sesión.");
            case "INVALID_EMAIL":
            case "MISSING_EMAIL":
                return Result.Fail(ResultCode.InvalidEmail, "El correo no es válido.");
            case "WEAK_PASSWORD":
            case "MISSING_PASSWORD":
                return Result.Fail(ResultCode.WeakPassword, "La contraseña debe tener al menos 6 caracteres.");
            case "EMAIL_NOT_FOUND":
            case "INVALID_PASSWORD":
            case "INVALID_LOGIN_CREDENTIALS":
            case "USER_NOT_FOUND":
                return Result.Fail(ResultCode.WrongCredentials, "Correo o contraseña incorrectos.");
            case "USER_DISABLED":
                return Result.Fail(ResultCode.UserDisabled, "Esta cuenta fue deshabilitada.");
            case "TOO_MANY_ATTEMPTS_TRY_LATER":
                return Result.Fail(ResultCode.TooManyAttempts, "Demasiados intentos. Espera un momento e inténtalo de nuevo.");
            case "TOKEN_EXPIRED":
            case "INVALID_REFRESH_TOKEN":
            case "INVALID_ID_TOKEN":
            case "CREDENTIAL_TOO_OLD_LOGIN_AGAIN":
                return Result.Fail(ResultCode.SessionExpired, "Tu sesión expiró. Vuelve a iniciar sesión.");
            case "OPERATION_NOT_ALLOWED":
                return Result.Fail(ResultCode.NotConfigured, "El inicio de sesión con correo no está activado en Firebase.");
        }

        if (message.StartsWith("API key not valid") || message.Contains("API_KEY_INVALID"))
            return Result.Fail(ResultCode.NotConfigured, "La Web API Key de Firebase no es válida (revisa FirebaseSettings).");

        if (r.Code == 401 || status == "UNAUTHENTICATED")
            return Result.Fail(ResultCode.SessionExpired, "Tu sesión expiró. Vuelve a iniciar sesión.");
        if (r.Code == 403 || status == "PERMISSION_DENIED")
            return Result.Fail(ResultCode.PermissionDenied, "Sin permiso para esa operación (revisa las reglas de Firestore).");
        if (r.Code == 404 && message.Contains("does not exist for project"))
            return Result.Fail(ResultCode.NotConfigured, "La base de datos Firestore no está creada en el proyecto.");
        if (r.Code == 429 || status == "RESOURCE_EXHAUSTED")
            return Result.Fail(ResultCode.TooManyAttempts, "Demasiadas peticiones. Inténtalo en un momento.");
        if (r.Code >= 500 || status == "UNAVAILABLE")
            return Result.Fail(ResultCode.NetworkError, "El servidor no responde. Inténtalo de nuevo.");

        string detail = string.IsNullOrEmpty(message) ? $"HTTP {r.Code}" : message;
        return Result.Fail(ResultCode.Unknown, $"Error inesperado ({detail}).");
    }
}
