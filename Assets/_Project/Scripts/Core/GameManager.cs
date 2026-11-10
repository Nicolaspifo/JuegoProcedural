using System;
using UnityEngine;

/// <summary>
/// Singleton que mantiene el estado global del juego.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameState initialState = GameState.Menu;
    [SerializeField] private bool pauseWithTimeScale = true;

    public GameState CurrentState { get; private set; }

    /// <summary>(estadoAnterior, estadoNuevo)</summary>
    public event Action<GameState, GameState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() => SetState(initialState, force: true);

    private void OnDestroy()
    {
        if (Instance != this) return;
        PauseControl.Pop(this);
        Instance = null;
    }

    public void SetState(GameState newState, bool force = false)
    {
        if (!force && newState == CurrentState) return;

        GameState previous = CurrentState;
        CurrentState = newState;

        if (pauseWithTimeScale)
        {
            if (newState == GameState.Playing) PauseControl.Pop(this);
            else PauseControl.Push(this);
        }

        Debug.Log($"[GameManager] {previous} -> {newState}");
        OnStateChanged?.Invoke(previous, newState);
    }

    public void StartGame() => SetState(GameState.Playing);
    public void Pause() { if (CurrentState == GameState.Playing) SetState(GameState.Paused); }
    public void Resume() { if (CurrentState == GameState.Paused) SetState(GameState.Playing); }
    public void TogglePause() { if (CurrentState == GameState.Playing) Pause(); else Resume(); }
    public void GameOver() => SetState(GameState.GameOver);
    public void Victory() => SetState(GameState.Victory);
    public void BackToMenu() => SetState(GameState.Menu);
}
