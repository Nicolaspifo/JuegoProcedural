using UnityEngine;

/// <summary>
/// Alimenta el Animator de un enemigo (zancudo, hipopotamo, Llorona...).
///
/// Parametros que usa si existen en el Animator Controller (los que falten se ignoran):
///   MoveX, MoveY (float): diagonal hacia la que mira (-1 o 1 en cada eje).
///   Speed (float): 0 = quieto, 1 = caminando.
///   Attack (trigger): al empezar la carga del ataque (EnemyContactDamage.WindupStarted).
///   Cry (trigger): al empezar el grito de La Llorona (CryAttack.CryStarted).
///   Die (trigger): cuando muere (Health.Died).
///
/// El enemigo siempre mira hacia el jugador.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("Referencias (se buscan solas si se dejan vacias)")]
    [SerializeField] private EnemyContactDamage contact;
    [SerializeField] private Health health;
    [SerializeField] private CryAttack cry;          // solo La Llorona
    [SerializeField] private BossLlorona boss;       // solo La Llorona

    [Header("Direccion")]
    [Tooltip("Cuanto debe desviarse el jugador de la vertical u horizontal para cambiar de lado. Evita parpadeos.")]
    [SerializeField, Range(0f, 0.9f)] private float axisDeadZone = 0.25f;

    [Header("Movimiento")]
    [Tooltip("Velocidad minima (unidades por segundo) para considerar que camina.")]
    [SerializeField, Min(0f)] private float walkThreshold = 0.3f;
    [Tooltip("Segundos que sigue en 'caminando' tras dejar de moverse. Evita parpadeos entre Idle y Walk.")]
    [SerializeField, Min(0f)] private float walkHold = 0.1f;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int CryHash = Animator.StringToHash("Cry");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private Animator animator;
    private Transform target;
    private Vector2 facing = new Vector2(-1f, -1f);   // abajo-izquierda por defecto
    private Vector3 lastPosition;
    private float lastMoveTime = float.NegativeInfinity;
    private float nextRetarget;
    private bool isDead;

    private bool hasSpeed;
    private bool hasAttack;
    private bool hasCry;
    private bool hasDie;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (contact == null) contact = GetComponent<EnemyContactDamage>();
        if (health == null) health = GetComponent<Health>();
        if (cry == null) cry = GetComponent<CryAttack>();
        if (boss == null) boss = GetComponent<BossLlorona>();

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[EnemyAnimator] El Animator no tiene Controller asignado.", this);
            enabled = false;
            return;
        }

        hasSpeed = HasParameter(SpeedHash);
        hasAttack = HasParameter(AttackHash);
        hasCry = HasParameter(CryHash);
        hasDie = HasParameter(DieHash);
    }

    private void OnEnable()
    {
        lastPosition = transform.position;

        if (contact != null) contact.WindupStarted += OnWindupStarted;
        if (cry != null) cry.CryStarted += OnCryStarted;
        if (health != null) health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (contact != null) contact.WindupStarted -= OnWindupStarted;
        if (cry != null) cry.CryStarted -= OnCryStarted;
        if (health != null) health.Died -= OnDied;
    }

    private void Start()
    {
        FindTarget();
        UpdateFacing();
        ApplyFacing();
    }

    private void Update()
    {
        // En pausa (timeScale 0) no hay nada que actualizar.
        if (isDead || Time.deltaTime <= 0f) return;

        // Mientras carga un golpe o grita no cambia de lado: lo que hace sale hacia donde apunto.
        bool busy = (contact != null && contact.IsBusy) || (boss != null && boss.MovementLocked);
        if (!busy && UpdateFacing()) ApplyFacing();

        if (hasSpeed)
        {
            float speed = ((Vector2)(transform.position - lastPosition)).magnitude / Time.deltaTime;
            if (speed > walkThreshold) lastMoveTime = Time.time;

            bool walking = Time.time - lastMoveTime < walkHold;
            animator.SetFloat(SpeedHash, walking ? 1f : 0f);
        }

        lastPosition = transform.position;
    }

    private void OnWindupStarted(float duration)
    {
        if (isDead || !hasAttack) return;

        // Se orienta hacia el jugador justo antes de empezar la animacion.
        if (UpdateFacing()) ApplyFacing();
        animator.SetTrigger(AttackHash);
    }

    private void OnCryStarted()
    {
        if (isDead || !hasCry) return;

        if (UpdateFacing()) ApplyFacing();
        animator.SetTrigger(CryHash);
    }

    private void OnDied()
    {
        if (isDead) return;
        isDead = true;

        if (hasSpeed) animator.SetFloat(SpeedHash, 0f);
        if (hasAttack) animator.ResetTrigger(AttackHash);
        if (hasCry) animator.ResetTrigger(CryHash);
        if (hasDie) animator.SetTrigger(DieHash);
    }

    /// <summary>Calcula hacia que diagonal mira. Devuelve true si cambio.</summary>
    private bool UpdateFacing()
    {
        if (target == null)
        {
            FindTarget();
            return false;
        }

        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return false;
        toTarget.Normalize();

        Vector2 previous = facing;
        if (Mathf.Abs(toTarget.x) >= axisDeadZone) facing.x = toTarget.x > 0f ? 1f : -1f;
        if (Mathf.Abs(toTarget.y) >= axisDeadZone) facing.y = toTarget.y > 0f ? 1f : -1f;

        return facing != previous;
    }

    private void ApplyFacing()
    {
        animator.SetFloat(MoveXHash, facing.x);
        animator.SetFloat(MoveYHash, facing.y);
    }

    private void FindTarget()
    {
        if (Time.time < nextRetarget) return;
        nextRetarget = Time.time + 0.5f;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }

    private bool HasParameter(int hash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash) return true;

        return false;
    }
}