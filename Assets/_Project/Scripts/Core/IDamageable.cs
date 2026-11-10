using UnityEngine;

/// <summary>
/// Contrato para todo lo que puede recibir dano (jugador, enemigos, jefes).
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(float amount, GameObject source = null);
}
