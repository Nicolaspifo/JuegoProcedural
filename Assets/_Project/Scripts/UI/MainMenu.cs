using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameSceneName = "Principal";
    [SerializeField] GameObject firstSelected;   // PlayButton
    [SerializeField] GameObject quitButton;

    void Start()
    {
        Time.timeScale = 1f;

        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
            quitButton.SetActive(false);

        EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    public void Play()
    {
        // Continua en el nivel mas alto desbloqueado del usuario (nivel 1 si no hay sesion).
        LevelSession.GameSceneName = gameSceneName;
        LevelSession.Play(LevelSession.HighestPlayableLevel);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}