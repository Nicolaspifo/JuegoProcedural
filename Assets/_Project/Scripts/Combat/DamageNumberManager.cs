using UnityEngine;

/// <summary>
/// Escucha el daño de cualquier Health y muestra un número flotante en el punto del golpe.
/// Debe haber UNO solo en la escena.
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private DamageNumber damageNumberPrefab;

    [Header("Posición")]
    [Tooltip("Altura respecto al centro del personaje.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.7f, 0f);
    [Tooltip("Dispersión horizontal para que no se encimen los números.")]
    [SerializeField, Min(0f)] private float horizontalSpread = 0.35f;

    [Header("Colores")]
    [SerializeField] private Color enemyDamageColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color playerDamageColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color poisonDamageColor = new Color(0.35f, 1f, 0.35f, 1f);

    private void OnEnable() => Health.AnyDamaged += Show;
    private void OnDisable() => Health.AnyDamaged -= Show;

    private void Show(Health target, float amount, DamageType type)
    {
        if (damageNumberPrefab == null || target == null) return;

        Vector3 position = target.transform.position + worldOffset;
        position.x += Random.Range(-horizontalSpread, horizontalSpread);

        Color color;
        if (type == DamageType.Poison)
            color = poisonDamageColor;                                   // verde
        else
            color = target.CompareTag("Player") ? playerDamageColor : enemyDamageColor;

        float drift = Random.Range(-horizontalSpread, horizontalSpread);

        DamageNumber number = Instantiate(damageNumberPrefab, position, Quaternion.identity);
        number.Init(amount, color, drift);
    }
}