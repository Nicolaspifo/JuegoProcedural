using TMPro;
using UnityEngine;
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    [SerializeField, Min(0.1f)] private float lifetime = 0.8f;
    [SerializeField, Min(0f)] private float riseHeight = 1.2f;
    [SerializeField, Min(1f)] private float punchScale = 1.5f;

    [Tooltip("Fracción de la vida a partir de la cual empieza a desvanecerse.")]
    [SerializeField, Range(0.1f, 0.95f)] private float fadeStart = 0.55f;

    private Vector3 startPosition;
    private Vector3 baseScale;
    private Color baseColor;
    private float horizontalDrift;
    private float elapsed;

    private void Awake()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        baseScale = transform.localScale;
    }

    public void Init(float amount, Color color, float drift)
    {
        if (text == null) return;

        int shown = Mathf.Max(1, Mathf.RoundToInt(amount));

        text.text = shown.ToString();
        text.color = color;

        baseColor = color;
        horizontalDrift = drift;
        startPosition = transform.position;
        elapsed = 0f;
        transform.localScale = baseScale * punchScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(elapsed / lifetime);

        // Subida con desaceleración + desvío lateral.
        float rise = riseHeight * (1f - (1f - t) * (1f - t));
        transform.position = startPosition
            + Vector3.up * rise
            + Vector3.right * (horizontalDrift * t);

        // "Punch" de escala al aparecer.
        transform.localScale = baseScale * Mathf.Lerp(punchScale, 1f, Mathf.Clamp01(t * 5f));

        // Desvanecido final.
        Color c = baseColor;
        c.a = t < fadeStart ? 1f : Mathf.Clamp01(1f - (t - fadeStart) / (1f - fadeStart));
        text.color = c;

        if (t >= 1f) Destroy(gameObject);
    }
}