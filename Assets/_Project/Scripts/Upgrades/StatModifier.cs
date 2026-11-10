using System;

public enum StatType { Damage, AttackCooldown, AttackRange, MoveSpeed, MaxHealth, PickupRadius,PoisonDamage, PoisonDuration, Regeneration  }
public enum ModifierOp { Add, Multiply }

[Serializable]
public struct StatModifier
{
    public StatType stat;
    public ModifierOp op;
    public float value;
}