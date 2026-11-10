
using UnityEngine;
using UnityEngine.Tilemaps;

public class InfiniteTilemapFloor : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Transform follow;

    [Header("Tiles del suelo")]
    [SerializeField] private TileBase[] tiles;

    [Header("Probabilidades (%)")]
    [Header("Generacion de biomas")]

    [SerializeField] private float landScale = 0.009f;
    [SerializeField] private float warpScale = 0.012f;
    [SerializeField] private float warpStrength = 12f;

    [Header("Niveles del terreno")]

    [SerializeField, Range(0f, 1f)]
    private float waterLevel = 0.40f;

    [SerializeField, Range(0f, 0.3f)]
    private float beachWidth = 0.08f;

    [SerializeField, Range(0f, 1f)]
    private float swampThreshold = 0.60f;

    [SerializeField, Range(0f, 1f)]
    private float rockThreshold = 0.64f;

    [Header("Variaciones")]

    [SerializeField] private float moistureScale = 0.012f;
    [SerializeField] private float rockScale = 0.009f;
    [SerializeField] private float waterVariationScale = 0.025f;

    [Header("Generacion")]
    [SerializeField] private int seed = 42;
    [SerializeField] private int extraCells = 3;

    private Camera cam;
    private Vector3Int lastCenter =
        new Vector3Int(int.MinValue, int.MinValue, 0);

    private Grid grid;

    private void Awake()
    {
        if (tilemap == null)
            tilemap = GetComponent<Tilemap>();

        if (follow == null && Camera.main != null)
            follow = Camera.main.transform;

        if (follow != null)
            cam = follow.GetComponent<Camera>();

        if (cam == null)
            cam = Camera.main;

        grid = tilemap.GetComponentInParent<Grid>();
    }

    private void LateUpdate()
    {
        if (follow == null || tilemap == null ||
            tiles == null || tiles.Length == 0)
            return;

        Vector3Int center = tilemap.WorldToCell(follow.position);

        // Solo regenerar cuando la camara cambia de celda.
        if (center == lastCenter)
            return;

        lastCenter = center;

        GenerateFloor(center);
    }

    private void GenerateFloor(Vector3Int center)
    {
        Vector3 cellSize = grid != null
            ? grid.cellSize
            : Vector3.one;

        float halfHeight = cam != null && cam.orthographic
            ? cam.orthographicSize
            : 10f;

        float aspect = cam != null
            ? cam.aspect
            : 1.777f;

        float halfWidth = halfHeight * aspect;

        int radiusX = Mathf.CeilToInt(
            halfWidth / cellSize.x) + extraCells;

        int radiusY = Mathf.CeilToInt(
            halfHeight / cellSize.y) + extraCells;

        // Limpiar el area anterior.
        tilemap.ClearAllTiles();

        // Crear el suelo alrededor de la camara.
        for (int x = center.x - radiusX;
             x <= center.x + radiusX; x++)
        {
            for (int y = center.y - radiusY;
                 y <= center.y + radiusY; y++)
            {
                Vector3Int position =
                    new Vector3Int(x, y, 0);

                TileBase selectedTile = ChooseTile(x, y);

                tilemap.SetTile(position, selectedTile);
            }
        }
    }



    private TileBase ChooseTile(int x, int y)
    {
        // Comprobar que tenemos los seis materiales.
        if (tiles == null || tiles.Length < 6)
            return tiles != null && tiles.Length > 0
                ? tiles[0]
                : null;

        // 1. Deformacion del terreno para evitar costas geometricas.
        float warpX = (
            Mathf.PerlinNoise(
                (x + seed + 1000) * warpScale,
                (y + seed + 1000) * warpScale
            ) - 0.5f
        ) * warpStrength;

        float warpY = (
            Mathf.PerlinNoise(
                (x + seed + 2000) * warpScale,
                (y + seed + 2000) * warpScale
            ) - 0.5f
        ) * warpStrength;

        // 2. Mapa de elevacion.
        float elevation = Mathf.PerlinNoise(
            (x + warpX + seed) * landScale,
            (y + warpY + seed) * landScale
        );

        // 3. Si estamos bajo el nivel del agua.
        if (elevation < waterLevel)
        {
            // Alternar los dos tiles de agua en agrupaciones.
            float waterNoise = Mathf.PerlinNoise(
                (x + seed + 3000) * waterVariationScale,
                (y + seed + 3000) * waterVariationScale
            );

            return waterNoise > 0.5f ? tiles[4] : tiles[5];
        }

        // 4. Playa: franja entre tierra y agua.
        if (elevation < waterLevel + beachWidth)
        {
            return tiles[3];
        }

        // 5. Mapa de humedad.
        float moisture = Mathf.PerlinNoise(
            (x + seed + 4000) * moistureScale,
            (y + seed + 4000) * moistureScale
        );

        // 6. Mapa de formaciones rocosas.
        float rockNoise = Mathf.PerlinNoise(
            (x + seed + 5000) * rockScale,
            (y + seed + 5000) * rockScale
        );

        // Pantano: zonas humedas.
        if (moisture > swampThreshold && elevation < 0.75f)
        {
            return tiles[1];
        }

        // Roca: formaciones terrestres.
        if (rockNoise > rockThreshold)
        {
            return tiles[2];
        }

        // Suelo: material predominante.
        return tiles[0];
    }

}

