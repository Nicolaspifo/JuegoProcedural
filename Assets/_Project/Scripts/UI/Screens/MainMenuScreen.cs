using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escena "MenuPrincipal". Al abrir la app recupera la sesion guardada y muestra el jugador, su puntaje
/// acumulado y su progreso. Botones (de arriba a abajo): Jugar (selector de niveles), Ranking, Instrucciones,
/// Cerrar sesion y Salir. Sin sesion, "Jugar" pasa a "Iniciar sesión".
/// </summary>
public class MainMenuScreen : ScreenBase
{
    private Button playButton, rankingButton, instructionsButton, logoutButton, quitButton;
    private TextMeshProUGUI nameText, statsText;
    private Image progressFill;

    private void Start()
    {
        playButton = UIKit.Find<Button>("PlayButton");
        instructionsButton = UIKit.Find<Button>("PlayButton (1)");
        quitButton = UIKit.Find<Button>("Salir");

        if (playButton == null)
        {
            Debug.LogWarning("[UI] MenuPrincipal: no se encontro 'PlayButton'.");
            return;
        }

        // Columna de botones a la derecha, con el mismo estilo del diseno (paso vertical del diseno: ~123 px).
        rankingButton = UIKit.CloneButton(playButton, "RankingButton", "Ranking", Vector2.zero, () => ScreenRouter.Go(ScreenRouter.Ranking));
        logoutButton = UIKit.CloneButton(playButton, "LogoutButton", "Cerrar sesión", Vector2.zero, Logout);
        Place(playButton, 532f);
        Place(rankingButton, 409f);
        Place(instructionsButton, 286f);
        Place(logoutButton, 163f);
        Place(quitButton, 49f);

        UIKit.Bind(playButton, OnPlay, "Jugar");
        if (instructionsButton != null) UIKit.Bind(instructionsButton, () => ScreenRouter.Go(ScreenRouter.Instructions), "Instrucciones");
        if (quitButton != null) UIKit.Bind(quitButton, Quit, "Salir");
        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer) quitButton.gameObject.SetActive(false);

        BuildUserPanel();
        Refresh(B.CurrentUser);
        B.ProfileChanged += Refresh;

        if (B.IsLoggedIn) ReloadProfile();
        else RestoreSession();
    }

    private void OnDestroy() => B.ProfileChanged -= Refresh;

    private static void Place(Button b, float y)
    {
        if (b == null) return;
        var rt = (RectTransform)b.transform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    // Tabla abajo a la izquierda: nombre, puntaje total y progreso.
    private void BuildUserPanel()
    {
        Canvas canvas = UIKit.MainCanvas();
        RectTransform content = UIKit.CreateBoard(canvas.transform, "PanelJugador", new Vector2(0.0f, 0.02f), new Vector2(0.45f, 0.52f));
        nameText = UIKit.CreateText(content, "Nombre", "", 46f, new Vector2(0.03f, 0.68f), new Vector2(0.97f, 0.98f), color: UIKit.TextGold, bold: true);
        statsText = UIKit.CreateText(content, "Datos", "", 32f, new Vector2(0.03f, 0.22f), new Vector2(0.97f, 0.68f));
        progressFill = UIKit.CreateProgressBar(content, "Progreso", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.16f), 0f);
    }

    private void Refresh(UserProfile user)
    {
        if (this == null || nameText == null) return;
        bool logged = user != null;

        if (logged)
        {
            nameText.text = user.displayName;
            statsText.text = $"Puntaje total: <color=#F2A300>{UIKit.Points(user.totalScore)}</color>\n" +
                             $"Niveles completados: {user.levelsCompleted}/{UserProfile.MaxLevels}\n" +
                             $"Nivel desbloqueado: {user.maxUnlockedLevel}";
            UIKit.SetProgress(progressFill, user.Progress01);
        }
        else
        {
            nameText.text = "Sin sesión";
            statsText.text = "Inicia sesión para guardar tu progreso y aparecer en el ranking.";
            UIKit.SetProgress(progressFill, 0f);
        }

        UIKit.SetLabel(playButton, logged ? "Jugar" : "Iniciar sesión");
        if (rankingButton != null) rankingButton.gameObject.SetActive(logged);
        if (logoutButton != null) logoutButton.gameObject.SetActive(logged);
    }

    private async void RestoreSession()
    {
        Busy = true;
        nameText.text = "Conectando...";
        statsText.text = "";
        Result r = await B.TryRestoreSession();
        Busy = false;
        if (this == null) return;

        if (r.Ok) return;   // Refresh llega por ProfileChanged
        Refresh(null);
        if (r.CanRetry) UIAviso.ShowResult(r, RestoreSession);
    }

    private async void ReloadProfile()
    {
        Result<UserProfile> r = await B.LoadProfile();   // silencioso: ya se muestra el perfil en cache
        if (this != null && !r.Ok && r.Code == ResultCode.SessionExpired) Refresh(null);
    }

    private void OnPlay()
    {
        if (Busy) return;
        ScreenRouter.Go(B.IsLoggedIn ? ScreenRouter.Levels : ScreenRouter.Login);
    }

    private void Logout()
    {
        UIAviso.Show(UIAviso.Kind.Sync, "¿Cerrar sesión?", "Sí", () =>
        {
            B.Logout();
            Refresh(null);
        }, "No");
    }

    protected override void OnBack() => Quit();

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
