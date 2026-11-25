using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escena "MenuPrincipal". Al abrir la app recupera la sesion guardada y muestra el jugador, su puntaje
/// acumulado y su progreso. Botones (de arriba a abajo): Jugar (selector de niveles), Ranking, Instrucciones,
/// Cerrar sesion y Salir. Sin sesion: Iniciar sesión, Jugar como invitado, Instrucciones y Salir.
/// </summary>
public class MainMenuScreen : ScreenBase
{
    private Button playButton, rankingButton, guestButton, instructionsButton, logoutButton, quitButton;
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
        guestButton = UIKit.CloneButton(playButton, "GuestButton", "Jugar como invitado", Vector2.zero, PlayAsGuest);
        TMPro.TMP_Text guestLabel = guestButton.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (guestLabel != null)   // texto mas largo que el del diseno: que se ajuste al boton
        {
            guestLabel.enableAutoSizing = true;
            guestLabel.fontSizeMax = guestLabel.fontSize;
            guestLabel.fontSizeMin = guestLabel.fontSize * 0.4f;
            guestLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }

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

    // El evento trae el perfil de la cuenta; se ignora y se lee el jugador actual (cuenta o invitado).
    private void Refresh(UserProfile _)
    {
        if (this == null || nameText == null) return;
        bool logged = B.IsLoggedIn;
        if (logged) GuestSession.End();   // al iniciar sesion se descarta la partida de invitado
        UserProfile user = GuestSession.CurrentProfile;

        if (user != null)
        {
            nameText.text = user.displayName;
            statsText.text = $"Puntaje total: <color=#F2A300>{UIKit.Points(user.totalScore)}</color>\n" +
                             $"Niveles completados: {user.levelsCompleted}/{UserProfile.MaxLevels}\n" +
                             $"Nivel desbloqueado: {user.maxUnlockedLevel}";
            if (!logged) statsText.text += "\n<size=80%>Modo invitado: el progreso no se guarda.</size>";
            UIKit.SetProgress(progressFill, user.Progress01);
        }
        else
        {
            nameText.text = "Sin sesión";
            statsText.text = "Inicia sesión para guardar tu progreso y aparecer en el ranking, o juega como invitado.";
            UIKit.SetProgress(progressFill, 0f);
        }

        UIKit.SetLabel(playButton, logged ? "Jugar" : "Iniciar sesión");
        UIKit.SetLabel(guestButton, GuestSession.IsActive ? "Seguir como invitado" : "Jugar como invitado");
        if (rankingButton != null) rankingButton.gameObject.SetActive(logged);
        if (logoutButton != null) logoutButton.gameObject.SetActive(logged);
        guestButton.gameObject.SetActive(!logged);

        // Columna de botones a la derecha, con el paso vertical del diseno (~123 px).
        Place(playButton, 532f);
        if (logged)
        {
            Place(rankingButton, 409f);
            Place(instructionsButton, 286f);
            Place(logoutButton, 163f);
            Place(quitButton, 49f);
        }
        else
        {
            Place(guestButton, 409f);
            Place(instructionsButton, 286f);
            Place(quitButton, 163f);
        }
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

    private void PlayAsGuest()
    {
        if (Busy || B.IsLoggedIn) return;
        GuestSession.Begin();
        ScreenRouter.Go(ScreenRouter.Levels);
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
