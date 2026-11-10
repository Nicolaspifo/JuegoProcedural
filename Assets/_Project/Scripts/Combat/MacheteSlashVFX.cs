using UnityEngine;

/// <summary>
/// Efecto visual provisional del machete: un tajo en media luna que barre el arco real de ataque, particulas
/// que salen por el borde del arco y chispas en cada enemigo golpeado. Se crea todo por codigo (sin assets).
///
/// Es modular a proposito: cada pieza se apaga por separado, asi cuando llegue la animacion del sprite se
/// puede dejar solo lo que complemente (por ejemplo solo las particulas) sin tocar nada mas.
/// Se sincroniza con MacheteAttack.Range y .Angle, asi que sigue las mejoras de alcance.
/// </summary>
[RequireComponent(typeof(MacheteAttack))]
public class MacheteSlashVFX : MonoBehaviour
{
    [Header("Piezas (apaga las que no necesites)")]
    [SerializeField] private bool showSlash = true;
    [SerializeField] private bool showParticles = true;
    [SerializeField] private bool showHitSparks = true;

    [Header("Aspecto")]
    [SerializeField] private Color color = new Color(1f, 0.72f, 0.3f, 0.85f);
    [Tooltip("Multiplica opacidad y cantidad de particulas. Bajalo cuando haya animacion de sprite.")]
    [SerializeField, Range(0f, 2f)] private float intensity = 1f;
    [Tooltip("Opcional. Si esta vacio se usa el shader Sprites/Default (sin luz).")]
    [SerializeField] private Material effectMaterial;
    [SerializeField] private int sortingOrder = 10;

    [Header("Tajo")]
    [SerializeField, Min(0.02f)] private float slashWidth = 0.35f;
    [SerializeField, Min(0.01f)] private float sweepTime = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeTime = 0.15f;
    [Tooltip("Radio del tajo como fraccion del alcance real (1 = justo en el borde del area).")]
    [SerializeField, Range(0.3f, 1f)] private float slashRadiusFactor = 0.85f;

    [Header("Particulas")]
    [SerializeField, Min(0)] private int arcParticles = 16;
    [SerializeField, Min(0)] private int hitParticles = 8;
    [Tooltip("Corrige el giro del arco de particulas si sale desfasado respecto al tajo (grados).")]
    [SerializeField] private float shapeRotationOffset = 0f;

    private const int SlashPoints = 18;

    private MacheteAttack machete;
    private LineRenderer slash;
    private ParticleSystem arcSparks;
    private ParticleSystem hitSparks;
    private Material runtimeMaterial;
    private readonly Vector3[] points = new Vector3[SlashPoints];

    private bool slashActive;
    private bool flip;
    private float slashElapsed;
    private float slashAimDeg;

    private void Awake()
    {
        machete = GetComponent<MacheteAttack>();

        Material material = ResolveMaterial();
        slash = BuildSlash(material);
        arcSparks = BuildSparks("ArcSparks", material, 0.2f, 0.35f, 1f, 3f, 0.06f, 0.14f);
        hitSparks = BuildSparks("HitSparks", material, 0.12f, 0.25f, 2f, 4.5f, 0.05f, 0.12f);
    }

    private void OnEnable()
    {
        machete.Attacked += OnAttacked;
        machete.Hit += OnHit;
    }

    private void OnDisable()
    {
        machete.Attacked -= OnAttacked;
        machete.Hit -= OnHit;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }

    private Material ResolveMaterial()
    {
        if (effectMaterial != null) return effectMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[MacheteSlashVFX] No se encontro el shader Sprites/Default: asigna un Effect Material.", this);
            return null;
        }

        runtimeMaterial = new Material(shader) { name = "MacheteSlashVFX (runtime)" };
        return runtimeMaterial;
    }

    // ───────────────────────── Construccion ─────────────────────────

    private LineRenderer BuildSlash(Material material)
    {
        GameObject go = new GameObject("MacheteSlash");
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;              // sigue al jugador mientras dura el tajo
        line.sharedMaterial = material;
        line.sortingOrder = sortingOrder;
        line.numCapVertices = 4;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.widthMultiplier = slashWidth;
        // Forma de lente: fino en las puntas y grueso en el centro = media luna.
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.05f), new Keyframe(0.55f, 1f), new Keyframe(1f, 0f));
        line.enabled = false;
        return line;
    }

    private ParticleSystem BuildSparks(string objectName, Material material,
        float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // AddComponent lo deja reproduciendo

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = 0f;
        main.maxParticles = 128;

        var emission = ps.emission;
        emission.enabled = false; // se emite a mano con Emit() en cada golpe

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;

        // Se desvanecen y encogen al morir.
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = material;
        psr.sortingOrder = sortingOrder;
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        return ps;
    }

    // ───────────────────────── Eventos del machete ─────────────────────────

    private void OnAttacked(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        float aimDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (showSlash && intensity > 0f) StartSlash(aimDeg);
        if (showParticles && intensity > 0f) EmitArcSparks(aimDeg);
    }

    private void OnHit(GameObject target)
    {
        if (!showHitSparks || intensity <= 0f || target == null) return;

        var emit = new ParticleSystem.EmitParams
        {
            position = target.transform.position,
            applyShapeToPosition = true
        };
        hitSparks.Emit(emit, Mathf.RoundToInt(hitParticles * intensity));
    }

    // ───────────────────────── Tajo ─────────────────────────

    private void StartSlash(float aimDeg)
    {
        slashAimDeg = aimDeg;
        flip = !flip; // alterna el sentido del barrido en cada golpe
        slashElapsed = 0f;
        slashActive = true;
        slash.enabled = true;
        UpdateSlash();
    }

    private void Update()
    {
        if (slashActive) UpdateSlash();
    }

    private void UpdateSlash()
    {
        // Time.deltaTime (escalado): en pausa el tajo se congela junto con el juego.
        slashElapsed += Time.deltaTime;

        float sweep = Mathf.Clamp01(slashElapsed / sweepTime);
        int count = Mathf.Max(2, Mathf.CeilToInt(sweep * SlashPoints));

        float half = machete.Angle * 0.5f;
        float radius = machete.Range * slashRadiusFactor;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(SlashPoints - 1);
            float offset = flip ? Mathf.Lerp(half, -half, t) : Mathf.Lerp(-half, half, t);
            float rad = (slashAimDeg + offset) * Mathf.Deg2Rad;
            points[i] = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        }

        slash.positionCount = count;
        slash.SetPositions(points);

        float alpha = 1f;
        if (slashElapsed > sweepTime)
        {
            float fade = (slashElapsed - sweepTime) / fadeTime;
            if (fade >= 1f)
            {
                slashActive = false;
                slash.enabled = false;
                return;
            }
            alpha = 1f - fade;
        }

        Color c = color;
        c.a = Mathf.Clamp01(color.a * alpha * intensity);
        slash.startColor = c;
        slash.endColor = c;
        slash.widthMultiplier = slashWidth;
    }

    // ───────────────────────── Particulas del arco ─────────────────────────

    private void EmitArcSparks(float aimDeg)
    {
        float angle = machete.Angle;

        var shape = arcSparks.shape;
        shape.radius = machete.Range * slashRadiusFactor;
        shape.radiusThickness = 0.08f;          // casi solo el borde del arco
        shape.arc = angle;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

        // El arco del modulo Circle arranca en +X y barre en sentido antihorario: lo centramos en la direccion de ataque.
        arcSparks.transform.localRotation = Quaternion.Euler(0f, 0f, aimDeg - angle * 0.5f + shapeRotationOffset);

        arcSparks.Emit(Mathf.RoundToInt(arcParticles * intensity));
    }
}
