
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Slider))]
public class SliderHandlePulse : MonoBehaviour,
    ISelectHandler, IDeselectHandler
{
    [Header("Handle")]
    [SerializeField] private RectTransform handle;

    [Header("Animation")]
    [SerializeField] private float scaleMultiplier = 1.15f;
    [SerializeField] private float animationSpeed = 3f;
    [SerializeField] private float transitionSpeed = 8f;

    private Vector3 originalScale;
    private bool isSelected;

    private void Awake()
    {
        Slider slider = GetComponent<Slider>();

        if (handle == null)
            handle = slider.handleRect;

        if (handle != null)
            originalScale = handle.localScale;
    }

    private void Update()
    {
        if (handle == null) return;

        float targetScale = 1f;

        if (isSelected)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime *
                animationSpeed * Mathf.PI * 2f) + 1f) / 2f;

            targetScale = Mathf.Lerp(1f, scaleMultiplier, pulse);
        }

        Vector3 desiredScale = originalScale * targetScale;

        handle.localScale = Vector3.Lerp(
            handle.localScale,
            desiredScale,
            Time.unscaledDeltaTime * transitionSpeed
        );
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
    }

    private void OnDisable()
    {
        isSelected = false;

        if (handle != null)
            handle.localScale = originalScale;
    }
}
