using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Upgrade_", menuName = "A Punta de Machete/Mejora")]
public class UpgradeData : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(1)] private int maxStacks = 3;
    [SerializeField] private StatModifier[] modifiers;

    [Tooltip("Cura instantanea al elegirla, como fraccion de la vida maxima (0.35 = 35%). Si ademas no tiene modificadores es una carta de relleno: se puede repetir sin limite.")]
    [SerializeField, Range(0f, 1f)] private float healPercent = 0f;

    public float HealPercent => healPercent;
    /// <summary>Carta de relleno: solo cura, no cambia stats ni cuenta para maxStacks.</summary>
    public bool IsConsumable => healPercent > 0f && (modifiers == null || modifiers.Length == 0);

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int MaxStacks => maxStacks;
    public IReadOnlyList<StatModifier> Modifiers => modifiers;
}