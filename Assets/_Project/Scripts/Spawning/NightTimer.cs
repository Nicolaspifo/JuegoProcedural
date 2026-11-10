using System;
using TMPro;
using UnityEngine;

public class NightTimer : MonoBehaviour
{
    [Header("Tiempo (respaldo)")]
    [Tooltip("Si el jugador no llega a la ronda del jefe, este tiempo largo lo hace aparecer igualmente.")]
    [SerializeField] float duration = 180f;

    [Header("Aparicion por ronda")]
    [Tooltip("Ronda de DifficultyDirector en la que aparece el jefe (su ultima ronda = la mas dificil de la curva). Hay que pelear hasta ahi: la ronda ya no la adelanta el nivel del jugador.")]
    [SerializeField, Min(2)] int bossRound = 18;

    [Header("Jefe")]
    [SerializeField] GameObject bossPrefab;
    [SerializeField] Transform player;
    [SerializeField] float spawnDistance = 8f;
    [Tooltip("Opcional: si lo asignas, el jefe aparece aquí en vez de alrededor del jugador")]
    [SerializeField] Transform spawnPoint;

    [Header("Al aparecer el jefe")]
    [Tooltip("Scripts a apagar, por ejemplo el EnemySpawner")]
    [SerializeField] Behaviour[] disableWhenBossAppears;

    [Header("UI (opcional)")]
    [SerializeField] TMP_Text label;

    public float Remaining { get; private set; }
    public bool BossSpawned { get; private set; }
    public event Action<GameObject> OnBossSpawned;

    public int BossRound => bossRound;

    bool stoppedSpawning;

    void Start()
    {
        Remaining = duration;
        UpdateLabel();
    }

    void Update()
    {
        if (BossSpawned)
        {
            UpdateLabel();   // el reloj del HUD sigue avanzando en la pelea (y despues, en el modo infinito)
            return;
        }

        Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
        UpdateLabel();

        // La ronda manda (hay que pelear de verdad); el tiempo largo es solo un respaldo si el jugador se estanca.
        DifficultyDirector director = DifficultyDirector.Instance;
        bool roundReached = director != null && director.Round >= bossRound;

        // Al llegar a la ultima ronda se deja de invocar mas enemigos: el jefe no aparece hasta terminarte a los
        // que ya estan en el campo (asi no se "regalan" enemigos sin matar al pasar al jefe).
        if (roundReached && !stoppedSpawning)
        {
            stoppedSpawning = true;
            foreach (var b in disableWhenBossAppears)
                if (b != null) b.enabled = false;

            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
        }

        bool fieldClear = stoppedSpawning && FindAnyObjectByType<EnemyChase>() == null;
        if ((roundReached && fieldClear) || Remaining <= 0f) SpawnBoss();
    }

    [ContextMenu("Probar: aparecer jefe ahora")]
    void SpawnBoss()
    {
        if (BossSpawned) return;
        BossSpawned = true;

        foreach (var b in disableWhenBossAppears)
            if (b != null) b.enabled = false;

        Vector3 pos;
        if (spawnPoint != null)
        {
            pos = spawnPoint.position;
        }
        else
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            pos = player.position + (Vector3)(dir * spawnDistance);
        }
        pos.z = 0f;

        GameObject boss = Instantiate(bossPrefab, pos, Quaternion.identity);
        boss.SetActive(true);
        OnBossSpawned?.Invoke(boss);
        Debug.Log("[Noche] ¡Apareció el jefe!");
    }

    void UpdateLabel()
    {
        if (label == null) return;

        // El HUD muestra el avance por rondas reales (hay que pelear), no por nivel del jugador, mas el reloj.
        DifficultyDirector director = DifficultyDirector.Instance;
        int round = director != null ? director.Round : 1;
        string clock = RunClock.Instance != null ? RunClock.Instance.Formatted : "00:00";
        label.text = BossSpawned ? $"¡LA LLORONA! · {clock}" : $"RONDA {round}/{bossRound} · {clock}";
    }
}
