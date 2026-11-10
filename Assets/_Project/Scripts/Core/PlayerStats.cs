using UnityEngine;

/// <summary>
/// Datos del jugador editables desde el Inspector. Solo datos, sin logica de juego.
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "APuntaDeMachete/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Vida")]
    public float maxHealth = 100f;

    [Header("Movimiento")]
    public float moveSpeed = 5f;

    [Header("Combate")]
    public float attackDamage = 10f;
    public float attackRate = 2f;

    [Header("Ulti")]
    public float ultiMaxCharge = 100f;
}
