
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class HealthBarUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Health target;
    [SerializeField] private RectTransform barRect;

    [Header("Configuracion de tamaño")]
    [SerializeField] private float widthPerHealth = 2f;
    [SerializeField] private float resizeSpeed = 5f;

    private Slider slider;

    private float baseMaxHealth;
    private float baseWidth;
    private float targetWidth;

    void Awake()
    {
        slider = GetComponent<Slider>();

        if (barRect == null)
            barRect = GetComponent<RectTransform>();

        if (target == null)
            Debug.LogError("HealthBarUI: falta asignar Health.", this);
    }

    void Start()
    {
        if (target == null || barRect == null) return;

        baseMaxHealth = target.Max;
        baseWidth = barRect.rect.width;

        targetWidth = baseWidth;

        Refresh(target.Current, target.Max);
    }

    void OnEnable()
    {
        if (target == null) return;

        target.Changed += Refresh;
        Refresh(target.Current, target.Max);
    }

    void OnDisable()
    {
        if (target != null)
            target.Changed -= Refresh;
    }

    void Update()
    {
        if (barRect == null) return;

        float currentWidth = barRect.rect.width;

        float newWidth = Mathf.Lerp(
            currentWidth,
            targetWidth,
            Time.unscaledDeltaTime * resizeSpeed
        );

        barRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            newWidth
        );
    }

    void Refresh(float current, float max)
    {
        slider.maxValue = max;
        slider.value = current;

        if (baseMaxHealth <= 0f) return;

        // Calcula el nuevo ancho según la vida máxima.
        targetWidth = baseWidth +
            (max - baseMaxHealth) * widthPerHealth;
    }
}
