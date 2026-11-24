using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Escena "Crear cuenta": correo, contrasena y nombre de jugador.</summary>
public class RegisterScreen : AuthScreenBase
{
    private TMP_InputField email, password, displayName;
    private Button registerButton;

    private void Start()
    {
        if (B.IsLoggedIn) { ScreenRouter.Go(ScreenRouter.Menu); return; }

        // Los avisos de ejemplo del diseno quedan apagados: los muestra UIAviso cuando hacen falta.
        foreach (string n in new[] { "Aviso1", "Aviso2", "Aviso3", "Aviso4" }) UIKit.Hide(n);

        PrepareFields();
        // Cajas de Secion.png: correo y 60..310 px, contrasena 450..700 px, nombre 808..1058 px (desde arriba).
        email = Field("Correo", "correo@ejemplo.com", 0.731f, 0.931f, TMP_InputField.ContentType.EmailAddress, 64);
        password = Field("Contrasena", "Contraseña (mín. 6)", 0.366f, 0.569f, TMP_InputField.ContentType.Password, 64, PasswordMaxX);
        displayName = Field("Nombre", "Nombre de jugador", 0.036f, 0.236f, TMP_InputField.ContentType.Alphanumeric, BackendValidation.MaxNameLength);
        AddShowPasswordToggle(password, 0.366f, 0.569f);

        registerButton = UIKit.Bind("Registrarme", DoRegister, "Registrarme");
        UIKit.Bind("Ya tengo cuenta", OnBack, "Ya tengo cuenta");

        email.onSubmit.AddListener(_ => password.ActivateInputField());
        password.onSubmit.AddListener(_ => displayName.ActivateInputField());
        displayName.onSubmit.AddListener(_ => DoRegister());
    }

    protected override void OnBack() => ScreenRouter.Go(ScreenRouter.Login);

    private async void DoRegister()
    {
        if (Busy) return;

        // El diseno no tiene "confirmar contrasena"; el boton "Ver" permite revisarla.
        Result valid = BackendValidation.ValidateRegister(displayName.text, email.text, password.text, password.text);
        if (!valid.Ok) { UIAviso.ShowError("¡Error!\n" + valid.Message); return; }

        Busy = true;
        UIKit.SetInteractable(registerButton, false);
        Result r = await UIKit.WithLoading(B.Register(email.text, password.text, displayName.text), "Creando cuenta...");
        Busy = false;
        if (this == null) return;
        UIKit.SetInteractable(registerButton, true);

        if (r.Ok)
        {
            PlayerPrefs.SetString("apdm.ui.lastEmail", email.text.Trim());
            UIAviso.ShowSuccess(r.Message, 1.5f, () => ScreenRouter.Go(ScreenRouter.Menu));
        }
        else if (r.Code == ResultCode.EmailInUse)
        {
            UIAviso.Show(UIAviso.Kind.Error, "¡Error!\n" + r.Message, "Iniciar sesión", () => ScreenRouter.Go(ScreenRouter.Login), "Cerrar");
        }
        else
        {
            UIAviso.ShowResult(r, DoRegister);
        }
    }
}
