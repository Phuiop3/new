using UnityEngine;

// ============================================================
// TERRAIN GENERATOR
// ============================================================
//
// Converts TerrainSettings into a Unity Terrain.
//
// WORKFLOW
// --------
// 1. Existing Plane is the initial ground.
// 2. AI requests terrain generation.
// 3. Terrain is generated at the Plane's location.
// 4. Terrain uses the Plane's X/Z size.
// 5. TerrainCollider is explicitly configured.
// 6. Existing Plane is automatically hidden.
// 7. Generated Terrain becomes the new ground.
// 8. User can later create Soil.
// 9. User can later plant Crops.
//
// Terrain generation NEVER creates soil.
//
// XR ray configuration is NOT handled here.
//
// ============================================================

public class TerrainGenerator : MonoBehaviour
{
    // =========================================================
    // TERRAIN PARENT
    // =========================================================

    [Header("Terrain Parent")]

    [Tooltip(
        "Optional parent for the generated Terrain. " +
        "The Terrain's world position is calculated from the Plane."
    )]
    [SerializeField]
    private Transform terrainParent;


    // =========================================================
    // EXISTING PLANE
    // =========================================================

    [Header("Existing Ground Plane")]

    [Tooltip(
        "The existing Plane that will be replaced by the " +
        "AI-generated Terrain."
    )]
    [SerializeField]
    private GameObject existingPlane;


    [Tooltip(
        "Automatically hide the existing Plane after " +
        "Terrain generation."
    )]
    [SerializeField]
    private bool hidePlaneWhenTerrainGenerated = true;


    [Tooltip(
        "Automatically use the Plane's world size for " +
        "the generated Terrain."
    )]
    [SerializeField]
    private bool matchPlaneSize = true;


    [Tooltip(
        "Automatically use the Plane's world position " +
        "for the generated Terrain."
    )]
    [SerializeField]
    private bool matchPlanePosition = true;


    // =========================================================
    // DEFAULT SETTINGS
    // =========================================================

    [Header("Default Settings")]

    [SerializeField]
    private int defaultWidth = 200;

    [SerializeField]
    private int defaultDepth = 200;

    [SerializeField]
    private int defaultHeight = 30;


    // =========================================================
    // RESOLUTION
    // =========================================================

    [Header("Resolution")]

    [SerializeField]
    private int heightmapResolution = 513;


    // =========================================================
    // GENERATED TERRAIN
    // =========================================================

    [Header("Generated Terrain")]

    [SerializeField]
    private Terrain generatedTerrain;


    // =========================================================
    // PUBLIC GETTER
    // =========================================================

    public Terrain GetGeneratedTerrain()
    {
        return generatedTerrain;
    }


    // =========================================================
    // CHECK TERRAIN
    // =========================================================

    public bool HasGeneratedTerrain()
    {
        return generatedTerrain != null;
    }


    // =========================================================
    // PUBLIC API
    // =========================================================

    public void GenerateTerrain(
        TerrainSettings settings
    )
    {
        // -----------------------------------------------------
        // VALIDATE
        // -----------------------------------------------------

        if (settings == null)
        {
            Debug.LogError(
                "TerrainGenerator: " +
                "Terrain settings are null."
            );

            return;
        }


        // -----------------------------------------------------
        // SAFETY LIMITS
        // -----------------------------------------------------

        settings.width =
            Mathf.Clamp(
                settings.width,
                20,
                1000
            );

        settings.depth =
            Mathf.Clamp(
                settings.depth,
                20,
                1000
            );

        settings.height =
            Mathf.Clamp(
                settings.height,
                1,
                500
            );

        settings.roughness =
            Mathf.Clamp01(
                settings.roughness
            );

        settings.detailScale =
            Mathf.Clamp(
                settings.detailScale,
                0.001f,
                0.2f
            );

        settings.octaves =
            Mathf.Clamp(
                settings.octaves,
                1,
                8
            );


        // -----------------------------------------------------
        // REMOVE OLD GENERATED TERRAIN
        // -----------------------------------------------------

        ClearTerrain();


        // =====================================================
        // GET EXISTING PLANE BOUNDS
        // =====================================================

        Bounds planeBounds =
            new Bounds();

        bool hasPlaneBounds = false;


        if (existingPlane != null)
        {
            Renderer planeRenderer =
                existingPlane.GetComponent<Renderer>();


            if (planeRenderer != null)
            {
                planeBounds =
                    planeRenderer.bounds;

                hasPlaneBounds = true;


                Debug.Log(
                    "[TerrainGenerator] " +
                    "Plane detected."
                );


                Debug.Log(
                    "[TerrainGenerator] " +
                    "Plane bounds: " +
                    planeBounds
                );
            }
            else
            {
                Debug.LogWarning(
                    "[TerrainGenerator] " +
                    "Existing Plane does not have a Renderer. " +
                    "Using TerrainSettings size instead."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "[TerrainGenerator] " +
                "Existing Plane is not assigned. " +
                "Using TerrainSettings size instead."
            );
        }


        // =====================================================
        // DETERMINE TERRAIN SIZE
        // =====================================================

        float terrainWidth =
            settings.width;


        float terrainDepth =
            settings.depth;


        if (
            matchPlaneSize &&
            hasPlaneBounds
        )
        {
            terrainWidth =
                Mathf.Max(
                    planeBounds.size.x,
                    20f
                );


            terrainDepth =
                Mathf.Max(
                    planeBounds.size.z,
                    20f
                );


            Debug.Log(
                "[TerrainGenerator] " +
                "Terrain size matched to Plane: " +
                terrainWidth +
                " x " +
                terrainDepth
            );
        }


        // =====================================================
        // CREATE TERRAIN DATA
        // =====================================================

        TerrainData terrainData =
            new TerrainData();


        terrainData.heightmapResolution =
            heightmapResolution;


        terrainData.size =
            new Vector3(
                terrainWidth,
                settings.height,
                terrainDepth
            );


        // -----------------------------------------------------
        // HEIGHTMAP
        // -----------------------------------------------------

        float[,] heights =
            GenerateHeightmap(
                settings
            );


        terrainData.SetHeights(
            0,
            0,
            heights
        );


        // =====================================================
        // CREATE TERRAIN GAMEOBJECT
        // =====================================================

        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(
                terrainData
            );


        terrainObject.name =
            "AI_Generated_Terrain";


        // =====================================================
        // POSITION TERRAIN
        // =====================================================

        if (
            matchPlanePosition &&
            hasPlaneBounds
        )
        {
            // Terrain transform.position represents
            // the minimum corner of the Terrain.
            //
            // Therefore use the Plane bounds minimum
            // for X and Z.
            //
            // Y is the base of the Terrain.

            terrainObject.transform.position =
                new Vector3(
                    planeBounds.min.x,
                    planeBounds.min.y,
                    planeBounds.min.z
                );


            Debug.Log(
                "[TerrainGenerator] " +
                "Terrain positioned at Plane bounds minimum: " +
                terrainObject.transform.position
            );
        }
        else if (terrainParent != null)
        {
            terrainObject.transform.SetParent(
                terrainParent,
                false
            );


            terrainObject.transform.localPosition =
                Vector3.zero;
        }


        // =====================================================
        // GET TERRAIN
        // =====================================================

        generatedTerrain =
            terrainObject.GetComponent<Terrain>();


        if (generatedTerrain == null)
        {
            Debug.LogError(
                "[TerrainGenerator] " +
                "Could not get Terrain component."
            );

            Destroy(
                terrainObject
            );

            return;
        }


        // =====================================================
        // TERRAIN COLLIDER
        // =====================================================
        //
        // Terrain.CreateTerrainGameObject() normally creates
        // a TerrainCollider automatically.
        //
        // We explicitly verify the collider because the
        // XR Character Controller must be able to collide
        // with the generated Terrain.
        //
        // =====================================================

        TerrainCollider terrainCollider =
            terrainObject.GetComponent<TerrainCollider>();


        if (terrainCollider == null)
        {
            Debug.LogWarning(
                "[TerrainGenerator] " +
                "TerrainCollider was missing. " +
                "Adding one now."
            );


            terrainCollider =
                terrainObject.AddComponent<TerrainCollider>();
        }


        // -----------------------------------------------------
        // CONNECT COLLIDER TO TERRAIN DATA
        // -----------------------------------------------------

        terrainCollider.terrainData =
            generatedTerrain.terrainData;


        // -----------------------------------------------------
        // ENABLE COLLIDER
        // -----------------------------------------------------

        terrainCollider.enabled = true;


        // -----------------------------------------------------
        // DEBUG COLLIDER
        // -----------------------------------------------------

        Debug.Log(
            "[TerrainGenerator] " +
            "TerrainCollider configured."
        );


        Debug.Log(
            "[TerrainGenerator] " +
            "Collider enabled = " +
            terrainCollider.enabled
        );


        Debug.Log(
            "[TerrainGenerator] " +
            "Collider TerrainData = " +
            terrainCollider.terrainData
        );


        Debug.Log(
            "[TerrainGenerator] " +
            "Terrain position = " +
            terrainObject.transform.position
        );


        Debug.Log(
            "[TerrainGenerator] " +
            "Terrain size = " +
            generatedTerrain.terrainData.size
        );


        // =====================================================
        // TERRAIN SETTINGS
        // =====================================================

        generatedTerrain.heightmapPixelError =
            5;


        generatedTerrain.basemapDistance =
            1000;


        // =====================================================
        // HIDE EXISTING PLANE
        // =====================================================

        if (
            hidePlaneWhenTerrainGenerated &&
            existingPlane != null
        )
        {
            existingPlane.SetActive(false);


            Debug.Log(
                "[TerrainGenerator] " +
                "Existing Plane hidden."
            );
        }


        // =====================================================
        // DEBUG
        // =====================================================

        Debug.Log(
            $"AI Terrain generated: " +
            $"{settings.terrainType}, " +
            $"{terrainWidth} x {terrainDepth}, " +
            $"height {settings.height}"
        );


        Debug.Log(
            "[TerrainGenerator] " +
            "Terrain generation complete. " +
            "Terrain replaces the Plane. " +
            "TerrainCollider is enabled. " +
            "No soil was created."
        );
    }


    // =========================================================
    // HEIGHTMAP
    // =========================================================

    private float[,] GenerateHeightmap(
        TerrainSettings settings
    )
    {
        int resolution =
            heightmapResolution;


        float[,] heights =
            new float[
                resolution,
                resolution
            ];


        float offsetX =
            settings.seed *
            13.37f;


        float offsetZ =
            settings.seed *
            7.91f;


        string terrainType =
            settings.terrainType == null
                ? "hills"
                : settings.terrainType
                    .ToLower()
                    .Trim();


        for (
            int z = 0;
            z < resolution;
            z++
        )
        {
            for (
                int x = 0;
                x < resolution;
                x++
            )
            {
                float normalizedX =
                    (float)x /
                    (resolution - 1);


                float normalizedZ =
                    (float)z /
                    (resolution - 1);


                float height = 0f;


                switch (terrainType)
                {
                    case "flat":

                        height =
                            GenerateFlatTerrain(
                                normalizedX,
                                normalizedZ
                            );

                        break;


                    case "hills":

                        height =
                            GenerateHills(
                                normalizedX,
                                normalizedZ,
                                settings,
                                offsetX,
                                offsetZ
                            );

                        break;


                    case "mountain":
                    case "mountains":

                        height =
                            GenerateMountains(
                                normalizedX,
                                normalizedZ,
                                settings,
                                offsetX,
                                offsetZ
                            );

                        break;


                    case "valley":

                        height =
                            GenerateValley(
                                normalizedX,
                                normalizedZ,
                                settings,
                                offsetX,
                                offsetZ
                            );

                        break;


                    case "island":

                        height =
                            GenerateIsland(
                                normalizedX,
                                normalizedZ,
                                settings,
                                offsetX,
                                offsetZ
                            );

                        break;


                    case "desert":

                        height =
                            GenerateDesert(
                                normalizedX,
                                normalizedZ,
                                settings,
                                offsetX,
                                offsetZ
                            );

                        break;


                    case "canyon":

                        height =
                            GenerateCanyon(
                                normalizedX,
                                normalizedZ,
                                settings,
                                offsetX,
                                offsetZ
                            );

                        break;


                    default:

                        Debug.LogWarning(
                            "TerrainGenerator: " +
                            "Unknown terrain type '" +
                            terrainType +
                            "'. Using hills."
                        );


                        height =
                            GenerateHills(
                                normalizedX,
                                normalizedZ,
                                settings,
                                offsetX,
                                offsetZ
                            );

                        break;
                }


                heights[z, x] =
                    Mathf.Clamp01(
                        height
                    );
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


        noise =
            Mathf.Pow(
                noise,
                1.5f
            );


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
            Mathf.Abs(
                x - 0.5f
            );


        float valleyShape =
            1f -
            Mathf.Clamp01(
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
            1f -
            Mathf.Clamp01(
                distance * 2f
            );


        islandMask =
            Mathf.Pow(
                islandMask,
                1.5f
            );


        return noise *
               islandMask;
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


        noise =
            Mathf.Pow(
                noise,
                1.3f
            );


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


        float canyonCenter =
            0.5f +
            Mathf.Sin(
                z * 8f
            ) * 0.08f;


        float distance =
            Mathf.Abs(
                x - canyonCenter
            );


        float canyonWidth =
            0.08f;


        if (
            distance <
            canyonWidth
        )
        {
            float canyonDepth =
                1f -
                (
                    distance /
                    canyonWidth
                );


            noise -=
                canyonDepth *
                0.6f;
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


        for (
            int i = 0;
            i < octaves;
            i++
        )
        {
            float sampleX =
                x *
                scale *
                frequency *
                100f
                +
                offsetX;


            float sampleZ =
                z *
                scale *
                frequency *
                100f
                +
                offsetZ;


            float noise =
                Mathf.PerlinNoise(
                    sampleX,
                    sampleZ
                );


            total +=
                noise *
                amplitude;


            maximum +=
                amplitude;


            amplitude *=
                Mathf.Lerp(
                    0.5f,
                    0.9f,
                    roughness
                );


            frequency *=
                2f;
        }


        if (
            maximum <= 0f
        )
        {
            return 0f;
        }


        return total /
               maximum;
    }


    // =========================================================
    // CLEAR TERRAIN
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


        Transform parent =
            terrainParent != null
                ? terrainParent
                : transform;


        for (
            int i =
                parent.childCount - 1;

            i >= 0;

            i--
        )
        {
            Transform child =
                parent.GetChild(i);


            if (
                child.name ==
                "AI_Generated_Terrain"
            )
            {
                Destroy(
                    child.gameObject
                );
            }
        }


        Debug.Log(
            "[TerrainGenerator] " +
            "Generated terrain cleared."
        );
    }


    // =========================================================
    // RESTORE ORIGINAL PLANE
    // =========================================================

    public void RestoreOriginalPlane()
    {
        if (existingPlane != null)
        {
            existingPlane.SetActive(true);


            Debug.Log(
                "[TerrainGenerator] " +
                "Original Plane restored."
            );
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


        GenerateTerrain(
            settings
        );
    }
}