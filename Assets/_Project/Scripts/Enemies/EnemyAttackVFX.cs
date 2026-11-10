using UnityEngine;

/// <summary>
/// Particulas del ataque cuerpo a cuerpo de un enemigo: una rafaga breve al empezar a cargar (aviso) y otra al
/// conectar el golpe. Los parametros se configuran por prefab para diferenciar cada tipo (zancudo: rapido,
/// pequeño, sin peso; hipopotamo: pesado, polvo, con caida). Particulas generadas por codigo, sin assets.
/// </summary>
[RequireComponent(typeof(EnemyContactDamage))]
public class EnemyAttackVFX : MonoBehaviour
{
    [Header("Color y forma")]
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float particleSize = 0.25f;
    [SerializeField, Min(0f)] private float particleSizeVariance = 0.1f;

    [Header("Aviso (windup)")]
    [Tooltip("Cuantas particulas suelta al empezar a cargar el golpe.")]
    [SerializeField, Min(0)] private int windupBurstCount = 6;
    [SerializeField, Min(0f)] private float windupSpeed = 1.5f;

    [Header("Impacto")]
    [Tooltip("Cuantas particulas suelta al conectar el golpe.")]
    [SerializeField, Min(0)] private int hitBurstCount = 14;
    [SerializeField, Min(0f)] private float hitSpeed = 4f;
    [Tooltip("Gravedad de las particulas de impacto (>0 = caen, da sensacion de peso; 0 = flotan, mas livianas).")]
    [SerializeField] private float hitGravity = 0f;

    private EnemyContactDamage contact;
    private ParticleSystem windupPs;
    private ParticleSystem hitPs;

    private void Awake()
    {
        contact = GetComponent<EnemyContactDamage>();
        windupPs = ProceduralSprites.CreateBurstSystem(transform, "WindupVFX", color, particleSize * 0.7f,
            particleSizeVariance * 0.7f, windupSpeed, 0f, 7);
        hitPs = ProceduralSprites.CreateBurstSystem(transform, "HitVFX", color, particleSize, particleSizeVariance,
            hitSpeed, hitGravity, 8);
    }

    private void OnEnable()
    {
        contact.WindupStarted += OnWindupStarted;
        contact.Attacked += OnAttacked;
    }

    private void OnDisable()
    {
        contact.WindupStarted -= OnWindupStarted;
        contact.Attacked -= OnAttacked;
    }

    private void OnWindupStarted(float duration)
    {
        if (windupBurstCount > 0) windupPs.Emit(windupBurstCount);
    }

    private void OnAttacked()
    {
        if (hitBurstCount > 0) hitPs.Emit(hitBurstCount);
    }
}
