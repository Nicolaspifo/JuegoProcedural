using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escena "Niveles": los botones N1..N10 muestran si el nivel esta bloqueado (calavera, atenuado), disponible o
/// completado (texto dorado). Al tocar uno se ve su meta y el mejor puntaje; "Jugar" lo inicia.
/// </summary>
public class LevelSelectScreen : ScreenBase
{
    private readonly Button[] levelButtons = new Button[LevelCatalog.Count];
    private readonly TMP_Text[] levelLabels = new TMP_Text[LevelCatalog.Count];
    private readonly GameObject[] locks = new GameObject[LevelCatalog.Count];
    private readonly Vector3[] baseScales = new Vector3[LevelCatalog.Count];   // escala del diseno
    private Button playButton;
    private TextMeshProUGUI details, userLine;
    private int selected;

    private void Start()
    {
        // Con cuenta o como invitado; si no hay ninguno, al login.
        if (!GuestSession.CanPlay) { ScreenRouter.Go(ScreenRouter.Login); return; }

        // La imagen "Tabla" del diseno cubre casi toda la pantalla y esta dibujada encima de Jugar/Instrucciones/
        // Volver: las imagenes decorativas no deben recibir clics.
        UIKit.DisableDecorativeRaycasts(UIKit.MainCanvas().transform);

        for (int i = 0; i < LevelCatalog.Count; i++)
        {
            int level = i + 1;
            Button b = UIKit.Find<Button>("N" + level);
            if (b == null) { Debug.LogWarning($"[UI] Niveles: falta el boton N{level}."); continue; }
            levelButtons[i] = b;
            baseScales[i] = b.transform.localScale;
            levelLabels[i] = b.GetComponentInChildren<TMP_Text>(true);
            if (levelLabels[i] != null) levelLabels[i].text = $"NIVEL {level}";
            UIKit.Bind(b, () => Select(level));
            FitHitArea(b);

            // Calavera encima del boton para los niveles bloqueados.
            locks[i] = CreateLock(b).gameObject;
        }

        playButton = UIKit.Bind("Jugar nivel seleccionado", PlaySelected, "Jugar");
        UIKit.Bind("Instrucciones", () => ScreenRouter.Go(ScreenRouter.Instructions), "Instrucciones");
        UIKit.Bind("Volver", OnBack, "Volver");

        // Columna derecha: el jugador encima del titulo "SELECCIONA UN NIVEL" y el detalle del nivel debajo,
        // cada uno sobre un fondo oscuro para que se lea sobre la ilustracion.
        Transform canvas = UIKit.MainCanvas().transform;
        RectTransform userBack = CreateBackdrop(canvas, "FondoJugador", new Vector2(0.64f, 0.855f), new Vector2(0.98f, 0.925f));
        userLine = UIKit.CreateText(userBack, "Jugador", "", 30f,
            new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f), TextAlignmentOptions.Center, UIKit.TextGold);
        RectTransform detailsBack = CreateBackdrop(canvas, "FondoDetalle", new Vector2(0.64f, 0.445f), new Vector2(0.98f, 0.665f));
        details = UIKit.CreateText(detailsBack, "DetalleNivel", "", 38f,
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), TextAlignmentOptions.Center, UIKit.TextCream);

        UserProfile user = GuestSession.CurrentProfile;
        selected = LevelSession.CurrentLevelNumber >= 1 && user != null && user.IsUnlocked(LevelSession.CurrentLevelNumber)
            ? LevelSession.CurrentLevelNumber
            : LevelSession.HighestPlayableLevel;
        Refresh(user);
        Reload();
    }

    protected override void OnBack() => ScreenRouter.Go(ScreenRouter.Menu);

    private const float LockHeight = 120f;   // alto de la calavera en unidades del canvas (1920x1080)

    /// <summary>
    /// Calavera de nivel bloqueado. Los botones N1..N10 del diseno tienen escala no uniforme (x 2.47, y 7.35), asi
    /// que la imagen se crea con la escala inversa (relativa al canvas) para que no se estire, y con tamano fijo.
    /// </summary>
    private static Image CreateLock(Button button)
    {
        var center = new Vector2(0.5f, 0.5f);
        Image img = UIKit.CreateImage(button.transform, "Bloqueado", UIKit.LoadSprite(UIKit.SpriteLock), center, center);
        img.raycastTarget = false;
        img.preserveAspect = true;

        Vector3 canvasScale = UIKit.MainCanvas().transform.lossyScale;
        Vector3 buttonScale = button.transform.lossyScale;
        RectTransform rt = img.rectTransform;
        rt.localScale = new Vector3(
            Mathf.Approximately(buttonScale.x, 0f) ? 1f : canvasScale.x / buttonScale.x,
            Mathf.Approximately(buttonScale.y, 0f) ? 1f : canvasScale.y / buttonScale.y,
            1f);
        // Calavera.png es 1920x1080 con la calavera (cuadrada) en el centro.
        rt.sizeDelta = new Vector2(LockHeight * 1920f / 1080f, LockHeight);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    // Area de clic de cada nivel en unidades del canvas: la caja dibujada mide ~210x195 y estan separadas ~220.
    private static readonly Vector2 LevelHitSize = new Vector2(200f, 185f);

    /// <summary>
    /// Los botones N1..N10 del diseno miden 394x220 (escala 2.47 x 7.35 sobre 160x30) y se solapan con el vecino:
    /// al tocar la mitad derecha de un nivel se elegia el siguiente. Se recorta el area de clic con raycastPadding.
    /// </summary>
    private static void FitHitArea(Button button)
    {
        if (!(button.targetGraphic is Image img)) return;
        Vector3 canvasScale = UIKit.MainCanvas().transform.lossyScale;
        Vector3 s = button.transform.lossyScale;
        if (Mathf.Approximately(s.x, 0f) || Mathf.Approximately(s.y, 0f)) return;

        Rect r = img.rectTransform.rect;   // espacio local del boton
        float wantW = LevelHitSize.x * canvasScale.x / s.x;
        float wantH = LevelHitSize.y * canvasScale.y / s.y;
        float padX = Mathf.Max(0f, (r.width - wantW) * 0.5f);
        float padY = Mathf.Max(0f, (r.height - wantH) * 0.5f);
        img.raycastPadding = new Vector4(padX, padY, padX, padY);   // izquierda, abajo, derecha, arriba

        // El texto "NIVEL n" ocupa todo el boton y tambien recibia clics: solo la imagen recortada los recibe.
        foreach (Graphic g in button.GetComponentsInChildren<Graphic>(true))
            if (g != img) g.raycastTarget = false;
    }

    /// <summary>Fondo oscuro semitransparente con borde dorado, para dar contraste a los textos.</summary>
    private static RectTransform CreateBackdrop(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        Image back = UIKit.CreateImage(parent, name, null, anchorMin, anchorMax, new Color(0.06f, 0.04f, 0.03f, 0.82f));
        back.preserveAspect = false;
        back.raycastTarget = false;
        var outline = back.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(UIKit.TextGold.r, UIKit.TextGold.g, UIKit.TextGold.b, 0.55f);
        outline.effectDistance = new Vector2(3f, -3f);
        return back.rectTransform;
    }

    private async void Reload()
    {
        if (!B.IsLoggedIn) return;   // invitado: el progreso es local, no hay nada que descargar
        Busy = true;
        Result<UserProfile> r = await UIKit.WithLoading(B.LoadProfile(), "Cargando progreso...");
        Busy = false;
        if (this == null) return;
        if (r.Ok) Refresh(r.Data);
        else if (r.Code == ResultCode.SessionExpired || r.Code == ResultCode.NoSession) ScreenRouter.Go(ScreenRouter.Login);
        else UIAviso.ShowResult(r, Reload);
    }

    private void Refresh(UserProfile user)
    {
        for (int i = 0; i < LevelCatalog.Count; i++)
        {
            if (levelButtons[i] == null) continue;
            int level = i + 1;
            bool unlocked = user != null && user.IsUnlocked(level);
            bool completed = user != null && user.IsCompleted(level);

            locks[i].SetActive(!unlocked);
            foreach (Graphic g in levelButtons[i].GetComponentsInChildren<Graphic>(true))
                if (g.gameObject != locks[i]) g.canvasRenderer.SetAlpha(unlocked ? 1f : 0.45f);

            if (levelLabels[i] != null)
                levelLabels[i].color = level == selected ? Color.white : completed ? UIKit.TextGold : UIKit.TextCream;
            levelButtons[i].transform.localScale = baseScales[i] * (level == selected ? 1.1f : 1f);
        }

        if (user != null)
            userLine.text = $"{user.displayName} · {UIKit.Points(user.totalScore)} pts · {user.levelsCompleted}/{UserProfile.MaxLevels}" +
                            (GuestSession.IsActive ? " · sin guardar" : "");
        ShowDetails(user);
    }

    private void Select(int level)
    {
        UserProfile user = GuestSession.CurrentProfile;
        if (user == null || !user.IsUnlocked(level))
        {
            UIAviso.ShowError($"Nivel {level} bloqueado\nGana el nivel {level - 1} para desbloquearlo.");
            return;
        }
        selected = level;
        Refresh(user);
    }

    private void ShowDetails(UserProfile user)
    {
        LevelDefinition def = LevelCatalog.Get(selected);
        if (def == null) { details.text = ""; return; }

        LevelProgress p = user?.GetLevel(selected);
        string status = p == null || p.attempts == 0 ? "Sin jugar"
            : p.completed ? $"Completado · Récord {UIKit.Points(p.bestScore)} pts"
            : $"Intentado {p.attempts} {(p.attempts == 1 ? "vez" : "veces")} · Récord {UIKit.Points(p.bestScore)} pts";

        details.text = $"<color=#F2A300><b>Nivel {def.number}: {def.name}</b></color>\n" +
                       $"Meta: {def.GoalText}\n" +
                       $"<size=80%>{def.description}</size>\n" +
                       $"<size=80%>{status}</size>";

        UIKit.SetInteractable(playButton, user != null && user.IsUnlocked(selected));
    }

    private void PlaySelected()
    {
        if (Busy) return;
        if (!LevelSession.Play(selected))
            UIAviso.ShowError($"El nivel {selected} está bloqueado.");
    }
}
