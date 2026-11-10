using UnityEngine;

[RequireComponent(typeof(MacheteAttack), typeof(PlayerRuntimeStats))]
public class PoisonOnHit : MonoBehaviour
{
    private MacheteAttack machete;
    private PlayerRuntimeStats stats;

    [Header("Depuracion")]
    [Tooltip("Imprime en consola cuando se aplica, refresca, hace tick y termina el veneno.")]
    [SerializeField] private bool debugLog = false;

    private void Awake()
    {
        machete = GetComponent<MacheteAttack>();
        stats = GetComponent<PlayerRuntimeStats>();
    }

    private void OnEnable() => machete.Hit += HandleHit;
    private void OnDisable() => machete.Hit -= HandleHit;

    private void HandleHit(GameObject target)
    {
        float dps = stats.Get(StatType.PoisonDamage);
        if (dps <= 0f) return;

        // El golpe pudo matar al objetivo: no tiene sentido envenenar algo que ya murio.
        if (target == null || (target.TryGetComponent(out IDamageable damageable) && damageable.IsDead)) return;

        PoisonStatus.Apply(target, dps, stats.Get(StatType.PoisonDuration), gameObject, debugLog);
    }
}