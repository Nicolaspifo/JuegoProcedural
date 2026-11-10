using System;
using UnityEngine;

 
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Tooltip("Segundos de invulnerabilidad tras recibir daño. 0 = desactivado (enemigos). Jugador: ~0.5.")]
    [SerializeField, Min(0f)] private float invulnerabilitySeconds = 0f;

    private float invulnerableUntil;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead => Current <= 0f;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    public event Action<float, float> Changed;
    public event Action Died;

    public event Action<float, float> Damaged;
    public static event Action<Health, float, DamageType> AnyDamaged;


    private void Awake() => Current = maxHealth;

    public void TakeDamage(float amount, GameObject source)
        => TakeDamage(amount, source, DamageType.Normal);

    public void TakeDamage(float amount, GameObject source, DamageType type)
    {
        if (IsDead || amount <= 0f || IsInvulnerable) return;

        invulnerableUntil = Time.time + invulnerabilitySeconds;

        float previous = Current;
        Current = Mathf.Max(previous - amount, 0f);
        float dealt = previous - Current;   // daño real aplicado

        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke(Current, maxHealth);
        AnyDamaged?.Invoke(this, dealt, type);   // ahora incluye el tipo

        if (IsDead)
        {
            Died?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        Current = Mathf.Min(Current + amount, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

    public void SetMax(float newMax)
    {
        newMax = Mathf.Max(1f, newMax);
        float gained = newMax - maxHealth;
        maxHealth = newMax;

        if (!IsDead) Current = Mathf.Clamp(Current + Mathf.Max(gained, 0f), 0f, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

#if UNITY_EDITOR
    [ContextMenu("Prueba: recibir 10 de daño")]
    private void TestDamage() => TakeDamage(10f, gameObject);
#endif
}