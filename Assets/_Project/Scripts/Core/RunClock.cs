using UnityEngine;

/// <summary>
/// Reloj unico de la partida: cuanto tiempo real se ha jugado. Usa Time.deltaTime, que ya vale 0 mientras
/// PauseControl pone Time.timeScale en 0 (cartas de mejora, pausa), asi que esa pausa no cuenta solo.
///
/// Fuente unica para el HUD y el reporte final (Game Over, victoria): antes DifficultyDirector, RunMetrics y
/// RunScore llevaban cada uno su propio contador identico por separado.
/// Va en un objeto vacio de la escena (uno solo). Sin este componente, los que lo usan caen a 0.
/// </summary>
public class RunClock : MonoBehaviour
{
    public static RunClock Instance { get; private set; }

    /// <summary>Segundos jugados desde que empezo la partida (sin contar pausas).</summary>
    public float Elapsed { get; private set; }

    /// <summary>Formato mm:ss, listo para HUD o reportes.</summary>
    public string Formatted => Format(Elapsed);

    /// <summary>Da formato mm:ss a cualquier cantidad de segundos (por ejemplo, un tiempo ya guardado).</summary>
    public static string Format(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void OnEnable()
    {
        Instance = this;
        Elapsed = 0f;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    private void Update() => Elapsed += Time.deltaTime;
}
