using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WaterManager : MonoBehaviour
{
    // =========================================================
    // TERRAIN
    // =========================================================

    [Header("Terrain")]

    [SerializeField]
    private Terrain targetTerrain;

    [Tooltip("Optional. Used to detect generated mountains.")]
    [SerializeField]
    private TerrainGenerator terrainGenerator;


    // =========================================================
    // WATER
    // =========================================================

    [Header("Water")]

    [SerializeField]
    private Material waterMaterial;

    [SerializeField]
    private float waterHeightOffset = 0.02f;


    // =========================================================
    // RANDOM WATER PLACEMENT
    // =========================================================

    [Header("Random Water Placement")]

    [Tooltip("Optional. If empty, Camera.main is used.")]
    [SerializeField]
    private Transform playerTransform;

    [Tooltip("Minimum distance from player.")]
    [SerializeField]
    private float minWaterDistanceFromPlayer = 15f;

    [Tooltip("Maximum distance from player.")]
    [SerializeField]
    private float maxWaterDistanceFromPlayer = 60f;

    [Tooltip("Number of random locations tested before giving up.")]
    [SerializeField]
    private int placementAttempts = 40;

    [Tooltip("Maximum acceptable terrain slope in degrees.")]
    [SerializeField]
    private float maxAllowedSlope = 12f;

    [Tooltip("Reject locations where terrain rises this much above nearby terrain.")]
    [SerializeField]
    private float mountainHeightThreshold = 5f;

    [Tooltip("Radius used to detect whether a location is on a mountain.")]
    [SerializeField]
    private float mountainCheckRadius = 15f;

    [Tooltip("Extra safety space around water.")]
    [SerializeField]
    private float placementMargin = 4f;

    [Tooltip("Extra separation between different water features.")]
    [SerializeField]
    private float minimumWaterSpacing = 8f;


    // =========================================================
    // RIVER SETTINGS
    // =========================================================

    [Header("River Settings")]

    [SerializeField]
    private float riverLength = 80f;

    [SerializeField]
    private float riverWidth = 12f;

    [SerializeField]
    private float riverDepth = 2f;

    [SerializeField]
    private float riverBankWidth = 5f;

    [SerializeField]
    private float riverBankHeight = 1.5f;

    [SerializeField]
    private int riverSegments = 16;

    [SerializeField]
    private float riverMeander = 10f;

    [Tooltip("How much the river water follows the terrain.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float riverWaterTerrainFollow = 0.75f;


    // =========================================================
    // RIVER BANK BEAUTY
    // =========================================================

    [Header("River Bank Shape")]

    [SerializeField]
    [Range(0f, 1f)]
    private float riverBankSmoothness = 0.85f;

    [SerializeField]
    [Range(0f, 1f)]
    private float riverBankNaturalVariation = 0.12f;

    [SerializeField]
    private float riverBankNoiseScale = 0.08f;


    // =========================================================
    // LAKE SETTINGS
    // =========================================================

    [Header("Lake Settings")]

    [SerializeField]
    private float lakeWidth = 50f;

    [SerializeField]
    private float lakeLength = 70f;

    [SerializeField]
    private float lakeDepth = 2f;

    [SerializeField]
    private float lakeBankWidth = 6f;

    [SerializeField]
    private float lakeBankHeight = 1.5f;


    // =========================================================
    // LAKE BANK BEAUTY
    // =========================================================

    [Header("Lake Shore Shape")]

    [SerializeField]
    [Range(0f, 1f)]
    private float lakeBankSmoothness = 0.9f;

    [SerializeField]
    [Range(0f, 1f)]
    private float lakeBankNaturalVariation = 0.12f;

    [SerializeField]
    private float lakeBankNoiseScale = 0.06f;


    // =========================================================
    // AUTOMATIC TEST
    // =========================================================

    [Header("Automatic Test")]

    [SerializeField]
    private bool generateLakeOnStart = false;

    [SerializeField]
    private bool generateRiverOnStart = false;


    // =========================================================
    // INTERNAL WATER DATA
    // =========================================================

    private class LakeInfo
    {
        public Vector3 center;
        public float radiusX;
        public float radiusZ;
    }

    private class RiverInfo
    {
        public List<Vector3> path;
        public float radius;
    }

    private readonly List<LakeInfo> generatedLakes =
        new List<LakeInfo>();

    private readonly List<RiverInfo> generatedRivers =
        new List<RiverInfo>();


    // =========================================================
    // INTERNAL
    // =========================================================

    private Transform waterParent;

    private float[,] originalHeights;

    private bool terrainBackupCreated = false;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        FindTerrain();

        FindTerrainGenerator();

        ResolvePlayerTransform();

        if (generateLakeOnStart)
        {
            CreateLake();
        }

        if (generateRiverOnStart)
        {
            CreateRiver();
        }
    }


#if UNITY_EDITOR

    // =========================================================
    // UNITY PLAY MODE CLEANUP
    // =========================================================

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -=
            OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state ==
            PlayModeStateChange.ExitingPlayMode)
        {
            DeleteWaterObjects();

            RestoreTerrain();

            generatedLakes.Clear();
            generatedRivers.Clear();

            Debug.Log(
                "WaterManager: Play Mode stopped. " +
                "Generated water deleted and terrain restored."
            );
        }
    }

#endif


    // =========================================================
    // FIND TERRAIN
    // =========================================================

    private void FindTerrain()
    {
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
        }

        if (targetTerrain == null)
        {
            targetTerrain = FindObjectOfType<Terrain>();
        }

        if (targetTerrain == null)
        {
            Debug.LogError(
                "WaterManager: No Terrain found."
            );
        }
    }


    // =========================================================
    // FIND TERRAIN GENERATOR
    // =========================================================

    private void FindTerrainGenerator()
    {
        if (terrainGenerator != null)
        {
            return;
        }

        terrainGenerator =
            FindObjectOfType<TerrainGenerator>();
    }


    // =========================================================
    // FIND PLAYER
    // =========================================================

    private void ResolvePlayerTransform()
    {
        if (playerTransform != null)
        {
            return;
        }

        if (Camera.main != null)
        {
            playerTransform =
                Camera.main.transform;
        }
    }


    // =========================================================
    // BACKUP TERRAIN
    // =========================================================

    private void BackupTerrain()
    {
        if (targetTerrain == null)
        {
            return;
        }

        if (terrainBackupCreated)
        {
            return;
        }

        TerrainData data =
            targetTerrain.terrainData;

        if (data == null)
        {
            return;
        }

        int resolution =
            data.heightmapResolution;

        originalHeights =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        terrainBackupCreated = true;
    }


    // =========================================================
    // PREPARE FOR NEW WATER
    // =========================================================
    //
    // IMPORTANT:
    //
    // Water is NO LONGER deleted here.
    //
    // This allows multiple lakes/rivers to coexist.
    //
    // =========================================================

    private void PrepareForNewWater()
    {
        if (targetTerrain == null)
        {
            return;
        }

        BackupTerrain();
    }


    // =========================================================
    // CREATE WATER PARENT
    // =========================================================

    private void CreateWaterParent()
    {
        if (waterParent != null)
        {
            return;
        }

        GameObject existing =
            GameObject.Find(
                "Generated_Water"
            );

        if (existing != null)
        {
            waterParent =
                existing.transform;

            return;
        }

        GameObject parent =
            new GameObject(
                "Generated_Water"
            );

        waterParent =
            parent.transform;

        waterParent.SetParent(
            transform
        );
    }


    // =========================================================
    // CREATE RIVER
    // =========================================================

    public void CreateRiver()
    {
        FindTerrain();
        FindTerrainGenerator();
        ResolvePlayerTransform();

        if (targetTerrain == null)
        {
            return;
        }

        if (!ValidateRiverSettings())
        {
            return;
        }

        PrepareForNewWater();

        List<Vector3> path =
            FindSafeRiverPath();

        if (path == null ||
            path.Count < 2)
        {
            Debug.LogWarning(
                "WaterManager: Could not find a safe nearby location for the river."
            );

            return;
        }

        CreateWaterParent();

        ShapeRiverTerrain(path);

        CreateRiverWater(path);

        RiverInfo river =
            new RiverInfo();

        river.path =
            new List<Vector3>(path);

        river.radius =
            riverWidth * 0.5f +
            riverBankWidth +
            placementMargin;

        generatedRivers.Add(
            river
        );

        RefreshTerrain();

        Debug.Log(
            "WaterManager: Natural river created. " +
            "Total rivers: " +
            generatedRivers.Count
        );
    }


    // =========================================================
    // CREATE LAKE
    // =========================================================

    public void CreateLake()
    {
        FindTerrain();
        FindTerrainGenerator();
        ResolvePlayerTransform();

        if (targetTerrain == null)
        {
            return;
        }

        if (!ValidateLakeSettings())
        {
            return;
        }

        PrepareForNewWater();

        Vector3 center;

        if (!FindSafeLakeLocation(
                out center))
        {
            Debug.LogWarning(
                "WaterManager: Could not find a safe nearby location for the lake."
            );

            return;
        }

        CreateWaterParent();

        ShapeLakeTerrain(center);

        CreateLakeWater(center);

        LakeInfo lake =
            new LakeInfo();

        lake.center =
            center;

        lake.radiusX =
            lakeWidth * 0.5f +
            lakeBankWidth +
            placementMargin;

        lake.radiusZ =
            lakeLength * 0.5f +
            lakeBankWidth +
            placementMargin;

        generatedLakes.Add(
            lake
        );

        RefreshTerrain();

        Debug.Log(
            "WaterManager: Natural lake created. " +
            "Total lakes: " +
            generatedLakes.Count
        );
    }


    // =========================================================
    // VALIDATE RIVER SETTINGS
    // =========================================================

    private bool ValidateRiverSettings()
    {
        if (riverLength <= 0f)
        {
            Debug.LogError(
                "WaterManager: River Length must be greater than 0."
            );

            return false;
        }

        if (riverWidth <= 0f)
        {
            Debug.LogError(
                "WaterManager: River Width must be greater than 0."
            );

            return false;
        }

        if (riverDepth <= 0f)
        {
            Debug.LogError(
                "WaterManager: River Depth must be greater than 0."
            );

            return false;
        }

        if (riverSegments < 2)
        {
            riverSegments = 2;
        }

        return true;
    }


    // =========================================================
    // VALIDATE LAKE SETTINGS
    // =========================================================

    private bool ValidateLakeSettings()
    {
        if (lakeWidth <= 0f)
        {
            Debug.LogError(
                "WaterManager: Lake Width must be greater than 0."
            );

            return false;
        }

        if (lakeLength <= 0f)
        {
            Debug.LogError(
                "WaterManager: Lake Length must be greater than 0."
            );

            return false;
        }

        if (lakeDepth <= 0f)
        {
            Debug.LogError(
                "WaterManager: Lake Depth must be greater than 0."
            );

            return false;
        }

        return true;
    }


    // =========================================================
    // FIND SAFE LAKE LOCATION
    // =========================================================

    private bool FindSafeLakeLocation(
        out Vector3 safeCenter)
    {
        safeCenter = Vector3.zero;

        for (int attempt = 0;
             attempt < placementAttempts;
             attempt++)
        {
            Vector3 candidate =
                GetRandomNearbyPoint();

            if (!IsLakeFootprintInsideTerrain(
                    candidate))
            {
                continue;
            }

            if (!IsSafeLakeLocation(
                    candidate))
            {
                continue;
            }

            if (!IsLakePositionAvailable(
                    candidate))
            {
                continue;
            }

            safeCenter =
                candidate;

            safeCenter.y =
                targetTerrain.SampleHeight(
                    safeCenter
                ) +
                targetTerrain.transform.position.y;

            return true;
        }

        return false;
    }


    // =========================================================
    // SAFE LAKE TEST
    // =========================================================

    private bool IsSafeLakeLocation(
        Vector3 center)
    {
        float sampleX =
            lakeWidth * 0.5f +
            lakeBankWidth +
            placementMargin;

        float sampleZ =
            lakeLength * 0.5f +
            lakeBankWidth +
            placementMargin;

        Vector3[] samples =
        {
            center,

            center + new Vector3(
                sampleX,
                0f,
                0f
            ),

            center + new Vector3(
                -sampleX,
                0f,
                0f
            ),

            center + new Vector3(
                0f,
                0f,
                sampleZ
            ),

            center + new Vector3(
                0f,
                0f,
                -sampleZ
            ),

            center + new Vector3(
                sampleX * 0.7f,
                0f,
                sampleZ * 0.7f
            ),

            center + new Vector3(
                -sampleX * 0.7f,
                0f,
                sampleZ * 0.7f
            ),

            center + new Vector3(
                sampleX * 0.7f,
                0f,
                -sampleZ * 0.7f
            ),

            center + new Vector3(
                -sampleX * 0.7f,
                0f,
                -sampleZ * 0.7f
            )
        };

        float totalSlope = 0f;

        for (int i = 0;
             i < samples.Length;
             i++)
        {
            Vector3 point =
                samples[i];

            if (!IsInsideTerrain(
                    point,
                    0f))
            {
                return false;
            }

            float slope =
                GetTerrainSlope(point);

            if (slope > maxAllowedSlope)
            {
                return false;
            }

            totalSlope += slope;

            if (IsMountainLocation(point))
            {
                return false;
            }
        }

        float averageSlope =
            totalSlope /
            samples.Length;

        if (averageSlope >
            maxAllowedSlope * 0.8f)
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // FIND SAFE RIVER PATH
    // =========================================================

    private List<Vector3> FindSafeRiverPath()
    {
        for (int attempt = 0;
             attempt < placementAttempts;
             attempt++)
        {
            Vector3 center =
                GetRandomNearbyPoint();

            float angle =
                Random.Range(
                    0f,
                    360f
                );

            List<Vector3> candidate =
                GenerateRiverPath(
                    center,
                    angle
                );

            if (candidate == null ||
                candidate.Count < 2)
            {
                continue;
            }

            if (!IsSafeRiverPath(
                    candidate))
            {
                continue;
            }

            if (!IsRiverPathAvailable(
                    candidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }


    // =========================================================
    // SAFE RIVER TEST
    // =========================================================

    private bool IsSafeRiverPath(
        List<Vector3> path)
    {
        float totalSlope = 0f;

        int sampleCount = 0;

        for (int i = 0;
             i < path.Count;
             i++)
        {
            Vector3 point =
                path[i];

            float footprint =
                riverWidth * 0.5f +
                riverBankWidth +
                placementMargin;

            if (!IsInsideTerrain(
                    point,
                    footprint))
            {
                return false;
            }

            float slope =
                GetTerrainSlope(point);

            if (slope >
                maxAllowedSlope)
            {
                return false;
            }

            if (IsMountainLocation(point))
            {
                return false;
            }

            totalSlope += slope;

            sampleCount++;

            if (i < path.Count - 1)
            {
                Vector3 next =
                    path[i + 1];

                Vector3 direction =
                    next - point;

                direction.y = 0f;

                if (direction.sqrMagnitude >
                    0.001f)
                {
                    direction.Normalize();

                    Vector3 side =
                        new Vector3(
                            -direction.z,
                            0f,
                            direction.x
                        );

                    float sideDistance =
                        riverWidth * 0.5f +
                        riverBankWidth +
                        placementMargin;

                    Vector3 left =
                        point +
                        side * sideDistance;

                    Vector3 right =
                        point -
                        side * sideDistance;

                    if (!IsInsideTerrain(
                            left,
                            0f) ||
                        !IsInsideTerrain(
                            right,
                            0f))
                    {
                        return false;
                    }

                    if (GetTerrainSlope(left) >
                        maxAllowedSlope ||
                        GetTerrainSlope(right) >
                        maxAllowedSlope)
                    {
                        return false;
                    }

                    if (IsMountainLocation(left) ||
                        IsMountainLocation(right))
                    {
                        return false;
                    }
                }
            }
        }

        if (sampleCount == 0)
        {
            return false;
        }

        float averageSlope =
            totalSlope /
            sampleCount;

        if (averageSlope >
            maxAllowedSlope * 0.8f)
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // WATER COLLISION
    // =========================================================

    private bool IsLakePositionAvailable(
        Vector3 center)
    {
        float newRadiusX =
            lakeWidth * 0.5f +
            lakeBankWidth +
            placementMargin;

        float newRadiusZ =
            lakeLength * 0.5f +
            lakeBankWidth +
            placementMargin;

        // -----------------------------------------------------
        // Existing lakes
        // -----------------------------------------------------

        for (int i = 0;
             i < generatedLakes.Count;
             i++)
        {
            LakeInfo lake =
                generatedLakes[i];

            if (lake == null)
            {
                continue;
            }

            float dx =
                Mathf.Abs(
                    center.x -
                    lake.center.x
                );

            float dz =
                Mathf.Abs(
                    center.z -
                    lake.center.z
                );

            float combinedX =
                newRadiusX +
                lake.radiusX +
                minimumWaterSpacing;

            float combinedZ =
                newRadiusZ +
                lake.radiusZ +
                minimumWaterSpacing;

            if (dx < combinedX &&
                dz < combinedZ)
            {
                return false;
            }
        }

        // -----------------------------------------------------
        // Existing rivers
        // -----------------------------------------------------

        for (int i = 0;
             i < generatedRivers.Count;
             i++)
        {
            RiverInfo river =
                generatedRivers[i];

            if (river == null ||
                river.path == null)
            {
                continue;
            }

            if (DistanceToRiver(
                    center,
                    river.path)
                <
                newRadiusX +
                river.radius +
                minimumWaterSpacing)
            {
                return false;
            }
        }

        // -----------------------------------------------------
        // Mountains
        // -----------------------------------------------------

        if (IsMountainAreaOccupied(
                center,
                Mathf.Max(
                    newRadiusX,
                    newRadiusZ
                )))
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // RIVER COLLISION
    // =========================================================

    private bool IsRiverPathAvailable(
        List<Vector3> path)
    {
        if (path == null ||
            path.Count < 2)
        {
            return false;
        }

        float riverRadius =
            riverWidth * 0.5f +
            riverBankWidth +
            placementMargin;

        // -----------------------------------------------------
        // Check every path point against lakes
        // -----------------------------------------------------

        for (int p = 0;
             p < path.Count;
             p++)
        {
            Vector3 point =
                path[p];

            for (int l = 0;
                 l < generatedLakes.Count;
                 l++)
            {
                LakeInfo lake =
                    generatedLakes[l];

                if (lake == null)
                {
                    continue;
                }

                if (IsPointInsideExpandedLake(
                        point,
                        lake,
                        riverRadius +
                        minimumWaterSpacing))
                {
                    return false;
                }
            }
        }

        // -----------------------------------------------------
        // Check river against existing rivers
        // -----------------------------------------------------

        for (int r = 0;
             r < generatedRivers.Count;
             r++)
        {
            RiverInfo existingRiver =
                generatedRivers[r];

            if (existingRiver == null ||
                existingRiver.path == null)
            {
                continue;
            }

            for (int p = 0;
                 p < path.Count;
                 p++)
            {
                if (DistanceToRiver(
                        path[p],
                        existingRiver.path)
                    <
                    riverRadius +
                    existingRiver.radius +
                    minimumWaterSpacing)
                {
                    return false;
                }
            }
        }

        // -----------------------------------------------------
        // Check entire river path against mountains
        // -----------------------------------------------------

        for (int p = 0;
             p < path.Count;
             p++)
        {
            Vector3 point =
                path[p];

            if (IsMountainAreaOccupied(
                    point,
                    riverRadius))
            {
                return false;
            }
        }

        return true;
    }


    // =========================================================
    // PUBLIC MOUNTAIN COLLISION API
    // =========================================================
    //
    // TerrainGenerator calls this before creating a mountain.
    //
    // =========================================================

    public bool IsMountainPositionAvailable(
        Vector2 terrainPosition,
        float mountainRadius)
    {
        if (targetTerrain == null)
        {
            FindTerrain();
        }

        Vector3 worldPosition =
            new Vector3(
                terrainPosition.x,
                0f,
                terrainPosition.y
            );

        return IsMountainAreaOccupied(
            worldPosition,
            mountainRadius
        ) == false;
    }


    // =========================================================
    // CHECK MOUNTAIN AREA AGAINST WATER
    // =========================================================

    private bool IsMountainAreaOccupied(
        Vector3 center,
        float mountainRadius)
    {
        float radius =
            Mathf.Max(
                mountainRadius,
                1f
            );

        // -----------------------------------------------------
        // LAKES
        // -----------------------------------------------------

        for (int i = 0;
             i < generatedLakes.Count;
             i++)
        {
            LakeInfo lake =
                generatedLakes[i];

            if (lake == null)
            {
                continue;
            }

            if (CircleIntersectsEllipse(
                    center,
                    radius,
                    lake.center,
                    lake.radiusX,
                    lake.radiusZ))
            {
                return true;
            }
        }

        // -----------------------------------------------------
        // RIVERS
        // -----------------------------------------------------

        for (int i = 0;
             i < generatedRivers.Count;
             i++)
        {
            RiverInfo river =
                generatedRivers[i];

            if (river == null ||
                river.path == null)
            {
                continue;
            }

            float riverRadius =
                river.radius +
                minimumWaterSpacing;

            if (DistanceToRiver(
                    center,
                    river.path)
                <
                radius +
                riverRadius)
            {
                return true;
            }
        }

        return false;
    }


    // =========================================================
    // CIRCLE / ELLIPSE INTERSECTION
    // =========================================================

    private bool CircleIntersectsEllipse(
        Vector3 circleCenter,
        float circleRadius,
        Vector3 ellipseCenter,
        float ellipseRadiusX,
        float ellipseRadiusZ)
    {
        float dx =
            circleCenter.x -
            ellipseCenter.x;

        float dz =
            circleCenter.z -
            ellipseCenter.z;

        float normalizedDistance =
            Mathf.Sqrt(
                (dx * dx) /
                Mathf.Max(
                    ellipseRadiusX *
                    ellipseRadiusX,
                    0.001f
                )
                +
                (dz * dz) /
                Mathf.Max(
                    ellipseRadiusZ *
                    ellipseRadiusZ,
                    0.001f
                )
            );

        float expandedX =
            ellipseRadiusX +
            circleRadius;

        float expandedZ =
            ellipseRadiusZ +
            circleRadius;

        float expandedDistance =
            Mathf.Sqrt(
                (dx * dx) /
                Mathf.Max(
                    expandedX *
                    expandedX,
                    0.001f
                )
                +
                (dz * dz) /
                Mathf.Max(
                    expandedZ *
                    expandedZ,
                    0.001f
                )
            );

        return expandedDistance < 1f ||
               normalizedDistance < 1f;
    }


    // =========================================================
    // POINT INSIDE EXPANDED LAKE
    // =========================================================

    private bool IsPointInsideExpandedLake(
        Vector3 point,
        LakeInfo lake,
        float extraRadius)
    {
        float radiusX =
            lake.radiusX +
            extraRadius;

        float radiusZ =
            lake.radiusZ +
            extraRadius;

        float dx =
            point.x -
            lake.center.x;

        float dz =
            point.z -
            lake.center.z;

        float value =
            (dx * dx) /
            Mathf.Max(
                radiusX * radiusX,
                0.001f
            )
            +
            (dz * dz) /
            Mathf.Max(
                radiusZ * radiusZ,
                0.001f
            );

        return value <= 1f;
    }


    // =========================================================
    // RANDOM NEARBY POINT
    // =========================================================

    private Vector3 GetRandomNearbyPoint()
    {
        Vector3 origin;

        if (playerTransform != null)
        {
            origin =
                playerTransform.position;
        }
        else
        {
            origin =
                GetTerrainCenter();
        }

        float minimum =
            Mathf.Max(
                0f,
                minWaterDistanceFromPlayer
            );

        float maximum =
            Mathf.Max(
                minimum,
                maxWaterDistanceFromPlayer
            );

        float angle =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        float distance =
            Random.Range(
                minimum,
                maximum
            );

        Vector3 candidate =
            origin +
            new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );

        candidate.y =
            targetTerrain.SampleHeight(
                candidate
            ) +
            targetTerrain.transform.position.y;

        return candidate;
    }


    // =========================================================
    // GENERATE RANDOM RIVER PATH
    // =========================================================

    private List<Vector3> GenerateRiverPath(
        Vector3 center,
        float angleDegrees)
    {
        List<Vector3> path =
            new List<Vector3>();

        int segments =
            Mathf.Max(
                2,
                riverSegments
            );

        float angle =
            angleDegrees *
            Mathf.Deg2Rad;

        Vector3 forward =
            new Vector3(
                Mathf.Sin(angle),
                0f,
                Mathf.Cos(angle)
            );

        Vector3 side =
            new Vector3(
                forward.z,
                0f,
                -forward.x
            );

        float halfLength =
            riverLength * 0.5f;

        for (int i = 0;
             i < segments;
             i++)
        {
            float t =
                i /
                (float)(segments - 1);

            float distance =
                Mathf.Lerp(
                    -halfLength,
                    halfLength,
                    t
                );

            float meander =
                Mathf.Sin(
                    t *
                    Mathf.PI *
                    2f
                ) *
                riverMeander;

            Vector3 point =
                center +
                forward * distance +
                side * meander;

            float margin =
                riverWidth * 0.5f +
                riverBankWidth +
                placementMargin;

            if (!IsInsideTerrain(
                    point,
                    margin))
            {
                return null;
            }

            point.y =
                targetTerrain.SampleHeight(
                    point
                ) +
                targetTerrain.transform.position.y;

            path.Add(point);
        }

        return path;
    }


    // =========================================================
    // TERRAIN BOUNDS
    // =========================================================

    private bool IsInsideTerrain(
        Vector3 point,
        float margin)
    {
        if (targetTerrain == null)
        {
            return false;
        }

        Vector3 terrainPosition =
            targetTerrain.transform.position;

        Vector3 terrainSize =
            targetTerrain.terrainData.size;

        float minX =
            terrainPosition.x +
            margin;

        float maxX =
            terrainPosition.x +
            terrainSize.x -
            margin;

        float minZ =
            terrainPosition.z +
            margin;

        float maxZ =
            terrainPosition.z +
            terrainSize.z -
            margin;

        return
            point.x >= minX &&
            point.x <= maxX &&
            point.z >= minZ &&
            point.z <= maxZ;
    }


    // =========================================================
    // LAKE FOOTPRINT BOUNDS
    // =========================================================

    private bool IsLakeFootprintInsideTerrain(
        Vector3 center)
    {
        float radiusX =
            lakeWidth * 0.5f +
            lakeBankWidth +
            placementMargin;

        float radiusZ =
            lakeLength * 0.5f +
            lakeBankWidth +
            placementMargin;

        Vector3[] points =
        {
            center + new Vector3(
                radiusX,
                0f,
                0f
            ),

            center + new Vector3(
                -radiusX,
                0f,
                0f
            ),

            center + new Vector3(
                0f,
                0f,
                radiusZ
            ),

            center + new Vector3(
                0f,
                0f,
                -radiusZ
            )
        };

        for (int i = 0;
             i < points.Length;
             i++)
        {
            if (!IsInsideTerrain(
                    points[i],
                    0f))
            {
                return false;
            }
        }

        return true;
    }


    // =========================================================
    // TERRAIN SLOPE
    // =========================================================

    private float GetTerrainSlope(
        Vector3 worldPosition)
    {
        if (targetTerrain == null)
        {
            return 90f;
        }

        Vector3 terrainSize =
            targetTerrain.terrainData.size;

        float sampleDistance =
            Mathf.Clamp(
                2f,
                0.5f,
                Mathf.Min(
                    terrainSize.x,
                    terrainSize.z
                ) * 0.02f
            );

        Vector3 left =
            worldPosition +
            Vector3.left *
            sampleDistance;

        Vector3 right =
            worldPosition +
            Vector3.right *
            sampleDistance;

        Vector3 forward =
            worldPosition +
            Vector3.forward *
            sampleDistance;

        Vector3 back =
            worldPosition +
            Vector3.back *
            sampleDistance;

        float terrainY =
            targetTerrain.transform.position.y;

        float hLeft =
            targetTerrain.SampleHeight(
                left
            ) +
            terrainY;

        float hRight =
            targetTerrain.SampleHeight(
                right
            ) +
            terrainY;

        float hForward =
            targetTerrain.SampleHeight(
                forward
            ) +
            terrainY;

        float hBack =
            targetTerrain.SampleHeight(
                back
            ) +
            terrainY;

        float dx =
            (hRight - hLeft) /
            (sampleDistance * 2f);

        float dz =
            (hForward - hBack) /
            (sampleDistance * 2f);

        float slope =
            Mathf.Sqrt(
                dx * dx +
                dz * dz
            );

        return
            Mathf.Atan(
                slope
            ) *
            Mathf.Rad2Deg;
    }


    // =========================================================
    // MOUNTAIN DETECTION
    // =========================================================

    private bool IsMountainLocation(
        Vector3 point)
    {
        if (targetTerrain == null)
        {
            return true;
        }

        float terrainY =
            targetTerrain.transform.position.y;

        float centerHeight =
            targetTerrain.SampleHeight(
                point
            ) +
            terrainY;

        float lowestNearby =
            centerHeight;

        int samples = 12;

        for (int i = 0;
             i < samples;
             i++)
        {
            float angle =
                i /
                (float)samples *
                Mathf.PI *
                2f;

            Vector3 nearby =
                point +
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)
                ) *
                mountainCheckRadius;

            if (!IsInsideTerrain(
                    nearby,
                    0f))
            {
                continue;
            }

            float height =
                targetTerrain.SampleHeight(
                    nearby
                ) +
                terrainY;

            if (height < lowestNearby)
            {
                lowestNearby = height;
            }
        }

        float localRise =
            centerHeight -
            lowestNearby;

        if (localRise >
            mountainHeightThreshold)
        {
            return true;
        }

        if (GetTerrainSlope(point) >
            maxAllowedSlope)
        {
            return true;
        }

        return false;
    }


    // =========================================================
    // DISTANCE TO RIVER
    // =========================================================

    private float DistanceToRiver(
        Vector3 point,
        List<Vector3> path)
    {
        if (path == null ||
            path.Count == 0)
        {
            return float.MaxValue;
        }

        float closest =
            float.MaxValue;

        for (int i = 0;
             i < path.Count - 1;
             i++)
        {
            Vector3 a =
                path[i];

            Vector3 b =
                path[i + 1];

            a.y = 0f;
            b.y = 0f;

            float distance =
                DistanceToLine(
                    point,
                    a,
                    b
                );

            if (distance < closest)
            {
                closest = distance;
            }
        }

        return closest;
    }


    // =========================================================
    // DISTANCE TO LINE
    // =========================================================

    private float DistanceToLine(
        Vector3 point,
        Vector3 a,
        Vector3 b)
    {
        Vector3 line =
            b - a;

        float lengthSquared =
            line.sqrMagnitude;

        if (lengthSquared < 0.001f)
        {
            return Vector3.Distance(
                point,
                a
            );
        }

        float t =
            Vector3.Dot(
                point - a,
                line
            ) /
            lengthSquared;

        t =
            Mathf.Clamp01(t);

        Vector3 closest =
            a +
            line * t;

        return Vector3.Distance(
            point,
            closest
        );
    }


    // =========================================================
    // SHAPE RIVER TERRAIN
    // =========================================================

    private void ShapeRiverTerrain(
        List<Vector3> path)
    {
        TerrainData data =
            targetTerrain.terrainData;

        int resolution =
            data.heightmapResolution;

        float[,] heights =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        Vector3 terrainPosition =
            targetTerrain.transform.position;

        Vector3 terrainSize =
            data.size;

        float riverRadius =
            riverWidth * 0.5f;

        float totalWidth =
            riverRadius +
            riverBankWidth;

        for (int y = 0;
             y < resolution;
             y++)
        {
            for (int x = 0;
                 x < resolution;
                 x++)
            {
                float worldX =
                    terrainPosition.x +
                    x /
                    (float)(resolution - 1) *
                    terrainSize.x;

                float worldZ =
                    terrainPosition.z +
                    y /
                    (float)(resolution - 1) *
                    terrainSize.z;

                Vector3 worldPoint =
                    new Vector3(
                        worldX,
                        0f,
                        worldZ
                    );

                float distance =
                    DistanceToRiver(
                        worldPoint,
                        path
                    );

                float original =
                    heights[y, x];

                if (distance <= riverRadius)
                {
                    float t =
                        distance /
                        riverRadius;

                    t =
                        Mathf.Clamp01(t);

                    float bedShape =
                        Mathf.SmoothStep(
                            1f,
                            0.15f,
                            t
                        );

                    float depth =
                        riverDepth *
                        bedShape;

                    float target =
                        original -
                        depth /
                        terrainSize.y;

                    float edgeBlend =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            t
                        );

                    heights[y, x] =
                        Mathf.Lerp(
                            target,
                            original,
                            edgeBlend * 0.15f
                        );
                }
                else if (distance <= totalWidth)
                {
                    float bankDistance =
                        distance -
                        riverRadius;

                    float bankT =
                        bankDistance /
                        riverBankWidth;

                    bankT =
                        Mathf.Clamp01(
                            bankT
                        );

                    float slope =
                        Mathf.Sin(
                            bankT *
                            Mathf.PI
                        );

                    slope =
                        Mathf.Pow(
                            slope,
                            Mathf.Lerp(
                                1f,
                                2.5f,
                                riverBankSmoothness
                            )
                        );

                    float noise =
                        Mathf.PerlinNoise(
                            worldX *
                            riverBankNoiseScale,
                            worldZ *
                            riverBankNoiseScale
                        );

                    noise =
                        (noise - 0.5f) * 2f;

                    float naturalAmount =
                        1f +
                        noise *
                        riverBankNaturalVariation;

                    float bankRaise =
                        riverBankHeight *
                        slope *
                        naturalAmount;

                    float raised =
                        original +
                        bankRaise /
                        terrainSize.y;

                    float blend =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            slope
                        );

                    heights[y, x] =
                        Mathf.Lerp(
                            original,
                            raised,
                            blend
                        );
                }
            }
        }

        data.SetHeights(
            0,
            0,
            heights
        );
    }


    // =========================================================
    // CREATE RIVER WATER
    // =========================================================

    private void CreateRiverWater(
        List<Vector3> path)
    {
        if (path == null ||
            path.Count < 2)
        {
            return;
        }

        GameObject river =
            new GameObject(
                "River_Water"
            );

        river.transform.SetParent(
            waterParent
        );

        MeshFilter meshFilter =
            river.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer =
            river.AddComponent<MeshRenderer>();

        Mesh mesh =
            new Mesh();

        mesh.name =
            "Continuous_River_Water";

        float halfWidth =
            riverWidth * 0.5f;

        int capSegments = 16;

        List<Vector3> vertices =
            new List<Vector3>();

        List<int> triangles =
            new List<int>();

        List<Vector2> uvs =
            new List<Vector2>();


        // =====================================================
        // MAIN RIVER STRIP
        // =====================================================

        for (int i = 0;
             i < path.Count;
             i++)
        {
            Vector3 point =
                path[i];

            Vector3 direction;

            if (i == 0)
            {
                direction =
                    path[1] -
                    path[0];
            }
            else if (i ==
                     path.Count - 1)
            {
                direction =
                    path[i] -
                    path[i - 1];
            }
            else
            {
                direction =
                    path[i + 1] -
                    path[i - 1];
            }

            direction.y = 0f;

            if (direction.sqrMagnitude <
                0.001f)
            {
                direction =
                    Vector3.forward;
            }

            direction.Normalize();

            Vector3 side =
                new Vector3(
                    -direction.z,
                    0f,
                    direction.x
                );

            side.Normalize();

            float terrainHeight =
                targetTerrain.SampleHeight(
                    point
                ) +
                targetTerrain.transform.position.y;

            float waterY =
                terrainHeight +
                waterHeightOffset;

            Vector3 left =
                point +
                side *
                halfWidth;

            Vector3 right =
                point -
                side *
                halfWidth;

            left.y =
                waterY;

            right.y =
                waterY;

            vertices.Add(left);
            vertices.Add(right);

            float uvX =
                i /
                (float)(path.Count - 1);

            uvs.Add(
                new Vector2(
                    uvX,
                    0f
                )
            );

            uvs.Add(
                new Vector2(
                    uvX,
                    1f
                )
            );
        }


        // =====================================================
        // MAIN RIVER TRIANGLES
        // =====================================================

        for (int i = 0;
             i < path.Count - 1;
             i++)
        {
            int current =
                i * 2;

            int next =
                (i + 1) * 2;

            triangles.Add(current);
            triangles.Add(next);
            triangles.Add(current + 1);

            triangles.Add(current + 1);
            triangles.Add(next);
            triangles.Add(next + 1);
        }


        // =====================================================
        // START CAP
        // =====================================================

        Vector3 startPoint =
            path[0];

        Vector3 startDirection =
            path[1] -
            path[0];

        startDirection.y = 0f;

        if (startDirection.sqrMagnitude <
            0.001f)
        {
            startDirection =
                Vector3.forward;
        }

        startDirection.Normalize();

        Vector3 startSide =
            new Vector3(
                -startDirection.z,
                0f,
                startDirection.x
            );

        startSide.Normalize();

        float startWaterY =
            targetTerrain.SampleHeight(
                startPoint
            ) +
            targetTerrain.transform.position.y +
            waterHeightOffset;

        int startCenterIndex =
            vertices.Count;

        vertices.Add(
            new Vector3(
                startPoint.x,
                startWaterY,
                startPoint.z
            )
        );

        uvs.Add(
            new Vector2(
                0f,
                0.5f
            )
        );

        int startFirstIndex =
            vertices.Count;

        for (int i = 0;
             i <= capSegments;
             i++)
        {
            float t =
                i /
                (float)capSegments;

            float angle =
                Mathf.PI +
                t * Mathf.PI;

            Vector3 radial =
                startSide *
                Mathf.Cos(angle) +
                startDirection *
                Mathf.Sin(angle);

            Vector3 p =
                startPoint +
                radial *
                halfWidth;

            p.y =
                startWaterY;

            vertices.Add(p);

            uvs.Add(
                new Vector2(
                    0f,
                    t
                )
            );
        }

        for (int i = 0;
             i < capSegments;
             i++)
        {
            int a =
                startFirstIndex + i;

            int b =
                startFirstIndex + i + 1;

            triangles.Add(
                startCenterIndex
            );

            triangles.Add(
                a
            );

            triangles.Add(
                b
            );
        }


        // =====================================================
        // END CAP
        // =====================================================

        int lastIndex =
            path.Count - 1;

        Vector3 endPoint =
            path[lastIndex];

        Vector3 endDirection =
            path[lastIndex] -
            path[lastIndex - 1];

        endDirection.y = 0f;

        if (endDirection.sqrMagnitude <
            0.001f)
        {
            endDirection =
                Vector3.forward;
        }

        endDirection.Normalize();

        Vector3 endSide =
            new Vector3(
                -endDirection.z,
                0f,
                endDirection.x
            );

        endSide.Normalize();

        float endWaterY =
            targetTerrain.SampleHeight(
                endPoint
            ) +
            targetTerrain.transform.position.y +
            waterHeightOffset;

        int endCenterIndex =
            vertices.Count;

        vertices.Add(
            new Vector3(
                endPoint.x,
                endWaterY,
                endPoint.z
            )
        );

        uvs.Add(
            new Vector2(
                1f,
                0.5f
            )
        );

        int endFirstIndex =
            vertices.Count;

        for (int i = 0;
             i <= capSegments;
             i++)
        {
            float t =
                i /
                (float)capSegments;

            float angle =
                t * Mathf.PI;

            Vector3 radial =
                -endSide *
                Mathf.Cos(angle) +
                endDirection *
                Mathf.Sin(angle);

            Vector3 p =
                endPoint +
                radial *
                halfWidth;

            p.y =
                endWaterY;

            vertices.Add(p);

            uvs.Add(
                new Vector2(
                    1f,
                    t
                )
            );
        }

        for (int i = 0;
             i < capSegments;
             i++)
        {
            int a =
                endFirstIndex + i;

            int b =
                endFirstIndex + i + 1;

            triangles.Add(
                endCenterIndex
            );

            triangles.Add(
                b
            );

            triangles.Add(
                a
            );
        }


        // =====================================================
        // APPLY MESH
        // =====================================================

        if (vertices.Count >
            65000)
        {
            mesh.indexFormat =
                UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.SetVertices(
            vertices
        );

        mesh.SetTriangles(
            triangles,
            0
        );

        mesh.SetUVs(
            0,
            uvs
        );

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh =
            mesh;


        // =====================================================
        // MATERIAL
        // =====================================================

        if (waterMaterial != null)
        {
            meshRenderer.sharedMaterial =
                waterMaterial;
        }
        else
        {
            Debug.LogWarning(
                "WaterManager: No water material assigned."
            );
        }
    }


    // =========================================================
    // SHAPE LAKE TERRAIN
    // =========================================================

    private void ShapeLakeTerrain(
        Vector3 center)
    {
        TerrainData data =
            targetTerrain.terrainData;

        int resolution =
            data.heightmapResolution;

        float[,] heights =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        Vector3 terrainPosition =
            targetTerrain.transform.position;

        Vector3 terrainSize =
            data.size;

        float lakeRadiusX =
            lakeWidth * 0.5f;

        float lakeRadiusZ =
            lakeLength * 0.5f;

        float bankRadiusX =
            lakeRadiusX +
            lakeBankWidth;

        float bankRadiusZ =
            lakeRadiusZ +
            lakeBankWidth;

        float centerHeight =
            targetTerrain.SampleHeight(
                center
            ) +
            terrainPosition.y;

        for (int y = 0;
             y < resolution;
             y++)
        {
            for (int x = 0;
                 x < resolution;
                 x++)
            {
                float worldX =
                    terrainPosition.x +
                    x /
                    (float)(resolution - 1) *
                    terrainSize.x;

                float worldZ =
                    terrainPosition.z +
                    y /
                    (float)(resolution - 1) *
                    terrainSize.z;

                float dx =
                    (worldX - center.x) /
                    lakeRadiusX;

                float dz =
                    (worldZ - center.z) /
                    lakeRadiusZ;

                float distance =
                    Mathf.Sqrt(
                        dx * dx +
                        dz * dz
                    );

                float original =
                    heights[y, x];

                if (distance <= 1f)
                {
                    float bedT =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            distance
                        );

                    float bedShape =
                        Mathf.Lerp(
                            1f,
                            0.1f,
                            bedT
                        );

                    float depth =
                        lakeDepth *
                        bedShape;

                    float targetWorldHeight =
                        centerHeight -
                        depth;

                    float originalWorldHeight =
                        terrainPosition.y +
                        original *
                        terrainSize.y;

                    float blend =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            distance
                        );

                    float finalHeight =
                        Mathf.Lerp(
                            targetWorldHeight,
                            originalWorldHeight,
                            blend * 0.15f
                        );

                    heights[y, x] =
                        Mathf.InverseLerp(
                            terrainPosition.y,
                            terrainPosition.y +
                            terrainSize.y,
                            finalHeight
                        );
                }
                else
                {
                    float outerDX =
                        (worldX - center.x) /
                        bankRadiusX;

                    float outerDZ =
                        (worldZ - center.z) /
                        bankRadiusZ;

                    float outerDistance =
                        Mathf.Sqrt(
                            outerDX * outerDX +
                            outerDZ * outerDZ
                        );

                    if (outerDistance <= 1f)
                    {
                        float bankT =
                            Mathf.InverseLerp(
                                1f,
                                0f,
                                outerDistance
                            );

                        bankT =
                            Mathf.Clamp01(
                                bankT
                            );

                        float slope =
                            Mathf.Sin(
                                bankT *
                                Mathf.PI
                            );

                        slope =
                            Mathf.Pow(
                                slope,
                                Mathf.Lerp(
                                    1f,
                                    2.5f,
                                    lakeBankSmoothness
                                )
                            );

                        float noise =
                            Mathf.PerlinNoise(
                                worldX *
                                lakeBankNoiseScale,
                                worldZ *
                                lakeBankNoiseScale
                            );

                        noise =
                            (noise - 0.5f) * 2f;

                        float naturalAmount =
                            1f +
                            noise *
                            lakeBankNaturalVariation;

                        float bankRaise =
                            lakeBankHeight *
                            slope *
                            naturalAmount;

                        float raised =
                            original +
                            bankRaise /
                            terrainSize.y;

                        float blend =
                            Mathf.SmoothStep(
                                0f,
                                1f,
                                slope
                            );

                        heights[y, x] =
                            Mathf.Lerp(
                                original,
                                raised,
                                blend
                            );
                    }
                }
            }
        }

        data.SetHeights(
            0,
            0,
            heights
        );
    }


    // =========================================================
    // CREATE LAKE WATER
    // =========================================================

    private void CreateLakeWater(
        Vector3 center)
    {
        float waterY =
            targetTerrain.SampleHeight(
                center
            ) +
            targetTerrain.transform.position.y +
            waterHeightOffset;

        GameObject lake =
            GameObject.CreatePrimitive(
                PrimitiveType.Plane
            );

        lake.name =
            "Lake_Water";

        lake.transform.SetParent(
            waterParent
        );

        lake.transform.position =
            new Vector3(
                center.x,
                waterY,
                center.z
            );

        lake.transform.rotation =
            Quaternion.identity;

        lake.transform.localScale =
            new Vector3(
                lakeWidth / 10f,
                1f,
                lakeLength / 10f
            );

        if (waterMaterial != null)
        {
            Renderer renderer =
                lake.GetComponent<Renderer>();

            renderer.sharedMaterial =
                waterMaterial;
        }

        Collider collider =
            lake.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }
    }


    // =========================================================
    // TERRAIN CENTER
    // =========================================================

    private Vector3 GetTerrainCenter()
    {
        if (targetTerrain == null)
        {
            return Vector3.zero;
        }

        Vector3 position =
            targetTerrain.transform.position;

        Vector3 size =
            targetTerrain.terrainData.size;

        Vector3 center =
            new Vector3(
                position.x +
                size.x * 0.5f,
                0f,
                position.z +
                size.z * 0.5f
            );

        center.y =
            targetTerrain.SampleHeight(
                center
            ) +
            position.y;

        return center;
    }


    // =========================================================
    // REFRESH TERRAIN
    // =========================================================

    private void RefreshTerrain()
    {
        if (targetTerrain == null)
        {
            return;
        }

        TerrainCollider collider =
            targetTerrain.GetComponent<TerrainCollider>();

        if (collider != null)
        {
            collider.terrainData = null;

            collider.terrainData =
                targetTerrain.terrainData;
        }
    }


    // =========================================================
    // DELETE WATER
    // =========================================================

    public void DeleteWater()
    {
        DeleteWaterObjects();

        RestoreTerrain();

        generatedLakes.Clear();
        generatedRivers.Clear();

        Debug.Log(
            "WaterManager: All water deleted and terrain restored."
        );
    }


    // =========================================================
    // DELETE WATER OBJECTS
    // =========================================================

    private void DeleteWaterObjects()
    {
        if (waterParent != null)
        {
            Destroy(
                waterParent.gameObject
            );

            waterParent = null;
        }

        GameObject existing =
            GameObject.Find(
                "Generated_Water"
            );

        if (existing != null)
        {
            Destroy(existing);
        }
    }


    // =========================================================
    // RESTORE TERRAIN
    // =========================================================

    private void RestoreTerrain()
    {
        if (!terrainBackupCreated)
        {
            return;
        }

        if (targetTerrain == null)
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

        RefreshTerrain();
    }


    // =========================================================
    // CONTEXT MENU
    // =========================================================

    [ContextMenu("Create River")]
    private void DebugCreateRiver()
    {
        CreateRiver();
    }


    [ContextMenu("Create Lake")]
    private void DebugCreateLake()
    {
        CreateLake();
    }


    [ContextMenu("Delete Water")]
    private void DebugDeleteWater()
    {
        DeleteWater();
    }
}