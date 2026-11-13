using System.Text.RegularExpressions;

/// <summary>
/// Validaciones locales para los formularios de registro y login. La UI puede llamarlas antes de enviar para
/// mostrar el error al instante; los backends las vuelven a aplicar por seguridad.
/// </summary>
public static class BackendValidation
{
    public const int MinPasswordLength = 6;     // minimo que exige Firebase
    public const int MinNameLength = 3;
    public const int MaxNameLength = 16;

    private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    public static Result ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Result.Fail(ResultCode.InvalidInput, "Escribe tu correo.");
        if (!EmailRegex.IsMatch(email.Trim())) return Result.Fail(ResultCode.InvalidEmail, "El correo no es válido.");
        return Result.Success();
    }

    public static Result ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return Result.Fail(ResultCode.InvalidInput, "Escribe tu contraseña.");
        if (password.Length < MinPasswordLength)
            return Result.Fail(ResultCode.WeakPassword, $"La contraseña debe tener al menos {MinPasswordLength} caracteres.");
        return Result.Success();
    }

    public static Result ValidateDisplayName(string name)
    {
        string n = name?.Trim() ?? "";
        if (n.Length < MinNameLength || n.Length > MaxNameLength)
            return Result.Fail(ResultCode.InvalidInput, $"El nombre debe tener entre {MinNameLength} y {MaxNameLength} caracteres.");
        return Result.Success();
    }

    public static Result ValidateLogin(string email, string password)
    {
        Result r = ValidateEmail(email);
        if (!r.Ok) return r;
        if (string.IsNullOrEmpty(password)) return Result.Fail(ResultCode.InvalidInput, "Escribe tu contraseña.");
        return Result.Success();
    }

    /// <summary>Validacion completa del formulario de registro (incluye confirmar contrasena).</summary>
    public static Result ValidateRegister(string displayName, string email, string password, string confirmPassword)
    {
        Result r = ValidateDisplayName(displayName);
        if (!r.Ok) return r;
        r = ValidateEmail(email);
        if (!r.Ok) return r;
        r = ValidatePassword(password);
        if (!r.Ok) return r;
        if (password != confirmPassword) return Result.Fail(ResultCode.InvalidInput, "Las contraseñas no coinciden.");
        return Result.Success();
    }
}
