using UnityEngine;


public static class Sfx
{
    private static AudioSource source;

    
    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        EnsureSource();

        source.pitch = pitch;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static void EnsureSource()
    {
        if (source != null) return;

        GameObject go = new GameObject("Sfx (runtime)");
        Object.DontDestroyOnLoad(go);

        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   
        source.volume = 1f;
    }
}
