using UnityEngine;

public class PoisonStatus : MonoBehaviour
{
    private const float TickInterval = 0.5f;

    private IDamageable target;
    private Health health;
    private GameObject source;
    private float damagePerSecond;
    private float endTime;
    private float nextTick;
    private bool debugLog;
    private int tickCount;

    public static void Apply(GameObject targetObject, float dps, float duration, GameObject source, bool debugLog = false)
    {
        if (targetObject == null || dps <= 0f || duration <= 0f) return;

        if (!targetObject.TryGetComponent(out PoisonStatus poison))
            poison = targetObject.AddComponent<PoisonStatus>();

        poison.Refresh(dps, duration, source, debugLog);
    }

    private void Awake()
    {
        target = GetComponent<IDamageable>();
        TryGetComponent(out health);
        nextTick = Time.time + TickInterval;
    }

    private void Refresh(float dps, float duration, GameObject newSource, bool logEnabled)
    {
        bool isNew = damagePerSecond <= 0f;
        damagePerSecond = Mathf.Max(damagePerSecond, dps);
        endTime = Time.time + duration;
        source = newSource;
        debugLog = logEnabled;

        if (debugLog)
            Debug.Log($"[Veneno] {(isNew ? "APLICADO" : "REFRESCADO")} en {name}: {damagePerSecond}/s durante {duration}s (t={Time.time:0.00}, ticks cada {TickInterval}s)", this);
    }

    private void Update()
    {
        if (target == null || target.IsDead || Time.time >= endTime)
        {
            if (debugLog)
                Debug.Log($"[Veneno] TERMINADO en {name}: {tickCount} ticks (t={Time.time:0.00})", this);
            Destroy(this);
            return;
        }

        if (Time.time < nextTick) return;

        float damage = damagePerSecond * TickInterval;
        if (health != null)
            health.TakeDamage(damage, source, DamageType.Poison);
        else
            target.TakeDamage(damage, source);
        nextTick = Time.time + TickInterval;
        tickCount++;

        if (debugLog)
        {
            string left = health != null ? $", vida restante {health.Current:0.0}/{health.Max:0.0}" : "";
            Debug.Log($"[Veneno] tick #{tickCount} en {name}: -{damage:0.0}{left} (t={Time.time:0.00})", this);
        }
    }
}
