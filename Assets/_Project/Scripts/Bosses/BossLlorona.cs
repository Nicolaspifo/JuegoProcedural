using System;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class BossLlorona : MonoBehaviour
{
    [Header("Fases (% de vida)")]
    [Tooltip("Cada valor es el % de vida al que empieza la siguiente fase: 0.75 / 0.5 / 0.25 = 4 fases (100-75, 75-50, 50-25, 25-0).")]
    [SerializeField] float[] phaseThresholds = { 0.75f, 0.5f, 0.25f };

    [Header("Movimiento")]
    [SerializeField] float[] speedPerPhase = { 1.5f, 2f, 2.5f, 3f };
    [SerializeField] float stopDistance = 1.2f;
    [Tooltip("Cuanto se desvia perpendicularmente al acercarse (serpenteo), para que no camine siempre en linea recta.")]
    [SerializeField, Min(0f)] float weaveAmplitude = 1.2f;
    [Tooltip("Ciclos de serpenteo por segundo, aprox.")]
    [SerializeField, Min(0f)] float weaveFrequency = 0.6f;

    [Header("Embestida por fase (usa el ataque de EnemyContactDamage)")]
    [Tooltip("Metros que se lanza hacia el jugador en cada fase: crece con las fases.")]
    [SerializeField] float[] lungeDistancePerPhase = { 2.5f, 3.5f, 4.5f, 5.5f };
    [Tooltip("Duracion de la embestida por fase: menos tiempo = mas rapida.")]
    [SerializeField] float[] lungeTimePerPhase = { 0.3f, 0.3f, 0.28f, 0.26f };
    [Tooltip("El ataque empieza a cargarse a esta fraccion de la distancia de embestida.")]
    [SerializeField, Range(0.5f, 1f)] float lungeTriggerFraction = 0.85f;
    [SerializeField] float phaseChangePause = 1f;

    public int Phase { get; private set; } = 1;
    public bool IsDead { get; private set; }
    public bool MovementLocked { get; set; }

    public event Action<int> OnPhaseChanged;
    public event Action OnDefeated;

    Health health;
    Rigidbody2D rb;
    EnemyContactDamage contact;
    Transform target;
    float pauseUntil;
    float nextRetarget;

    void Awake()
    {
        health = GetComponent<Health>();
        contact = GetComponent<EnemyContactDamage>();
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            // Igual que EnemyChase: cinemático, se mueve solo con MovePosition.
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    void Start()
    {
        FindTarget();
        ApplyLunge();
        BossEncounter.Begin(gameObject);   // campo limpio y sin spawner: la fase 1 es el jefe solo
    }

    void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }

    void Update()
    {
        if (IsDead) return;

        float max = health.Max;
        if (max <= 0f) return;

        float pct = health.Current / max;
        if (pct <= 0f)
        {
            Die();
            return;
        }

        int wanted = 1;
        for (int i = 0; i < phaseThresholds.Length; i++)
            if (pct <= phaseThresholds[i]) wanted = i + 2;
        if (wanted > Phase) SetPhase(wanted);
    }

    void FixedUpdate()
    {
        if (IsDead) return;

        if (target == null)
        {
            if (Time.time >= nextRetarget)
            {
                nextRetarget = Time.time + 0.5f;
                FindTarget();
            }
            if (target == null) return;
        }

        if (MovementLocked || Time.time < pauseUntil) return;
        if (contact != null && contact.IsBusy) return;   // cargando o recuperándose de un golpe

        float attackDistance = contact != null ? contact.Range * 0.9f : stopDistance;

        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 toTarget = (Vector2)target.position - pos;
        float distance = toTarget.magnitude;
        if (distance <= attackDistance) return;

        // Serpentea perpendicular a la linea hacia el jugador (se desvanece cerca del ataque, para no titubear
        // justo al llegar): asi no camina siempre en linea recta, se siente mas vivo.
        Vector2 dir = toTarget / Mathf.Max(distance, 0.0001f);
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float weaveFalloff = Mathf.Clamp01((distance - attackDistance) / 3f);
        Vector2 moveDir = (dir + perp * Mathf.Sin(Time.time * weaveFrequency * Mathf.PI * 2f) * weaveAmplitude * weaveFalloff).normalized;

        float speed = speedPerPhase[Mathf.Clamp(Phase - 1, 0, speedPerPhase.Length - 1)];
        Vector2 next = pos + moveDir * speed * Time.fixedDeltaTime;

        if (rb != null) rb.MovePosition(next);
        else transform.position = next;
    }

    void SetPhase(int p)
    {
        Phase = p;
        ApplyLunge();
        pauseUntil = Time.time + phaseChangePause;
        OnPhaseChanged?.Invoke(p);
        Debug.Log($"[Llorona] Fase {p}");
    }

    void ApplyLunge()
    {
        if (contact == null || lungeDistancePerPhase.Length == 0 || lungeTimePerPhase.Length == 0) return;

        float distance = lungeDistancePerPhase[Mathf.Clamp(Phase - 1, 0, lungeDistancePerPhase.Length - 1)];
        float time = lungeTimePerPhase[Mathf.Clamp(Phase - 1, 0, lungeTimePerPhase.Length - 1)];
        contact.SetLunge(distance, time);
        contact.SetRange(distance * lungeTriggerFraction);
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnDefeated?.Invoke();
        GameEvents.RaiseBossDefeated();
        Debug.Log("[Llorona] Derrotada");
    }

    void OnDestroy()
    {
        if (!IsDead && health != null && health.Current <= 0f) Die();
    }

    [ContextMenu("Probar: fase 2")] void TestPhase2() => SetPhase(2);
    [ContextMenu("Probar: fase 3")] void TestPhase3() => SetPhase(3);
    [ContextMenu("Probar: fase 4")] void TestPhase4() => SetPhase(4);
}