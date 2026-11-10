using UnityEngine;

public class GameVolume : MonoBehaviour
{
    private const string Key = "Audio.MasterVolume";
    private const float DefaultValue = 0.8f;

    /// <summary>Volumen actual entre 0 y 1.</summary>
    public static float Value { get; private set; } = DefaultValue;

    /// <summary>Cambia el volumen, lo aplica al instante y lo guarda.</summary>
    public static void Set(float value)
    {
        Value = Mathf.Clamp01(value);
        AudioListener.volume = Value;
        PlayerPrefs.SetFloat(Key, Value);
    }

    /// <summary>Escribe los PlayerPrefs a disco.</summary>
    public static void Save() => PlayerPrefs.Save();

    // Corre solo, una vez, al iniciar el juego y antes de cargar cualquier escena.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Value = Mathf.Clamp01(PlayerPrefs.GetFloat(Key, DefaultValue));
        AudioListener.volume = Value;
    }
}
