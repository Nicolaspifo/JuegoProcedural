using System.Collections;
using UnityEngine;

/// <summary>
/// Casillas de aturdimiento del jefe. Cada vez que la Llorona empieza a cantar, aparecen cuadrados avisados en el
/// suelo FUERA del aro; al terminar el aviso, quien este encima queda aturdido. Aparecen donde el jugador suele huir
/// del aro, asi que escapar del canto tiene riesgo. Cuadrados dibujados por codigo (sin assets).
/// Sin casillas en la fase 1; despues 3 / 4 / 6.
/// </summary>
[RequireComponent(typeof(CryAttack), typeof(BossLlorona))]
public class StunTileAttack : MonoBehaviour
{
    [Tooltip("Casillas por fase (fase 1 = 0, sin aturdimiento).")]
    [SerializeField] int[] tilesPerPhase = { 0, 3, 4, 6 };
    [SerializeField, Min(0.5f)] float tileSize = 2.6f;
    [Tooltip("Segundos de aviso de la casilla (se queda a la vista este tiempo antes de aturdir).")]
    [SerializeField, Min(0.2f)] float warningTime = 1.2f;
    [Tooltip("Fraccion final del aviso en la que la casilla deja de seguir al jugador y queda fija (el telegrafo real: antes de esto persigue, lo justo para que sea posible acertar).")]
    [SerializeField, Range(0.1f, 0.9f)] float lockFraction = 0.35f;
    [SerializeField, Min(0.1f)] float stunDuration = 1.2f;
    [Tooltip("Margen entre el borde del aro del canto y la casilla.")]
    [SerializeField, Min(0f)] float ringMargin = 1.2f;
    [Tooltip("Dispersion de las casillas extra alrededor del jugador.")]
    [SerializeField, Min(0f)] float scatter = 4f;
    [SerializeField] Color warningColor = new Color(1f, 0.1f, 0.2f, 0.55f);

    BossLlorona boss;
    CryAttack cry;
    Transform player;
    Stunnable stunnable;
    Sprite square;

    void Awake()
    {
        boss = GetComponent<BossLlorona>();
        cry = GetComponent<CryAttack>();

        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void Start()
    {
        FindPlayer();
        cry.CryStarted += OnCryStarted;
    }

    void OnDestroy()
    {
        if (cry != null) cry.CryStarted -= OnCryStarted;
    }

    void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        player = p.transform;
        if (!p.TryGetComponent(out stunnable)) stunnable = p.AddComponent<Stunnable>();   // el estuneo no requiere montaje manual
    }

    void OnCryStarted()
    {
        if (boss.IsDead) return;
        if (player == null) FindPlayer();
        if (player == null) return;

        int count = tilesPerPhase[Mathf.Clamp(boss.Phase - 1, 0, tilesPerPhase.Length - 1)];
        for (int i = 0; i < count; i++)
        {
            // La primera casilla sigue justo al jugador (offset 0); las demas, repartidas a su alrededor.
            Vector2 offset = i == 0 ? Vector2.zero : Random.insideUnitCircle * scatter;
            StartCoroutine(Tile(offset));
        }
    }

    // Posicion deseada ahora mismo: jugador + offset, empujada fuera del aro si cae dentro.
    Vector2 ComputePosition(Vector2 offset)
    {
        Vector2 center = (Vector2)player.position + offset;

        Vector2 bossPos = transform.position;
        Vector2 away = center - bossPos;
        float minDistance = cry.Radius + ringMargin + tileSize * 0.5f;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle.normalized;
        if (away.magnitude < minDistance) center = bossPos + away.normalized * minDistance;
        return center;
    }

    // La casilla persigue al jugador (con su offset) durante la primera parte del aviso, y se congela en la
    // ultima fraccion (lockFraction): asi el telegrafo final es real y acertable, no una prediccion que ya no
    // vale apenas el jugador cambia de direccion.
    IEnumerator Tile(Vector2 offset)
    {
        var go = new GameObject("StunTile");
        Vector2 pos = ComputePosition(offset);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(tileSize, tileSize, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.sortingOrder = 5;

        float lockTime = warningTime * (1f - lockFraction);
        for (float t = 0f; t < warningTime; t += Time.deltaTime)
        {
            if (t < lockTime && player != null)
            {
                pos = ComputePosition(offset);
                go.transform.position = pos;
            }

            float alpha = Mathf.Lerp(0.25f, warningColor.a, Mathf.PingPong(t * 6f, 1f));
            sr.color = new Color(warningColor.r, warningColor.g, warningColor.b, alpha);
            yield return null;
        }

        // Descarga: destello y aturdimiento si el jugador sigue encima.
        sr.color = Color.white;
        if (stunnable != null && player != null && !boss.IsDead)
        {
            Vector2 d = (Vector2)player.position - pos;
            float half = tileSize * 0.5f;
            if (Mathf.Abs(d.x) <= half && Mathf.Abs(d.y) <= half) stunnable.TryStun(stunDuration);
        }

        yield return new WaitForSeconds(0.15f);
        Destroy(go);
    }
}
