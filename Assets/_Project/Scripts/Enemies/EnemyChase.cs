using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChase : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 4f;
    public float SpeedMultiplier { get; set; } = 1f;
    [SerializeField, Min(0f)] private float stopDistance = 1.3f;

    [Header("Rodeo")]
    [Tooltip("Cada enemigo reclama una casilla propia en un anillo alrededor del jugador y camina hacia ella. Si el anillo se llena, toma el de afuera.")]
    [SerializeField] private bool flank = true;
    [Tooltip("Maximo de enemigos en el anillo interior (el de ataque). Menos = deja huecos para que el jugador no quede encerrado.")]
    [SerializeField, Range(3, 16)] private int maxInnerSlots = 5;
    [Tooltip("Radio del anillo interior alrededor del jugador, igual para todos los enemigos para que sus anillos no se crucen.")]
    [SerializeField, Min(0.5f)] private float ringBaseRadius = 1.3f;
    [Tooltip("Hueco entre casillas vecinas, como multiplo del tamaño del enemigo. 1.0 = pegados; 1.4 = se ve aire entre ellos.")]
    [SerializeField, Range(1f, 2.5f)] private float slotSpacingFactor = 1.4f;

    [Header("Llegada")]
    [Tooltip("Distancia a la que empieza a frenar al acercarse a su punto.")]
    [SerializeField, Min(0.1f)] private float arriveSlowRadius = 0.8f;
    [Tooltip("Si esta a menos de esto de su punto, se considera llegado (evita temblores).")]
    [SerializeField, Min(0f)] private float arriveTolerance = 0.2f;

    [Header("Separacion entre enemigos")]
    [Tooltip("Minimo de separacion. Se amplia sola segun el tamaño del collider para que nunca quede mas chico que el sprite.")]
    [SerializeField, Min(0f)] private float separationRadius = 1.3f;
    [SerializeField, Min(0f)] private float separationWeight = 2f;
    [Tooltip("Capas con las que se separa. Si esta en Nothing usa la capa de este mismo enemigo.")]
    [SerializeField] private LayerMask separationLayers;

    [Header("Depuracion")]
    [Tooltip("Imprime una vez por segundo el estado fisico y de separacion de este enemigo.")]
    [SerializeField] private bool debugLog = false;

    private const float RetargetInterval = 0.5f;
    // Radio de separacion = diametro del collider * este factor, para dejar un hueco visible entre enemigos.
    private const float SeparationPerColliderSize = 1.3f;
    // Tope de correccion de solape por paso de fisica, para que no den saltos bruscos.
    private const float MaxCorrectionPerStep = 0.12f;
    // Numero de anillos de casillas alrededor del jugador (interior = ataque, el resto espera).
    private const int MaxTiers = 8;

    private Rigidbody2D body;
    private Transform target;
    private EnemyContactDamage contact;
    private ContactFilter2D separationFilter;
    private readonly Collider2D[] neighbours = new Collider2D[16];
    private float nextRetarget;
    private float slotAngle;
    private int slotTier;
    private bool slotClaimed;
    private float nextReclaim;
    private float effectiveSeparation;
    private float minGap;
    private float colliderDiameter = 1f;
    private bool separationReady;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        contact = GetComponent<EnemyContactDamage>();

        LayerMask mask = separationLayers.value != 0 ? separationLayers : (LayerMask)(1 << gameObject.layer);
        separationFilter = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = false };

        effectiveSeparation = separationRadius;

        // Cinematico: el enemigo se mueve SOLO con MovePosition y no recibe empujes. Siendo dinamico, un
        // choque del jugador le transferia velocidad (damping 0) y salia volando; ademas, al estar quieto
        // (windup/recover) seguia deslizandose con la ultima velocidad. Un cuerpo cinematico sigue
        // bloqueando al jugador, pero nunca se mueve por la fisica ni choca con otros cinematicos.
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = false;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start() => FindTarget();

    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        target = player.transform;
    }

    private float AttackDistance() => contact != null ? contact.Range * 0.9f : stopDistance;

    /// <summary>
    /// Distancia entre centros de casillas vecinas: el collider mas un hueco visible. Se redondea a 0.1
    /// para que todos los tipos de enemigo con colliders parecidos compartan exactamente los mismos anillos.
    /// </summary>
    private float SlotSpacing() => Mathf.Round(Mathf.Max(colliderDiameter, 0.5f) * slotSpacingFactor * 10f) / 10f;

    /// <summary>
    /// Radio del anillo interior. Es el mismo para todos los tipos (si no, el anillo del hipopotamo y el del
    /// zancudo se cruzan y se encimen), salvo que el alcance de ataque del enemigo no llegue hasta ahi.
    /// </summary>
    private float SlotBaseRadius() => Mathf.Round(Mathf.Min(ringBaseRadius, AttackDistance()) * 10f) / 10f;

    /// <summary>
    /// Reclama una casilla en el anillo mas interior que tenga hueco, prefiriendo el lado por el que
    /// llega el enemigo. Con <paramref name="onlyBetter"/> solo busca anillos mas cercanos que el actual.
    /// Cada enemigo tiene su propia casilla, asi no se amontonan en el mismo punto.
    /// </summary>
    private void ClaimSlot(bool onlyBetter)
    {
        if (target == null || !separationReady) return;

        Vector2 fromPlayer = body.position - (Vector2)target.position;
        float preferred = fromPlayer.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(fromPlayer.y, fromPlayer.x)
            : Random.Range(0f, Mathf.PI * 2f);

        float baseRadius = SlotBaseRadius();
        float spacing = SlotSpacing();
        int limit = onlyBetter ? slotTier : MaxTiers;

        for (int tier = 0; tier < limit; tier++)
        {
            float radius = baseRadius + tier * spacing;
            int count = EnemySlots.SlotCount(radius, spacing, tier, maxInnerSlots);
            long key = EnemySlots.RingKey(baseRadius, spacing, tier, maxInnerSlots);

            if (!EnemySlots.TryClaim(this, key, count, preferred, out float angle)) continue;

            slotAngle = angle;
            slotTier = tier;
            slotClaimed = true;
            return;
        }

        // Todos los anillos llenos: espera en el exterior, sin casilla, hasta que se libere una.
        if (!slotClaimed)
        {
            slotAngle = preferred;
            slotTier = MaxTiers;
            slotClaimed = true;
        }
    }

    private void OnDisable()
    {
        EnemySlots.Release(this);
        slotClaimed = false;
    }

    /// <summary>
    /// La separacion nunca puede ser menor que el propio collider, o los enemigos que se tocan no se empujan.
    /// Collider2D.bounds puede valer 0 hasta que la fisica sincroniza: si es asi se mide el collider por su
    /// forma, y si aun no hay tamaño valido se reintenta en el siguiente paso (antes quedaba en 0 para siempre).
    /// </summary>
    private void ComputeSeparation()
    {
        if (!TryGetComponent(out Collider2D col))
        {
            separationReady = true;
            return;
        }

        float diameter = ColliderDiameter(col);
        if (diameter <= 0.0001f) return; // todavia sin tamaño: se reintenta

        effectiveSeparation = Mathf.Max(separationRadius, diameter * SeparationPerColliderSize);
        minGap = diameter * 0.95f; // por debajo de esto los colliders se solapan de verdad
        colliderDiameter = diameter;
        separationReady = true;
    }

    private static float ColliderDiameter(Collider2D col)
    {
        Vector3 size = col.bounds.size;
        float fromBounds = Mathf.Max(size.x, size.y);
        if (fromBounds > 0.0001f) return fromBounds;

        Vector3 scale = col.transform.lossyScale;
        switch (col)
        {
            case BoxCollider2D box:
                return Mathf.Max(box.size.x * Mathf.Abs(scale.x), box.size.y * Mathf.Abs(scale.y));
            case CircleCollider2D circle:
                return circle.radius * 2f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            case CapsuleCollider2D capsule:
                return Mathf.Max(capsule.size.x * Mathf.Abs(scale.x), capsule.size.y * Mathf.Abs(scale.y));
            default:
                return 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!separationReady) ComputeSeparation();
        if (debugLog) DebugTick();

        if (target == null)
        {
            // El jugador pudo aparecer despues (o reinstanciarse): reintenta sin hacerlo cada frame.
            if (Time.time >= nextRetarget)
            {
                nextRetarget = Time.time + RetargetInterval;
                FindTarget();
            }
            if (target == null) return;
        }

        // Mientras carga o se recupera de un golpe, el enemigo se queda quieto (es la oportunidad del jugador).
        if (contact != null && contact.IsBusy) return;

        if (flank)
        {
            if (!slotClaimed) ClaimSlot(false);
            else if (slotTier > 0 && Time.time >= nextReclaim)
            {
                // Si se libero un hueco en un anillo mas cercano, avanza hacia el.
                nextReclaim = Time.time + 0.5f;
                ClaimSlot(true);
            }
        }

        Vector2 goal = flank && slotClaimed ? SlotPoint() : (Vector2)target.position;
        Vector2 toGoal = goal - body.position;
        float distance = toGoal.magnitude;

        // Arrive: frena al acercarse a su punto y se detiene al llegar.
        Vector2 desired = Vector2.zero;
        if (distance > arriveTolerance)
            desired = toGoal / distance * Mathf.Clamp01(distance / arriveSlowRadius);

        desired += Separation(out Vector2 correction) * separationWeight;

        Vector2 step = Vector2.zero;
        if (desired.sqrMagnitude >= 0.0025f)
            step = Vector2.ClampMagnitude(desired, 1f) * (speed * SpeedMultiplier * Time.fixedDeltaTime);

        // Ademas del empuje de direccion, los solapes reales se corrigen directamente en la posicion.
        step += correction;

        if (step.sqrMagnitude < 0.0000001f) return;

        body.MovePosition(body.position + step);
    }

    /// <summary>Punto de su casilla: anillo segun su turno, en el angulo asignado, siguiendo al jugador.</summary>
    private Vector2 SlotPoint()
    {
        int tier = Mathf.Min(slotTier, MaxTiers - 1);
        float radius = SlotBaseRadius() + tier * SlotSpacing();
        Vector2 dir = new Vector2(Mathf.Cos(slotAngle), Mathf.Sin(slotAngle));
        return (Vector2)target.position + dir * radius;
    }

    /// <summary>
    /// Devuelve el empuje de direccion que aleja de los vecinos cercanos (mas fuerte cuanto mas cerca)
    /// y, en <paramref name="correction"/>, el desplazamiento directo que deshace los solapes reales.
    /// </summary>
    private Vector2 Separation(out Vector2 correction)
    {
        correction = Vector2.zero;
        if (effectiveSeparation <= 0f) return Vector2.zero;

        int count = Physics2D.OverlapCircle(body.position, effectiveSeparation, separationFilter, neighbours);
        Vector2 push = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            Collider2D other = neighbours[i];
            if (other.attachedRigidbody == body) continue;

            Vector2 away = body.position - (Vector2)other.transform.position;
            float distance = away.magnitude;

            if (distance < 0.0001f)
            {
                // Exactamente encima: sin direccion posible, elige una al azar. Antes se calculaba
                // y no se sumaba, asi que los enemigos en el mismo punto nunca se separaban.
                Vector2 random = Random.insideUnitCircle.normalized;
                push += random;
                correction += random * (minGap * 0.5f);
                continue;
            }

            Vector2 dir = away / distance;
            push += dir * (1f - distance / effectiveSeparation);

            // Solape real: cada uno de los dos cede la mitad.
            if (distance < minGap)
                correction += dir * ((minGap - distance) * 0.5f);
        }

        correction = Vector2.ClampMagnitude(correction, MaxCorrectionPerStep);
        return push;
    }

    private float nextDebugTime;

    private void DebugTick()
    {
        if (Time.time < nextDebugTime) return;

        nextDebugTime = Time.time + 1f;
        LogState();
    }

    [ContextMenu("Prueba: estado")]
    private void LogState()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[EnemyChase] Solo funciona en Play Mode."); return; }

        int neighbourCount = Physics2D.OverlapCircle(body.position, Mathf.Max(effectiveSeparation, 0.01f), separationFilter, neighbours) - 1;
        Debug.Log(
            $"[EnemyChase] {name} | cuerpo={body.bodyType} vel={body.linearVelocity.magnitude:0.00} | " +
            $"ocupado={(contact != null && contact.IsBusy)} | vecinos={Mathf.Max(neighbourCount, 0)} | " +
            $"separacion={effectiveSeparation:0.00} minGap={minGap:0.00} lista={separationReady} | " +
            $"casilla={(slotClaimed ? $"anillo {slotTier}, angulo {slotAngle * Mathf.Rad2Deg:0}°" : "sin reclamar")} | " +
            $"objetivo={(target != null ? target.name : "NINGUNO")}", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? effectiveSeparation : separationRadius);
    }
}
