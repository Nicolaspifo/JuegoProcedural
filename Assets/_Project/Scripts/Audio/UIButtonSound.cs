using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
    [Header("Hover (mouse encima o seleccion con mando/teclado)")]
    [SerializeField] AudioClip hoverClip;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.3f;

    [Header("Click")]
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] float clickVolume = 0.4f;

    static AudioSource sharedSource;

    Button button;
    float ignoreSelectUntil;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        // Al abrir un menu, SelectFirst() selecciona un boton solo: sin esto sonaria sin que el jugador haga nada.
        ignoreSelectUntil = Time.unscaledTime + 0.1f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable) Play(hoverClip, hoverVolume);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (Time.unscaledTime < ignoreSelectUntil) return;
        if (button.interactable) Play(hoverClip, hoverVolume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (button.interactable) Play(clickClip, clickVolume);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (button.interactable) Play(clickClip, clickVolume);
    }

    static void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;

        if (sharedSource == null)
        {
            var go = new GameObject("UIButtonSoundSource");
            DontDestroyOnLoad(go);
            sharedSource = go.AddComponent<AudioSource>();
            sharedSource.playOnAwake = false;
            sharedSource.spatialBlend = 0f;   // 2D
        }

        sharedSource.PlayOneShot(clip, volume);
    }
}