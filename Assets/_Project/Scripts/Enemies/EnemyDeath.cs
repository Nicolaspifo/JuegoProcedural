using UnityEngine;

[RequireComponent(typeof(Health))]
public class EnemyDeath : MonoBehaviour
{
    [Tooltip("Segundos que se queda el cuerpo antes de desaparecer. Debe ser mayor o igual a la animacion de muerte (0.5 s).")]
    [SerializeField, Min(0f)] private float destroyDelay = 0.8f;

    private Health health;
    private bool dying;

    private void Awake() => health = GetComponent<Health>();
    private void OnEnable() => health.Died += HandleDeath;
    private void OnDisable() => health.Died -= HandleDeath;

    private void HandleDeath()
    {
        if (dying) return;
        dying = true;
        GameEvents.RaiseEnemyKilled(gameObject); // avisa a quien escuche (medidor, puntaje, jefe...)

        // Deja de perseguir y de atacar. Al apagarse, ambos liberan su casilla del anillo y su ficha de ataque.
        if (TryGetComponent(out EnemyChase chase)) chase.enabled = false;
        if (TryGetComponent(out EnemyContactDamage contact)) contact.enabled = false;

        // Sin colliders: no bloquea al jugador ni recibe golpes mientras cae.
        foreach (Collider2D col in GetComponents<Collider2D>())
            col.enabled = false;

        Destroy(gameObject, destroyDelay);
    }
}