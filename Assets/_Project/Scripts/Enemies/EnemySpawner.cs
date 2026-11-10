using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Ronda (nivel del jugador) desde la que puede salir este enemigo (usa el DifficultyDirector). 1 = desde el inicio.")]
        [Min(1)] public int unlockRound = 1;
    }

    [Header("Enemigos")]
    [SerializeField] private EnemyEntry[] enemies;
    [SerializeField, Min(1)] private int enemiesPerSpawn = 1;
    [SerializeField, Min(1)] private int maxAlive = 20;

    [Header("Zona de spawn")]
    [Tooltip("Calcula el borde de spawn desde la camara (ortho x aspecto + margen), asi los enemigos nacen siempre fuera de la vista en cualquier resolucion.")]
    [SerializeField] private bool useCameraBounds = true;
    [Tooltip("Distancia extra fuera del encuadre de la camara donde aparecen.")]
    [SerializeField, Min(0f)] private float cameraMargin = 2f;
    [Tooltip("Solo se usa si no hay camara ortografica o Use Camera Bounds esta apagado.")]
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(10f, 10f);

    [Header("Ritmo")]
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
    [SerializeField, Min(0.1f)] private float minInterval = 0.5f;
    [SerializeField, Min(0f)] private float intervalDecreasePerSecond = 0.02f;

    private readonly List<GameObject> alive = new();
    private float timer;
    private float elapsed;
    private float currentInterval;

    private void Awake()
    {
        currentInterval = spawnInterval;

        if (enemies == null || enemies.Length == 0)
            Debug.LogWarning("[EnemySpawner] No hay enemigos configurados.", this);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        elapsed += Time.deltaTime;

        // Con director, el ritmo sale de su curva; sin el, se usa el intervalo que baja solo.
        DifficultyDirector director = DifficultyDirector.Instance;
        float interval = director != null ? director.SpawnInterval : currentInterval;

        if (timer >= interval)
        {
            SpawnEnemies();
            timer = 0f;
        }

        if (director == null && elapsed >= 1f)
        {
            currentInterval = Mathf.Max(
                minInterval,
                currentInterval - intervalDecreasePerSecond);

            elapsed = 0f;
        }
    }

    private void SpawnEnemies()
    {
        // Limpia enemigos que ya fueron destruidos.
        alive.RemoveAll(enemy => enemy == null);

        DifficultyDirector director = DifficultyDirector.Instance;
        int limit = director != null ? director.MaxAlive : maxAlive;

        for (int i = 0; i < enemiesPerSpawn; i++)
        {
            if (alive.Count >= limit) return;

            GameObject prefab = PickRandomPrefab();
            if (prefab == null) continue;

            GameObject enemy = Instantiate(
                prefab,
                RandomBorderPosition(),
                Quaternion.identity);

            alive.Add(enemy);
        }
    }

    // Valido si tiene prefab y peso y, con director, si ya se alcanzo su ronda de desbloqueo.
    private static bool IsSpawnable(EnemyEntry entry)
    {
        if (entry == null || entry.prefab == null || entry.weight <= 0f) return false;

        DifficultyDirector director = DifficultyDirector.Instance;
        return director == null || director.Round >= entry.unlockRound;
    }

    private GameObject PickRandomPrefab()
    {
        float totalWeight = 0f;

        foreach (EnemyEntry entry in enemies)
        {
            if (!IsSpawnable(entry)) continue;

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);

        foreach (EnemyEntry entry in enemies)
        {
            if (!IsSpawnable(entry)) continue;

            roll -= entry.weight;

            if (roll <= 0f)
                return entry.prefab;
        }

        return null;
    }

    /// <summary>
    /// Centro y medio tamaño del rectangulo de spawn. Con camara ortografica sale del encuadre real
    /// (la camara ve mas ancho que alto, p. ej. 17.8 x 10 en 16:9); si no, se usa arenaHalfSize.
    /// </summary>
    private void GetSpawnArea(out Vector3 center, out Vector2 halfSize)
    {
        Camera cam = useCameraBounds ? Camera.main : null;

        if (cam != null && cam.orthographic)
        {
            halfSize = new Vector2(
                cam.orthographicSize * cam.aspect + cameraMargin,
                cam.orthographicSize + cameraMargin);
            Vector3 camPos = cam.transform.position;
            center = new Vector3(camPos.x, camPos.y, transform.position.z);
            return;
        }

        center = transform.position;
        halfSize = arenaHalfSize;
    }

    private Vector3 RandomBorderPosition()
    {
        GetSpawnArea(out Vector3 center, out Vector2 half);

        float x = Random.Range(-half.x, half.x);
        float y = Random.Range(-half.y, half.y);

        switch (Random.Range(0, 4))
        {
            case 0:
                return new Vector3(center.x - half.x, center.y + y, center.z);

            case 1:
                return new Vector3(center.x + half.x, center.y + y, center.z);

            case 2:
                return new Vector3(center.x + x, center.y - half.y, center.z);

            default:
                return new Vector3(center.x + x, center.y + half.y, center.z);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Prueba: spawnear ahora")]
    private void TestSpawn()
    {
        if (Application.isPlaying)
            SpawnEnemies();
    }
#endif

    private void OnDrawGizmosSelected()
    {
        GetSpawnArea(out Vector3 center, out Vector2 half);
        Gizmos.DrawWireCube(center, new Vector3(half.x * 2f, half.y * 2f, 0f));
    }
}