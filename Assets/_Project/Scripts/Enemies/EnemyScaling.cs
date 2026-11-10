using UnityEngine;

/// <summary>
/// Va en el prefab del enemigo. Al nacer aplica los multiplicadores de DifficultyScale
/// a su vida, su daño y su velocidad, sin tocar los valores del prefab.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyScaling : MonoBehaviour
{
    [Tooltip("Tope de velocidad de este tipo. Zancudo 1.35; hipopotamo 1.2 (su ataque se esquiva por el windup).")]
    [SerializeField, Min(1f)] private float maxSpeedMultiplier = 1.35f;
    [SerializeField] private bool debugLog = false;

    private float extraHp = 1f;
    private float extraDamage = 1f;

    /// <summary>Refuerzos del jefe: multiplica vida y daño ademas de DifficultyScale. Llamar justo tras Instantiate, antes de Start.</summary>
    public void SetExtra(float hp, float damage)
    {
        extraHp = Mathf.Max(0.1f, hp);
        extraDamage = Mathf.Max(0.1f, damage);
    }

    private void Start()
    {
        float speed = Mathf.Min(DifficultyScale.Speed, maxSpeedMultiplier);

        // SetMax sube el maximo y rellena la vida ganada: el enemigo nace con la vida llena.
        Health health = GetComponent<Health>();
        health.SetMax(health.Max * DifficultyScale.Hp * extraHp);

        if (TryGetComponent(out EnemyContactDamage contact)) contact.DamageMultiplier = DifficultyScale.Damage * extraDamage;
        if (TryGetComponent(out EnemyChase chase)) chase.SpeedMultiplier = speed;

        if (debugLog)
            Debug.Log($"[Escalado] {name}: vida {health.Max:0.#} | daño x{DifficultyScale.Damage:0.##} | velocidad x{speed:0.##}", this);
    }

#if UNITY_EDITOR
    [ContextMenu("Prueba: dificultad alta (vida x3, daño x2, velocidad x1.5)")]
    private void TestHard()
    {
        DifficultyScale.Hp = 3f;
        DifficultyScale.Damage = 2f;
        DifficultyScale.Speed = 1.5f;
        Debug.Log("[Escalado] Dificultad alta: afecta a los enemigos que nazcan a partir de ahora.");
    }
#endif
}
