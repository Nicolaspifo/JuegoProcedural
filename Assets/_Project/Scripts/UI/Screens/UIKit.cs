using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Utilidades para conectar por codigo las pantallas disenadas en las escenas (buscar objetos por nombre,
/// cambiar textos, reemplazar el OnClick de un boton, crear campos de texto y textos con la fuente del juego).
/// Asi las escenas se pueden seguir editando en Unity sin romper la logica, siempre que se conserven los
/// nombres de los objetos.
/// </summary>
public static class UIKit
{
    // Recursos copiados del diseno a Assets/_Project/Resources/UI
    public const string SpriteLoading = "UI/AvisoCargando";
    public const string SpriteError = "UI/AvisoError";
    public const string SpriteNoConnection = "UI/AvisoSinConexion";
    public const string SpriteSuccess = "UI/AvisoExito";
    public const string SpriteSync = "UI/AvisoSincronizar";
    public const string SpritePanel = "UI/Tabla";
    public const string SpriteLock = "UI/Calavera";
    public const string SpriteBackground = "UI/FondoPantalla";
    public const string SpriteButton = "UI/Boton";

    public static readonly Color TextBrown = new Color(0.49f, 0.45f, 0.38f, 1f);   // color de los titulos del diseno
    public static readonly Color TextCream = new Color(0.93f, 0.89f, 0.80f, 1f);
    public static readonly Color TextGold = new Color(0.95f, 0.64f, 0f, 1f);

    private static TMP_FontAsset regularFont, boldFont;

    // ================================================================ buscar

    /// <summary>Busca un objeto por nombre en la escena activa (incluye inactivos). Null si no existe.</summary>
    public static GameObject Find(string name)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    public static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    public static T Find<T>(string name) where T : Component
    {
        GameObject go = Find(name);
        return go != null ? go.GetComponent<T>() : null;
    }

    /// <summary>Canvas principal de la escena (el primero que encuentre).</summary>
    public static Canvas MainCanvas()
    {
        Canvas c = Find<Canvas>("Canvas");
        return c != null ? c : UnityEngine.Object.FindAnyObjectByType<Canvas>();
    }

    // ================================================================ botones

    /// <summary>
    /// Reemplaza lo que hace un boton: apaga los OnClick puestos en el Inspector (en el diseno algunos apuntan a
    /// MainMenu.Quit como relleno) y deja solo la accion indicada. Devuelve el boton (o null si no existe).
    /// </summary>
    public static Button Bind(string buttonName, UnityAction action, string newLabel = null)
    {
        Button b = Find<Button>(buttonName);
        if (b == null)
        {
            Debug.LogWarning($"[UI] No se encontro el boton '{buttonName}' en la escena.");
            return null;
        }
        Bind(b, action, newLabel);
        return b;
    }

    public static void Bind(Button b, UnityAction action, string newLabel = null)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            b.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        b.onClick.RemoveAllListeners();
        if (action != null) b.onClick.AddListener(action);
        if (newLabel != null) SetLabel(b, newLabel);
    }

    public static void SetLabel(Component buttonOrObject, string text)
    {
        TMP_Text label = buttonOrObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    /// <summary>Duplica un boton existente (mismo estilo) y lo coloca desplazado.</summary>
    public static Button CloneButton(Button template, string name, string label, Vector2 offset, UnityAction action)
    {
        Button copy = UnityEngine.Object.Instantiate(template, template.transform.parent);
        copy.name = name;
        var rt = (RectTransform)copy.transform;
        rt.anchoredPosition = ((RectTransform)template.transform).anchoredPosition + offset;
        Bind(copy, action, label);
        return copy;
    }

    /// <summary>
    /// Quita la deteccion de clics a las imagenes y textos decorativos (fondos, tablas, titulos) para que no tapen
    /// a los botones que tengan debajo. Conserva los graficos de botones, campos de texto, scrolls y demas
    /// controles (todo lo que tenga un componente que recibe eventos en el mismo objeto o en un padre).
    /// </summary>
    public static void DisableDecorativeRaycasts(Transform root)
    {
        foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
        {
            if (!g.raycastTarget) continue;
            if (g.GetComponentInParent<Selectable>(true) != null) continue;
            if (g.GetComponentInParent<ScrollRect>(true) != null) continue;
            if (g.GetComponent<UnityEngine.EventSystems.IEventSystemHandler>() != null) continue;
            g.raycastTarget = false;
        }
    }

    public static readonly Color ButtonYellow = new Color(1f, 1f, 0.12f, 1f);

    /// <summary>
    /// Le pone a un boton la misma madera que otro boton del diseno (o Boton.png si no hay modelo), tenida del
    /// color indicado, y cambia el color de su texto.
    /// </summary>
    public static void PaintButton(Button button, Button template, Color tint, Color labelColor)
    {
        if (button == null) return;
        Image img = button.targetGraphic as Image;
        if (img == null) img = button.GetComponent<Image>();
        if (img != null)
        {
            Image model = template != null ? template.targetGraphic as Image : null;
            if (model != null && model.sprite != null)
            {
                img.sprite = model.sprite;
                img.type = model.type;
                img.preserveAspect = model.preserveAspect;
            }
            else
            {
                img.sprite = LoadSprite(SpriteButton);
                img.type = Image.Type.Simple;
            }
            img.color = tint;
        }
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = labelColor;
    }

    /// <summary>Activa/desactiva un boton y lo atenua visualmente.</summary>
    public static void SetInteractable(Button b, bool on)
    {
        if (b == null) return;
        b.interactable = on;
        foreach (Graphic g in b.GetComponentsInChildren<Graphic>(true))
            g.canvasRenderer.SetAlpha(on ? 1f : 0.45f);
    }

    // ================================================================ fuentes y textos

    public static TMP_FontAsset Font(bool bold = false)
    {
        if (bold)
        {
            if (boldFont == null) boldFont = Resources.Load<TMP_FontAsset>("UI/FuenteNegrita");
            if (boldFont != null) return boldFont;
        }
        if (regularFont == null) regularFont = Resources.Load<TMP_FontAsset>("UI/FuenteRegular");
        return regularFont != null ? regularFont : TMP_Settings.defaultFontAsset;
    }

    /// <summary>Crea un texto TMP dentro de <paramref name="parent"/> con anclas normalizadas (0..1).</summary>
    public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions align = TextAlignmentOptions.Center,
        Color? color = null, bool bold = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = Font(bold);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = Mathf.Max(8f, fontSize * 0.4f);
        tmp.fontSizeMax = fontSize;
        tmp.alignment = align;
        tmp.color = color ?? TextCream;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>
    /// Crea un campo de texto (TMP_InputField) sobre una caja dibujada en una imagen. Las anclas son la posicion
    /// de la caja dentro de la imagen, normalizada 0..1 (0,0 = abajo a la izquierda).
    /// </summary>
    public static TMP_InputField CreateInputField(Transform parent, string name, string placeholder,
        Vector2 anchorMin, Vector2 anchorMax, float fontSize, TMP_InputField.ContentType contentType, int characterLimit = 64)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = anchorMin;
        rootRt.anchorMax = anchorMax;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        // Imagen transparente: recibe el toque (la caja ya esta dibujada en el fondo).
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.001f);

        var viewport = new GameObject("Text Area", typeof(RectTransform));
        viewport.transform.SetParent(root.transform, false);
        var vpRt = (RectTransform)viewport.transform;
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(fontSize * 0.5f, 0f);
        vpRt.offsetMax = new Vector2(-fontSize * 0.5f, 0f);
        viewport.AddComponent<RectMask2D>();

        TextMeshProUGUI ph = CreateText(viewport.transform, "Placeholder", placeholder, fontSize,
            Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.35f));
        ph.enableAutoSizing = false;
        ph.fontStyle = FontStyles.Italic;

        TextMeshProUGUI txt = CreateText(viewport.transform, "Text", "", fontSize,
            Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft, Color.white);
        txt.enableAutoSizing = false;

        // Se agrega con el objeto apagado para que el campo ya tenga sus referencias al activarse.
        root.SetActive(false);
        var input = root.AddComponent<TMP_InputField>();
        input.textViewport = vpRt;
        input.textComponent = txt;
        input.placeholder = ph;
        input.fontAsset = txt.font;
        input.pointSize = fontSize;
        input.contentType = contentType;
        input.characterLimit = characterLimit;
        input.caretWidth = Mathf.Max(2, Mathf.RoundToInt(fontSize * 0.06f));
        input.customCaretColor = true;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.95f, 0.64f, 0f, 0.4f);
        input.targetGraphic = bg;
        input.shouldHideMobileInput = false;
        root.SetActive(true);
        return input;
    }

    // ================================================================ imagenes

    public static Sprite LoadSprite(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s != null) return s;
        Sprite[] all = Resources.LoadAll<Sprite>(path);   // sprites en modo "Multiple" (por ejemplo Boton)
        return all != null && all.Length > 0 ? all[0] : null;
    }

    public static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color ?? Color.white;
        img.preserveAspect = sprite != null;
        return img;
    }

    /// <summary>Boton creado por codigo con el sprite de boton del diseno.</summary>
    public static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, UnityAction action)
    {
        Image img = CreateImage(parent, name, LoadSprite(SpriteButton), anchorMin, anchorMax);
        img.preserveAspect = false;
        img.type = Image.Type.Simple;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        CreateText(img.transform, "Text (TMP)", label, fontSize, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f), color: Color.white);
        if (action != null) b.onClick.AddListener(action);
        return b;
    }

    /// <summary>
    /// Tabla de madera del diseno (Tabla.png) dentro de la zona indicada, siempre en 16:9 para que no se deforme.
    /// Devuelve el area interior de la tabla, donde se ponen textos y botones (anclas 0..1 sobre esa area).
    /// </summary>
    public static RectTransform CreateBoard(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        var hrt = (RectTransform)holder.transform;
        hrt.anchorMin = anchorMin;
        hrt.anchorMax = anchorMax;
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;

        Image board = CreateImage(holder.transform, "Tabla", LoadSprite(SpritePanel), Vector2.zero, Vector2.one);
        board.preserveAspect = false;
        var fit = board.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 1920f / 1080f;

        // Interior de la tabla en Tabla.png: x 570..1355, y 230..880 (desde arriba) sobre 1920x1080.
        var content = new GameObject("Contenido", typeof(RectTransform));
        content.transform.SetParent(board.transform, false);
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0.300f, 0.190f);
        crt.anchorMax = new Vector2(0.703f, 0.783f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        return crt;
    }

    /// <summary>Barra de progreso simple (fondo oscuro + relleno dorado). Devuelve la imagen de relleno.</summary>
    public static Image CreateProgressBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float value01)
    {
        Image back = CreateImage(parent, name, null, anchorMin, anchorMax, new Color(0f, 0f, 0f, 0.6f));
        back.preserveAspect = false;
        Image fill = CreateImage(back.transform, "Relleno", null, Vector2.zero, new Vector2(Mathf.Clamp01(value01), 1f), TextGold);
        fill.preserveAspect = false;
        return fill;
    }

    public static void SetProgress(Image fill, float value01)
    {
        if (fill == null) return;
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
    }

    /// <summary>Canvas superpuesto propio (popups, HUD) con la misma escala que las pantallas del diseno.</summary>
    public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ================================================================ varios

    /// <summary>Las escenas del diseno no traen EventSystem: sin el, los botones no responden.</summary>
    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    public static void Hide(string name)
    {
        GameObject go = Find(name);
        if (go != null) go.SetActive(false);
    }

    /// <summary>Formato de miles con punto: 12.345</summary>
    public static string Points(int value) =>
        value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");

    /// <summary>Ejecuta una tarea async mostrando el aviso de "Cargando..." y oculta el aviso al terminar.</summary>
    public static async System.Threading.Tasks.Task<T> WithLoading<T>(System.Threading.Tasks.Task<T> task, string message = "Cargando...")
        where T : Result
    {
        UIAviso.ShowLoading(message);
        try
        {
            T r = await task;
            UIAviso.Hide();
            return r;
        }
        catch (Exception)
        {
            UIAviso.Hide();
            throw;
        }
    }
}
