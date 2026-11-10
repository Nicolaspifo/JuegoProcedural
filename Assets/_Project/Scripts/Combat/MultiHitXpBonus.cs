using UnityEngine;

/// <summary>
/// Bonus de XP por golpear a varios enemigos de un solo tajo ("racha de masacre"), para subir de nivel mas
/// rapido (y llegar antes al boss) a cambio de riesgo: hay que meterse en medio de la horda.
///
/// - Un golpe con al menos <see cref="minHits"/> impactos da XP extra: (impactos - (minHits - 1)) x xpPerExtraHit x racha.
/// - La racha sube con cada golpe multiple consecutivo (x1.0, x1.5, x2.0...). Un golpe flojo la perdona una vez.
/// - Recibir dano reinicia la racha: premia matar en masa sin ser golpeado.
/// - Se apaga mientras el jefe esta vivo, para no abrir cartas de mejora en plena pelea.
/// Va en el Player. Solo escucha eventos de MacheteAttack y de Health.
/// </summary>
[RequireComponent(typeof(MacheteAttack), typeof(PlayerXP))]
public class MultiHitXpBonus : MonoBehaviour
{
    [Tooltip("Impactos minimos en un mismo golpe para dar bonus.")]
    [SerializeField, Min(2)] private int minHits = 3;
    [Tooltip("XP por cada enemigo alcanzado por encima del minimo menos uno (3 impactos = 1 enemigo extra).")]
    [SerializeField, Min(0f)] private float xpPerExtraHit = 3f;
    [Tooltip("Multiplicador segun la racha: golpes multiples consecutivos. El ultimo valor es el tope.")]
    [SerializeField] private float[] streakMultipliers = { 1f, 1.5f, 2f };
    [Tooltip("Maximo de impactos que cuentan por golpe: evita bonus enormes cuando 11 enemigos quedan pegados.")]
    [SerializeField, Min(3)] private int maxCountedHits = 6;
    [Tooltip("El bonus de un solo golpe no supera esta fraccion de la XP que cuesta el siguiente nivel.")]
    [SerializeField, Range(0.05f, 1f)] private float maxFractionOfLevel = 0.35f;

    [Header("Depuracion")]
    [SerializeField] private bool debugLog = true;

    public int Swings { get; private set; }
    public int MultiHitSwings { get; private set; }
    public int TotalHits { get; private set; }
    public int BonusXpTotal { get; private set; }
    public int Streak { get; private set; }

    private MacheteAttack machete;
    private PlayerXP xp;
    private Health health;
    private NightTimer nightTimer;
    private bool graceUsed;
    private float lastHealth;

    private void Awake()
    {
        machete = GetComponent<MacheteAttack>();
        xp = GetComponent<PlayerXP>();
        TryGetComponent(out health);
    }

    private void OnEnable()
    {
        machete.SwingResolved += OnSwing;
        if (health != null)
        {
            lastHealth = health.Current;
            health.Changed += OnHealthChanged;
        }
    }

    private void OnDisable()
    {
        machete.SwingResolved -= OnSwing;
        if (health != null) health.Changed -= OnHealthChanged;
    }

    private void OnSwing(int hits)
    {
        Swings++;
        TotalHits += hits;

        if (BossActive()) return;

        if (hits >= minHits)
        {
            MultiHitSwings++;
            graceUsed = false;
            Streak = Mathf.Min(Streak + 1, Mathf.Max(1, streakMultipliers.Length));

            float multiplier = streakMultipliers.Length > 0 ? streakMultipliers[Streak - 1] : 1f;
            int counted = Mathf.Min(hits, maxCountedHits);
            int raw = Mathf.RoundToInt((counted - (minHits - 1)) * xpPerExtraHit * multiplier);
            int cap = Mathf.Max(1, Mathf.RoundToInt(xp.XpToNextLevel * maxFractionOfLevel));
            int bonus = Mathf.Clamp(raw, 1, cap);

            BonusXpTotal += bonus;
            if (debugLog) Debug.Log($"[Racha] {hits} impactos -> +{bonus} XP (racha x{multiplier:0.0})", this);
            xp.AddXP(bonus);
        }
        else if (Streak > 0)
        {
            // Un golpe flojo mantiene la racha una vez; el segundo seguido la pierde.
            if (!graceUsed) graceUsed = true;
            else ResetStreak("golpe flojo");
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        if (current < lastHealth && Streak > 0) ResetStreak("dano recibido");
        lastHealth = current;
    }

    private void ResetStreak(string reason)
    {
        if (debugLog) Debug.Log($"[Racha] perdida ({reason})", this);
        Streak = 0;
        graceUsed = false;
    }

    private bool BossActive()
    {
        if (nightTimer == null) nightTimer = FindAnyObjectByType<NightTimer>();
        return nightTimer != null && nightTimer.BossSpawned;
    }
}
