using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BossLlorona))]
public class BossSummons : MonoBehaviour
{
    [Serializable] public class Entry
    {
        public GameObject prefab;
        [Min(1)] public int count = 1;
        [Tooltip("Vida extra de este refuerzo (se multiplica sobre la dificultad actual).")] public float hpMultiplier = 1f;
        [Tooltip("Daño extra de este refuerzo.")] public float damageMultiplier = 1f;
    }
    [Serializable] public class PhaseSummons
    {
        public float interval = 8f;
        [Tooltip("Tandas que invoca en esta etapa antes de parar (0 = sin limite). Para etapas finales: pocas tandas, mas espacio para pelear.")]
        public int maxWaves = 0;
        public Entry[] entries;
    }

    [SerializeField] PhaseSummons[] phases;   // 4 elementos: etapa 1 (100-75 %), 2, 3 y 4
    [SerializeField] float spawnRadius = 3f;
    [SerializeField] int maxAlive = 10;
    [SerializeField] float firstDelay = 4f;
    [SerializeField] bool killSummonsOnDeath = false;

    BossLlorona boss;
    readonly List<GameObject> alive = new List<GameObject>();
    float timer;
    int wavesThisPhase;

    void Reset()
    {
        phases = new[]
        {
            new PhaseSummons { interval = 9f },
            new PhaseSummons { interval = 8f },
            new PhaseSummons { interval = 9f },
            new PhaseSummons { interval = 7f },
        };
    }

    void Awake() => boss = GetComponent<BossLlorona>();

    void Start()
    {
        timer = firstDelay;
        boss.OnPhaseChanged += _ => { wavesThisPhase = 0; SummonWave(); timer = Current().interval; };
        boss.OnDefeated += OnBossDefeated;
    }

    void Update()
    {
        if (boss.IsDead || phases == null || phases.Length == 0) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SummonWave();
            timer = Current().interval;
        }
    }

    PhaseSummons Current() => phases[Mathf.Clamp(boss.Phase - 1, 0, phases.Length - 1)];

    void SummonWave()
    {
        if (phases == null || phases.Length == 0) return;

        PhaseSummons phase = Current();
        if (phase.maxWaves > 0 && wavesThisPhase >= phase.maxWaves) return;
        wavesThisPhase++;

        alive.RemoveAll(g => g == null);
        var entries = phase.entries;
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e.prefab == null) continue;
            for (int i = 0; i < e.count; i++)
            {
                if (alive.Count >= maxAlive) return;

                Vector2 pos = (Vector2)transform.position +
                              UnityEngine.Random.insideUnitCircle.normalized * spawnRadius;
                GameObject go = Instantiate(e.prefab, pos, Quaternion.identity);
                if (go.TryGetComponent(out EnemyScaling scaling)) scaling.SetExtra(e.hpMultiplier, e.damageMultiplier);
                go.SetActive(true);
                alive.Add(go);
            }
        }
    }

    void OnBossDefeated()
    {
        if (!killSummonsOnDeath) return;
        foreach (var g in alive) if (g != null) Destroy(g);
        alive.Clear();
    }
}