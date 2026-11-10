using UnityEngine;

public class PlayerXP : MonoBehaviour
{
    [SerializeField] int baseXpToLevel = 5;
    [SerializeField] int xpIncreasePerLevel = 3;

    [Tooltip("Al subir de nivel cura esta fraccion de la vida maxima (0.1 = 10%). 0 = desactivado.")]
    [SerializeField, Range(0f, 0.5f)] float healPercentOnLevelUp = 0.1f;

    Health health;

    void Awake()
    {
        TryGetComponent(out health);

        // El bonus de XP por impactos multiples es parte del bucle de progresion: si el Player no lo tiene en la
        // escena (o quedo como "script missing"), se anade aqui con los valores por defecto para que nunca falte.
        if (!TryGetComponent<MultiHitXpBonus>(out _) && TryGetComponent<MacheteAttack>(out _))
            gameObject.AddComponent<MultiHitXpBonus>();
    }

    public int Level { get; private set; } = 1;
    public int CurrentXP { get; private set; } 
    public int XpToNextLevel => baseXpToLevel + (Level - 1) * xpIncreasePerLevel;

    public event System.Action<int, int, int> OnXPChanged;
    public event System.Action<int> OnLevelUp;

    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        CurrentXP += amount;
        while (CurrentXP >= XpToNextLevel)
        {
            CurrentXP -= XpToNextLevel;
            Level++;
            if (health != null && healPercentOnLevelUp > 0f) health.Heal(health.Max * healPercentOnLevelUp);
            OnLevelUp?.Invoke(Level);
        }
        OnXPChanged?.Invoke(CurrentXP, XpToNextLevel, Level);
    }

    [ContextMenu("Test +3 XP")]
    void TestAdd() => AddXP(3);
}
