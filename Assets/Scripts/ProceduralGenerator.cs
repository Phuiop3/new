using UnityEngine;
using System.Collections.Generic;


// ============================================================
// TERRAIN GENERATOR
// ============================================================
//
// FEATURES
// --------
// 1. Uses an EXISTING Unity Terrain.
// 2. Generates random mountains.
// 3. Generates random hills.
// 4. Generates random valleys.
// 5. Generates random lakes.
// 6. Generates random rivers.
// 7. Prevents generated features from overlapping.
// 8. Creates simple water.
// 9. Automatically paints Terrain Layers.
// 10. Grass on normal terrain.
// 11. Rock on steep mountain slopes.
// 12. Snow on high terrain.
// 13. Saves original terrain.
// 14. Supports terrain reset.
// 15. Refreshes TerrainCollider.
// 16. Keyboard testing.
//
// TERRAIN TEXTURE SYSTEM
// ----------------------
// Grass = default terrain.
// Rock  = based on slope.
// Snow  = based on elevation.
//
// IMPORTANT
// ---------
// The Grass/Rock/Snow fields must contain Unity
// TerrainLayer assets.
//
// ============================================================


public class TerrainGenerator : MonoBehaviour
{
    // ========================================================
    // EXISTING TERRAIN
    // ========================================================

    [Header("Existing Terrain")]

    [Tooltip(
        "Drag your EXISTING Unity Terrain here. " +
        "This terrain will be modified directly."
    )]
    [SerializeField]
    private Terrain targetTerrain;


    // ========================================================
    // RANDOM MOUNTAIN SETTINGS
    // ========================================================

    [Header("Random Mountain Settings")]

    [SerializeField]
    private float minimumMountainRadius = 15f;

    [SerializeField]
    private float maximumMountainRadius = 30f;

    [SerializeField]
    private float minimumMountainHeight = 5f;

    [SerializeField]
    private float maximumMountainHeight = 12f;


    // ========================================================
    // RANDOM HILL SETTINGS
    // ========================================================

    [Header("Random Hill Settings")]

    [SerializeField]
    private float minimumHillRadius = 10f;

    [SerializeField]
    private float maximumHillRadius = 25f;

    [SerializeField]
    private float minimumHillHeight = 2f;

    [SerializeField]
    private float maximumHillHeight = 6f;


    // ========================================================
    // RANDOM VALLEY SETTINGS
    // ========================================================

    [Header("Random Valley Settings")]

    [SerializeField]
    private float minimumValleyRadius = 15f;

    [SerializeField]
    private float maximumValleyRadius = 30f;

    [SerializeField]
    private float minimumValleyDepth = 3f;

    [SerializeField]
    private float maximumValleyDepth = 8f;


    // ========================================================
    // RANDOM LAKE SETTINGS
    // ========================================================

    [Header("Random Lake Settings")]

    [Tooltip("Minimum lake radius in terrain units.")]
    [SerializeField]
    private float minimumLakeRadius = 10f;

    [Tooltip("Maximum lake radius in terrain units.")]
    [SerializeField]
    private float maximumLakeRadius = 25f;

    [Tooltip("Minimum lake depth in terrain units.")]
    [SerializeField]
    private float minimumLakeDepth = 2f;

    [Tooltip("Maximum lake depth in terrain units.")]
    [SerializeField]
    private float maximumLakeDepth = 5f;

    [Tooltip("Water surface thickness.")]
    [SerializeField]
    private float lakeWaterThickness = 0.2f;

    [Tooltip("How far above the bottom the lake water surface sits.")]
    [SerializeField]
    private float lakeWaterLevelOffset = 0.05f;


    // ========================================================
    // RANDOM RIVER SETTINGS
    // ========================================================

    [Header("Random River Settings")]

    [Tooltip("Minimum river width.")]
    [SerializeField]
    private float minimumRiverWidth = 3f;

    [Tooltip("Maximum river width.")]
    [SerializeField]
    private float maximumRiverWidth = 7f;

    [Tooltip("Minimum river depth.")]
    [SerializeField]
    private float minimumRiverDepth = 1.5f;

    [Tooltip("Maximum river depth.")]
    [SerializeField]
    private float maximumRiverDepth = 3f;

    [Tooltip("Number of river sections.")]
    [SerializeField]
    private int riverSegments = 12;

    [Tooltip("Distance between river points.")]
    [SerializeField]
    private float riverSegmentLength = 10f;

    [Tooltip("Maximum random sideways movement.")]
    [SerializeField]
    private float riverWanderAmount = 8f;

    [Tooltip("Thickness of the generated river water.")]
    [SerializeField]
    private float riverWaterThickness = 0.15f;

    [Tooltip("Height above terrain for river water.")]
    [SerializeField]
    private float riverWaterLevelOffset = 0.05f;


    // ========================================================
    // GENERATED FEATURE SPACING
    // ========================================================

    [Header("Generated Feature Spacing")]

    [Tooltip(
        "Minimum empty space between generated terrain features."
    )]
    [SerializeField]
    private float minimumFeatureSpacing = 5f;

    [Tooltip(
        "Maximum number of attempts to find a valid position."
    )]
    [SerializeField]
    private int maximumPlacementAttempts = 50;


    // ========================================================
    // TERRAIN TEXTURES
    // ========================================================

    [Header("Terrain Textures")]

    [Tooltip(
        "Terrain Layer used for normal/low terrain."
    )]
    [SerializeField]
    private TerrainLayer grassLayer;

    [Tooltip(
        "Terrain Layer used for steep mountain slopes."
    )]
    [SerializeField]
    private TerrainLayer rockLayer;

    [Tooltip(
        "Terrain Layer used for high elevation."
    )]
    [SerializeField]
    private TerrainLayer snowLayer;


    // ========================================================
    // TERRAIN TEXTURE SETTINGS
    // ========================================================

    [Header("Terrain Texture Settings")]

    [Tooltip(
        "Slope angle where rock starts appearing."
    )]
    [SerializeField]
    private float rockStartSlope = 25f;

    [Tooltip(
        "Slope angle where rock reaches maximum strength."
    )]
    [SerializeField]
    private float rockFullSlope = 45f;

    [Tooltip(
        "Normalized terrain height where snow starts."
    )]
    [SerializeField]
    [Range(0f, 1f)]
    private float snowStartHeight = 0.75f;

    [Tooltip(
        "Normalized terrain height where snow becomes dominant."
    )]
    [SerializeField]
    [Range(0f, 1f)]
    private float snowFullHeight = 0.90f;

    [Tooltip(
        "Automatically repaint terrain after terrain modification."
    )]
    [SerializeField]
    private bool automaticallyPaintTerrain = true;


    // ========================================================
    // TEXTURE PAINT RESOLUTION
    // ========================================================

    [Tooltip(
        "Controls the resolution used when painting terrain textures. " +
        "0 = use the terrain's current alphamap resolution."
    )]
    [SerializeField]
    private int textureResolutionOverride = 0;


    // ========================================================
    // WATER MATERIAL
    // ========================================================

    [Header("Water Appearance")]

    [Tooltip(
        "Optional water material. " +
        "If empty, a simple transparent material is created."
    )]
    [SerializeField]
    private Material waterMaterial;


    // ========================================================
    // GENERATED WATER
    // ========================================================

    private GameObject generatedWaterParent;


    // ========================================================
    // ORIGINAL TERRAIN
    // ========================================================

    private float[,] originalHeights;

    private bool originalHeightsSaved = false;


    // ========================================================
    // GENERATED FEATURES
    // ========================================================

    private class GeneratedFeature
    {
        public Vector2 position;
        public float radius;

        public GeneratedFeature(
            Vector2 position,
            float radius
        )
        {
            this.position = position;
            this.radius = radius;
        }
    }


    private List<GeneratedFeature> generatedFeatures =
        new List<GeneratedFeature>();


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
    // UNITY START
    // ========================================================

    private void Start()
    {
        SaveOriginalTerrain();

        CreateWaterParent();

        // Paint the existing terrain once when the scene starts.
        ApplyTerrainTextures();
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

        TerrainData terrainData =
            targetTerrain.terrainData;

        int resolution =
            terrainData.heightmapResolution;

        originalHeights =
            terrainData.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        originalHeightsSaved = true;

        Debug.Log(
            "TerrainGenerator: Original terrain heightmap saved."
        );
    }


    // ========================================================
    // CHECK TERRAIN
    // ========================================================

    private bool HasValidTerrain()
    {
        if (targetTerrain == null)
        {
            Debug.LogWarning(
                "TerrainGenerator: No existing Terrain assigned. " +
                "Please drag your Terrain into Target Terrain."
            );

            return false;
        }

        if (targetTerrain.terrainData == null)
        {
            Debug.LogWarning(
                "TerrainGenerator: Target Terrain has no TerrainData."
            );

            return false;
        }

        return true;
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
    // CHECK FEATURE OVERLAP
    // ========================================================

    private bool IsPositionAvailable(
        Vector2 position,
        float radius
    )
    {
        foreach (
            GeneratedFeature feature
            in generatedFeatures
        )
        {
            float requiredDistance =
                radius +
                feature.radius +
                minimumFeatureSpacing;

            float actualDistance =
                Vector2.Distance(
                    position,
                    feature.position
                );

            if (actualDistance < requiredDistance)
            {
                return false;
            }
        }

        return true;
    }


    // ========================================================
    // FIND NON-OVERLAPPING POSITION
    // ========================================================

    private bool TryGetNonOverlappingPosition(
        float radius,
        out Vector2 position
    )
    {
        int attempts =
            Mathf.Max(
                maximumPlacementAttempts,
                1
            );

        for (
            int attempt = 0;
            attempt < attempts;
            attempt++
        )
        {
            Vector2 candidate =
                GetRandomTerrainPosition(
                    radius
                );

            if (
                IsPositionAvailable(
                    candidate,
                    radius
                )
            )
            {
                position = candidate;

                return true;
            }
        }

        position = Vector2.zero;

        return false;
    }


    // ========================================================
    // REGISTER GENERATED FEATURE
    // ========================================================

    private void RegisterFeature(
        Vector2 position,
        float radius
    )
    {
        generatedFeatures.Add(
            new GeneratedFeature(
                position,
                radius
            )
        );
    }


    // ========================================================
    // RANDOM MOUNTAIN
    // ========================================================

    public void CreateRandomMountain()
    {
        if (!HasValidTerrain())
        {
            return;
        }

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


        // ----------------------------------------------------
        // Find valid position
        // ----------------------------------------------------

        Vector2 position;

        if (
            !TryGetNonOverlappingPosition(
                radius,
                out position
            )
        )
        {
            Debug.LogWarning(
                "TerrainGenerator: Could not find enough space " +
                "for a mountain."
            );

            return;
        }


        // ----------------------------------------------------
        // Modify terrain
        // ----------------------------------------------------

        ModifyTerrainArea(
            position,
            radius,
            height,
            false,
            true
        );


        // ----------------------------------------------------
        // Register feature
        // ----------------------------------------------------

        RegisterFeature(
            position,
            radius
        );


        // ----------------------------------------------------
        // Debug
        // ----------------------------------------------------

        Debug.Log(
            "=================================================\n" +
            "RANDOM MOUNTAIN CREATED\n" +
            "=================================================\n" +
            $"Position X: {position.x:F1}\n" +
            $"Position Z: {position.y:F1}\n" +
            $"Radius: {radius:F1}\n" +
            $"Height: {height:F1}\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "================================================="
        );
    }


    // ========================================================
    // RANDOM MOUNTAINS
    // ========================================================

    public void CreateRandomMountains(
        int count
    )
    {
        if (!HasValidTerrain())
        {
            return;
        }

        count =
            Mathf.Clamp(
                count,
                1,
                20
            );

        Debug.Log(
            "Creating " +
            count +
            " random mountains."
        );

        for (
            int i = 0;
            i < count;
            i++
        )
        {
            CreateRandomMountain();
        }
    }


    // ========================================================
    // RANDOM HILL
    // ========================================================

    public void CreateRandomHill()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        float radius =
            Random.Range(
                minimumHillRadius,
                maximumHillRadius
            );

        float height =
            Random.Range(
                minimumHillHeight,
                maximumHillHeight
            );


        // ----------------------------------------------------
        // Find valid position
        // ----------------------------------------------------

        Vector2 position;

        if (
            !TryGetNonOverlappingPosition(
                radius,
                out position
            )
        )
        {
            Debug.LogWarning(
                "TerrainGenerator: Could not find enough space " +
                "for a hill."
            );

            return;
        }


        // ----------------------------------------------------
        // Modify terrain
        // ----------------------------------------------------

        ModifyTerrainArea(
            position,
            radius,
            height,
            false,
            false
        );


        // ----------------------------------------------------
        // Register feature
        // ----------------------------------------------------

        RegisterFeature(
            position,
            radius
        );


        // ----------------------------------------------------
        // Debug
        // ----------------------------------------------------

        Debug.Log(
            "=================================================\n" +
            "RANDOM HILL CREATED\n" +
            "=================================================\n" +
            $"Position X: {position.x:F1}\n" +
            $"Position Z: {position.y:F1}\n" +
            $"Radius: {radius:F1}\n" +
            $"Height: {height:F1}\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "================================================="
        );
    }


    // ========================================================
    // RANDOM VALLEY
    // ========================================================

    public void CreateRandomValley()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        float radius =
            Random.Range(
                minimumValleyRadius,
                maximumValleyRadius
            );

        float depth =
            Random.Range(
                minimumValleyDepth,
                maximumValleyDepth
            );


        // ----------------------------------------------------
        // Find valid position
        // ----------------------------------------------------

        Vector2 position;

        if (
            !TryGetNonOverlappingPosition(
                radius,
                out position
            )
        )
        {
            Debug.LogWarning(
                "TerrainGenerator: Could not find enough space " +
                "for a valley."
            );

            return;
        }


        // ----------------------------------------------------
        // Modify terrain
        // ----------------------------------------------------

        ModifyTerrainArea(
            position,
            radius,
            depth,
            true,
            false
        );


        // ----------------------------------------------------
        // Register feature
        // ----------------------------------------------------

        RegisterFeature(
            position,
            radius
        );


        // ----------------------------------------------------
        // Debug
        // ----------------------------------------------------

        Debug.Log(
            "=================================================\n" +
            "RANDOM VALLEY CREATED\n" +
            "=================================================\n" +
            $"Position X: {position.x:F1}\n" +
            $"Position Z: {position.y:F1}\n" +
            $"Radius: {radius:F1}\n" +
            $"Depth: {depth:F1}\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "================================================="
        );
    }


    // ========================================================
    // RANDOM LAKE
    // ========================================================

    public void CreateRandomLake()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        CreateWaterParent();


        // ----------------------------------------------------
        // Random lake settings
        // ----------------------------------------------------

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


        // ----------------------------------------------------
        // Find valid position
        // ----------------------------------------------------

        Vector2 position;

        if (
            !TryGetNonOverlappingPosition(
                radius,
                out position
            )
        )
        {
            Debug.LogWarning(
                "TerrainGenerator: Could not find enough space " +
                "for a lake."
            );

            return;
        }


        // ----------------------------------------------------
        // Lower terrain
        // ----------------------------------------------------

        ModifyTerrainArea(
            position,
            radius,
            depth,
            true,
            false
        );


        // ----------------------------------------------------
        // Find terrain height
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
        // Create lake
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


        // ----------------------------------------------------
        // Position
        // ----------------------------------------------------

        lake.transform.position =
            new Vector3(
                worldPosition.x,
                waterHeight,
                worldPosition.z
            );


        // ----------------------------------------------------
        // Scale
        // ----------------------------------------------------

        float diameter =
            radius * 2f;

        lake.transform.localScale =
            new Vector3(
                diameter,
                lakeWaterThickness,
                diameter
            );


        // ----------------------------------------------------
        // Remove collider
        // ----------------------------------------------------

        Collider lakeCollider =
            lake.GetComponent<Collider>();

        if (lakeCollider != null)
        {
            Destroy(lakeCollider);
        }


        // ----------------------------------------------------
        // Material
        // ----------------------------------------------------

        ApplyWaterMaterial(
            lake
        );


        // ----------------------------------------------------
        // Register feature
        // ----------------------------------------------------

        RegisterFeature(
            position,
            radius
        );


        // ----------------------------------------------------
        // Debug
        // ----------------------------------------------------

        Debug.Log(
            "=================================================\n" +
            "RANDOM LAKE CREATED\n" +
            "=================================================\n" +
            $"Position X: {position.x:F1}\n" +
            $"Position Z: {position.y:F1}\n" +
            $"Radius: {radius:F1}\n" +
            $"Depth: {depth:F1}\n" +
            $"Water Height: {waterHeight:F2}\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "================================================="
        );
    }


    // ========================================================
    // RANDOM RIVER
    // ========================================================

    public void CreateRandomRiver()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        CreateWaterParent();


        // ----------------------------------------------------
        // River settings
        // ----------------------------------------------------

        int segmentCount =
            Mathf.Clamp(
                riverSegments,
                4,
                50
            );

        float riverWidth =
            Random.Range(
                minimumRiverWidth,
                maximumRiverWidth
            );

        float riverDepth =
            Random.Range(
                minimumRiverDepth,
                maximumRiverDepth
            );


        // ----------------------------------------------------
        // River placement radius
        // ----------------------------------------------------

        float riverLength =
            segmentCount *
            riverSegmentLength;

        float riverPlacementRadius =
            riverLength * 0.35f +
            riverWidth;


        // ----------------------------------------------------
        // Find starting point
        // ----------------------------------------------------

        Vector2 start;

        if (
            !TryGetNonOverlappingPosition(
                riverPlacementRadius,
                out start
            )
        )
        {
            Debug.LogWarning(
                "TerrainGenerator: Could not find enough space " +
                "for a river."
            );

            return;
        }


        // ----------------------------------------------------
        // Initial direction
        // ----------------------------------------------------

        Vector2 direction;

        if (Random.value > 0.5f)
        {
            direction =
                Vector2.right;
        }
        else
        {
            direction =
                Vector2.up;
        }


        // ----------------------------------------------------
        // River points
        // ----------------------------------------------------

        List<Vector2> riverPoints =
            new List<Vector2>();

        Vector2 current =
            start;

        riverPoints.Add(
            current
        );


        // ----------------------------------------------------
        // Generate curved river path
        // ----------------------------------------------------

        for (
            int i = 1;
            i < segmentCount;
            i++
        )
        {
            float angle =
                Random.Range(
                    -25f,
                    25f
                );


            float wander =
                Random.Range(
                    -riverWanderAmount,
                    riverWanderAmount
                );

            direction =
                RotateVector(
                    direction,
                    angle
                );


            Vector2 forward =
                direction.normalized *
                riverSegmentLength;

            Vector2 sideways =
                new Vector2(
                    -direction.y,
                    direction.x
                ) *
                (wander / riverSegmentLength);


            Vector2 next =
                current +
                forward +
                sideways;


            // ------------------------------------------------
            // Keep river inside terrain
            // ------------------------------------------------

            float boundary =
                riverWidth +
                2f;

            next.x =
                Mathf.Clamp(
                    next.x,
                    boundary,
                    targetTerrain.terrainData.size.x -
                    boundary
                );

            next.y =
                Mathf.Clamp(
                    next.y,
                    boundary,
                    targetTerrain.terrainData.size.z -
                    boundary
                );


            riverPoints.Add(
                next
            );

            current =
                next;
        }


        // ----------------------------------------------------
        // Check river path
        // ----------------------------------------------------

        if (
            !DoesRiverPathFit(
                riverPoints,
                riverWidth
            )
        )
        {
            Debug.LogWarning(
                "TerrainGenerator: River path would overlap " +
                "another generated feature. River cancelled."
            );

            return;
        }


        // ----------------------------------------------------
        // Lower terrain along river
        // ----------------------------------------------------

        for (
            int i = 0;
            i < riverPoints.Count;
            i++
        )
        {
            ModifyTerrainArea(
                riverPoints[i],
                riverWidth,
                riverDepth,
                true,
                false
            );
        }


        // ----------------------------------------------------
        // Create water segments
        // ----------------------------------------------------

        for (
            int i = 0;
            i < riverPoints.Count - 1;
            i++
        )
        {
            CreateRiverWaterSegment(
                riverPoints[i],
                riverPoints[i + 1],
                riverWidth
            );
        }


        // ----------------------------------------------------
        // Register river
        // ----------------------------------------------------

        RegisterRiver(
            riverPoints,
            riverWidth
        );


        // ----------------------------------------------------
        // Debug
        // ----------------------------------------------------

        Debug.Log(
            "=================================================\n" +
            "RANDOM RIVER CREATED\n" +
            "=================================================\n" +
            $"Segments: {riverPoints.Count}\n" +
            $"Width: {riverWidth:F1}\n" +
            $"Depth: {riverDepth:F1}\n" +
            $"Wander: {riverWanderAmount:F1}\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "================================================="
        );
    }


    // ========================================================
    // CHECK RIVER PATH
    // ========================================================

    private bool DoesRiverPathFit(
        List<Vector2> riverPoints,
        float riverWidth
    )
    {
        foreach (
            Vector2 point
            in riverPoints
        )
        {
            foreach (
                GeneratedFeature feature
                in generatedFeatures
            )
            {
                float requiredDistance =
                    riverWidth +
                    feature.radius +
                    minimumFeatureSpacing;

                float distance =
                    Vector2.Distance(
                        point,
                        feature.position
                    );

                if (
                    distance <
                    requiredDistance
                )
                {
                    return false;
                }
            }
        }

        return true;
    }


    // ========================================================
    // REGISTER RIVER
    // ========================================================

    private void RegisterRiver(
        List<Vector2> riverPoints,
        float riverWidth
    )
    {
        foreach (
            Vector2 point
            in riverPoints
        )
        {
            generatedFeatures.Add(
                new GeneratedFeature(
                    point,
                    riverWidth
                )
            );
        }
    }


    // ========================================================
    // CREATE RIVER WATER SEGMENT
    // ========================================================

    private void CreateRiverWaterSegment(
        Vector2 start,
        Vector2 end,
        float width
    )
    {
        Vector2 middle =
            (start + end) * 0.5f;

        Vector2 difference =
            end - start;

        float length =
            difference.magnitude;

        if (length <= 0.01f)
        {
            return;
        }


        // ----------------------------------------------------
        // Convert to world position
        // ----------------------------------------------------

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


        // ----------------------------------------------------
        // Sample terrain height
        // ----------------------------------------------------

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
            ) / 3f;

        waterHeight +=
            riverWaterLevelOffset;


        // ----------------------------------------------------
        // Create water
        // ----------------------------------------------------

        GameObject river =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        river.name =
            "RiverWater";

        river.transform.SetParent(
            generatedWaterParent.transform
        );


        // ----------------------------------------------------
        // Position
        // ----------------------------------------------------

        river.transform.position =
            new Vector3(
                worldMiddle.x,
                waterHeight,
                worldMiddle.z
            );


        // ----------------------------------------------------
        // Rotation
        // ----------------------------------------------------

        float angle =
            Mathf.Atan2(
                difference.x,
                difference.y
            ) *
            Mathf.Rad2Deg;

        river.transform.rotation =
            Quaternion.Euler(
                0f,
                angle,
                0f
            );


        // ----------------------------------------------------
        // Scale
        // ----------------------------------------------------

        river.transform.localScale =
            new Vector3(
                width,
                riverWaterThickness,
                length
            );


        // ----------------------------------------------------
        // Remove collider
        // ----------------------------------------------------

        Collider riverCollider =
            river.GetComponent<Collider>();

        if (riverCollider != null)
        {
            Destroy(
                riverCollider
            );
        }


        // ----------------------------------------------------
        // Material
        // ----------------------------------------------------

        ApplyWaterMaterial(
            river
        );
    }


    // ========================================================
    // APPLY WATER MATERIAL
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


        // ----------------------------------------------------
        // User supplied material
        // ----------------------------------------------------

        if (waterMaterial != null)
        {
            renderer.material =
                waterMaterial;

            return;
        }


        // ----------------------------------------------------
        // Create default material
        // ----------------------------------------------------

        Shader shader =
            Shader.Find(
                "Standard"
            );

        if (shader == null)
        {
            return;
        }

        Material material =
            new Material(
                shader
            );

        material.name =
            "Generated Water Material";


        // ----------------------------------------------------
        // Water colour
        // ----------------------------------------------------

        material.color =
            new Color(
                0.05f,
                0.35f,
                0.8f,
                0.75f
            );


        // ----------------------------------------------------
        // Transparency
        // ----------------------------------------------------

        material.SetFloat(
            "_Mode",
            3f
        );

        material.SetInt(
            "_SrcBlend",
            (int)
            UnityEngine.Rendering.BlendMode.SrcAlpha
        );

        material.SetInt(
            "_DstBlend",
            (int)
            UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
        );

        material.SetInt(
            "_ZWrite",
            0
        );

        material.DisableKeyword(
            "_ALPHATEST_ON"
        );

        material.EnableKeyword(
            "_ALPHABLEND_ON"
        );

        material.DisableKeyword(
            "_ALPHAPREMULTIPLY_ON"
        );

        material.renderQueue =
            3000;

        renderer.material =
            material;
    }


    // ========================================================
    // APPLY TERRAIN TEXTURES
    // ========================================================
    //
    // Grass:
    // Default terrain.
    //
    // Rock:
    // Appears as slope increases.
    //
    // Snow:
    // Appears as terrain becomes higher.
    //
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


        // ----------------------------------------------------
        // Validate Terrain Layers
        // ----------------------------------------------------

        if (
            grassLayer == null &&
            rockLayer == null &&
            snowLayer == null
        )
        {
            Debug.LogWarning(
                "TerrainGenerator: No Terrain Layers assigned. " +
                "Assign Grass, Rock and/or Snow Terrain Layers."
            );

            return;
        }


        TerrainData terrainData =
            targetTerrain.terrainData;


        // ----------------------------------------------------
        // Create layer list
        // ----------------------------------------------------

        List<TerrainLayer> layers =
            new List<TerrainLayer>();


        if (grassLayer != null)
        {
            layers.Add(
                grassLayer
            );
        }


        if (rockLayer != null)
        {
            layers.Add(
                rockLayer
            );
        }


        if (snowLayer != null)
        {
            layers.Add(
                snowLayer
            );
        }


        if (layers.Count == 0)
        {
            return;
        }


        // ----------------------------------------------------
        // Assign Terrain Layers
        // ----------------------------------------------------

        terrainData.terrainLayers =
            layers.ToArray();


        // ----------------------------------------------------
        // Alpha map resolution
        // ----------------------------------------------------

        int alphaResolution;

        if (textureResolutionOverride > 0)
        {
            alphaResolution =
                textureResolutionOverride;
        }
        else
        {
            alphaResolution =
                terrainData.alphamapResolution;
        }


        alphaResolution =
            Mathf.Max(
                alphaResolution,
                16
            );


        int layerCount =
            layers.Count;


        // ----------------------------------------------------
        // Create alpha map
        // ----------------------------------------------------

        float[,,] alphamaps =
            new float[
                alphaResolution,
                alphaResolution,
                layerCount
            ];


        // ----------------------------------------------------
        // Paint terrain
        // ----------------------------------------------------

        for (
            int z = 0;
            z < alphaResolution;
            z++
        )
        {
            for (
                int x = 0;
                x < alphaResolution;
                x++
            )
            {
                float normalizedX;

                if (alphaResolution <= 1)
                {
                    normalizedX = 0f;
                }
                else
                {
                    normalizedX =
                        x /
                        (float)(alphaResolution - 1);
                }


                float normalizedZ;

                if (alphaResolution <= 1)
                {
                    normalizedZ = 0f;
                }
                else
                {
                    normalizedZ =
                        z /
                        (float)(alphaResolution - 1);
                }


                // --------------------------------------------
                // Terrain height
                // --------------------------------------------

                float terrainHeight =
                    terrainData.GetInterpolatedHeight(
                        normalizedX,
                        normalizedZ
                    );


                float normalizedHeight;

                if (terrainData.size.y <= 0.001f)
                {
                    normalizedHeight = 0f;
                }
                else
                {
                    normalizedHeight =
                        terrainHeight /
                        terrainData.size.y;
                }


                normalizedHeight =
                    Mathf.Clamp01(
                        normalizedHeight
                    );


                // --------------------------------------------
                // Terrain slope
                // --------------------------------------------

                float slope =
                    terrainData.GetSteepness(
                        normalizedX,
                        normalizedZ
                    );


                // --------------------------------------------
                // ROCK WEIGHT
                // --------------------------------------------

                float rockWeight = 0f;

                if (rockLayer != null)
                {
                    rockWeight =
                        Mathf.InverseLerp(
                            rockStartSlope,
                            rockFullSlope,
                            slope
                        );

                    rockWeight =
                        Mathf.Clamp01(
                            rockWeight
                        );

                    // Smooth transition.
                    rockWeight =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            rockWeight
                        );
                }


                // --------------------------------------------
                // SNOW WEIGHT
                // --------------------------------------------

                float snowWeight = 0f;

                if (snowLayer != null)
                {
                    snowWeight =
                        Mathf.InverseLerp(
                            snowStartHeight,
                            snowFullHeight,
                            normalizedHeight
                        );

                    snowWeight =
                        Mathf.Clamp01(
                            snowWeight
                        );

                    // Smooth transition.
                    snowWeight =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            snowWeight
                        );
                }


                // --------------------------------------------
                // GRASS WEIGHT
                // --------------------------------------------

                float grassWeight = 1f;


                if (grassLayer != null)
                {
                    grassWeight =
                        1f -
                        rockWeight;

                    grassWeight *=
                        1f -
                        snowWeight;

                    grassWeight =
                        Mathf.Clamp01(
                            grassWeight
                        );
                }


                // --------------------------------------------
                // Snow covers rock
                // --------------------------------------------

                rockWeight *=
                    1f -
                    snowWeight;


                // --------------------------------------------
                // If there is no grass layer
                // --------------------------------------------

                if (grassLayer == null)
                {
                    grassWeight = 0f;
                }


                // --------------------------------------------
                // Assign layers
                // --------------------------------------------

                int layerIndex = 0;


                if (grassLayer != null)
                {
                    alphamaps[
                        z,
                        x,
                        layerIndex
                    ] =
                        grassWeight;

                    layerIndex++;
                }


                if (rockLayer != null)
                {
                    alphamaps[
                        z,
                        x,
                        layerIndex
                    ] =
                        rockWeight;

                    layerIndex++;
                }


                if (snowLayer != null)
                {
                    alphamaps[
                        z,
                        x,
                        layerIndex
                    ] =
                        snowWeight;
                }


                // --------------------------------------------
                // Normalize weights
                // --------------------------------------------

                float totalWeight = 0f;


                for (
                    int layer = 0;
                    layer < layerCount;
                    layer++
                )
                {
                    totalWeight +=
                        alphamaps[
                            z,
                            x,
                            layer
                        ];
                }


                if (totalWeight > 0.0001f)
                {
                    for (
                        int layer = 0;
                        layer < layerCount;
                        layer++
                    )
                    {
                        alphamaps[
                            z,
                            x,
                            layer
                        ] /=
                            totalWeight;
                    }
                }
                else
                {
                    // If all weights are zero, use the
                    // first available layer.
                    alphamaps[
                        z,
                        x,
                        0
                    ] = 1f;
                }
            }
        }


        // ----------------------------------------------------
        // Apply alpha map
        // ----------------------------------------------------

        terrainData.SetAlphamaps(
            0,
            0,
            alphamaps
        );


        // ----------------------------------------------------
        // Refresh terrain
        // ----------------------------------------------------

        targetTerrain.Flush();


        Debug.Log(
            "TerrainGenerator: Terrain textures applied."
        );
    }


    // ========================================================
    // GET RANDOM TERRAIN POSITION
    // ========================================================

    private Vector2 GetRandomTerrainPosition(
        float radius
    )
    {
        TerrainData terrainData =
            targetTerrain.terrainData;

        float margin =
            radius + 2f;

        float minimumX =
            margin;

        float maximumX =
            terrainData.size.x -
            margin;

        float minimumZ =
            margin;

        float maximumZ =
            terrainData.size.z -
            margin;


        // ----------------------------------------------------
        // Safety
        // ----------------------------------------------------

        if (maximumX <= minimumX)
        {
            minimumX = 0f;
            maximumX =
                terrainData.size.x;
        }

        if (maximumZ <= minimumZ)
        {
            minimumZ = 0f;
            maximumZ =
                terrainData.size.z;
        }


        // ----------------------------------------------------
        // Random position
        // ----------------------------------------------------

        float x =
            Random.Range(
                minimumX,
                maximumX
            );

        float z =
            Random.Range(
                minimumZ,
                maximumZ
            );

        return new Vector2(
            x,
            z
        );
    }


    // ========================================================
    // MODIFY TERRAIN AREA
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


        // ----------------------------------------------------
        // Convert position to heightmap coordinates
        // ----------------------------------------------------

        int centerX =
            Mathf.RoundToInt(
                (
                    position.x /
                    terrainData.size.x
                ) *
                (resolution - 1)
            );

        int centerZ =
            Mathf.RoundToInt(
                (
                    position.y /
                    terrainData.size.z
                ) *
                (resolution - 1)
            );


        // ----------------------------------------------------
        // Calculate radius in samples
        // ----------------------------------------------------

        int radiusSamplesX =
            Mathf.CeilToInt(
                (
                    radius /
                    terrainData.size.x
                ) *
                (resolution - 1)
            );

        int radiusSamplesZ =
            Mathf.CeilToInt(
                (
                    radius /
                    terrainData.size.z
                ) *
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


        // ----------------------------------------------------
        // Calculate affected area
        // ----------------------------------------------------

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


        // ----------------------------------------------------
        // Get current heights
        // ----------------------------------------------------

        float[,] heights =
            terrainData.GetHeights(
                startX,
                startZ,
                width,
                height
            );


        // ----------------------------------------------------
        // Modify heights
        // ----------------------------------------------------

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


                // --------------------------------------------
                // Normalized distance
                // --------------------------------------------

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


                // --------------------------------------------
                // Smooth falloff
                // --------------------------------------------

                float falloff =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        distance
                    );


                // --------------------------------------------
                // Shape
                // --------------------------------------------

                float shape;

                if (isMountain)
                {
                    shape =
                        Mathf.Pow(
                            falloff,
                            2.2f
                        );


                    float noise =
                        Mathf.PerlinNoise(
                            absoluteX * 0.035f,
                            absoluteZ * 0.035f
                        );


                    noise =
                        Mathf.Lerp(
                            0.85f,
                            1.15f,
                            noise
                        );


                    shape *=
                        noise;
                }
                else
                {
                    shape =
                        Mathf.Pow(
                            falloff,
                            1.5f
                        );
                }


                // --------------------------------------------
                // Normalize height amount
                // --------------------------------------------

                float normalizedAmount =
                    amount /
                    terrainData.size.y;

                float modification =
                    normalizedAmount *
                    shape;


                // --------------------------------------------
                // Lower terrain
                // --------------------------------------------

                if (isLowering)
                {
                    modification =
                        -Mathf.Abs(
                            modification
                        );
                }


                // --------------------------------------------
                // Apply
                // --------------------------------------------

                heights[
                    z,
                    x
                ] +=
                    modification;


                heights[
                    z,
                    x
                ] =
                    Mathf.Clamp01(
                        heights[
                            z,
                            x
                        ]
                    );
            }
        }


        // ----------------------------------------------------
        // Apply modified heights
        // ----------------------------------------------------

        terrainData.SetHeights(
            startX,
            startZ,
            heights
        );


        // ----------------------------------------------------
        // Repaint terrain textures
        // ----------------------------------------------------

        ApplyTerrainTextures();


        // ----------------------------------------------------
        // Refresh collider
        // ----------------------------------------------------

        RefreshTerrainCollider();
    }


    // ========================================================
    // TERRAIN LOCAL → WORLD
    // ========================================================

    private Vector3 TerrainLocalToWorld(
        float localX,
        float localZ
    )
    {
        Vector3 terrainPosition =
            targetTerrain.transform.position;

        Vector3 samplePosition =
            terrainPosition +
            new Vector3(
                localX,
                0f,
                localZ
            );

        float terrainHeight =
            targetTerrain.SampleHeight(
                samplePosition
            );

        return terrainPosition +
               new Vector3(
                   localX,
                   terrainHeight,
                   localZ
               );
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

        TerrainCollider terrainCollider =
            targetTerrain.GetComponent<
                TerrainCollider
            >();


        // ----------------------------------------------------
        // Create collider if missing
        // ----------------------------------------------------

        if (terrainCollider == null)
        {
            terrainCollider =
                targetTerrain.gameObject.AddComponent<
                    TerrainCollider
                >();
        }


        // ----------------------------------------------------
        // Assign terrain data
        // ----------------------------------------------------

        terrainCollider.terrainData =
            targetTerrain.terrainData;


        // ----------------------------------------------------
        // Force collider refresh
        // ----------------------------------------------------

        terrainCollider.enabled =
            false;

        terrainCollider.enabled =
            true;
    }


    // ========================================================
    // RESET TERRAIN
    // ========================================================

    public void ResetTerrain()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        if (!originalHeightsSaved)
        {
            Debug.LogWarning(
                "TerrainGenerator: Original terrain " +
                "heightmap has not been saved."
            );

            return;
        }

        TerrainData terrainData =
            targetTerrain.terrainData;


        // ----------------------------------------------------
        // Restore original terrain
        // ----------------------------------------------------

        terrainData.SetHeights(
            0,
            0,
            originalHeights
        );


        // ----------------------------------------------------
        // Repaint terrain
        // ----------------------------------------------------

        ApplyTerrainTextures();


        // ----------------------------------------------------
        // Refresh collider
        // ----------------------------------------------------

        RefreshTerrainCollider();


        // ----------------------------------------------------
        // Remove generated water
        // ----------------------------------------------------

        ClearGeneratedWater();


        // ----------------------------------------------------
        // Clear feature records
        // ----------------------------------------------------

        generatedFeatures.Clear();


        // ----------------------------------------------------
        // Debug
        // ----------------------------------------------------

        Debug.Log(
            "=================================================\n" +
            "TERRAIN RESET\n" +
            "=================================================\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "Original terrain shape restored.\n" +
            "Terrain textures reapplied.\n" +
            "Generated lakes and rivers removed.\n" +
            "Feature placement records cleared.\n" +
            "================================================="
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
                generatedWaterParent.transform.childCount - 1;

            i >= 0;

            i--
        )
        {
            GameObject child =
                generatedWaterParent
                    .transform
                    .GetChild(i)
                    .gameObject;

            Destroy(
                child
            );
        }
    }


    // ========================================================
    // KEYBOARD TEST
    // ========================================================

    private void Update()
    {
        // ----------------------------------------------------
        // M = Mountain
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.M
            )
        )
        {
            Debug.Log(
                "TEST: Creating random mountain..."
            );

            CreateRandomMountain();
        }


        // ----------------------------------------------------
        // H = Hill
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.H
            )
        )
        {
            Debug.Log(
                "TEST: Creating random hill..."
            );

            CreateRandomHill();
        }


        // ----------------------------------------------------
        // V = Valley
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.V
            )
        )
        {
            Debug.Log(
                "TEST: Creating random valley..."
            );

            CreateRandomValley();
        }


        // ----------------------------------------------------
        // N = 3 Mountains
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.N
            )
        )
        {
            Debug.Log(
                "TEST: Creating 3 random mountains..."
            );

            CreateRandomMountains(
                3
            );
        }


        // ----------------------------------------------------
        // L = Lake
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.L
            )
        )
        {
            Debug.Log(
                "TEST: Creating random lake..."
            );

            CreateRandomLake();
        }


        // ----------------------------------------------------
        // R = River
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.R
            )
        )
        {
            Debug.Log(
                "TEST: Creating random river..."
            );

            CreateRandomRiver();
        }


        // ----------------------------------------------------
        // T = Repaint Terrain
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.T
            )
        )
        {
            Debug.Log(
                "TEST: Repainting terrain textures..."
            );

            ApplyTerrainTextures();
        }


        // ----------------------------------------------------
        // C = Reset
        // ----------------------------------------------------

        if (
            Input.GetKeyDown(
                KeyCode.C
            )
        )
        {
            Debug.Log(
                "TEST: Resetting terrain and water..."
            );

            ResetTerrain();
        }
    }
}