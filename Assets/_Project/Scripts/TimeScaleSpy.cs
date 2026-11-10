using UnityEngine;

public class TimeScaleSpy : MonoBehaviour
{
    float last = -1f;
    bool lastPaused;

    void Start()
    {
        last = Time.timeScale;
        lastPaused = PauseControl.IsPaused;
        Debug.Log($"[Spy] INICIO timeScale={last} paused={lastPaused} frame={Time.frameCount}");
    }

    void Update()
    {
        if (Time.timeScale != last)
        {
            Debug.Log($"[Spy] timeScale {last} -> {Time.timeScale} | frame={Time.frameCount} t={Time.unscaledTime:F2}\n{StackTraceUtility.ExtractStackTrace()}");
            last = Time.timeScale;
        }

        if (PauseControl.IsPaused != lastPaused)
        {
            lastPaused = PauseControl.IsPaused;
            Debug.Log($"[Spy] PauseControl.IsPaused = {lastPaused} | frame={Time.frameCount}");
        }
    }
}