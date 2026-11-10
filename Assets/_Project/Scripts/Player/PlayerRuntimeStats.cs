using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRuntimeStats : MonoBehaviour
{
    [SerializeField] private MacheteAttack machete;
    [SerializeField] private Health health;
    [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;
    [SerializeField, Min(0f)] private float basePickupRadius = 2f;
    [SerializeField] private UpgradeData testUpgrade;
    [Tooltip("Todas las mejoras del juego. Solo lo usa la prueba 'maxear todas las mejoras'.")]
    [SerializeField] private UpgradeData[] allUpgrades;
    [SerializeField, Min(0f)] private float basePoisonDuration = 3f;

    [Header("Topes")]
    [Tooltip("Velocidad maxima como multiplo de la base. 1.35 = como mucho +35% aunque haya muchas mejoras.")]
    [SerializeField, Min(1f)] private float maxMoveSpeedMultiplier = 1.35f;


    private readonly List<UpgradeData> upgrades = new();
    private readonly Dictionary<StatType, float> baseValues = new();
    private readonly Dictionary<StatType, float> current = new();

    public event Action StatsChanged;
    public IReadOnlyList<UpgradeData> Upgrades => upgrades;
    public float MoveSpeed => Get(StatType.MoveSpeed);
    public float PickupRadius => Get(StatType.PickupRadius);

    /// <summary>Cuanto vale la velocidad con mejoras respecto a la base (1 = sin mejoras).</summary>
    public float MoveSpeedMultiplier => Ratio(MoveSpeed, baseMoveSpeed);
    /// <summary>Cuanto vale el radio de recogida con mejoras respecto a la base (1 = sin mejoras).</summary>
    public float PickupRadiusMultiplier => Ratio(PickupRadius, basePickupRadius);

    private static float Ratio(float value, float baseValue) =>
        baseValue > 0f && value > 0f ? value / baseValue : 1f;

    private void Awake()
    {
        if (machete == null) machete = GetComponent<MacheteAttack>();
        if (health == null) health = GetComponent<Health>();

        baseValues[StatType.Damage] = machete.Damage;
        baseValues[StatType.AttackCooldown] = machete.Cooldown;
        baseValues[StatType.AttackRange] = machete.Range;
        baseValues[StatType.MaxHealth] = health.Max;
        baseValues[StatType.MoveSpeed] = baseMoveSpeed;
        baseValues[StatType.PickupRadius] = basePickupRadius;
        baseValues[StatType.PoisonDamage] = 0f;
        baseValues[StatType.PoisonDuration] = basePoisonDuration;
        baseValues[StatType.Regeneration] = 0f;

        Recalculate();
    }

    public float Get(StatType stat) => current.TryGetValue(stat, out float value) ? value : 0f;

    public int CountOf(UpgradeData upgrade)
    {
        int count = 0;
        foreach (UpgradeData owned in upgrades)
            if (owned == upgrade) count++;
        return count;
    }

    // Las cartas de relleno (solo curan) se pueden repetir sin limite; las normales respetan maxStacks.
    public bool CanApply(UpgradeData upgrade) =>
        upgrade != null && (upgrade.IsConsumable || CountOf(upgrade) < upgrade.MaxStacks);

    public bool Apply(UpgradeData upgrade)
    {
        if (!CanApply(upgrade)) return false;

        if (upgrade.HealPercent > 0f) health.Heal(health.Max * upgrade.HealPercent);

        // Una carta de relleno no cambia stats: no se guarda en la lista de mejoras.
        if (upgrade.IsConsumable) return true;

        upgrades.Add(upgrade);
        Recalculate();
        return true;
    }

    private void Recalculate()
    {
        foreach (KeyValuePair<StatType, float> pair in baseValues)
        {
            float add = 0f;
            float multiply = 1f;

            foreach (UpgradeData upgrade in upgrades)
            foreach (StatModifier modifier in upgrade.Modifiers)
            {
                if (modifier.stat != pair.Key) continue;
                if (modifier.op == ModifierOp.Add) add += modifier.value;
                else multiply *= modifier.value;
            }

            current[pair.Key] = (pair.Value + add) * multiply;

            if (pair.Key == StatType.MoveSpeed)
            current[pair.Key] = Mathf.Min(current[pair.Key], pair.Value * maxMoveSpeedMultiplier);

        }

        machete.Damage = Get(StatType.Damage);
        machete.Cooldown = Get(StatType.AttackCooldown);
        machete.Range = Get(StatType.AttackRange);
        health.SetMax(Get(StatType.MaxHealth));

        StatsChanged?.Invoke();
    }

    [ContextMenu("Prueba: aplicar mejora")]
    private void TestApply()
    {
        bool applied = Apply(testUpgrade);
        Debug.Log(applied
            ? $"{testUpgrade.DisplayName}: daño {Get(StatType.Damage)}, cooldown {Get(StatType.AttackCooldown)}, vida {health.Current:0}/{Get(StatType.MaxHealth):0}, velocidad x{MoveSpeedMultiplier:0.00}"
            : "No se pudo aplicar: falta la mejora o llegó al máximo");
    }

    [ContextMenu("Prueba: maxear todas las mejoras")]
    private void TestMaxAll()
    {
        if (allUpgrades == null || allUpgrades.Length == 0)
        {
            Debug.LogWarning("[Prueba] Asigna las mejoras en 'All Upgrades' del PlayerRuntimeStats.", this);
            return;
        }

        int applied = 0;
        foreach (UpgradeData upgrade in allUpgrades)
        {
            if (upgrade == null || upgrade.IsConsumable) continue;
            // Apply devuelve false al llegar al maximo de acumulaciones; el limite de 99 evita bucles infinitos.
            for (int i = 0; i < 99 && Apply(upgrade); i++) applied++;
        }

        health.Heal(health.Max);   // al maxear, la vida queda llena
        Debug.Log($"[Prueba] {applied} acumulaciones aplicadas | daño {Get(StatType.Damage):0.0} | cooldown {Get(StatType.AttackCooldown):0.00} | alcance {Get(StatType.AttackRange):0.0} | vida {health.Current:0}/{Get(StatType.MaxHealth):0} | velocidad x{MoveSpeedMultiplier:0.00} | recogida {Get(StatType.PickupRadius):0.0}", this);
    }
}