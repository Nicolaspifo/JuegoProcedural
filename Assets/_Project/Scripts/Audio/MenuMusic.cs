using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MenuMusic : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float baseVolume = 0.6f;
    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    void Update()
    {
        source.volume = baseVolume * GameVolume.Value;
    }
}