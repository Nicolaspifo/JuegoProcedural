using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoffeeBean : MonoBehaviour
{
    [Header("Valor")]
    [SerializeField, Min(1)] private int value = 1;

    [Header("Atracción")]
    [SerializeField, Min(0.05f)] private float collectDistance = 0.25f;
    [SerializeField, Min(0.1f)] private float magnetSpeed = 9f;

    [Header("Vida")]
    [SerializeField, Min(0f)] private float lifetime = 30f;

    private bool collected;

    public int Value => value;

    private void Awake()
    {
        // El grano se mueve cada frame (iman). Un collider sin Rigidbody2D es "estatico" y moverlo obliga a
        // reconstruir la fisica 2D cada frame; con un cuerpo cinematico es barato y no cae ni recibe empujes.
        if (!TryGetComponent(out Rigidbody2D rb))
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    private void Start()
    {
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    /// <summary>Llamado por el colector del jugador mientras el grano esté en el radio.</summary>
    public void AttractTo(Transform target, float deltaTime)
    {
        if (collected || target == null) return;

        transform.position = Vector2.MoveTowards(
            transform.position,
            target.position,
            magnetSpeed * deltaTime);

        if ((target.position - transform.position).sqrMagnitude
            <= collectDistance * collectDistance)
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;

        if (PlayerPickupCollector.Instance != null)
            PlayerPickupCollector.Instance.AddCoffee(value);

        Destroy(gameObject);
    }
}
