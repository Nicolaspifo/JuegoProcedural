using UnityEngine;
using UnityEngine.EventSystems;

public class SelectScaleEffect : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] float selectedScale = 1.1f;
    [SerializeField] float speed = 12f;

    Vector3 baseScale;
    Vector3 targetScale;

    void Awake()
    {
        baseScale = transform.localScale;
        targetScale = baseScale;
    }

    void OnEnable()
    {
        transform.localScale = baseScale;
        targetScale = baseScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, speed * Time.unscaledDeltaTime);
    }

    public void OnSelect(BaseEventData e) => targetScale = baseScale * selectedScale;
    public void OnDeselect(BaseEventData e) => targetScale = baseScale;

    // Con el ratón, pasar por encima también selecciona el botón
    public void OnPointerEnter(PointerEventData e) => EventSystem.current.SetSelectedGameObject(gameObject);
}