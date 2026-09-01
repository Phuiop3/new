using UnityEngine;
using System.Collections.Generic;

// ============================================================
// TERRAIN GENERATOR
// ============================================================
//
// GENERATES:
//
//      🏔️ REALISTIC OVERLAPPING MOUNTAIN RANGES
//      🌊 RIVERS
//      🏞️ LAKES
//
// IMPORTANT
// ------------------------------------------------------------
// Mountains can overlap each other.
//
// Rivers and lakes are protected from mountain areas.
//
// PUBLIC METHODS FOR DEMOCHAT:
// ------------------------------------------------------------
//
//      GenerateTerrain()
//      CreateRandomMountains()
//      CreateMountainRange()
//      CreateRandomLake()
//      CreateRandomRiver()
//      ResetTerrain()
//
// KEYBOARD:
// ------------------------------------------------------------
//
//      G = Generate Mountains + Rivers + Lakes
//      M = Generate Mountain Range
//      L = Generate Lake
//      R = Generate River
//      C = Reset
//
// PLAY MODE:
// ------------------------------------------------------------
//
// Original terrain is saved when Play Mode starts.
//
// When Play Mode stops:
//      Original terrain is restored.
//
// ============================================================


public class TerrainGenerator : MonoBehaviour
{
    // ========================================================
    // EXISTING TERRAIN
    // ========================================================

    [Header("EXISTING TERRAIN")]

    [Tooltip("Drag your existing Unity Terrain here.")]
    [SerializeField]
    private Terrain targetTerrain;


    // ========================================================
    // AUTOMATIC GENERATION
    // ========================================================

    [Header("AUTOMATIC GENERATION")]

    [SerializeField]
    private bool generateAutomatically = true;

    [SerializeField]
    private int automaticMountainRangeCount = 2;

    [SerializeField]
    private int automaticLakes = 2;

    [SerializeField]
    private bool automaticRiver = true;


    // ========================================================
    // MOUNTAIN RANGE
    // ========================================================

    [Header("MOUNTAIN RANGE")]

    [Tooltip("Number of peaks inside each mountain range.")]
    [SerializeField]
    private int peaksPerRange = 5;

    [Tooltip("Minimum mountain peak height.")]
    [SerializeField]
    private float minimumMountainHeight = 12f;

    [Tooltip("Maximum mountain peak height.")]
    [SerializeField]
    private float maximumMountainHeight = 25f;

    [Tooltip("Minimum mountain radius.")]
    [SerializeField]
    private float minimumMountainRadius = 18f;

    [Tooltip("Maximum mountain radius.")]
    [SerializeField]
    private float maximumMountainRadius = 35f;

    [Tooltip("Distance between mountain peaks.")]
    [SerializeField]
    private float mountainPeakSpacing = 25f;

    [Tooltip("How much mountains overlap.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float mountainOverlap = 0.45f;

    [Tooltip("Sideways movement of mountain range.")]
    [SerializeField]
    private float mountainWander = 20f;

    [Tooltip("Extra protected area around mountains.")]
    [SerializeField]
    private float mountainProtectionPadding = 8f;


    // ========================================================
    // MOUNTAIN SHAPE
    // ========================================================

    [Header("MOUNTAIN SHAPE")]

    [Tooltip("Higher value creates sharper peaks.")]
    [SerializeField]
    private float mountainSharpness = 2.4f;

    [Tooltip("Mountain surface noise scale.")]
    [SerializeField]
    private float mountainNoiseScale = 0.035f;

    [Tooltip("Mountain surface noise strength.")]
    [SerializeField]
    private float mountainNoiseStrength = 0.25f;


    // ========================================================
    // LAKES
    // ========================================================

    [Header("LAKES")]

    [SerializeField]
    private float minimumLakeRadius = 10f;

    [SerializeField]
    private float maximumLakeRadius = 22f;

    [SerializeField]
    private float minimumLakeDepth = 2f;

    [SerializeField]
    private float maximumLakeDepth = 5f;

    [SerializeField]
    private float lakeWaterThickness = 0.2f;

    [SerializeField]
    private float lakeWaterLevelOffset = 0.08f;

    [Tooltip("Minimum distance between lakes.")]
    [SerializeField]
    private float lakeSpacing = 8f;


    // ========================================================
    // RIVER
    // ========================================================

    [Header("RIVER")]

    [SerializeField]
    private float minimumRiverWidth = 3f;

    [SerializeField]
    private float maximumRiverWidth = 7f;

    [SerializeField]
    private float minimumRiverDepth = 1.5f;

    [SerializeField]
    private float maximumRiverDepth = 3f;

    [SerializeField]
    private int riverSegments = 25;

    [SerializeField]
    private float riverSegmentLength = 10f;

    [SerializeField]
    private float riverWanderAmount = 7f;

    [SerializeField]
    private float riverWaterThickness = 0.15f;

    [SerializeField]
    private float riverWaterLevelOffset = 0.05f;


    // ========================================================
    // TERRAIN TEXTURES
    // ========================================================

    [Header("TERRAIN TEXTURES")]

    [Tooltip("Grass texture.")]
    [SerializeField]
    private Texture2D grassTexture;

    [Tooltip("Rock texture.")]
    [SerializeField]
    private Texture2D rockTexture;

    [Tooltip("Snow texture.")]
    [SerializeField]
    private Texture2D snowTexture;

    [SerializeField]
    private bool automaticallyCreateTerrainLayers = true;

    [SerializeField]
    private bool automaticallyPaintTerrain = true;


    // ========================================================
    // TEXTURE SETTINGS
    // ========================================================

    [Header("TEXTURE SETTINGS")]

    [SerializeField]
    private float rockStartSlope = 25f;

    [SerializeField]
    private float rockFullSlope = 45f;

    [Range(0f, 1f)]
    [SerializeField]
    private float snowStartHeight = 0.70f;

    [Range(0f, 1f)]
    [SerializeField]
    private float snowFullHeight = 0.90f;

    [SerializeField]
    private Vector2 textureTileSize =
        new Vector2(15f, 15f);

    [SerializeField]
    private Vector2 textureTileOffset =
        Vector2.zero;

    [SerializeField]
    private bool configureURP = true;


    // ========================================================
    // WATER
    // ========================================================

    [Header("WATER")]

    [Tooltip("Optional water material.")]
    [SerializeField]
    private Material waterMaterial;


    // ========================================================
    // INTERNAL WATER PARENT
    // ========================================================

    private GameObject generatedWaterParent;


    // ========================================================
    // ORIGINAL TERRAIN
    // ========================================================

    private float[,] originalHeights;

    private bool originalHeightsSaved;


    // ========================================================
    // TERRAIN LAYERS
    // ========================================================

    private TerrainLayer grassLayer;

    private TerrainLayer rockLayer;

    private TerrainLayer snowLayer;


    // ========================================================
    // MOUNTAIN ZONE
    // ========================================================

    private class MountainZone
    {
        public Vector2 center;

        public float radius;


        public MountainZone(
            Vector2 center,
            float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }


    private List<MountainZone> mountainZones =
        new List<MountainZone>();


    // ========================================================
    // WATER FEATURE
    // ========================================================

    private class WaterFeature
    {
        public Vector2 position;

        public float radius;


        public WaterFeature(
            Vector2 position,
            float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }


    private List<WaterFeature> waterFeatures =
        new List<WaterFeature>();


    // ========================================================
    // PUBLIC ACCESS
    // ========================================================

    public Terrain GetGeneratedTerrain()
    {
        return targetTerrain;
    }


    public Terrain GetTargetTerrain()
    {
        return targetTerrain;
    }


    // ========================================================
    // START
    // ========================================================

    private void Start()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        SaveOriginalTerrain();

        CreateWaterParent();

        FindAndCreateTerrainLayers();

        ConfigureTerrainForURP();


        if (generateAutomatically)
        {
            GenerateTerrain();
        }
        else
        {
            ApplyTerrainTextures();
        }
    }


    // ========================================================
    // DESTROY
    // ========================================================

    private void OnDestroy()
    {
        RestoreOriginalTerrain();
    }


    // ========================================================
    // GENERATE EVERYTHING
    // ========================================================

    public void GenerateTerrain()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        Debug.Log(
            "============================================\n" +
            "TERRAIN GENERATION STARTED\n" +
            "============================================"
        );


        FindAndCreateTerrainLayers();

        ConfigureTerrainForURP();

        mountainZones.Clear();

        waterFeatures.Clear();

        ClearGeneratedWater();


        // ----------------------------------------------------
        // MOUNTAINS
        // ----------------------------------------------------

        int mountainRanges =
            Mathf.Clamp(
                automaticMountainRangeCount,
                0,
                10
            );


        CreateRandomMountains(
            mountainRanges
        );


        // ----------------------------------------------------
        // LAKES
        // ----------------------------------------------------

        int lakeCount =
            Mathf.Clamp(
                automaticLakes,
                0,
                20
            );


        for (
            int i = 0;
            i < lakeCount;
            i++)
        {
            CreateRandomLake();
        }


        // ----------------------------------------------------
        // RIVER
        // ----------------------------------------------------

        if (automaticRiver)
        {
            CreateRandomRiver();
        }


        // ----------------------------------------------------
        // TEXTURES
        // ----------------------------------------------------

        ApplyTerrainTextures();

        RefreshTerrainCollider();


        Debug.Log(
            "============================================\n" +
            "TERRAIN GENERATION COMPLETE\n" +
            "============================================\n" +
            $"Mountain ranges: {mountainRanges}\n" +
            $"Lakes: {lakeCount}\n" +
            $"River: {automaticRiver}\n" +
            "============================================"
        );
    }


    // ========================================================
    // CREATE RANDOM MOUNTAINS
    // ========================================================
    //
    // IMPORTANT:
    //
    // This method fixes the DemoChat error:
    //
    //     TerrainGenerator does not contain
    //     CreateRandomMountains
    //
    // DemoChat can safely call:
    //
    //     terrainGenerator.CreateRandomMountains();
    //
    // ========================================================

    public void CreateRandomMountains()
    {
        CreateRandomMountains(
            automaticMountainRangeCount
        );
    }


    // ========================================================
    // CREATE RANDOM MOUNTAINS
    // ========================================================

    public void CreateRandomMountains(
        int numberOfRanges
    )
    {
        if (!HasValidTerrain())
        {
            return;
        }


        numberOfRanges =
            Mathf.Clamp(
                numberOfRanges,
                1,
                10
            );


        for (
            int i = 0;
            i < numberOfRanges;
            i++
        )
        {
            CreateMountainRange();
        }


        ApplyTerrainTextures();

        RefreshTerrainCollider();


        Debug.Log(
            $"Created {numberOfRanges} " +
            "random mountain range(s)."
        );
    }


    // ========================================================
    // CREATE MOUNTAIN RANGE
    // ========================================================

    public void CreateMountainRange()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        TerrainData terrainData =
            targetTerrain.terrainData;


        int peakCount =
            Mathf.Clamp(
                peaksPerRange,
                2,
                15
            );


        // ----------------------------------------------------
        // START POSITION
        // ----------------------------------------------------

        float firstRadius =
            Random.Range(
                minimumMountainRadius,
                maximumMountainRadius
            );


        Vector2 start =
            GetRandomTerrainPosition(
                firstRadius +
                mountainProtectionPadding
            );


        // ----------------------------------------------------
        // RANGE DIRECTION
        // ----------------------------------------------------

        Vector2 direction =
            Random.value > 0.5f
            ? Vector2.right
            : Vector2.up;


        direction =
            RotateVector(
                direction,
                Random.Range(
                    -35f,
                    35f
                )
            );


        Vector2 current =
            start;


        // ----------------------------------------------------
        // CREATE PEAKS
        // ----------------------------------------------------

        for (
            int i = 0;
            i < peakCount;
            i++
        )
        {
            float radius =
                Random.Range(
                    minimumMountainRadius,
                    maximumMountainRadius
                );


            float height =
                Random.Range(
                    minimumMountainHeight,
                    maximumMountainHeight
                );


            // ------------------------------------------------
            // MOVE TO NEXT PEAK
            // ------------------------------------------------

            if (i > 0)
            {
                float sideways =
                    Random.Range(
                        -mountainWander,
                        mountainWander
                    );


                Vector2 perpendicular =
                    new Vector2(
                        -direction.y,
                        direction.x
                    );


                float forwardDistance =
                    mountainPeakSpacing *
                    (
                        1f -
                        mountainOverlap
                    );


                current +=
                    direction.normalized *
                    forwardDistance;


                current +=
                    perpendicular *
                    sideways;
            }


            // ------------------------------------------------
            // KEEP INSIDE TERRAIN
            // ------------------------------------------------

            float edge =
                radius +
                mountainProtectionPadding;


            current.x =
                Mathf.Clamp(
                    current.x,
                    edge,
                    terrainData.size.x - edge
                );


            current.y =
                Mathf.Clamp(
                    current.y,
                    edge,
                    terrainData.size.z - edge
                );


            // ------------------------------------------------
            // CREATE MOUNTAIN
            // ------------------------------------------------

            ModifyTerrainArea(
                current,
                radius,
                height,
                false,
                true
            );


            // ------------------------------------------------
            // REGISTER PROTECTION ZONE
            // ------------------------------------------------

            mountainZones.Add(
                new MountainZone(
                    current,
                    radius +
                    mountainProtectionPadding
                )
            );
        }


        Debug.Log(
            $"Mountain range created. " +
            $"Peaks = {peakCount}"
        );
    }


    // ========================================================
    // CREATE RANDOM LAKE
    // ========================================================

    public void CreateRandomLake()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        CreateWaterParent();


        float radius =
            Random.Range(
                minimumLakeRadius,
                maximumLakeRadius
            );


        float depth =
            Random.Range(
                minimumLakeDepth,
                maximumLakeDepth
            );


        Vector2 position;


        if (!TryGetWaterPosition(
                radius,
                out position))
        {
            Debug.LogWarning(
                "Could not place lake " +
                "outside mountains."
            );

            return;
        }


        // ----------------------------------------------------
        // LOWER TERRAIN
        // ----------------------------------------------------

        ModifyTerrainArea(
            position,
            radius,
            depth,
            true,
            false
        );


        // ----------------------------------------------------
        // WORLD POSITION
        // ----------------------------------------------------

        Vector3 worldPosition =
            TerrainLocalToWorld(
                position.x,
                position.y
            );


        float terrainHeight =
            targetTerrain.SampleHeight(
                worldPosition
            );


        float waterHeight =
            terrainHeight +
            lakeWaterLevelOffset;


        // ----------------------------------------------------
        // CREATE LAKE
        // ----------------------------------------------------

        GameObject lake =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );


        lake.name =
            "Lake_" +
            Random.Range(
                1000,
                9999
            );


        lake.transform.SetParent(
            generatedWaterParent.transform
        );


        lake.transform.position =
            new Vector3(
                worldPosition.x,
                waterHeight,
                worldPosition.z
            );


        float diameter =
            radius * 2f;


        lake.transform.localScale =
            new Vector3(
                diameter,
                lakeWaterThickness,
                diameter
            );


        Collider collider =
            lake.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(collider);
        }


        ApplyWaterMaterial(lake);


        waterFeatures.Add(
            new WaterFeature(
                position,
                radius +
                lakeSpacing
            )
        );


        Debug.Log(
            $"Lake created. " +
            $"Radius = {radius:F1}"
        );
    }


    // ========================================================
    // CREATE RANDOM RIVER
    // ========================================================

    public void CreateRandomRiver()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        CreateWaterParent();


        int segmentCount =
            Mathf.Clamp(
                riverSegments,
                4,
                60
            );


        float width =
            Random.Range(
                minimumRiverWidth,
                maximumRiverWidth
            );


        float depth =
            Random.Range(
                minimumRiverDepth,
                maximumRiverDepth
            );


        // ----------------------------------------------------
        // FIND SAFE RIVER PATH
        // ----------------------------------------------------

        for (
            int attempt = 0;
            attempt < 50;
            attempt++
        )
        {
            Vector2 start =
                GetRandomTerrainPosition(
                    width + 5f
                );


            List<Vector2> points =
                BuildRiverPath(
                    start,
                    width,
                    segmentCount
                );


            if (
                points == null ||
                points.Count < 2
            )
            {
                continue;
            }


            if (
                RiverTouchesMountain(
                    points,
                    width
                )
            )
            {
                continue;
            }


            // ------------------------------------------------
            // CUT RIVER INTO TERRAIN
            // ------------------------------------------------

            for (
                int i = 0;
                i < points.Count;
                i++
            )
            {
                ModifyTerrainArea(
                    points[i],
                    width,
                    depth,
                    true,
                    false
                );
            }


            // ------------------------------------------------
            // CREATE WATER
            // ------------------------------------------------

            for (
                int i = 0;
                i < points.Count - 1;
                i++
            )
            {
                CreateRiverWaterSegment(
                    points[i],
                    points[i + 1],
                    width
                );
            }


            foreach (
                Vector2 point
                in points
            )
            {
                waterFeatures.Add(
                    new WaterFeature(
                        point,
                        width
                    )
                );
            }


            Debug.Log(
                $"River created. " +
                $"Segments = {points.Count}, " +
                $"Width = {width:F1}"
            );


            return;
        }


        Debug.LogWarning(
            "Could not find river path " +
            "outside mountains."
        );
    }


    // ========================================================
    // BUILD RIVER PATH
    // ========================================================

    private List<Vector2> BuildRiverPath(
        Vector2 start,
        float width,
        int segmentCount
    )
    {
        TerrainData terrainData =
            targetTerrain.terrainData;


        List<Vector2> points =
            new List<Vector2>();


        Vector2 current =
            start;


        Vector2 direction =
            Random.value > 0.5f
            ? Vector2.right
            : Vector2.up;


        direction =
            RotateVector(
                direction,
                Random.Range(
                    -25f,
                    25f
                )
            );


        points.Add(current);


        for (
            int i = 1;
            i < segmentCount;
            i++
        )
        {
            float angle =
                Random.Range(
                    -20f,
                    20f
                );


            direction =
                RotateVector(
                    direction,
                    angle
                );


            float wander =
                Random.Range(
                    -riverWanderAmount,
                    riverWanderAmount
                );


            Vector2 sideways =
                new Vector2(
                    -direction.y,
                    direction.x
                );


            Vector2 next =
                current +
                direction.normalized *
                riverSegmentLength;


            next +=
                sideways.normalized *
                wander;


            float edge =
                width + 2f;


            next.x =
                Mathf.Clamp(
                    next.x,
                    edge,
                    terrainData.size.x - edge
                );


            next.y =
                Mathf.Clamp(
                    next.y,
                    edge,
                    terrainData.size.z - edge
                );


            points.Add(next);


            current = next;
        }


        return points;
    }


    // ========================================================
    // RIVER VS MOUNTAIN
    // ========================================================

    private bool RiverTouchesMountain(
        List<Vector2> points,
        float riverWidth
    )
    {
        foreach (
            Vector2 point
            in points
        )
        {
            foreach (
                MountainZone mountain
                in mountainZones
            )
            {
                float distance =
                    Vector2.Distance(
                        point,
                        mountain.center
                    );


                if (
                    distance <
                    mountain.radius +
                    riverWidth
                )
                {
                    return true;
                }
            }
        }


        return false;
    }


    // ========================================================
    // WATER POSITION
    // ========================================================

    private bool TryGetWaterPosition(
        float radius,
        out Vector2 position
    )
    {
        for (
            int attempt = 0;
            attempt < 100;
            attempt++
        )
        {
            Vector2 candidate =
                GetRandomTerrainPosition(
                    radius +
                    mountainProtectionPadding
                );


            // ------------------------------------------------
            // NEVER PLACE WATER IN MOUNTAINS
            // ------------------------------------------------

            if (
                IsInsideMountain(
                    candidate,
                    radius
                )
            )
            {
                continue;
            }


            // ------------------------------------------------
            // KEEP WATER FEATURES APART
            // ------------------------------------------------

            if (
                IsTooCloseToWater(
                    candidate,
                    radius
                )
            )
            {
                continue;
            }


            position =
                candidate;


            return true;
        }


        position =
            Vector2.zero;


        return false;
    }


    // ========================================================
    // IS INSIDE MOUNTAIN
    // ========================================================

    private bool IsInsideMountain(
        Vector2 position,
        float radius
    )
    {
        foreach (
            MountainZone mountain
            in mountainZones
        )
        {
            float distance =
                Vector2.Distance(
                    position,
                    mountain.center
                );


            if (
                distance <
                mountain.radius +
                radius
            )
            {
                return true;
            }
        }


        return false;
    }


    // ========================================================
    // WATER SPACING
    // ========================================================

    private bool IsTooCloseToWater(
        Vector2 position,
        float radius
    )
    {
        foreach (
            WaterFeature feature
            in waterFeatures
        )
        {
            float distance =
                Vector2.Distance(
                    position,
                    feature.position
                );


            if (
                distance <
                radius +
                feature.radius
            )
            {
                return true;
            }
        }


        return false;
    }


    // ========================================================
    // MODIFY TERRAIN
    // ========================================================

    private void ModifyTerrainArea(
        Vector2 position,
        float radius,
        float amount,
        bool isLowering,
        bool isMountain
    )
    {
        if (!HasValidTerrain())
        {
            return;
        }


        TerrainData terrainData =
            targetTerrain.terrainData;


        int resolution =
            terrainData.heightmapResolution;


        int centerX =
            Mathf.RoundToInt(
                position.x /
                terrainData.size.x *
                (resolution - 1)
            );


        int centerZ =
            Mathf.RoundToInt(
                position.y /
                terrainData.size.z *
                (resolution - 1)
            );


        int radiusSamplesX =
            Mathf.CeilToInt(
                radius /
                terrainData.size.x *
                (resolution - 1)
            );


        int radiusSamplesZ =
            Mathf.CeilToInt(
                radius /
                terrainData.size.z *
                (resolution - 1)
            );


        radiusSamplesX =
            Mathf.Max(
                radiusSamplesX,
                1
            );


        radiusSamplesZ =
            Mathf.Max(
                radiusSamplesZ,
                1
            );


        int startX =
            Mathf.Max(
                0,
                centerX -
                radiusSamplesX
            );


        int endX =
            Mathf.Min(
                resolution - 1,
                centerX +
                radiusSamplesX
            );


        int startZ =
            Mathf.Max(
                0,
                centerZ -
                radiusSamplesZ
            );


        int endZ =
            Mathf.Min(
                resolution - 1,
                centerZ +
                radiusSamplesZ
            );


        int width =
            endX -
            startX +
            1;


        int height =
            endZ -
            startZ +
            1;


        float[,] heights =
            terrainData.GetHeights(
                startX,
                startZ,
                width,
                height
            );


        for (
            int z = 0;
            z < height;
            z++
        )
        {
            for (
                int x = 0;
                x < width;
                x++
            )
            {
                int absoluteX =
                    startX + x;


                int absoluteZ =
                    startZ + z;


                float normalizedX =
                    (
                        absoluteX -
                        centerX
                    ) /
                    (float)
                    radiusSamplesX;


                float normalizedZ =
                    (
                        absoluteZ -
                        centerZ
                    ) /
                    (float)
                    radiusSamplesZ;


                float distance =
                    Mathf.Sqrt(
                        normalizedX *
                        normalizedX +
                        normalizedZ *
                        normalizedZ
                    );


                if (distance > 1f)
                {
                    continue;
                }


                // ------------------------------------------------
                // SMOOTH FALLOFF
                // ------------------------------------------------

                float falloff =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        distance
                    );


                float shape;


                // ------------------------------------------------
                // MOUNTAIN
                // ------------------------------------------------

                if (isMountain)
                {
                    shape =
                        Mathf.Pow(
                            falloff,
                            mountainSharpness
                        );


                    // ------------------------------------------------
                    // IRREGULAR SURFACE
                    // ------------------------------------------------

                    float noise =
                        Mathf.PerlinNoise(
                            absoluteX *
                            mountainNoiseScale,

                            absoluteZ *
                            mountainNoiseScale
                        );


                    noise =
                        Mathf.Lerp(
                            1f -
                            mountainNoiseStrength,

                            1f +
                            mountainNoiseStrength,

                            noise
                        );


                    shape *= noise;
                }


                // ------------------------------------------------
                // WATER
                // ------------------------------------------------

                else
                {
                    shape =
                        Mathf.Pow(
                            falloff,
                            1.5f
                        );
                }


                float normalizedAmount =
                    amount /
                    terrainData.size.y;


                float modification =
                    normalizedAmount *
                    shape;


                if (isLowering)
                {
                    modification =
                        -Mathf.Abs(
                            modification
                        );
                }


                heights[z, x] +=
                    modification;


                heights[z, x] =
                    Mathf.Clamp01(
                        heights[z, x]
                    );
            }
        }


        terrainData.SetHeights(
            startX,
            startZ,
            heights
        );
    }


    // ========================================================
    // RANDOM TERRAIN POSITION
    // ========================================================

    private Vector2 GetRandomTerrainPosition(
        float margin
    )
    {
        TerrainData terrainData =
            targetTerrain.terrainData;


        margin =
            Mathf.Max(
                margin,
                1f
            );


        float minX =
            margin;


        float maxX =
            terrainData.size.x -
            margin;


        float minZ =
            margin;


        float maxZ =
            terrainData.size.z -
            margin;


        if (maxX <= minX)
        {
            minX = 0f;
            maxX = terrainData.size.x;
        }


        if (maxZ <= minZ)
        {
            minZ = 0f;
            maxZ = terrainData.size.z;
        }


        return new Vector2(
            Random.Range(
                minX,
                maxX
            ),

            Random.Range(
                minZ,
                maxZ
            )
        );
    }


    // ========================================================
    // TERRAIN LOCAL -> WORLD
    // ========================================================

    private Vector3 TerrainLocalToWorld(
        float localX,
        float localZ
    )
    {
        Vector3 terrainPosition =
            targetTerrain.transform.position;


        Vector3 sample =
            terrainPosition +
            new Vector3(
                localX,
                0f,
                localZ
            );


        float height =
            targetTerrain.SampleHeight(
                sample
            );


        return terrainPosition +
               new Vector3(
                   localX,
                   height,
                   localZ
               );
    }


    // ========================================================
    // CREATE RIVER WATER
    // ========================================================

    private void CreateRiverWaterSegment(
        Vector2 start,
        Vector2 end,
        float width
    )
    {
        Vector2 middle =
            (start + end) *
            0.5f;


        Vector2 difference =
            end - start;


        float length =
            difference.magnitude;


        if (length <= 0.01f)
        {
            return;
        }


        Vector3 worldStart =
            TerrainLocalToWorld(
                start.x,
                start.y
            );


        Vector3 worldEnd =
            TerrainLocalToWorld(
                end.x,
                end.y
            );


        Vector3 worldMiddle =
            TerrainLocalToWorld(
                middle.x,
                middle.y
            );


        float heightStart =
            targetTerrain.SampleHeight(
                worldStart
            );


        float heightEnd =
            targetTerrain.SampleHeight(
                worldEnd
            );


        float heightMiddle =
            targetTerrain.SampleHeight(
                worldMiddle
            );


        float waterHeight =
            (
                heightStart +
                heightEnd +
                heightMiddle
            )
            / 3f;


        waterHeight +=
            riverWaterLevelOffset;


        GameObject river =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );


        river.name =
            "RiverWater";


        river.transform.SetParent(
            generatedWaterParent.transform
        );


        river.transform.position =
            new Vector3(
                worldMiddle.x,
                waterHeight,
                worldMiddle.z
            );


        float angle =
            Mathf.Atan2(
                difference.x,
                difference.y
            )
            *
            Mathf.Rad2Deg;


        river.transform.rotation =
            Quaternion.Euler(
                0f,
                angle,
                0f
            );


        river.transform.localScale =
            new Vector3(
                width,
                riverWaterThickness,
                length
            );


        Collider collider =
            river.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(collider);
        }


        ApplyWaterMaterial(river);
    }


    // ========================================================
    // WATER MATERIAL
    // ========================================================

    private void ApplyWaterMaterial(
        GameObject waterObject
    )
    {
        Renderer renderer =
            waterObject.GetComponent<Renderer>();


        if (renderer == null)
        {
            return;
        }


        if (waterMaterial != null)
        {
            renderer.material =
                waterMaterial;

            return;
        }


        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Standard"
                );
        }


        if (shader == null)
        {
            return;
        }


        Material material =
            new Material(shader);


        material.name =
            "Generated Water Material";


        material.color =
            new Color(
                0.05f,
                0.35f,
                0.8f,
                0.75f
            );


        if (
            shader.name.Contains(
                "Universal Render Pipeline"
            )
        )
        {
            material.SetFloat(
                "_Surface",
                1f
            );


            material.SetFloat(
                "_Blend",
                0f
            );


            material.SetFloat(
                "_AlphaClip",
                0f
            );


            material.renderQueue =
                3000;
        }


        renderer.material =
            material;
    }


    // ========================================================
    // TERRAIN LAYERS
    // ========================================================

    private void FindAndCreateTerrainLayers()
    {
        if (!automaticallyCreateTerrainLayers)
        {
            return;
        }


        if (grassTexture != null)
        {
            grassLayer =
                CreateTerrainLayer(
                    grassTexture,
                    "Automatic_Grass"
                );
        }


        if (rockTexture != null)
        {
            rockLayer =
                CreateTerrainLayer(
                    rockTexture,
                    "Automatic_Rock"
                );
        }


        if (snowTexture != null)
        {
            snowLayer =
                CreateTerrainLayer(
                    snowTexture,
                    "Automatic_Snow"
                );
        }


        AssignTerrainLayers();
    }


    // ========================================================
    // CREATE TERRAIN LAYER
    // ========================================================

    private TerrainLayer CreateTerrainLayer(
        Texture2D texture,
        string layerName
    )
    {
        TerrainLayer layer =
            new TerrainLayer();


        layer.name =
            layerName;


        layer.diffuseTexture =
            texture;


        layer.tileSize =
            textureTileSize;


        layer.tileOffset =
            textureTileOffset;


        layer.smoothness =
            0f;


        layer.metallic =
            0f;


        return layer;
    }


    // ========================================================
    // ASSIGN TERRAIN LAYERS
    // ========================================================

    private void AssignTerrainLayers()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        List<TerrainLayer> layers =
            new List<TerrainLayer>();


        if (grassLayer != null)
        {
            layers.Add(grassLayer);
        }


        if (rockLayer != null)
        {
            layers.Add(rockLayer);
        }


        if (snowLayer != null)
        {
            layers.Add(snowLayer);
        }


        if (layers.Count == 0)
        {
            return;
        }


        targetTerrain
            .terrainData
            .terrainLayers =
            layers.ToArray();
    }


    // ========================================================
    // APPLY TERRAIN TEXTURES
    // ========================================================

    private void ApplyTerrainTextures()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        if (!automaticallyPaintTerrain)
        {
            return;
        }


        FindAndCreateTerrainLayers();


        TerrainData terrainData =
            targetTerrain.terrainData;


        List<TerrainLayer> layers =
            new List<TerrainLayer>();


        if (grassLayer != null)
        {
            layers.Add(grassLayer);
        }


        if (rockLayer != null)
        {
            layers.Add(rockLayer);
        }


        if (snowLayer != null)
        {
            layers.Add(snowLayer);
        }


        if (layers.Count == 0)
        {
            return;
        }


        terrainData.terrainLayers =
            layers.ToArray();


        int resolution =
            terrainData.alphamapResolution;


        int layerCount =
            layers.Count;


        float[,,] maps =
            new float[
                resolution,
                resolution,
                layerCount
            ];


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
                float nx =
                    x /
                    (float)
                    Mathf.Max(
                        resolution - 1,
                        1
                    );


                float nz =
                    z /
                    (float)
                    Mathf.Max(
                        resolution - 1,
                        1
                    );


                float height =
                    terrainData
                        .GetInterpolatedHeight(
                            nx,
                            nz
                        );


                float normalizedHeight =
                    terrainData.size.y > 0.001f
                    ? height /
                      terrainData.size.y
                    : 0f;


                normalizedHeight =
                    Mathf.Clamp01(
                        normalizedHeight
                    );


                float slope =
                    terrainData.GetSteepness(
                        nx,
                        nz
                    );


                float rockWeight = 0f;

                float snowWeight = 0f;

                float grassWeight = 0f;


                // ------------------------------------------------
                // ROCK
                // ------------------------------------------------

                if (rockLayer != null)
                {
                    rockWeight =
                        Mathf.InverseLerp(
                            rockStartSlope,
                            rockFullSlope,
                            slope
                        );


                    rockWeight =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            rockWeight
                        );
                }


                // ------------------------------------------------
                // SNOW
                // ------------------------------------------------

                if (snowLayer != null)
                {
                    snowWeight =
                        Mathf.InverseLerp(
                            snowStartHeight,
                            snowFullHeight,
                            normalizedHeight
                        );


                    snowWeight =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            snowWeight
                        );
                }


                // ------------------------------------------------
                // GRASS
                // ------------------------------------------------

                if (grassLayer != null)
                {
                    grassWeight =
                        1f -
                        rockWeight;


                    grassWeight *=
                        1f -
                        snowWeight;
                }


                rockWeight *=
                    1f -
                    snowWeight;


                int layerIndex = 0;


                if (grassLayer != null)
                {
                    maps[
                        z,
                        x,
                        layerIndex
                    ] =
                        grassWeight;

                    layerIndex++;
                }


                if (rockLayer != null)
                {
                    maps[
                        z,
                        x,
                        layerIndex
                    ] =
                        rockWeight;

                    layerIndex++;
                }


                if (snowLayer != null)
                {
                    maps[
                        z,
                        x,
                        layerIndex
                    ] =
                        snowWeight;
                }


                // ------------------------------------------------
                // NORMALIZE
                // ------------------------------------------------

                float total = 0f;


                for (
                    int layer = 0;
                    layer < layerCount;
                    layer++
                )
                {
                    total +=
                        maps[
                            z,
                            x,
                            layer
                        ];
                }


                if (total > 0.0001f)
                {
                    for (
                        int layer = 0;
                        layer < layerCount;
                        layer++
                    )
                    {
                        maps[
                            z,
                            x,
                            layer
                        ] /=
                            total;
                    }
                }
                else
                {
                    maps[
                        z,
                        x,
                        0
                    ] = 1f;
                }
            }
        }


        terrainData.SetAlphamaps(
            0,
            0,
            maps
        );


        targetTerrain.Flush();
    }


    // ========================================================
    // URP
    // ========================================================

    private void ConfigureTerrainForURP()
    {
        if (!configureURP)
        {
            return;
        }


        if (!HasValidTerrain())
        {
            return;
        }


        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Terrain/Lit"
            );


        if (shader == null)
        {
            Debug.LogWarning(
                "URP Terrain Lit shader not found."
            );

            return;
        }


        if (
            targetTerrain.materialTemplate != null &&
            targetTerrain.materialTemplate.shader == shader
        )
        {
            return;
        }


        Material material =
            new Material(shader);


        material.name =
            "Automatic URP Terrain Lit";


        targetTerrain.materialTemplate =
            material;
    }


    // ========================================================
    // SAVE ORIGINAL TERRAIN
    // ========================================================

    private void SaveOriginalTerrain()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        TerrainData data =
            targetTerrain.terrainData;


        int resolution =
            data.heightmapResolution;


        originalHeights =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution
            );


        originalHeightsSaved =
            true;
    }


    // ========================================================
    // RESTORE ORIGINAL TERRAIN
    // ========================================================

    private void RestoreOriginalTerrain()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        if (!originalHeightsSaved)
        {
            return;
        }


        if (originalHeights == null)
        {
            return;
        }


        TerrainData data =
            targetTerrain.terrainData;


        data.SetHeights(
            0,
            0,
            originalHeights
        );


        ClearGeneratedWater();


        mountainZones.Clear();

        waterFeatures.Clear();


        RefreshTerrainCollider();
    }


    // ========================================================
    // RESET TERRAIN
    // ========================================================

    public void ResetTerrain()
    {
        RestoreOriginalTerrain();

        ApplyTerrainTextures();


        Debug.Log(
            "TerrainGenerator: Terrain reset."
        );
    }


    // ========================================================
    // CREATE WATER PARENT
    // ========================================================

    private void CreateWaterParent()
    {
        if (generatedWaterParent != null)
        {
            return;
        }


        generatedWaterParent =
            new GameObject(
                "Generated Water"
            );


        generatedWaterParent.transform.SetParent(
            transform
        );
    }


    // ========================================================
    // CLEAR GENERATED WATER
    // ========================================================

    private void ClearGeneratedWater()
    {
        if (generatedWaterParent == null)
        {
            return;
        }


        for (
            int i =
                generatedWaterParent
                    .transform
                    .childCount - 1;

            i >= 0;

            i--
        )
        {
            GameObject child =
                generatedWaterParent
                    .transform
                    .GetChild(i)
                    .gameObject;


            Destroy(child);
        }
    }


    // ========================================================
    // ROTATE VECTOR
    // ========================================================

    private Vector2 RotateVector(
        Vector2 vector,
        float degrees
    )
    {
        float radians =
            degrees *
            Mathf.Deg2Rad;


        float cos =
            Mathf.Cos(
                radians
            );


        float sin =
            Mathf.Sin(
                radians
            );


        return new Vector2(
            vector.x * cos -
            vector.y * sin,

            vector.x * sin +
            vector.y * cos
        );
    }


    // ========================================================
    // REFRESH TERRAIN COLLIDER
    // ========================================================

    private void RefreshTerrainCollider()
    {
        if (!HasValidTerrain())
        {
            return;
        }


        TerrainCollider collider =
            targetTerrain.GetComponent<
                TerrainCollider
            >();


        if (collider == null)
        {
            collider =
                targetTerrain.gameObject
                    .AddComponent<
                        TerrainCollider
                    >();
        }


        collider.terrainData =
            targetTerrain.terrainData;


        collider.enabled = false;

        collider.enabled = true;
    }


    // ========================================================
    // VALID TERRAIN CHECK
    // ========================================================

    private bool HasValidTerrain()
    {
        if (targetTerrain == null)
        {
            Debug.LogWarning(
                "TerrainGenerator: " +
                "Target Terrain missing."
            );

            return false;
        }


        if (targetTerrain.terrainData == null)
        {
            Debug.LogWarning(
                "TerrainGenerator: " +
                "TerrainData missing."
            );

            return false;
        }


        return true;
    }


    // ========================================================
    // KEYBOARD TESTING
    // ========================================================

    private void Update()
    {
        // ----------------------------------------------------
        // M = MOUNTAIN
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.M
            )
        )
        {
            CreateMountainRange();
            ApplyTerrainTextures();

            RefreshTerrainCollider();
        }


        // ----------------------------------------------------
        // L = LAKE
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.L
            )
        )
        {
            CreateRandomLake();

            RefreshTerrainCollider();

        }


        // ----------------------------------------------------
        // R = RIVER
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.R
            )
        )
        {
            CreateRandomRiver();
            RefreshTerrainCollider();


        }


        // ----------------------------------------------------
        // G = GENERATE EVERYTHING
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.G
            )
        )
        {
            GenerateTerrain();
        }


        // ----------------------------------------------------
        // C = RESET
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.C
            )
        )
        {
            ResetTerrain();
        }
    }
}