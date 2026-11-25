using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Panel de PRUEBAS del backend (IMGUI). Agregarlo a cualquier objeto de la escena del menu para probar
/// registro, login, guardado de niveles y ranking sin tener las pantallas finales, y sacar capturas para las
/// evidencias. Se abre/cierra con el boton "Backend" de la esquina. Quitarlo antes de la entrega final.
/// </summary>
public class BackendTestPanel : MonoBehaviour
{
    [SerializeField] private bool startOpen = true;

    private bool open;
    private bool busy;
    private string displayName = "Machetero";
    private string email = "prueba@correo.com";
    private string password = "123456";
    private string levelText = "1";
    private string scoreText = "1500";
    private bool won = true;
    private Vector2 scroll;
    private readonly List<string> log = new List<string>();
    private GUIStyle label, button, field, toggle;

    private IBackendService B => Backend.Service;

    private void Awake() => open = startOpen;

    private void OnGUI()
    {
        BuildStyles();
        int unit = Mathf.RoundToInt(Screen.height * 0.03f);

        if (GUI.Button(new Rect(10, 10, unit * 6, unit * 2), open ? "Cerrar" : "Backend", button)) open = !open;
        if (!open) return;

        var area = new Rect(10, unit * 2 + 20, Mathf.Min(Screen.width - 20, unit * 30), Screen.height - unit * 2 - 30);
        GUI.color = new Color(0, 0, 0, 0.85f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(area.x + 10, area.y + 10, area.width - 20, area.height - 20));
        scroll = GUILayout.BeginScrollView(scroll);

        string mode = Backend.IsMock ? "MOCK (datos falsos)" : "FIREBASE";
        UserProfile u = B.CurrentUser;
        GUILayout.Label($"Backend: {mode}", label);
        GUILayout.Label(u == null
            ? "Sin sesión"
            : $"{u.displayName} | total {u.totalScore} | nivel {u.maxUnlockedLevel}/10 | completados {u.levelsCompleted} | partidas {u.gamesPlayed}", label);

        GUI.enabled = !busy;

        Row("Nombre", ref displayName);
        Row("Correo", ref email);
        Row("Contraseña", ref password);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Restaurar sesión", button)) Run("TryRestoreSession", B.TryRestoreSession());
        if (GUILayout.Button("Registrar", button)) Run("Register", B.Register(email, password, displayName));
        if (GUILayout.Button("Login", button)) Run("Login", B.Login(email, password));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Cargar perfil", button)) Run("LoadProfile", B.LoadProfile());
        if (GUILayout.Button("Olvidé contraseña", button)) Run("SendPasswordReset", B.SendPasswordReset(email));
        if (GUILayout.Button("Logout", button)) { B.Logout(); Add("Logout OK"); }
        GUILayout.EndHorizontal();

        GUILayout.Space(unit * 0.5f);
        Row("Nivel", ref levelText);
        Row("Puntaje", ref scoreText);
        won = GUILayout.Toggle(won, " Ganó", toggle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Enviar resultado", button))
        {
            int.TryParse(levelText, out int lvl);
            int.TryParse(scoreText, out int sc);
            RunSubmit(new LevelResultRequest { level = lvl, score = sc, won = won, kills = sc / 10, timeSeconds = 120f });
        }
        if (GUILayout.Button("Ranking", button)) RunRanking();
        if (GUILayout.Button("Jugar nivel", button))
        {
            int.TryParse(levelText, out int lvl);
            if (!LevelSession.Play(lvl)) Add($"El nivel {lvl} está bloqueado para este usuario.");
        }
        GUILayout.EndHorizontal();

        if (B is MockBackend mock && GUILayout.Button("Borrar datos del Mock", button))
        {
            mock.ResetAllData();
            Add("Datos del Mock borrados.");
        }

        GUI.enabled = true;
        GUILayout.Space(unit * 0.5f);
        for (int i = log.Count - 1; i >= 0; i--) GUILayout.Label(log[i], label);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void Row(string name, ref string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, label, GUILayout.Width(Screen.height * 0.2f));
        value = GUILayout.TextField(value, field);
        GUILayout.EndHorizontal();
    }

    private async void Run(string name, Task<Result> task)
    {
        busy = true;
        Result r = await task;
        busy = false;
        Add($"{name}: {r}");
    }

    private async void Run(string name, Task<Result<UserProfile>> task)
    {
        busy = true;
        Result<UserProfile> r = await task;
        busy = false;
        Add(r.Ok ? $"{name}: OK {JsonUtility.ToJson(r.Data)}" : $"{name}: {r}");
    }

    private async void RunSubmit(LevelResultRequest req)
    {
        busy = true;
        Result<LevelResultResponse> r = await B.SubmitLevelResult(req);
        busy = false;
        if (!r.Ok) { Add($"SubmitLevelResult: {r}"); return; }
        LevelResultResponse d = r.Data;
        Add($"SubmitLevelResult: OK nivel {d.level} {(d.won ? "ganado" : "perdido")} | {d.levelScore} pts " +
            $"(antes {d.previousBest}{(d.isNewRecord ? ", RÉCORD" : "")}) | desbloqueó {d.unlockedLevel} | total {d.newTotalScore} (+{d.scoreGained})");
    }

    private async void RunRanking()
    {
        busy = true;
        Result<RankingResult> r = await B.GetRanking(10);
        busy = false;
        if (!r.Ok) { Add($"GetRanking: {r}"); return; }

        var lines = new System.Text.StringBuilder("RANKING\n");
        foreach (var e in r.Data.entries)
            lines.AppendLine($"{e.rank}. {e.displayName} — {e.totalScore}{(e.isCurrentUser ? "  <- tú" : "")}");
        if (r.Data.me != null && !r.Data.meInTop) lines.AppendLine($"... {r.Data.me.rank}. {r.Data.me.displayName} — {r.Data.me.totalScore}  <- tú");
        Add(lines.ToString());
    }

    private void Add(string line)
    {
        Debug.Log("[BackendTest] " + line);
        log.Add(line);
        if (log.Count > 30) log.RemoveAt(0);
    }

    private void BuildStyles()
    {
        int unit = Mathf.RoundToInt(Screen.height * 0.03f);
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { wordWrap = true };
            button = new GUIStyle(GUI.skin.button);
            field = new GUIStyle(GUI.skin.textField);
            toggle = new GUIStyle(GUI.skin.toggle);
        }
        label.fontSize = button.fontSize = field.fontSize = toggle.fontSize = unit;
        button.fixedHeight = field.fixedHeight = unit * 2f;
    }
}
