using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BossLlorona))]
public class CryAttack : MonoBehaviour
{
    [SerializeField] float[] intervalPerPhase = { 6f, 5f, 4f, 3f };
    [SerializeField] float[] radiusPerPhase = { 4f, 5f, 6f };
    [Tooltip("Segundos de aviso del aro por fase: menos tiempo = ataque mas rapido y mas dificil de esquivar.")]
    [SerializeField] float[] warningTimePerPhase = { 0.8f, 0.7f, 0.6f, 0.5f };
    [Tooltip("Daño del aro como fraccion de la vida maxima del jugador (0.3 = 30 %). El aturdimiento lo dan las casillas del suelo.")]
    [SerializeField, Range(0f, 1f)] float ringDamagePercent = 0.18f;
    [SerializeField] float firstCryDelay = 3f;
    [Tooltip("Segundos despues del canto en los que el jefe NO empieza ataques (embestidas): tras gritar, respira.")]
    [SerializeField, Min(0f)] float attackHoldAfterCry = 2.5f;
    [SerializeField] Color warningColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] Material lineMaterial;   // opcional

    /// <summary>Se dispara cuando la Llorona empieza a gritar (antes del aviso circular).</summary>
    public event Action CryStarted;

    /// <summary>Radio y aviso del canto en curso (validos desde que se dispara CryStarted).</summary>
    public float Radius { get; private set; }
    public float WarningTime { get; private set; }

    const int Segments = 48;

    BossLlorona boss;
    Health target;
    LineRenderer ring;
    float cooldown;
    bool crying;

    void Awake()
    {
        boss = GetComponent<BossLlorona>();

        ring = new GameObject("CryWarning").AddComponent<LineRenderer>();
        ring.transform.SetParent(transform, false);
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = Segments;
        ring.widthMultiplier = 0.15f;
        ring.sortingOrder = 50;
        ring.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        ring.enabled = false;
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) player.TryGetComponent(out target);
        if (target == null) Debug.LogWarning("CryAttack: no se encontro la Health del Player (tag Player).");

        cooldown = firstCryDelay;
        boss.OnPhaseChanged += _ => cooldown = Mathf.Min(cooldown, 1f);
    }

    void Update()
    {
        if (boss.IsDead || crying) return;

        cooldown -= Time.deltaTime;
        if (cooldown <= 0f) StartCoroutine(Cry());
    }

    IEnumerator Cry()
    {
        crying = true;
        boss.MovementLocked = true;

        float radius = Pick(radiusPerPhase);
        float warningTime = Pick(warningTimePerPhase);
        Radius = radius;
        WarningTime = warningTime;

        CryStarted?.Invoke();

        StartCoroutine(RippleWaves(radius, warningTime));   // ondas viajeras, decorativas (como piedras en un estanque)

        ring.enabled = true;
        for (float t = 0f; t < warningTime; t += Time.deltaTime)
        {
            float alpha = Mathf.Lerp(0.3f, 1f, Mathf.PingPong(t * 6f, 1f));
            Color c = new Color(warningColor.r, warningColor.g, warningColor.b, alpha);
            ring.startColor = c;
            ring.endColor = c;
            Draw(radius);
            yield return null;
        }
        ring.enabled = false;

        // El aro hace mucho daño a quien siga dentro cuando termina el aviso.
        if (target != null && !target.IsDead &&
            Vector2.Distance(target.transform.position, transform.position) <= radius)
        {
            float damage = target.Max * ringDamagePercent;
            target.TakeDamage(damage, gameObject);
            Debug.Log($"[Llorona] El canto golpeo al jugador: {damage:0} de daño ({ringDamagePercent:P0} de su vida maxima)");
        }

        boss.MovementLocked = false;
        if (TryGetComponent(out EnemyContactDamage contact)) contact.Hold(attackHoldAfterCry);
        cooldown = Pick(intervalPerPhase);
        crying = false;
    }

    void Draw(float radius) => DrawOn(ring, radius);

    void DrawOn(LineRenderer line, float radius)
    {
        Vector3 c = transform.position;
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
        }
    }

    // Ondas que viajan desde el centro hacia el radio de aviso, como piedras cayendo en un estanque: puramente
    // decorativo, no cambia el radio ni el daño del canto.
    IEnumerator RippleWaves(float maxRadius, float duration)
    {
        const int waveCount = 3;
        float interval = duration / waveCount;
        for (int i = 0; i < waveCount; i++)
        {
            StartCoroutine(Ripple(maxRadius, Mathf.Min(0.6f, duration)));
            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator Ripple(float maxRadius, float rippleDuration)
    {
        var go = new GameObject("CryRipple");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = Segments;
        line.widthMultiplier = 0.08f;
        line.sortingOrder = 49;
        line.material = ring.material;

        for (float t = 0f; t < rippleDuration; t += Time.deltaTime)
        {
            float k = t / rippleDuration;
            float r = Mathf.Lerp(maxRadius * 0.15f, maxRadius, k);
            float alpha = (1f - k) * 0.8f;
            Color c = new Color(warningColor.r, warningColor.g, warningColor.b, alpha);
            line.startColor = c;
            line.endColor = c;
            DrawOn(line, r);
            yield return null;
        }

        Destroy(go);
    }

    float Pick(float[] values) => values[Mathf.Clamp(boss.Phase - 1, 0, values.Length - 1)];

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radiusPerPhase.Length > 0 ? radiusPerPhase[0] : 4f);
    }
}