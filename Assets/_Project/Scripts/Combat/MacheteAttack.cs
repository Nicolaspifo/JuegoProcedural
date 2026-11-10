using System;
using System.Collections.Generic;
using UnityEngine;

public class MacheteAttack : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 5f;
    [SerializeField, Min(0.1f)] private float cooldown = 1.25f;
    [SerializeField, Min(0.1f)] private float range = 2f;
    [SerializeField, Range(10f, 360f)] private float angle = 120f;
    [SerializeField] private LayerMask targetLayers;

    [Header("Direccion")]
    [Tooltip("Si esta activo, el machete apunta hacia donde el jugador pulsa moverse (PlayerMovement), o hacia donde se mueve el Rigidbody2D si no hay PlayerMovement.")]
    [SerializeField] private bool followMovement = true;
    [SerializeField] private Rigidbody2D body;

    [Header("Depuracion")]
    [Tooltip("Imprime en consola cada golpe: direccion, candidatos, descartados e impactados.")]
    [SerializeField] private bool debugLog = false;

    public float Damage { get => damage; set => damage = Mathf.Max(0f, value); }
    public float Cooldown
    {
        get => cooldown;
        set
        {
            float newCooldown = Mathf.Max(0.1f, value);

            // PlayerRuntimeStats escribe este valor cada vez que aplicas una mejora,
            // aunque no cambie. Si es el mismo, no tocamos el ciclo en curso.
            if (Mathf.Approximately(newCooldown, cooldown)) return;

            if (Time.time < nextAttackTime && cooldown > 0f)
            {
                // 0 = acaba de golpear, 1 = listo para el siguiente golpe.
                float progress = Mathf.Clamp01(1f - (nextAttackTime - Time.time) / cooldown);

                cooldown = newCooldown;
                nextAttackTime = Time.time + (1f - progress) * newCooldown;
            }
            else
            {
                cooldown = newCooldown;
            }
        }
    }
    public float Range { get => range; set => range = Mathf.Max(0.1f, value); }
    /// <summary>Apertura total del arco de golpe, en grados.</summary>
    public float Angle => angle;
    public float CooldownProgress
    {
        get
        {
            if (Time.time >= nextAttackTime)
                return 1f;

            float remaining = nextAttackTime - Time.time;

            return Mathf.Clamp01(1f - remaining / cooldown);
        }
    }

    public event Action<Vector2> Attacked;
    public event Action<GameObject> Hit;
    /// <summary>Se dispara al final de cada golpe con el numero de enemigos alcanzados (puede ser 0).</summary>
    public event Action<int> SwingResolved;

    private readonly HashSet<IDamageable> hitThisSwing = new();
    private readonly Collider2D[] hitBuffer = new Collider2D[32]; // reutilizado: evita reservar memoria en cada golpe
    private PlayerMovement movement;
    private Vector2 aimDirection;
    private float nextAttackTime;

    public void SetAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f) aimDirection = direction.normalized;
    }

    private void Awake()
    {
        if (body == null) TryGetComponent(out body);
        TryGetComponent(out movement);
    }

    private void Update()
    {
        if (followMovement)
        {
            // Preferimos la direccion del input: la velocidad real baja a ~0 si el jugador empuja contra enemigos.
            // Si esta quieto, SetAimDirection ignora el vector nulo y se conserva la ultima direccion.
            if (movement != null) SetAimDirection(movement.LastMoveDirection);
            else if (body != null) SetAimDirection(body.linearVelocity);
        }

        if (Time.time < nextAttackTime) return;

        Swing();
        nextAttackTime = Time.time + cooldown;
    }

    private void Swing()
    {
        Vector2 forward = CurrentAim();
        Vector2 origin = transform.position;
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = targetLayers, useTriggers = true };
        int candidates = Physics2D.OverlapCircle(origin, range, filter, hitBuffer);

        hitThisSwing.Clear();
        int hitCount = 0;

        if (debugLog)
            Debug.Log($"[Machete] Golpe hacia {forward} | capas={targetLayers.value} | colliders en rango={candidates}", this);

        for (int i = 0; i < candidates; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit.transform.IsChildOf(transform)) continue; // nunca golpearse a si mismo

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null)
            {
                if (debugLog) Debug.Log($"[Machete]   x {hit.name}: sin IDamageable (¿falta Health?)", hit);
                continue;
            }
            if (target.IsDead || !hitThisSwing.Add(target)) continue;

            Vector2 toTarget = (Vector2)hit.transform.position - origin;
            float angleToTarget = Vector2.Angle(forward, toTarget);

            if (angleToTarget > angle * 0.5f)
            {
                if (debugLog) Debug.Log($"[Machete]   x {hit.name}: fuera del arco ({angleToTarget:0}° > {angle * 0.5f:0}°)", hit);
                continue;
            }

            target.TakeDamage(damage, gameObject);
            if(target is Component component) Hit?.Invoke(component.gameObject);
            hitCount++;
            if (debugLog) Debug.Log($"[Machete]   ✓ {hit.name}: {damage} de daño a {toTarget.magnitude:0.0} u, angulo {angleToTarget:0}°, muerto={target.IsDead}", hit);
        }

        if (debugLog) Debug.Log($"[Machete] Resultado: {hitCount} impactos", this);

        Attacked?.Invoke(forward);
        SwingResolved?.Invoke(hitCount);
    }

#if UNITY_EDITOR
    [ContextMenu("Prueba: golpear ahora")]
    private void TestSwing()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Machete] Solo funciona en Play Mode."); return; }
        Swing();
    }
#endif

    private Vector2 CurrentAim()
    {
        // Sin direccion de apuntado, usa el eje X local (derecha) del objeto.
        return aimDirection.sqrMagnitude > 0.01f ? aimDirection : (Vector2)transform.right;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 forward = Application.isPlaying ? CurrentAim() : (Vector2)transform.right;

        Vector3 left = Quaternion.Euler(0f, 0f, angle * 0.5f) * (Vector3)forward;
        Vector3 right = Quaternion.Euler(0f, 0f, -angle * 0.5f) * (Vector3)forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
        Gizmos.DrawLine(transform.position, transform.position + left * range);
        Gizmos.DrawLine(transform.position, transform.position + right * range);
    }
}
