using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Genera un impulso de Cinemachine (sacudida de cámara) cada vez que
/// el Health de este objeto recibe daño.
/// </summary>
[RequireComponent(typeof(Health))]
public class CameraShakeOnHit : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Tooltip("Fuerza del sacudón. 1 = normal.")]
    [SerializeField, Min(0f)] private float force = 1f;

    [Tooltip("Dirección aleatoria en cada golpe (más natural).")]
    [SerializeField] private bool randomDirection = true;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable() => health.Damaged += Shake;
    private void OnDisable() => health.Damaged -= Shake;

    private void Shake(float current, float max)
    {
        if (impulseSource == null || force <= 0f) return;

        if (randomDirection)
        {
            Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.down;

            impulseSource.GenerateImpulseWithVelocity(dir.normalized * force);
        }
        else
        {
            impulseSource.GenerateImpulseWithForce(force);
        }
    }
}