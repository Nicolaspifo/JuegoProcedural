using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ataque cuerpo a cuerpo con anticipacion. Estados:
/// Idle → Windup (se detiene y avisa) → golpe → Recover (quieto, vulnerable) → Idle.
/// Un enemigo solo empieza a cargar si consigue una ficha de <see cref="AttackTokens"/>.
/// </summary>
public class EnemyContactDamage : MonoBehaviour
{
    private enum State { Idle, Windup, Lunge, Recover }

    [SerializeField, Min(0f)] private float damage = 5f;
    public float DamageMultiplier { get; set; } = 1f;
    [SerializeField, Min(0f)] private float range = 1.2f;
    [SerializeField, Min(0.1f)] private float cooldown = 1f;

    [Header("Anticipacion (windup)")]
    [Tooltip("Segundos quieto avisando antes de golpear. Es el tiempo que tiene el jugador para salir.")]
    [SerializeField, Min(0f)] private float windup = 0.3f;
    [Tooltip("Segundos quieto tras golpear. Ventana para contraatacar o alejarse.")]
    [SerializeField, Min(0f)] private float recover = 0.3f;
    [Tooltip("Fraccion del rango visible que realmente hace dano. <1 = un poco generoso con el jugador.")]
    [SerializeField, Range(0.5f, 1f)] private float damageRadiusScale = 0.9f;

    [Header("Embestida (hace que el golpe conecte)")]
    [Tooltip("Metros que se lanza hacia el jugador al terminar el windup. 0 = sin embestida (el golpe solo cuenta si el jugador sigue dentro del radio).")]
    [SerializeField, Min(0f)] private float lungeDistance = 1.5f;
    [Tooltip("Duracion de la embestida. Mas corta = mas rapida y mas dificil de esquivar.")]
    [SerializeField, Min(0.05f)] private float lungeTime = 0.12f;
    [Tooltip("Distancia al jugador a la que la embestida cuenta como contacto y hace dano.")]
    [SerializeField, Min(0.2f)] private float lungeHitRadius = 1f;
    [Tooltip("Fraccion del windup durante la que sigue al jugador; despues fija la direccion y es esquivable.")]
    [SerializeField, Range(0f, 1f)] private float aimLockFraction = 0.6f;
    [Tooltip("Cuanto anticipa el movimiento del jugador al apuntar (0 = apunta donde esta, 1 = donde estara).")]
    [SerializeField, Range(0f, 1f)] private float leadFactor = 0.7f;

    [Header("Fichas de ataque")]
    [Tooltip("Cuantos enemigos pueden estar cargando/golpeando a la vez (todos los tipos).")]
    [SerializeField, Min(1)] private int maxConcurrentAttackers = 3;

    /// <summary>Empieza la carga. Parametro: duracion del windup en segundos.</summary>
    public event Action<float> WindupStarted;
    /// <summary>Se dispara cuando el golpe conecta.</summary>
    public event Action Attacked;
    /// <summary>Empieza la embestida (fin del windup, con embestida activa). Para VFX: estela, polvo...</summary>
    public event Action LungeStarted;
    /// <summary>Termina la embestida, haya golpeado o fallado (justo antes de Recover).</summary>
    public event Action LungeEnded;

    // Contadores por tipo de enemigo (ataques iniciados / golpes que conectaron), para medir en RunMetrics.
    public static readonly Dictionary<string, int> WindupCount = new();
    public static readonly Dictionary<string, int> HitCount = new();
    // Ataques que no llegaron a golpear porque el enemigo murio (o se desactivo) cargando o embistiendo.
    public static readonly Dictionary<string, int> CancelledCount = new();

    private static void Count(Dictionary<string, int> map, string enemyName)
    {
        string key = enemyName.Replace("(Clone)", "");
        map.TryGetValue(key, out int n);
        map[key] = n + 1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCounters() { WindupCount.Clear(); HitCount.Clear(); CancelledCount.Clear(); }

    /// <summary>Ajusta la embestida en ejecucion (el jefe la hace mas larga en cada fase).</summary>
    public void SetLunge(float distance, float time)
    {
        lungeDistance = Mathf.Max(0f, distance);
        lungeTime = Mathf.Max(0.05f, time);
    }

    /// <summary>Distancia a la que empieza a cargar el ataque.</summary>
    public void SetRange(float newRange) => range = Mathf.Max(0.2f, newRange);

    /// <summary>Impide empezar nuevos ataques durante unos segundos (el jefe lo usa justo despues de su canto).</summary>
    public void Hold(float seconds) => nextHitTime = Mathf.Max(nextHitTime, Time.time + seconds);

    public float Range => range;
    public float Windup => windup;
    /// <summary>Direccion fijada de la embestida (valida desde que empieza el windup).</summary>
    public Vector2 LungeDirection => lungeDir;
    public float LungeDistance => lungeDistance;
    /// <summary>True mientras carga o se recupera: el enemigo no debe moverse.</summary>
    public bool IsBusy => state != State.Idle;

    private const float RetargetInterval = 0.5f;

    private Transform target;
    private IDamageable damageable;
    private State state = State.Idle;
    private float stateEndTime;
    private float nextHitTime;
    private float nextRetarget;

    private Rigidbody2D body;
    private Rigidbody2D targetBody;   // para anticipar hacia donde corre el jugador
    private Vector2 lungeDir;
    private float aimLockTime;
    private bool lungeHit;

    private void Awake() => TryGetComponent(out body);

    private void Start() => FindTarget();

    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        target = player.transform;
        player.TryGetComponent(out damageable);
        player.TryGetComponent(out targetBody);
    }

    private void Update()
    {
        if (target == null || damageable == null)
        {
            if (Time.time >= nextRetarget)
            {
                nextRetarget = Time.time + RetargetInterval;
                FindTarget();
            }
            return;
        }

        if (damageable.IsDead)
        {
            Stop();
            return;
        }

        switch (state)
        {
            case State.Idle:
                TryStartWindup();
                break;

            case State.Windup:
                // Mientras dura la fraccion de seguimiento, reajusta la direccion; luego queda fija (esquivable).
                if (lungeDistance > 0f && Time.time < aimLockTime) AimLunge();
                if (Time.time >= stateEndTime) Strike();
                break;

            case State.Lunge:
                // El dano se aplica en FixedUpdate al tocar al jugador; aqui solo se cierra la embestida.
                if (lungeHit || Time.time >= stateEndTime) BeginRecover();
                break;

            case State.Recover:
                if (Time.time >= stateEndTime) Finish();
                break;
        }
    }

    private void TryStartWindup()
    {
        if (Time.time < nextHitTime || !InAttackRange()) return;
        if (!AttackTokens.TryAcquire(this, maxConcurrentAttackers)) return;

        state = State.Windup;
        stateEndTime = Time.time + windup;
        WindupStarted?.Invoke(windup);
        Count(WindupCount, name);

        aimLockTime = Time.time + windup * aimLockFraction;
        if (lungeDistance > 0f) AimLunge();
    }

    /// <summary>
    /// Fija la direccion de la embestida hacia donde estara el jugador cuando llegue el golpe
    /// (posicion actual + velocidad × tiempo restante). Castiga correr en linea recta; cambiar de direccion la esquiva.
    /// </summary>
    private void AimLunge()
    {
        Vector2 predicted = target.position;
        if (targetBody != null)
        {
            float lead = Mathf.Max(0f, stateEndTime - Time.time) + lungeTime * 0.5f;
            predicted += targetBody.linearVelocity * (lead * leadFactor);
        }

        Vector2 toPredicted = predicted - (Vector2)transform.position;
        if (toPredicted.sqrMagnitude > 0.0001f) lungeDir = toPredicted.normalized;
    }

    private void Strike()
    {
        if (lungeDistance > 0f && body != null)
        {
            // Con embestida: el dano llega al tocar al jugador durante el avance (ver FixedUpdate).
            state = State.Lunge;
            stateEndTime = Time.time + lungeTime;
            lungeHit = false;
            LungeStarted?.Invoke();
            return;
        }

        // Sin embestida: el golpe solo cuenta si el jugador sigue dentro; si salio, el enemigo falla igualmente.
        if (InStrikeRange() && !damageable.IsDead) ApplyHit();
        BeginRecover();
    }

    private void FixedUpdate()
    {
        if (state != State.Lunge || lungeHit || target == null || body == null) return;

        // Contacto: se detiene y golpea. Si no, sigue avanzando en la direccion fijada.
        Vector2 toTarget = (Vector2)target.position - body.position;
        if (toTarget.magnitude <= lungeHitRadius)
        {
            if (!damageable.IsDead) ApplyHit();
            lungeHit = true;
            return;
        }

        float stepLength = lungeDistance / lungeTime * Time.fixedDeltaTime;
        body.MovePosition(body.position + lungeDir * stepLength);
    }

    private void ApplyHit()
    {
        damageable.TakeDamage(damage * DamageMultiplier, gameObject);
        Attacked?.Invoke();
        Count(HitCount, name);
    }

    private void BeginRecover()
    {
        bool wasLunging = state == State.Lunge;

        nextHitTime = Time.time + cooldown;
        state = State.Recover;
        stateEndTime = Time.time + recover;
        AttackTokens.Release(this);

        if (wasLunging) LungeEnded?.Invoke();
    }

    private void Finish()
    {
        state = State.Idle;
    }

    private void Stop()
    {
        state = State.Idle;
        AttackTokens.Release(this);
    }

    private bool InAttackRange() => DistanceSqToTarget() <= range * range;

    private bool InStrikeRange()
    {
        float r = range * damageRadiusScale;
        return DistanceSqToTarget() <= r * r;
    }

    private float DistanceSqToTarget() =>
        ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude;

    private void OnDisable()
    {
        // Si moria mientras cargaba o embestia, ese ataque no cuenta como fallo del enemigo sino como cancelado.
        if (state == State.Windup || state == State.Lunge) Count(CancelledCount, name);
        AttackTokens.Release(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
