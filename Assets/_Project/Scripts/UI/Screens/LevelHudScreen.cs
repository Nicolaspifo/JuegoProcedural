using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escena de juego ("Principal"): HUD del nivel y pantalla de resultado.
///  - HUD: usa los textos del diseno "Temporizador" (reloj) y "Temporizador (1)" (calavera, kills) y agrega
///    arriba al centro el nombre del nivel, la meta y una barra de avance.
///  - Resultado: al terminar el nivel muestra la tabla con victoria/derrota, el desglose del puntaje y, cuando
///    llega la respuesta del servidor, el record, el nivel desbloqueado y el puntaje total.
/// </summary>
public class LevelHudScreen : MonoBehaviour
{
    private TMP_Text clockText, killsText;
    private TextMeshProUGUI levelTitle, goalText;
    private Image goalFill;

    private Canvas resultCanvas;
    private TextMeshProUGUI resultTitle, resultBody, saveStatus;
    private Button nextButton, retrySaveButton;

    private void Awake()
    {
        // Se suscribe antes de que termine el nivel: asi LevelRuntime no usa su pantalla de respaldo.
        LevelRuntime.LevelFinished += OnFinished;
        LevelRuntime.ResultSaved += OnSaved;
        LevelRuntime.ResultSaveFailed += OnSaveFailed;
        LevelRuntime.ShowFallbackHud = false;
    }

    private void OnDestroy()
    {
        LevelRuntime.LevelFinished -= OnFinished;
        LevelRuntime.ResultSaved -= OnSaved;
        LevelRuntime.ResultSaveFailed -= OnSaveFailed;
        LevelRuntime.ShowFallbackHud = true;
    }

    private void Start()
    {
        clockText = UIKit.Find<TMP_Text>("Temporizador");
        killsText = UIKit.Find<TMP_Text>("Temporizador (1)");

        Canvas hud = UIKit.CreateOverlayCanvas("[HUD Nivel]", 10);
        // Los toques pasan a traves del HUD (solo es informativo).
        hud.GetComponent<GraphicRaycaster>().enabled = false;
        levelTitle = UIKit.CreateText(hud.transform, "Nivel", "", 40f, new Vector2(0.3f, 0.925f), new Vector2(0.7f, 0.985f),
            TextAlignmentOptions.Center, UIKit.TextGold, bold: true);
        goalText = UIKit.CreateText(hud.transform, "Meta", "", 32f, new Vector2(0.3f, 0.875f), new Vector2(0.7f, 0.93f));
        goalFill = UIKit.CreateProgressBar(hud.transform, "Avance", new Vector2(0.38f, 0.86f), new Vector2(0.62f, 0.875f), 0f);
    }

    private void Update()
    {
        LevelRuntime lr = LevelRuntime.Instance;
        if (lr == null || lr.Definition == null) return;
        LevelDefinition def = lr.Definition;

        levelTitle.text = $"Nivel {def.number}: {def.name}";
        goalText.text = lr.GoalProgressText;
        UIKit.SetProgress(goalFill, lr.GoalProgress01);

        if (killsText != null)
            killsText.text = def.goal == LevelGoal.KillCount ? $"{Mathf.Min(lr.Kills, def.goalTarget)}/{def.goalTarget}" : lr.Kills.ToString();

        // En los niveles de jefe el reloj lo maneja NightTimer (muestra la ronda); en los demas, este HUD.
        if (clockText != null && !def.IsBossLevel)
            clockText.text = def.goal == LevelGoal.SurviveTime
                ? $"{LevelDefinition.FormatTime(lr.Elapsed)} / {LevelDefinition.FormatTime(def.goalTarget)}"
                : LevelDefinition.FormatTime(lr.Elapsed);
    }

    // ================================================================ resultado

    private void OnFinished(LevelOutcome o)
    {
        BuildResult();
        LevelScore s = o.score;

        resultTitle.text = o.won ? "¡VICTORIA!" : "DERROTA";
        resultTitle.color = o.won ? UIKit.TextGold : new Color(0.85f, 0.25f, 0.2f);
        resultBody.text =
            $"Nivel {o.level.number}: {o.level.name}\n<size=80%>{o.reason}</size>\n\n" +
            $"<size=120%><color=#F2A300>{UIKit.Points(s.total)} pts</color></size>\n" +
            $"<size=70%>Enemigos {s.killPoints} + victoria {s.victoryBonus} + rapidez {s.speedBonus} + vida {s.healthBonus}  ×{s.multiplier:0.0}\n" +
            $"Tiempo {LevelDefinition.FormatTime(o.timeSeconds)} · {o.kills} enemigos</size>";

        nextButton.gameObject.SetActive(false);   // se activa cuando el servidor confirma el desbloqueo
        retrySaveButton.gameObject.SetActive(false);
        LevelRuntime lr = LevelRuntime.Instance;
        if (lr != null && lr.SavedResponse != null) OnSaved(lr.SavedResponse);
        else if (lr != null && lr.LastSaveError != null) OnSaveFailed(lr.LastSaveError);
        else saveStatus.text = "Guardando...";

        resultCanvas.gameObject.SetActive(true);
    }

    private void OnSaved(LevelResultResponse r)
    {
        if (saveStatus == null) return;
        string text = "";
        if (r.isNewRecord && r.previousBest > 0) text += $"¡Nuevo récord! (antes {UIKit.Points(r.previousBest)})\n";
        if (r.unlockedLevel > 0) text += $"¡Nivel {r.unlockedLevel} desbloqueado!\n";
        text += $"Puntaje total: {UIKit.Points(r.newTotalScore)}" + (r.scoreGained > 0 ? $" (+{UIKit.Points(r.scoreGained)})" : "");
        saveStatus.text = text;
        retrySaveButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(r.won && LevelSession.HasNextLevel);
    }

    private void OnSaveFailed(Result error)
    {
        if (saveStatus == null) return;
        saveStatus.text = $"<color=#E0503A>No se guardó: {error.Message}</color>";
        retrySaveButton.gameObject.SetActive(error.Code != ResultCode.NoSession);
    }

    private void RetrySave()
    {
        saveStatus.text = "Guardando...";
        retrySaveButton.gameObject.SetActive(false);
        LevelRuntime.Instance?.RetrySave();
    }

    private void BuildResult()
    {
        if (resultCanvas != null) return;

        resultCanvas = UIKit.CreateOverlayCanvas("[Resultado]", 150);
        UIKit.CreateImage(resultCanvas.transform, "Oscuro", null, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.6f));

        RectTransform c = UIKit.CreateBoard(resultCanvas.transform, "Tabla", new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.98f));
        resultTitle = UIKit.CreateText(c, "Titulo", "", 80f, new Vector2(0f, 0.82f), new Vector2(1f, 1f), bold: true);
        resultBody = UIKit.CreateText(c, "Detalle", "", 38f, new Vector2(0.02f, 0.33f), new Vector2(0.98f, 0.82f));
        saveStatus = UIKit.CreateText(c, "Guardado", "", 34f, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.32f), color: UIKit.TextGold);

        // Botones debajo de la tabla.
        nextButton = UIKit.CreateButton(resultCanvas.transform, "Siguiente", "Siguiente nivel",
            new Vector2(0.69f, 0.02f), new Vector2(0.93f, 0.12f), 36f, LevelSession.PlayNext);
        UIKit.CreateButton(resultCanvas.transform, "Reintentar", "Reintentar",
            new Vector2(0.38f, 0.02f), new Vector2(0.62f, 0.12f), 36f, LevelSession.Retry);
        UIKit.CreateButton(resultCanvas.transform, "Niveles", "Niveles",
            new Vector2(0.07f, 0.02f), new Vector2(0.31f, 0.12f), 36f, () => ScreenRouter.Go(ScreenRouter.Levels));
        retrySaveButton = UIKit.CreateButton(c, "ReintentarGuardado", "Reintentar guardado",
            new Vector2(0.3f, -0.02f), new Vector2(0.7f, 0.1f), 30f, RetrySave);

        resultCanvas.gameObject.SetActive(false);
    }
}
