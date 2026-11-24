using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Avisos emergentes con las imagenes del diseno (Aviso-1..5): Cargando, Error, Sin conexion, Exito y
/// Sincronizar. Se crean por codigo encima de cualquier pantalla, asi funcionan en todas las escenas.
/// <code>
/// UIAviso.ShowLoading("Iniciando sesión...");
/// UIAviso.ShowResult(result, onRetry: Reintentar);   // elige la imagen segun el error
/// UIAviso.ShowSuccess("¡Cuenta creada!", autoHide: 1.5f, then: IrAlMenu);
/// UIAviso.Hide();
/// </code>
/// </summary>
public static class UIAviso
{
    public enum Kind { Loading, Error, NoConnection, Success, Sync }

    private static Canvas canvas;
    private static Image panel;
    private static TextMeshProUGUI message;
    private static Button primary, secondary;
    private static float hideAt = -1f;
    private static Action afterHide;

    public static bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

    public static void ShowLoading(string text = "Cargando...") => Show(Kind.Loading, text);

    public static void ShowError(string text, Action onRetry = null) =>
        Show(Kind.Error, text, onRetry != null ? "Reintentar" : "Aceptar", onRetry, onRetry != null ? "Cerrar" : null);

    public static void ShowSuccess(string text, float autoHide = 1.5f, Action then = null)
    {
        Show(Kind.Success, text);
        hideAt = Time.unscaledTime + autoHide;
        afterHide = then;
    }

    /// <summary>Muestra el error de un <see cref="Result"/> con la imagen adecuada (sin conexion o error).</summary>
    public static void ShowResult(Result r, Action onRetry = null)
    {
        if (r == null || r.Ok) { Hide(); return; }
        if (r.CanRetry)
            Show(Kind.NoConnection, "Sin conexión\n" + r.Message, onRetry != null ? "Reintentar" : "Aceptar", onRetry, onRetry != null ? "Cerrar" : null);
        else
            ShowError("¡Error!\n" + r.Message);
    }

    /// <summary>
    /// Aviso generico. <paramref name="primaryLabel"/> null = sin botones (por ejemplo Cargando).
    /// El boton principal ejecuta <paramref name="onPrimary"/> (o solo cierra si es null).
    /// </summary>
    public static void Show(Kind kind, string text, string primaryLabel = null, Action onPrimary = null,
        string secondaryLabel = null, Action onSecondary = null)
    {
        Build();
        hideAt = -1f;
        afterHide = null;

        panel.sprite = UIKit.LoadSprite(SpriteFor(kind));
        message.text = text;

        Setup(primary, primaryLabel, onPrimary);
        Setup(secondary, secondaryLabel, onSecondary);

        // Un solo boton: centrado en el cartel. Dos: lado a lado.
        var prt = (RectTransform)primary.transform;
        bool two = secondaryLabel != null;
        prt.anchorMin = new Vector2(two ? 0.37f : 0.465f, 0.18f);
        prt.anchorMax = new Vector2(two ? 0.53f : 0.625f, 0.28f);

        canvas.gameObject.SetActive(true);
        canvas.transform.SetAsLastSibling();
    }

    public static void Hide()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
        hideAt = -1f;
        Action then = afterHide;
        afterHide = null;
        then?.Invoke();
    }

    private static void Setup(Button b, string label, Action action)
    {
        b.gameObject.SetActive(label != null);
        if (label == null) return;
        UIKit.SetLabel(b, label);
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() =>
        {
            Hide();
            action?.Invoke();
        });
    }

    private static string SpriteFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Loading: return UIKit.SpriteLoading;
            case Kind.NoConnection: return UIKit.SpriteNoConnection;
            case Kind.Success: return UIKit.SpriteSuccess;
            case Kind.Sync: return UIKit.SpriteSync;
            default: return UIKit.SpriteError;
        }
    }

    // El aviso es un canvas propio que sobrevive al cambio de escena y siempre queda encima.
    private static void Build()
    {
        if (canvas != null) return;
        UIKit.EnsureEventSystem();

        canvas = UIKit.CreateOverlayCanvas("[Aviso]", 200);
        UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
        canvas.gameObject.AddComponent<UIAvisoTicker>();

        // Fondo oscuro que bloquea los toques a la pantalla de atras.
        UIKit.CreateImage(canvas.transform, "Bloqueo", null, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.55f));

        // Las imagenes son de 1920x1080 con el cartel centrado: el rectangulo se mantiene en 16:9 para que las
        // posiciones de texto y botones (normalizadas sobre la imagen) caigan siempre en su sitio.
        panel = UIKit.CreateImage(canvas.transform, "Cartel", null, Vector2.zero, Vector2.one);
        panel.preserveAspect = false;
        var fitter = panel.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1920f / 1080f;
        panel.rectTransform.localScale = Vector3.one * 0.95f;

        // Zona de texto: mitad inferior del cartel (debajo del icono).
        message = UIKit.CreateText(panel.transform, "Mensaje", "", 46f,
            new Vector2(0.34f, 0.29f), new Vector2(0.75f, 0.47f), TextAlignmentOptions.Center, UIKit.TextCream);

        primary = UIKit.CreateButton(panel.transform, "Principal", "Aceptar",
            new Vector2(0.37f, 0.18f), new Vector2(0.53f, 0.28f), 34f, null);
        secondary = UIKit.CreateButton(panel.transform, "Secundario", "Cerrar",
            new Vector2(0.56f, 0.18f), new Vector2(0.72f, 0.28f), 34f, null);

        canvas.gameObject.SetActive(false);
    }

    /// <summary>Lo llama <see cref="UIAvisoTicker"/> cada frame (tiempo real: funciona en pausa).</summary>
    internal static void Tick()
    {
        if (hideAt > 0f && Time.unscaledTime >= hideAt) Hide();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        canvas = null;
        hideAt = -1f;
        afterHide = null;
    }
}
