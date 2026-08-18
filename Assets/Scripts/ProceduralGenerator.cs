
using UnityEngine;

//
// ============================================================
// TERRAIN GENERATOR
// ============================================================
// Converts TerrainSettings into a Unity Terrain.
//
// The LLM decides WHAT type of terrain to create.
// Unity decides HOW to generate it.
// ============================================================

public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Parent")]
    [SerializeField]
    private Transform terrainParent;

    [Header("Default Settings")]
    [SerializeField]
    private int defaultWidth = 200;

    [SerializeField]
    private int defaultDepth = 200;

    [SerializeField]
    private int defaultHeight = 30;

    [Header("Resolution")]
    [SerializeField]
    private int heightmapResolution = 513;

    [Header("Generated Terrain")]
    [SerializeField]
    private Terrain generatedTerrain;

    public Terrain GetGeneratedTerrain()
    {
        return generatedTerrain;
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void GenerateTerrain(TerrainSettings settings)
    {
        if (settings == null)
        {
            Debug.LogError("TerrainGenerator: Terrain settings are null.");
            return;
        }

        // -----------------------------------------------------
        // Safety limits
        // -----------------------------------------------------

        settings.width = Mathf.Clamp(settings.width, 20, 1000);
        settings.depth = Mathf.Clamp(settings.depth, 20, 1000);
        settings.height = Mathf.Clamp(settings.height, 1, 500);

        settings.roughness = Mathf.Clamp01(settings.roughness);

        settings.detailScale = Mathf.Clamp(
            settings.detailScale,
            0.001f,
            0.2f
        );

        settings.octaves = Mathf.Clamp(
            settings.octaves,
            1,
            8
        );

        // -----------------------------------------------------
        // Remove previous terrain
        // -----------------------------------------------------

        ClearTerrain();

        // -----------------------------------------------------
        // Create TerrainData
        // -----------------------------------------------------

        TerrainData terrainData = new TerrainData();

        terrainData.heightmapResolution = heightmapResolution;

        terrainData.size = new Vector3(
            settings.width,
            settings.height,
            settings.depth
        );

        // -----------------------------------------------------
        // Generate heightmap
        // -----------------------------------------------------

        float[,] heights = GenerateHeightmap(settings);

        terrainData.SetHeights(
            0,
            0,
            heights
        );

        // -----------------------------------------------------
        // Create Terrain GameObject
        // -----------------------------------------------------

        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(terrainData);

        terrainObject.name = "AI_Generated_Terrain";

        if (terrainParent != null)
        {
            terrainObject.transform.SetParent(
                terrainParent,
                false
            );
        }

        generatedTerrain =
            terrainObject.GetComponent<Terrain>();

        // -----------------------------------------------------
        // Configure terrain
        // -----------------------------------------------------

        if (generatedTerrain != null)
        {
            generatedTerrain.heightmapPixelError = 5;
            generatedTerrain.basemapDistance = 1000;
        }

        Debug.Log(
            $"AI Terrain generated: " +
            $"{settings.terrainType}, " +
            $"{settings.width} x {settings.depth}, " +
            $"height {settings.height}"
        );
    }

    // =========================================================
    // HEIGHTMAP
    // =========================================================

    private float[,] GenerateHeightmap(
        TerrainSettings settings
    )
    {
        int resolution = heightmapResolution;

        float[,] heights =
            new float[resolution, resolution];

        float offsetX = settings.seed * 13.37f;
        float offsetZ = settings.seed * 7.91f;

        string terrainType =
            settings.terrainType == null
                ? "hills"
                : settings.terrainType.ToLower().Trim();

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX =
                    (float)x / (resolution - 1);

                float normalizedZ =
                    (float)z / (resolution - 1);

                float height = 0f;

                switch (terrainType)
                {
                    case "flat":

                        height = GenerateFlatTerrain(
                            normalizedX,
                            normalizedZ
                        );

                        break;

                    case "hills":

                        height = GenerateHills(
                            normalizedX,
                            normalizedZ,
                            settings,
                            offsetX,
                            offsetZ
                        );

                        break;

                    case "mountain":
                    case "mountains":

                        height = GenerateMountains(
                            normalizedX,
                            normalizedZ,
                            settings,
                            offsetX,
                            offsetZ
                        );

                        break;

                    case "valley":

                        height = GenerateValley(
                            normalizedX,
                            normalizedZ,
                            settings,
                            offsetX,
                            offsetZ
                        );

                        break;

                    case "island":

                        height = GenerateIsland(
                            normalizedX,
                            normalizedZ,
                            settings,
                            offsetX,
                            offsetZ
                        );

                        break;

                    case "desert":

                        height = GenerateDesert(
                            normalizedX,
                            normalizedZ,
                            settings,
                            offsetX,
                            offsetZ
                        );

                        break;

                    case "canyon":

                        height = GenerateCanyon(
                            normalizedX,
                            normalizedZ,
                            settings,
                            offsetX,
                            offsetZ
                        );

                        break;

                    default:

                        height = GenerateHills(
                            normalizedX,
                            normalizedZ,
                            settings,
                            offsetX,
                            offsetZ
                        );

                        break;
                }

                heights[z, x] =
                    Mathf.Clamp01(height);
            }
        }

        return heights;
    }

    // =========================================================
    // FLAT
    // =========================================================

    private float GenerateFlatTerrain(
        float x,
        float z
    )
    {
        return 0.02f;
    }

    // =========================================================
    // HILLS
    // =========================================================

    private float GenerateHills(
        float x,
        float z,
        TerrainSettings settings,
        float offsetX,
        float offsetZ
    )
    {
        float noise =
            FractalNoise(
                x,
                z,
                settings.detailScale,
                settings.octaves,
                settings.roughness,
                offsetX,
                offsetZ
            );

        return noise * 0.35f;
    }

    // =========================================================
    // MOUNTAINS
    // =========================================================

    private float GenerateMountains(
        float x,
        float z,
        TerrainSettings settings,
        float offsetX,
        float offsetZ
    )
    {
        float noise =
            FractalNoise(
                x,
                z,
                settings.detailScale * 0.7f,
                settings.octaves + 1,
                settings.roughness,
                offsetX,
                offsetZ
            );

        // Make mountains sharper.
        noise = Mathf.Pow(noise, 1.5f);

        return noise * 0.9f;
    }

    // =========================================================
    // VALLEY
    // =========================================================

    private float GenerateValley(
        float x,
        float z,
        TerrainSettings settings,
        float offsetX,
        float offsetZ
    )
    {
        float noise =
            FractalNoise(
                x,
                z,
                settings.detailScale,
                settings.octaves,
                settings.roughness,
                offsetX,
                offsetZ
            );

        float distanceFromCenter =
            Mathf.Abs(x - 0.5f);

        float valleyShape =
            1f - Mathf.Clamp01(
                distanceFromCenter * 2f
            );

        float mountains =
            noise * 0.7f;

        float valley =
            valleyShape * 0.3f;

        return Mathf.Clamp01(
            mountains - valley
        );
    }

    // =========================================================
    // ISLAND
    // =========================================================

    private float GenerateIsland(
        float x,
        float z,
        TerrainSettings settings,
        float offsetX,
        float offsetZ
    )
    {
        float noise =
            FractalNoise(
                x,
                z,
                settings.detailScale,
                settings.octaves,
                settings.roughness,
                offsetX,
                offsetZ
            );

        float centerX =
            x - 0.5f;

        float centerZ =
            z - 0.5f;

        float distance =
            Mathf.Sqrt(
                centerX * centerX +
                centerZ * centerZ
            );

        float islandMask =
            1f - Mathf.Clamp01(
                distance * 2f
            );

        islandMask =
            Mathf.Pow(islandMask, 1.5f);

        return noise * islandMask;
    }

    // =========================================================
    // DESERT
    // =========================================================

    private float GenerateDesert(
        float x,
        float z,
        TerrainSettings settings,
        float offsetX,
        float offsetZ
    )
    {
        float noise =
            FractalNoise(
                x,
                z,
                settings.detailScale * 0.5f,
                settings.octaves,
                settings.roughness,
                offsetX,
                offsetZ
            );

        // Softer rolling dunes.
        noise =
            Mathf.Pow(noise, 1.3f);

        return noise * 0.3f;
    }

    // =========================================================
    // CANYON
    // =========================================================

    private float GenerateCanyon(
        float x,
        float z,
        TerrainSettings settings,
        float offsetX,
        float offsetZ
    )
    {
        float noise =
            FractalNoise(
                x,
                z,
                settings.detailScale,
                settings.octaves,
                settings.roughness,
                offsetX,
                offsetZ
            );

        // Create a long canyon through the terrain.
        float canyonCenter =
            0.5f +
            Mathf.Sin(z * 8f) * 0.08f;

        float distance =
            Mathf.Abs(x - canyonCenter);

        float canyonWidth = 0.08f;

        if (distance < canyonWidth)
        {
            float canyonDepth =
                1f -
                (distance / canyonWidth);

            noise -=
                canyonDepth * 0.6f;
        }

        return Mathf.Clamp01(
            noise * 0.8f
        );
    }

    // =========================================================
    // FRACTAL PERLIN NOISE
    // =========================================================

    private float FractalNoise(
        float x,
        float z,
        float scale,
        int octaves,
        float roughness,
        float offsetX,
        float offsetZ
    )
    {
        float total = 0f;

        float amplitude = 1f;

        float frequency = 1f;

        float maximum = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX =
                x * scale * frequency * 100f
                + offsetX;

            float sampleZ =
                z * scale * frequency * 100f
                + offsetZ;

            float noise =
                Mathf.PerlinNoise(
                    sampleX,
                    sampleZ
                );

            total +=
                noise * amplitude;

            maximum += amplitude;

            amplitude *=
                Mathf.Lerp(
                    0.5f,
                    0.9f,
                    roughness
                );

            frequency *= 2f;
        }

        if (maximum <= 0f)
            return 0f;

        return total / maximum;
    }

    // =========================================================
    // CLEAR
    // =========================================================

    public void ClearTerrain()
    {
        if (generatedTerrain != null)
        {
            Destroy(
                generatedTerrain.gameObject
            );

            generatedTerrain = null;
        }

        // Also remove previously generated terrain
        // if the reference was lost.

        Transform parent =
            terrainParent != null
                ? terrainParent
                : transform;

        for (int i = parent.childCount - 1;
             i >= 0;
             i--)
        {
            Transform child =
                parent.GetChild(i);

            if (child.name ==
                "AI_Generated_Terrain")
            {
                Destroy(child.gameObject);
            }
        }
    }

    // =========================================================
    // DEFAULT TERRAIN
    // =========================================================

    public void GenerateDefaultTerrain()
    {
        TerrainSettings settings =
            new TerrainSettings();

        settings.width =
            defaultWidth;

        settings.depth =
            defaultDepth;

        settings.height =
            defaultHeight;

        settings.terrainType =
            "hills";

        settings.roughness =
            0.5f;

        settings.detailScale =
            0.03f;

        settings.octaves =
            4;

        settings.seed =
            Random.Range(
                0,
                999999
            );

        GenerateTerrain(settings);
    }
}

