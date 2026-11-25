using UnityEngine;

/// <summary>
/// Pantalla de resultado BASICA de respaldo (IMGUI, sin prefabs). Solo aparece si ninguna pantalla se suscribio a
/// <see cref="LevelRuntime.LevelFinished"/>, para poder probar el flujo completo (jugar, guardar, desbloquear)
/// antes de tener la pantalla final. Funciona con toque en el celular.
/// </summary>
public class LevelResultFallbackUI : MonoBehaviour
{
    private LevelRuntime runtime;
    private GUIStyle title, body, button;

    private void Awake() => runtime = GetComponent<LevelRuntime>();

    private void OnGUI()
    {
        if (runtime == null || runtime.Outcome == null) return;
        BuildStyles();

        float w = Mathf.Min(Screen.width * 0.9f, Screen.height * 1.3f);
        float h = Screen.height * 0.85f;
        var area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        LevelOutcome o = runtime.Outcome;
        LevelScore s = o.score;

        GUILayout.BeginArea(new Rect(area.x + 20, area.y + 15, area.width - 40, area.height - 30));
        GUILayout.Label(o.won ? "¡VICTORIA!" : "DERROTA", title);
        GUILayout.Label($"Nivel {o.level.number}: {o.level.name} — {o.reason}", body);
        GUILayout.Space(8);
        GUILayout.Label($"Puntaje: {s.total}   (tiempo {LevelDefinition.FormatTime(o.timeSeconds)}, {o.kills} enemigos)", body);
        GUILayout.Label($"Kills {s.killPoints} + victoria {s.victoryBonus} + rapidez {s.speedBonus} + vida {s.healthBonus}  x{s.multiplier:0.0}", body);
        GUILayout.Space(8);

        if (runtime.IsSaving)
        {
            GUILayout.Label("Guardando...", body);
        }
        else if (runtime.SavedResponse != null)
        {
            LevelResultResponse r = runtime.SavedResponse;
            if (r.isNewRecord) GUILayout.Label($"¡Nuevo récord! (antes {r.previousBest})", body);
            if (r.unlockedLevel > 0) GUILayout.Label($"¡Nivel {r.unlockedLevel} desbloqueado!", body);
            GUILayout.Label($"Puntaje total: {r.newTotalScore} (+{r.scoreGained})", body);
        }
        else if (runtime.LastSaveError != null)
        {
            GUILayout.Label($"No se guardó: {runtime.LastSaveError.Message}", body);
            if (runtime.LastSaveError.Code != ResultCode.NoSession && GUILayout.Button("Reintentar guardado", button))
                runtime.RetrySave();
        }

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUI.enabled = !runtime.IsSaving;
        if (o.won && LevelSession.HasNextLevel && GUILayout.Button("Siguiente nivel", button)) LevelSession.PlayNext();
        if (GUILayout.Button("Reintentar", button)) LevelSession.Retry();
        if (GUILayout.Button("Menú", button)) LevelSession.GoToMenu();
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void BuildStyles()
    {
        int unit = Mathf.RoundToInt(Screen.height * 0.035f);
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            button = new GUIStyle(GUI.skin.button);
        }
        title.fontSize = unit * 2;
        body.fontSize = unit;
        button.fontSize = unit;
        button.fixedHeight = unit * 2.4f;
    }
}
