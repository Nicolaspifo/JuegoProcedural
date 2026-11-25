using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Escena "Inicio Secion": correo + contrasena, Entrar, Crear cuenta, Volver y "Olvide mi contrasena".</summary>
public class LoginScreen : AuthScreenBase
{
    private TMP_InputField email, password;
    private Button enterButton;

    private void Start()
    {
        if (B.IsLoggedIn) { ScreenRouter.Go(ScreenRouter.Menu); return; }

        PrepareFields();
        // Cajas de Cuenta.png: correo y 215..455 px, contrasena y 607..847 px (desde arriba, sobre 1080).
        email = Field("Correo", "correo@ejemplo.com", 0.579f, 0.801f, TMP_InputField.ContentType.EmailAddress, 64);
        password = Field("Contrasena", "Contraseña", 0.216f, 0.438f, TMP_InputField.ContentType.Password, 64, PasswordMaxX);
        AddShowPasswordToggle(password, 0.216f, 0.438f);
        email.text = LastEmail;

        // "¿Olvidaste tu contraseña?" debajo de la segunda caja.
        TextMeshProUGUI forgot = UIKit.CreateText(fieldsRoot, "OlvideContrasena", "¿Olvidaste tu contraseña?",
            fieldFontSize * 0.6f, new Vector2(FieldMinX, 0.08f), new Vector2(FieldMaxX, 0.19f), TextAlignmentOptions.Center, UIKit.TextGold);
        forgot.raycastTarget = true;
        var forgotButton = forgot.gameObject.AddComponent<Button>();
        forgotButton.targetGraphic = forgot;
        forgotButton.onClick.AddListener(ForgotPassword);

        enterButton = UIKit.Bind("Entrar", DoLogin, "Entrar");
        Button createButton = UIKit.Bind("Crear cuenta", () => ScreenRouter.Go(ScreenRouter.Register), "Crear cuenta");
        // En el diseno este boton no tiene imagen: se pinta con la madera de "Entrar" en amarillo.
        UIKit.PaintButton(createButton, enterButton, UIKit.ButtonYellow, Color.white);
        UIKit.Bind("Volver", OnBack, "Volver");

        email.onSubmit.AddListener(_ => password.ActivateInputField());
        password.onSubmit.AddListener(_ => DoLogin());
    }

    protected override void OnBack() => ScreenRouter.Go(ScreenRouter.Menu);

    private async void DoLogin()
    {
        if (Busy) return;

        Result valid = BackendValidation.ValidateLogin(email.text, password.text);
        if (!valid.Ok) { UIAviso.ShowError("¡Error!\n" + valid.Message); return; }

        Busy = true;
        UIKit.SetInteractable(enterButton, false);
        Result r = await UIKit.WithLoading(B.Login(email.text, password.text), "Iniciando sesión...");
        Busy = false;
        if (this == null) return;   // la escena cambio mientras se esperaba
        UIKit.SetInteractable(enterButton, true);

        if (r.Ok)
        {
            LastEmail = email.text.Trim();
            UIAviso.ShowSuccess(r.Message, 1.2f, () => ScreenRouter.Go(ScreenRouter.Menu));
        }
        else
        {
            password.text = "";
            UIAviso.ShowResult(r, DoLogin);
        }
    }

    private async void ForgotPassword()
    {
        if (Busy) return;
        Result valid = BackendValidation.ValidateEmail(email.text);
        if (!valid.Ok) { UIAviso.ShowError("Escribe tu correo arriba para enviarte el enlace."); return; }

        Busy = true;
        Result r = await UIKit.WithLoading(B.SendPasswordReset(email.text), "Enviando correo...");
        Busy = false;
        if (r.Ok) UIAviso.ShowSuccess(r.Message, 2.5f);
        else UIAviso.ShowResult(r, ForgotPassword);
    }
}
