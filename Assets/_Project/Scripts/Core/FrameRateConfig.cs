using UnityEngine;

public class FrameRateConfig : MonoBehaviour
{
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        // VSync tiene prioridad sobre targetFrameRate: hay que apagarlo.
        QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = TargetFrameRate;
    }
}
