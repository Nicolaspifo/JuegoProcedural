using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset actions;      // GameInputActions

    [Header("UI")]
    [SerializeField] private GameObject panel;               // PausePanel
    [SerializeField] private GameObject firstSelected;       // boton Reanudar
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeLabel;           // opcional
    [SerializeField] private GameObject quitButton;          // se oculta en WebGL

    [Header("Reglas")]
    [SerializeField] private Health playerHealth;            // para no pausar si ya murio
    [SerializeField] private string menuSceneName = "MenuPrincipal";

    [Header("Feedback de volumen (opcional)")]
    [SerializeField] private AudioClip volumePreviewClip;    // un sonido corto para probar el volumen
    [SerializeField] private float previewCooldown = 0.12f;

    private InputAction pauseAction;
    private InputAction cancelAction;
    private bool isOpen;
    private float nextPreviewTime;

    private void Awake()
    {
        if (actions != null)
        {
            pauseAction = actions.FindAction("Pause", throwIfNotFound: true);
            cancelAction = actions.FindAction("Cancel", throwIfNotFound: true);
        }
        else
        {
            Debug.LogError("PauseMenu: falta asignar el InputActionAsset (GameInputActions).", this);
        }

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
            volumeSlider.SetValueWithoutNotify(GameVolume.Value);
        }
        UpdateLabel(GameVolume.Value);

        // En la version web no se puede cerrar el juego: se oculta el boton.
        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
            quitButton.SetActive(false);

        if (panel != null) panel.SetActive(false);
    }

    private void OnEnable()
    {
        pauseAction?.Enable();
        cancelAction?.Enable();
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnDisable()
    {
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        pauseAction?.Disable();
        cancelAction?.Disable();

        if (isOpen)
        {
            isOpen = false;
            PauseControl.Pop(this);   // nunca dejar el juego congelado
        }
        GameVolume.Save();
    }

    private void Update()
    {
        if (pauseAction == null) return;

        if (pauseAction.WasPressedThisFrame())
        {
            if (isOpen) Close();
            else TryOpen();
        }
        else if (isOpen && cancelAction.WasPressedThisFrame())
        {
            Close();   // B del mando o Backspace
        }
    }

    private void TryOpen()
    {
        if (playerHealth != null && playerHealth.IsDead) return;  // Game Over manda
        if (PauseControl.IsPaused) return;                        // otro sistema ya pauso (cartas)

        isOpen = true;
        PauseControl.Push(this);
        panel.SetActive(true);
        SelectFirst();
    }

    /// <summary>Cierra el menu y reanuda. Lo llama el boton Reanudar.</summary>
    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        panel.SetActive(false);
        GameVolume.Save();
        PauseControl.Pop(this);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void Restart()
    {
        LeaveScene();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMenu()
    {
        LeaveScene();
        SceneManager.LoadScene(menuSceneName);
    }

    public void Quit()
    {
        GameVolume.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LeaveScene()
    {
        isOpen = false;
        GameVolume.Save();
        PauseControl.Reset();   // devuelve Time.timeScale a 1 antes de cambiar de escena
    }

    private void SelectFirst()
    {
        if (EventSystem.current == null || firstSelected == null) return;

        // Primero null y luego el boton: asi la navegacion con mando se reinicia bien.
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    private void OnVolumeChanged(float value)
    {
        GameVolume.Set(value);
        UpdateLabel(value);

        // Mientras esta pausado no suena nada, asi que reproducimos un sonido corto de prueba.
        if (volumePreviewClip != null && Time.unscaledTime >= nextPreviewTime)
        {
            nextPreviewTime = Time.unscaledTime + previewCooldown;
            Sfx.Play(volumePreviewClip);
        }
    }

    private void UpdateLabel(float value)
    {
        if (volumeLabel != null)
            volumeLabel.text = $"Volumen: {Mathf.RoundToInt(value * 100f)}%";
    }
}