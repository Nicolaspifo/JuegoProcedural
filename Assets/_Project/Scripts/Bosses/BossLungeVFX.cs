using System.Collections;
using UnityEngine;

/// <summary>
/// Vistoso para la embestida ("dash") del jefe. Mientras carga (windup) dibuja una estela apuntando hacia donde
/// se va a lanzar, que crece en intensidad segun se acerca el golpe (telegrafo). Al embestir, deja un rastro
/// (TrailRenderer) y polvo detras mientras avanza. Todo generado por codigo, sin assets.
/// </summary>
[RequireComponent(typeof(EnemyContactDamage))]
public class BossLungeVFX : MonoBehaviour
{
    [Header("Estela de puntería (windup)")]
    [SerializeField] private Color aimColor = new Color(1f, 0.7f, 0.1f, 0.85f);

    [Header("Rastro y polvo (embestida)")]
    [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.4f, 0.55f);
    [SerializeField, Min(0f)] private float trailTime = 0.25f;
    [SerializeField, Min(0.01f)] private float dustInterval = 0.04f;
    [SerializeField, Min(0.05f)] private float dustSize = 0.35f;

    private EnemyContactDamage contact;
    private LineRenderer aimLine;
    private TrailRenderer trail;
    private ParticleSystem dust;
    private Coroutine aimRoutine;
    private Coroutine dustRoutine;

    private void Awake()
    {
        contact = GetComponent<EnemyContactDamage>();

        aimLine = new GameObject("LungeAim").AddComponent<LineRenderer>();
        aimLine.transform.SetParent(transform, false);
        aimLine.positionCount = 2;
        aimLine.useWorldSpace = true;
        aimLine.widthMultiplier = 0.08f;
        aimLine.material = new Material(Shader.Find("Sprites/Default"));
        aimLine.sortingOrder = 6;
        aimLine.enabled = false;

        trail = new GameObject("LungeTrail").AddComponent<TrailRenderer>();
        trail.transform.SetParent(transform, false);
        trail.time = trailTime;
        trail.startWidth = 1f;
        trail.endWidth = 0.1f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.colorGradient = FadeGradient(trailColor);
        trail.sortingOrder = 4;
        trail.minVertexDistance = 0.05f;
        trail.emitting = false;
        trail.autodestruct = false;

        dust = ProceduralSprites.CreateBurstSystem(transform, "LungeDustVFX",
            new Color(0.85f, 0.75f, 0.55f, 0.6f), dustSize, dustSize * 0.3f, 1.2f, 0.3f, 5);
    }

    private void OnEnable()
    {
        contact.WindupStarted += OnWindupStarted;
        contact.LungeStarted += OnLungeStarted;
        contact.LungeEnded += OnLungeEnded;
    }

    private void OnDisable()
    {
        contact.WindupStarted -= OnWindupStarted;
        contact.LungeStarted -= OnLungeStarted;
        contact.LungeEnded -= OnLungeEnded;

        StopAllCoroutines();
        aimLine.enabled = false;
        trail.emitting = false;
    }

    private void OnWindupStarted(float duration)
    {
        if (contact.LungeDistance <= 0f) return;   // sin embestida (ataques normales de la Llorona): sin estela

        if (aimRoutine != null) StopCoroutine(aimRoutine);
        aimLine.enabled = true;
        aimRoutine = StartCoroutine(AimTelegraph(duration));
    }

    private IEnumerator AimTelegraph(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);   // crece en intensidad segun se acerca el golpe

            Vector3 origin = transform.position;
            Vector3 end = origin + (Vector3)(contact.LungeDirection * contact.LungeDistance);
            aimLine.SetPosition(0, origin);
            aimLine.SetPosition(1, end);

            Color start = aimColor;
            start.a = Mathf.Lerp(0.1f, aimColor.a, k);
            aimLine.startColor = start;
            aimLine.endColor = new Color(aimColor.r, aimColor.g, aimColor.b, 0f);   // se desvanece hacia la punta
            aimLine.widthMultiplier = Mathf.Lerp(0.03f, 0.14f, k);

            yield return null;
        }
    }

    private void OnLungeStarted()
    {
        aimLine.enabled = false;
        if (aimRoutine != null) { StopCoroutine(aimRoutine); aimRoutine = null; }

        trail.Clear();
        trail.emitting = true;

        if (dustRoutine != null) StopCoroutine(dustRoutine);
        dustRoutine = StartCoroutine(DustTrail());
    }

    private void OnLungeEnded()
    {
        trail.emitting = false;
        if (dustRoutine != null) { StopCoroutine(dustRoutine); dustRoutine = null; }
    }

    private IEnumerator DustTrail()
    {
        var wait = new WaitForSeconds(dustInterval);
        while (true)
        {
            dust.transform.position = transform.position;
            dust.Emit(2);
            yield return wait;
        }
    }

    private static Gradient FadeGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
