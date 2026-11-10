using UnityEngine;

[RequireComponent(typeof(Health))]
public class CoffeeDropper : MonoBehaviour
{
    [SerializeField] private CoffeeBean coffeeBeanPrefab;
    [SerializeField, Min(1)] private int minDrops = 1;
    [SerializeField, Min(1)] private int maxDrops = 3;
    [SerializeField, Min(0f)] private float scatterRadius = 0.5f;
    [Tooltip("Si esta activo, suelta granos cada vez que le aciertan, ademas de los de la muerte. Para el jefe: premia la agresividad.")]
    [SerializeField] private bool dropOnHit = false;
    [Tooltip("Granos que suelta por cada golpe recibido (solo si dropOnHit esta activo).")]
    [SerializeField, Min(1)] private int hitDropAmount = 1;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.Died += DropCoffee;
        if (dropOnHit) health.Damaged += DropOnHit;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= DropCoffee;
            health.Damaged -= DropOnHit;
        }
    }

    private void DropOnHit(float current, float max)
    {
        if (current > 0f) SpawnBeans(hitDropAmount);   // el golpe final no suma: lo cubren los granos de la muerte
    }

    private void DropCoffee() => SpawnBeans(Random.Range(minDrops, maxDrops + 1));

    private void SpawnBeans(int amount)
    {
        if (coffeeBeanPrefab == null) return;

        for (int i = 0; i < amount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Instantiate(coffeeBeanPrefab, transform.position + (Vector3)offset, Quaternion.identity);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Prueba: soltar café")]
    private void TestDrop() => DropCoffee();
#endif
}
