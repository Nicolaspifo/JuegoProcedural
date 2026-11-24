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
        if (!RequireSession()) return;

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

            // Calavera encima del boton para los niveles bloqueados.
            Image lockImg = UIKit.CreateImage(b.transform, "Bloqueado", UIKit.LoadSprite(UIKit.SpriteLock),
                new Vector2(0.2f, -0.8f), new Vector2(0.8f, 1.8f));
            lockImg.raycastTarget = false;
            locks[i] = lockImg.gameObject;
        }

        playButton = UIKit.Bind("Jugar nivel seleccionado", PlaySelected, "Jugar");
        UIKit.Bind("Instrucciones", () => ScreenRouter.Go(ScreenRouter.Instructions), "Instrucciones");
        UIKit.Bind("Volver", OnBack, "Volver");

        // Detalle del nivel elegido, en la columna derecha debajo del titulo.
        Transform canvas = UIKit.MainCanvas().transform;
        details = UIKit.CreateText(canvas, "DetalleNivel", "", 40f,
            new Vector2(0.62f, 0.43f), new Vector2(0.98f, 0.76f), TextAlignmentOptions.Center, UIKit.TextCream);
        userLine = UIKit.CreateText(canvas, "Jugador", "", 30f,
            new Vector2(0.62f, 0.76f), new Vector2(0.98f, 0.81f), TextAlignmentOptions.Center, UIKit.TextGold);

        UserProfile user = B.CurrentUser;
        selected = LevelSession.CurrentLevelNumber >= 1 && user != null && user.IsUnlocked(LevelSession.CurrentLevelNumber)
            ? LevelSession.CurrentLevelNumber
            : LevelSession.HighestPlayableLevel;
        Refresh(user);
        Reload();
    }

    protected override void OnBack() => ScreenRouter.Go(ScreenRouter.Menu);

    private async void Reload()
    {
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
            userLine.text = $"{user.displayName} · {UIKit.Points(user.totalScore)} pts · {user.levelsCompleted}/{UserProfile.MaxLevels}";
        ShowDetails(user);
    }

    private void Select(int level)
    {
        UserProfile user = B.CurrentUser;
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
