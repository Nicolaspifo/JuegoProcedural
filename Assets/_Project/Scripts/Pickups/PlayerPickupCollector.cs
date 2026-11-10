using UnityEngine;

public class PlayerPickupCollector : MonoBehaviour
{
    public static PlayerPickupCollector Instance { get; private set; }

    [Header("Radio de recolección")]
    [SerializeField, Min(0f)] private float pickupRadius = 2.5f;
    [SerializeField] private LayerMask pickupLayers = ~0;

    [Header("Experiencia")]
    [SerializeField, Min(0)] private int xpPerCoffee = 1;

    [Header("Depuracion")]
    [SerializeField] private bool debugLog = false;

    private PlayerXP playerXP;
    private PlayerRuntimeStats runtimeStats; // opcional: radio con mejoras aplicadas

    private ContactFilter2D filter;
    private readonly Collider2D[] hits = new Collider2D[32];

    // El radio base es el de este componente; las mejoras (PlayerRuntimeStats) solo lo multiplican.
    public float PickupRadius =>
        runtimeStats != null ? pickupRadius * runtimeStats.PickupRadiusMultiplier : pickupRadius;
    public int CoffeeCount { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Solo se elimina este componente duplicado: destruir el GameObject se llevaria al jugador entero.
            Destroy(this);
            return;
        }
        Instance = this;

        playerXP = GetComponent<PlayerXP>();
        TryGetComponent(out runtimeStats);

        filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = pickupLayers
        };
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        int count = Physics2D.OverlapCircle(
            transform.position, PickupRadius, filter, hits);

        for (int i = 0; i < count; i++)
        {
            CoffeeBean bean = hits[i].GetComponent<CoffeeBean>();
            if (bean != null)
                bean.AttractTo(transform, Time.deltaTime);
        }
    }

    public void AddCoffee(int amount)
    {
        if (amount <= 0) return;

        CoffeeCount += amount;
        GameEvents.RaiseCoffeeChanged(CoffeeCount);

        if (playerXP != null)
            playerXP.AddXP(amount * xpPerCoffee);

        if (debugLog)
            Debug.Log($"[Café] Total: {CoffeeCount}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}