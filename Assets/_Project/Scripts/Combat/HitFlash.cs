using System.Collections;
using UnityEngine;
[RequireComponent(typeof(Health))]
public class HitFlash : MonoBehaviour
{
    [Header("Sprites que titilan")]
    [Tooltip("Déjalo vacío para usar el SpriteRenderer de este mismo objeto. No incluye los hijos.")]
    [SerializeField] private SpriteRenderer[] renderers;

    [Header("Titileo")]
    [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("Cuántas veces se enciende y apaga por golpe.")]
    [SerializeField, Min(1)] private int flashes = 2;
    [Tooltip("Duración de cada encendido/apagado, en segundos.")]
    [SerializeField, Min(0.01f)] private float flashInterval = 0.07f;
    [Tooltip("Si está activo, el titileo termina aunque el juego esté en pausa (timeScale = 0).")]
    [SerializeField] private bool useUnscaledTime = true;

    private Health health;
    private Color[] baseColors;
    private Coroutine routine;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (renderers == null || renderers.Length == 0)
        {
            SpriteRenderer own = GetComponent<SpriteRenderer>();
            renderers = own != null ? new[] { own } : new SpriteRenderer[0];
        }

        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                baseColors[i] = renderers[i].color;
        }

        if (renderers.Length == 0)
            Debug.LogWarning("[HitFlash] No hay SpriteRenderer para titilar.", this);
    }

    private void OnEnable() => health.Damaged += Play;

    private void OnDisable()
    {
        health.Damaged -= Play;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Restore();
    }

    // Health.Damaged entrega (vidaActual, vidaMaxima). Los recibimos aunque no los usemos,
    // para que la firma coincida con el delegate Action<float, float>.
    private void Play(float current, float max)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        int totalPhases = flashes * 2;   // encendido + apagado por cada titileo

        for (int phase = 0; phase < totalPhases; phase++)
        {
            ApplyColor(phase % 2 == 0 ? flashColor : (Color?)null);
            yield return Wait(flashInterval);
        }

        Restore();
        routine = null;
    }

    private IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    private void ApplyColor(Color? color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].color = color ?? baseColors[i];
        }
    }

    private void Restore() => ApplyColor(null);
}