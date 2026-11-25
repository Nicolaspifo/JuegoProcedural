using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Navegacion entre las escenas de pantallas y arranque automatico de su logica: al cargar cada escena del
/// diseno se le agrega su controlador (no hay que arrastrar scripts en las escenas). Los controladores
/// encuentran los objetos por NOMBRE, asi que si se renombra un boton en la escena hay que cambiarlo aqui.
///
/// Flujo: MenuPrincipal → (sin sesion) Inicio Secion ⇄ Crear cuenta → MenuPrincipal → Niveles → Principal
///        (juego) → resultado → Niveles · MenuPrincipal → Ranking · Instrucciones.
/// </summary>
public static class ScreenRouter
{
    public const string Menu = "MenuPrincipal";
    public const string Login = "Inicio Secion";
    public const string Register = "Crear cuenta";
    public const string Levels = "Niveles";
    public const string Ranking = "Ranking";
    public const string Instructions = "Instrucciones";
    public const string Game = "Principal";

    /// <summary>Escena desde la que se llego a la actual (para los botones "Volver").</summary>
    public static string PreviousScene { get; private set; } = Menu;

    public static void Go(string scene)
    {
        PauseControl.Reset();   // nunca cambiar de escena con el juego congelado
        if (UIAviso.IsVisible) UIAviso.Hide();
        PreviousScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(scene);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        // Al salir de un nivel con "Menu" se vuelve al selector de niveles.
        LevelSession.MenuSceneName = Levels;
        LevelSession.GameSceneName = Game;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        System.Type controller = null;
        switch (scene.name)
        {
            case Menu: controller = typeof(MainMenuScreen); break;
            case Login: controller = typeof(LoginScreen); break;
            case Register: controller = typeof(RegisterScreen); break;
            case Levels: controller = typeof(LevelSelectScreen); break;
            case Ranking: controller = typeof(RankingScreen); break;
            case Instructions: controller = typeof(InstructionsScreen); break;
            case Game: controller = typeof(LevelHudScreen); break;
        }
        if (controller == null) return;

        // En el editor la escena puede abrirse de forma aditiva (por ejemplo junto a una escena "Untitled").
        // Se activa para que los controladores encuentren sus objetos por nombre en ella.
        if (SceneManager.GetActiveScene() != scene) SceneManager.SetActiveScene(scene);

        UIKit.EnsureEventSystem();
        var go = new GameObject("[Pantalla] " + controller.Name);
        if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent(controller);
    }
}
