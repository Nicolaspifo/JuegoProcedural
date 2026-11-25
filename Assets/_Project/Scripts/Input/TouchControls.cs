using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Controles tactiles para celular, creados por codigo (no hay que tocar la escena):
///  - Joystick virtual abajo a la izquierda -> simula el stick izquierdo de un mando (accion "Move").
///  - Boton de pausa arriba a la derecha   -> simula el boton Start de un mando (accion "Pause").
/// El ataque del machete es automatico, asi que no hace falta boton de ataque.
///
/// Como simulan un mando (On-Screen Controls del Input System), el resto del juego no cambia.
/// Solo aparecen en dispositivos tactiles; para verlos en el editor poner <see cref="ForceShow"/> en true.
/// Lo crea <see cref="LevelRuntime"/> al empezar el nivel. La UI final puede reemplazarlos: basta con poner
/// <see cref="Enabled"/> en false antes de cargar la escena de juego.
/// </summary>
public static class TouchControls
{
    /// <summary>False = no crear los controles (si la UI final trae los suyos).</summary>
    public static bool Enabled = true;
    /// <summary>True = mostrarlos tambien sin pantalla tactil (para probar en el editor con el mouse).</summary>
    public static bool ForceShow = false;

    private const float StickRange = 110f;

    public static bool ShouldShow =>
        Enabled && (ForceShow || Application.isMobilePlatform || Touchscreen.current != null);

    /// <summary>Crea el canvas de controles en la escena actual (si corresponde y no existe ya).</summary>
    public static GameObject CreateIfNeeded()
    {
        if (!ShouldShow) return null;

        GameObject existing = GameObject.Find("[TouchControls]");
        if (existing != null) return existing;

        EnsureEventSystem();

        var root = new GameObject("[TouchControls]");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // encima del HUD, debajo de los paneles modales con orden mayor
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // escala por alto: mismo tamano fisico en pantallas anchas
        root.AddComponent<GraphicRaycaster>();

        Sprite circle = CircleSprite();

        // ---- Joystick (abajo a la izquierda)
        RectTransform baseRect = CreateImage("JoystickBase", root.transform, circle, new Color(1f, 1f, 1f, 0.25f));
        baseRect.anchorMin = baseRect.anchorMax = Vector2.zero;
        baseRect.pivot = new Vector2(0.5f, 0.5f);
        baseRect.anchoredPosition = new Vector2(260f, 240f);
        baseRect.sizeDelta = new Vector2(320f, 320f);
        baseRect.GetComponent<Image>().raycastTarget = false;

        RectTransform handle = CreateImage("JoystickHandle", baseRect, circle, new Color(1f, 1f, 1f, 0.7f));
        handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(170f, 170f);
        AddOnScreen<OnScreenStick>(handle.gameObject, "<Gamepad>/leftStick").movementRange = StickRange;

        // ---- Pausa (arriba a la derecha)
        RectTransform pause = CreateImage("PauseButton", root.transform, circle, new Color(0f, 0f, 0f, 0.55f));
        pause.anchorMin = pause.anchorMax = Vector2.one;
        pause.pivot = Vector2.one;
        pause.anchoredPosition = new Vector2(-30f, -30f);
        pause.sizeDelta = new Vector2(120f, 120f);
        for (int i = -1; i <= 1; i += 2)   // dos barras: icono de pausa
        {
            RectTransform bar = CreateImage("Bar", pause, null, Color.white);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = new Vector2(i * 16f, 0f);
            bar.sizeDelta = new Vector2(16f, 50f);
            bar.GetComponent<Image>().raycastTarget = false;
        }
        AddOnScreen<OnScreenButton>(pause.gameObject, "<Gamepad>/start");

        return root;
    }

    // El control se agrega con el objeto apagado para fijar el controlPath antes de su OnEnable.
    private static T AddOnScreen<T>(GameObject go, string controlPath) where T : OnScreenControl
    {
        bool wasActive = go.activeSelf;
        go.SetActive(false);
        T control = go.AddComponent<T>();
        control.controlPath = controlPath;
        go.SetActive(wasActive);
        return control;
    }

    private static RectTransform CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return (RectTransform)go.transform;
    }

    private static Sprite circleSprite;

    private static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        // Disco con borde suave, generado por codigo (sin assets).
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));   // 1 px de suavizado en el borde
            }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }
}
