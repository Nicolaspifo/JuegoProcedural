using UnityEngine;

[RequireComponent(typeof(Health), typeof(PlayerRuntimeStats))]
public class HealthRegeneration : MonoBehaviour
{
    [Tooltip("Cada cuantos segundos se cura. La cantidad curada por vez es el valor de la mejora (Regeneration).")]
    [SerializeField, Min(0.1f)] private float interval = 3f;

    private Health health;
    private PlayerRuntimeStats stats;
    private float nextTick;
    private bool regenerating;

    private void Awake()
    {
        health = GetComponent<Health>();
        stats = GetComponent<PlayerRuntimeStats>();
    }

    private void Update()
    {
        float amount = stats.Get(StatType.Regeneration);
        if (amount <= 0f || health.IsDead)
        {
            regenerating = false;
            return;
        }

        // Al conseguir la mejora, el primer tick llega tras un intervalo completo (antes curaba al instante).
        if (!regenerating)
        {
            regenerating = true;
            nextTick = Time.time + interval;
            return;
        }

        if (Time.time < nextTick) return;

        health.Heal(amount);
        nextTick = Time.time + interval;
    }
}