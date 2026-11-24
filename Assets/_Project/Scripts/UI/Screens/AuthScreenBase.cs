using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base de Login y Registro: crea los campos de texto encima de las cajas dibujadas en la imagen "Datos"
/// (Cuenta.png / Secion.png, de 1920x1080) usando posiciones normalizadas de cada caja.
/// </summary>
public abstract class AuthScreenBase : ScreenBase
{
    private const string LastEmailKey = "apdm.ui.lastEmail";

    // Zona horizontal util de las cajas dentro de la imagen (x 615..1615 de 1920).
    protected const float FieldMinX = 0.322f;
    protected const float FieldMaxX = 0.838f;

    protected Transform fieldsRoot;
    protected float fieldFontSize = 80f;

    /// <summary>Prepara el contenedor de los campos sobre la imagen "Datos".</summary>
    protected void PrepareFields()
    {
        Image datos = UIKit.Find<Image>("Datos");
        if (datos == null)
        {
            Debug.LogWarning("[UI] No se encontro la imagen 'Datos': los campos se crean en el Canvas.");
            fieldsRoot = UIKit.MainCanvas().transform;
            fieldFontSize = 40f;
            return;
        }

        fieldsRoot = datos.transform;
        // Si la imagen conserva su proporcion, se dibuja con bandas: los campos van en un hijo 16:9 que coincide
        // con la imagen dibujada.
        if (datos.preserveAspect)
        {
            var box = new GameObject("Campos", typeof(RectTransform));
            box.transform.SetParent(datos.transform, false);
            var rt = (RectTransform)box.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var fit = box.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1920f / 1080f;
            fieldsRoot = box.transform;
        }
    }

    /// <summary>Crea un campo sobre la caja que ocupa de <paramref name="yMin"/> a <paramref name="yMax"/> (0 = abajo).</summary>
    protected TMP_InputField Field(string name, string placeholder, float yMin, float yMax, TMP_InputField.ContentType type,
        int limit, float maxX = FieldMaxX)
    {
        return UIKit.CreateInputField(fieldsRoot, name, placeholder,
            new Vector2(FieldMinX, yMin), new Vector2(maxX, yMax), fieldFontSize, type, limit);
    }

    /// <summary>Fin del campo de contrasena: deja sitio dentro de la caja para el boton "Ver".</summary>
    protected const float PasswordMaxX = 0.75f;

    /// <summary>Boton "Ver" dentro de la caja de contrasena (a la derecha) para mostrarla u ocultarla.</summary>
    protected void AddShowPasswordToggle(TMP_InputField password, float yMin, float yMax)
    {
        TextMeshProUGUI label = UIKit.CreateText(fieldsRoot, "VerContrasena", "Ver", fieldFontSize * 0.7f,
            new Vector2(PasswordMaxX, yMin), new Vector2(FieldMaxX, yMax), TextAlignmentOptions.Center, UIKit.TextGold);
        label.raycastTarget = true;
        var b = label.gameObject.AddComponent<Button>();
        b.targetGraphic = label;
        b.onClick.AddListener(() =>
        {
            bool hidden = password.contentType == TMP_InputField.ContentType.Password;
            password.contentType = hidden ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            password.ForceLabelUpdate();
            label.text = hidden ? "Ocultar" : "Ver";   // auto-ajuste: cabe en la caja
        });
    }

    protected static string LastEmail
    {
        get => PlayerPrefs.GetString(LastEmailKey, "");
        set { PlayerPrefs.SetString(LastEmailKey, value ?? ""); PlayerPrefs.Save(); }
    }
}
