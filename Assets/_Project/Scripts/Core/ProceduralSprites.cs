using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera texturas y sistemas de particulas simples en tiempo de ejecucion (sin depender de assets de arte), para
/// que los efectos de golpes/ataques se puedan montar por codigo. Cachea las texturas para no regenerarlas por
/// cada enemigo.
/// </summary>
public static class ProceduralSprites
{
    private static readonly Dictionary<int, Texture2D> circleCache = new();

    /// <summary>Circulo blanco con caida suave hacia el borde: sirve de "mota"/particula generica.</summary>
    public static Texture2D SoftCircleTexture(int size = 32)
    {
        if (circleCache.TryGetValue(size, out Texture2D cached) && cached != null) return cached;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                float a = Mathf.Clamp01(1f - d);
                a *= a;   // caida mas suave hacia el borde que una lineal
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        circleCache[size] = tex;
        return tex;
    }

    /// <summary>
    /// Crea un ParticleSystem de rafaga (sin loop ni emision automatica: se dispara con Emit()) usando el
    /// circulo suave como textura. Pensado para golpes/impactos cortos.
    /// </summary>
    public static ParticleSystem CreateBurstSystem(Transform parent, string name, Color color, float size,
        float sizeVariance, float speed, float gravity, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // AddComponent<ParticleSystem> lo deja reproduciendo (playOnAwake por defecto); hay que pararlo antes de
        // tocar "main" (p.ej. duration), que Unity no permite modificar mientras esta en marcha.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = 0.4f;
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Max(0.03f, size - sizeVariance), size + sizeVariance);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;   // se dispara a mano con Emit(), no solo con tiempo

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftCircleTexture() };
        renderer.sortingOrder = sortingOrder;

        return ps;
    }
}
