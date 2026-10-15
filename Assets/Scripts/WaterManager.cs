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
    // MODIFY / VISIBILITY SETTINGS
    // =========================================================

    [Header("Modify Settings")]

    [Tooltip("Smallest depth change applied by 'deeper' or 'shallower', so the change can be seen.")]
    [SerializeField]
    private float minimumDepthStep = 3f;

    [Tooltip("Smallest width change applied by 'wider' or 'narrower'.")]
    [SerializeField]
    private float minimumWidthStep = 4f;

    [Tooltip("How far the water surface sits below the surrounding ground. The surface stays at this level when the bed is lowered.")]
    [SerializeField]
    private float surfaceDrop = 0.3f;

    [Tooltip("Colour the water by depth so 'deeper' and 'shallower' are visible from above.")]
    [SerializeField]
    private bool tintWaterByDepth = true;

    [SerializeField]
    private Color shallowWaterColor = new Color(0.30f, 0.62f, 0.78f, 0.85f);

    [SerializeField]
    private Color deepWaterColor = new Color(0.02f, 0.12f, 0.32f, 0.95f);

    [Tooltip("Depth (m) at which the water reaches the deep colour.")]
    [SerializeField]
    private float depthForDeepestColor = 12f;


    // =========================================================
    // WATER DATA (public: used by DemoChat and WaterPointingDetector)
    // =========================================================

    public class CarveRecord
    {
        public int x0;
        public int y0;
        public float[,] delta;
    }

    public class LakeInfo
    {
        public int id;
        public Vector3 center;
        public float groundY;      // ground height at the centre before carving
        public float width;
        public float length;
        public float depth;
        public float radiusX;      // footprint incl. bank + margin (used for spacing)
        public float radiusZ;
        public GameObject waterObject;
        public CarveRecord carve;
    }

    public class RiverInfo
    {
        public int id;
        public List<Vector3> path; // y = ground height before carving
        public float width;
        public float depth;
        public float radius;       // footprint incl. bank + margin (used for spacing)
        public GameObject waterObject;
        public CarveRecord carve;
    }

    private readonly List<LakeInfo> generatedLakes =
        new List<LakeInfo>();

    private readonly List<RiverInfo> generatedRivers =
        new List<RiverInfo>();

    private int nextLakeId = 0;
    private int nextRiverId = 0;


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
            return;

        if (!ValidateRiverSettings())
            return;

        PrepareForNewWater();

        List<Vector3> path = FindSafeRiverPath();

        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("WaterManager: Could not find a safe nearby location for the river.");
            return;
        }

        CreateWaterParent();

        RiverInfo river = new RiverInfo();
        river.id = nextRiverId++;
        river.path = new List<Vector3>(path);
        river.width = riverWidth;
        river.depth = riverDepth;
        UpdateRiverFootprint(river);

        BuildRiver(river);
        generatedRivers.Add(river);

        RefreshTerrain();

        Debug.Log("WaterManager: Natural river created. ID = " + river.id +
                  " | Total rivers: " + generatedRivers.Count);
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
            return;

        if (!ValidateLakeSettings())
            return;

        PrepareForNewWater();

        Vector3 center;

        if (!FindSafeLakeLocation(out center))
        {
            Debug.LogWarning("WaterManager: Could not find a safe nearby location for the lake.");
            return;
        }

        CreateWaterParent();

        LakeInfo lake = new LakeInfo();
        lake.id = nextLakeId++;
        lake.center = center;
        lake.groundY = center.y;
        lake.width = lakeWidth;
        lake.length = lakeLength;
        lake.depth = lakeDepth;
        UpdateLakeFootprint(lake);

        BuildLake(lake);
        generatedLakes.Add(lake);

        RefreshTerrain();

        Debug.Log("WaterManager: Natural lake created. ID = " + lake.id +
                  " | Total lakes: " + generatedLakes.Count);
    }


    // =========================================================
    // PUBLIC: LOOK-UP
    // =========================================================

    public RiverInfo GetRiverById(int id)
    {
        foreach (RiverInfo river in generatedRivers)
            if (river != null && river.id == id)
                return river;

        return null;
    }

    public LakeInfo GetLakeById(int id)
    {
        foreach (LakeInfo lake in generatedLakes)
            if (lake != null && lake.id == id)
                return lake;

        return null;
    }

    public IReadOnlyList<RiverInfo> GetRivers()
    {
        return generatedRivers;
    }

    public IReadOnlyList<LakeInfo> GetLakes()
    {
        return generatedLakes;
    }

    public int GetRiverCount()
    {
        return generatedRivers.Count;
    }

    public int GetLakeCount()
    {
        return generatedLakes.Count;
    }

    // Returns the river whose water covers the given world position, or -1.
    public int FindRiverAtPosition(Vector3 worldPosition, float extraTolerance = 2f)
    {
        int bestId = -1;
        float best = float.MaxValue;
        Vector3 p = new Vector3(worldPosition.x, 0f, worldPosition.z);

        foreach (RiverInfo river in generatedRivers)
        {
            if (river == null || river.path == null)
                continue;

            float d = DistanceToRiver(p, river.path);

            if (d <= river.width * 0.5f + riverBankWidth + extraTolerance && d < best)
            {
                best = d;
                bestId = river.id;
            }
        }

        return bestId;
    }

    // Returns the lake whose water covers the given world position, or -1.
    public int FindLakeAtPosition(Vector3 worldPosition, float extraTolerance = 2f)
    {
        int bestId = -1;
        float best = float.MaxValue;

        foreach (LakeInfo lake in generatedLakes)
        {
            if (lake == null)
                continue;

            float rx = lake.width * 0.5f + lakeBankWidth + extraTolerance;
            float rz = lake.length * 0.5f + lakeBankWidth + extraTolerance;
            float dx = (worldPosition.x - lake.center.x) / rx;
            float dz = (worldPosition.z - lake.center.z) / rz;
            float d = dx * dx + dz * dz;

            if (d <= 1f && d < best)
            {
                best = d;
                bestId = lake.id;
            }
        }

        return bestId;
    }


    // =========================================================
    // PUBLIC: MODIFY
    // =========================================================
    //
    // The water surface stays where it is and only the bed is
    // lowered or raised, so "deeper" no longer moves the water
    // down with the bed. Small requested changes are enlarged to
    // a minimum step so that the change can be seen.
    //
    // =========================================================

    public bool ModifyRiver(int id, float newWidth, float newDepth)
    {
        RiverInfo river = GetRiverById(id);

        if (river == null || targetTerrain == null)
            return false;

        float width = ApplyMinimumStep(river.width, newWidth, minimumWidthStep, 2f, 100f);
        float depth = ApplyMinimumStep(river.depth, newDepth, minimumDepthStep, 0.5f, 20f);

        UndoCarve(river.carve);
        DestroyWaterObject(river.waterObject);

        river.width = width;
        river.depth = depth;
        UpdateRiverFootprint(river);

        BuildRiver(river);
        RefreshTerrain();

        Debug.Log("WaterManager: River " + id + " modified. Width = " + width.ToString("F1") +
                  " m, depth = " + depth.ToString("F1") + " m.");

        return true;
    }

    public bool ModifyLake(int id, float newWidth, float newLength, float newDepth)
    {
        LakeInfo lake = GetLakeById(id);

        if (lake == null || targetTerrain == null)
            return false;

        float width = ApplyMinimumStep(lake.width, newWidth, minimumWidthStep, 5f, 150f);
        float length = ApplyMinimumStep(lake.length, newLength, minimumWidthStep, 5f, 150f);
        float depth = ApplyMinimumStep(lake.depth, newDepth, minimumDepthStep, 0.5f, 20f);

        UndoCarve(lake.carve);
        DestroyWaterObject(lake.waterObject);

        lake.width = width;
        lake.length = length;
        lake.depth = depth;
        UpdateLakeFootprint(lake);

        BuildLake(lake);
        RefreshTerrain();

        Debug.Log("WaterManager: Lake " + id + " modified. Width = " + width.ToString("F1") +
                  " m, length = " + length.ToString("F1") + " m, depth = " + depth.ToString("F1") + " m.");

        return true;
    }

    private float ApplyMinimumStep(float current, float requested, float minimumStep, float min, float max)
    {
        float change = requested - current;

        if (Mathf.Abs(change) < 0.01f)
            return current;

        if (Mathf.Abs(change) < minimumStep)
            change = Mathf.Sign(change) * minimumStep;

        return Mathf.Clamp(current + change, min, max);
    }


    // =========================================================
    // PUBLIC: DELETE ONE
    // =========================================================

    public bool DeleteRiver(int id)
    {
        RiverInfo river = GetRiverById(id);

        if (river == null)
            return false;

        UndoCarve(river.carve);
        DestroyWaterObject(river.waterObject);
        generatedRivers.Remove(river);
        RefreshTerrain();

        Debug.Log("WaterManager: River " + id + " deleted.");
        return true;
    }

    public bool DeleteLake(int id)
    {
        LakeInfo lake = GetLakeById(id);

        if (lake == null)
            return false;

        UndoCarve(lake.carve);
        DestroyWaterObject(lake.waterObject);
        generatedLakes.Remove(lake);
        RefreshTerrain();

        Debug.Log("WaterManager: Lake " + id + " deleted.");
        return true;
    }


    // =========================================================
    // FOOTPRINTS (used for spacing checks)
    // =========================================================

    private void UpdateRiverFootprint(RiverInfo river)
    {
        river.radius = river.width * 0.5f + riverBankWidth + placementMargin;
    }

    private void UpdateLakeFootprint(LakeInfo lake)
    {
        lake.radiusX = lake.width * 0.5f + lakeBankWidth + placementMargin;
        lake.radiusZ = lake.length * 0.5f + lakeBankWidth + placementMargin;
    }


    // =========================================================
    // BUILD (carve terrain + create water)
    // =========================================================

    private void BuildRiver(RiverInfo river)
    {
        float reach = river.width * 0.5f + riverBankWidth + 1f;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (Vector3 p in river.path)
        {
            minX = Mathf.Min(minX, p.x - reach);
            maxX = Mathf.Max(maxX, p.x + reach);
            minZ = Mathf.Min(minZ, p.z - reach);
            maxZ = Mathf.Max(maxZ, p.z + reach);
        }

        river.carve = CarveRegion(minX, maxX, minZ, maxZ,
            (wx, wz, original, size) => ShapeRiverHeight(river, wx, wz, original, size));

        river.waterObject = CreateRiverWater(river);
    }

    private void BuildLake(LakeInfo lake)
    {
        float rx = lake.width * 0.5f + lakeBankWidth + 1f;
        float rz = lake.length * 0.5f + lakeBankWidth + 1f;

        lake.carve = CarveRegion(lake.center.x - rx, lake.center.x + rx, lake.center.z - rz, lake.center.z + rz,
            (wx, wz, original, size) => ShapeLakeHeight(lake, wx, wz, original, size));

        lake.waterObject = CreateLakeWater(lake);
    }


    // =========================================================
    // CARVING WITH UNDO
    // =========================================================
    //
    // Each lake or river records exactly how much it changed every
    // heightmap cell. Modifying or deleting it subtracts that change
    // again, so mountains and other edits elsewhere are kept.
    //
    // =========================================================

    private delegate float HeightShaper(float worldX, float worldZ, float originalNormalized, Vector3 terrainSize);

    private CarveRecord CarveRegion(float minX, float maxX, float minZ, float maxZ, HeightShaper shaper)
    {
        TerrainData data = targetTerrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 tp = targetTerrain.transform.position;
        Vector3 size = data.size;

        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - tp.x) / size.x * (res - 1)), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - tp.x) / size.x * (res - 1)), 0, res - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt((minZ - tp.z) / size.z * (res - 1)), 0, res - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - tp.z) / size.z * (res - 1)), 0, res - 1);

        int w = x1 - x0 + 1;
        int h = y1 - y0 + 1;

        float[,] heights = data.GetHeights(x0, y0, w, h);
        float[,] delta = new float[h, w];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float worldX = tp.x + (x0 + x) / (float)(res - 1) * size.x;
                float worldZ = tp.z + (y0 + y) / (float)(res - 1) * size.z;

                float original = heights[y, x];
                float shaped = Mathf.Clamp01(shaper(worldX, worldZ, original, size));

                delta[y, x] = shaped - original;
                heights[y, x] = shaped;
            }
        }

        data.SetHeights(x0, y0, heights);

        CarveRecord record = new CarveRecord();
        record.x0 = x0;
        record.y0 = y0;
        record.delta = delta;
        return record;
    }

    private void UndoCarve(CarveRecord record)
    {
        if (record == null || record.delta == null || targetTerrain == null)
            return;

        TerrainData data = targetTerrain.terrainData;
        int h = record.delta.GetLength(0);
        int w = record.delta.GetLength(1);

        float[,] heights = data.GetHeights(record.x0, record.y0, w, h);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                heights[y, x] = Mathf.Clamp01(heights[y, x] - record.delta[y, x]);

        data.SetHeights(record.x0, record.y0, heights);
    }


    // =========================================================
    // RIVER BED SHAPE (same shape as before, per cell)
    // =========================================================

    private float ShapeRiverHeight(RiverInfo river, float worldX, float worldZ, float original, Vector3 terrainSize)
    {
        float riverRadius = river.width * 0.5f;
        float totalWidth = riverRadius + riverBankWidth;

        float distance = DistanceToRiver(new Vector3(worldX, 0f, worldZ), river.path);

        if (distance <= riverRadius)
        {
            float t = Mathf.Clamp01(distance / riverRadius);
            float bedShape = Mathf.SmoothStep(1f, 0.15f, t);
            float target = original - river.depth * bedShape / terrainSize.y;
            float edgeBlend = Mathf.SmoothStep(0f, 1f, t);

            return Mathf.Lerp(target, original, edgeBlend * 0.15f);
        }

        if (distance <= totalWidth)
        {
            float bankT = Mathf.Clamp01((distance - riverRadius) / riverBankWidth);
            float slope = Mathf.Sin(bankT * Mathf.PI);
            slope = Mathf.Pow(slope, Mathf.Lerp(1f, 2.5f, riverBankSmoothness));

            float noise = (Mathf.PerlinNoise(worldX * riverBankNoiseScale, worldZ * riverBankNoiseScale) - 0.5f) * 2f;
            float bankRaise = riverBankHeight * slope * (1f + noise * riverBankNaturalVariation);
            float raised = original + bankRaise / terrainSize.y;

            return Mathf.Lerp(original, raised, Mathf.SmoothStep(0f, 1f, slope));
        }

        return original;
    }


    // =========================================================
    // LAKE BED SHAPE (same shape as before, per cell)
    // =========================================================

    private float ShapeLakeHeight(LakeInfo lake, float worldX, float worldZ, float original, Vector3 terrainSize)
    {
        Vector3 tp = targetTerrain.transform.position;

        float lakeRadiusX = lake.width * 0.5f;
        float lakeRadiusZ = lake.length * 0.5f;

        float dx = (worldX - lake.center.x) / lakeRadiusX;
        float dz = (worldZ - lake.center.z) / lakeRadiusZ;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);

        if (distance <= 1f)
        {
            float bedT = Mathf.SmoothStep(0f, 1f, distance);
            float bedShape = Mathf.Lerp(1f, 0.1f, bedT);
            float targetWorldHeight = lake.groundY - lake.depth * bedShape;
            float originalWorldHeight = tp.y + original * terrainSize.y;
            float blend = Mathf.SmoothStep(0f, 1f, distance);
            float finalHeight = Mathf.Lerp(targetWorldHeight, originalWorldHeight, blend * 0.15f);

            return Mathf.InverseLerp(tp.y, tp.y + terrainSize.y, finalHeight);
        }

        float outerDX = (worldX - lake.center.x) / (lakeRadiusX + lakeBankWidth);
        float outerDZ = (worldZ - lake.center.z) / (lakeRadiusZ + lakeBankWidth);
        float outerDistance = Mathf.Sqrt(outerDX * outerDX + outerDZ * outerDZ);

        if (outerDistance <= 1f)
        {
            float bankT = Mathf.Clamp01(Mathf.InverseLerp(1f, 0f, outerDistance));
            float slope = Mathf.Sin(bankT * Mathf.PI);
            slope = Mathf.Pow(slope, Mathf.Lerp(1f, 2.5f, lakeBankSmoothness));

            float noise = (Mathf.PerlinNoise(worldX * lakeBankNoiseScale, worldZ * lakeBankNoiseScale) - 0.5f) * 2f;
            float bankRaise = lakeBankHeight * slope * (1f + noise * lakeBankNaturalVariation);
            float raised = original + bankRaise / terrainSize.y;

            return Mathf.Lerp(original, raised, Mathf.SmoothStep(0f, 1f, slope));
        }

        return original;
    }


    // =========================================================
    // WATER SURFACE HEIGHT
    // =========================================================
    //
    // Before: the water was placed on the carved bed, so lowering
    // the bed lowered the water too and nothing seemed to change.
    // Now: the water sits just below the ORIGINAL ground level and
    // stays there when the bed is made deeper.
    //
    // =========================================================

    private float SurfaceDrop(float depth)
    {
        return Mathf.Min(surfaceDrop, depth * 0.12f);
    }


    // =========================================================
    // CREATE RIVER WATER
    // =========================================================

    private GameObject CreateRiverWater(RiverInfo river)
    {
        List<Vector3> path = river.path;

        GameObject obj = new GameObject("River_Water_" + river.id);
        obj.transform.SetParent(waterParent);

        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

        float halfWidth = river.width * 0.5f;
        float drop = SurfaceDrop(river.depth);
        int capSegments = 16;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // ---------------- MAIN STRIP ----------------

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point = path[i];
            Vector3 direction =
                i == 0 ? path[1] - path[0] :
                i == path.Count - 1 ? path[i] - path[i - 1] :
                path[i + 1] - path[i - 1];

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();

            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            float waterY = point.y - drop;

            Vector3 left = point + side * halfWidth;
            Vector3 right = point - side * halfWidth;
            left.y = waterY;
            right.y = waterY;

            vertices.Add(left);
            vertices.Add(right);

            float u = i / (float)(path.Count - 1);
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 1f));
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            int c = i * 2;
            int n = (i + 1) * 2;

            triangles.Add(c); triangles.Add(n); triangles.Add(c + 1);
            triangles.Add(c + 1); triangles.Add(n); triangles.Add(n + 1);
        }

        // ---------------- END CAPS ----------------

        AddRiverCap(vertices, triangles, uvs, path[0], path[1] - path[0], halfWidth, path[0].y - drop, capSegments, true);
        AddRiverCap(vertices, triangles, uvs, path[path.Count - 1], path[path.Count - 1] - path[path.Count - 2],
                    halfWidth, path[path.Count - 1].y - drop, capSegments, false);

        Mesh mesh = new Mesh();
        mesh.name = "River_Water_Mesh_" + river.id;
        if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;

        ApplyWaterMaterial(meshRenderer, river.depth);
        return obj;
    }

    private void AddRiverCap(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
                             Vector3 centre, Vector3 direction, float halfWidth, float waterY,
                             int segments, bool isStart)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        direction.Normalize();

        Vector3 side = new Vector3(-direction.z, 0f, direction.x);

        int centreIndex = vertices.Count;
        vertices.Add(new Vector3(centre.x, waterY, centre.z));
        uvs.Add(new Vector2(isStart ? 0f : 1f, 0.5f));

        int first = vertices.Count;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 radial = isStart
                ? side * Mathf.Cos(Mathf.PI + t * Mathf.PI) + direction * Mathf.Sin(Mathf.PI + t * Mathf.PI)
                : -side * Mathf.Cos(t * Mathf.PI) + direction * Mathf.Sin(t * Mathf.PI);

            Vector3 p = centre + radial * halfWidth;
            p.y = waterY;
            vertices.Add(p);
            uvs.Add(new Vector2(isStart ? 0f : 1f, t));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles.Add(centreIndex);
            if (isStart) { triangles.Add(first + i); triangles.Add(first + i + 1); }
            else { triangles.Add(first + i + 1); triangles.Add(first + i); }
        }
    }


    // =========================================================
    // CREATE LAKE WATER (elliptical surface)
    // =========================================================

    private GameObject CreateLakeWater(LakeInfo lake)
    {
        GameObject obj = new GameObject("Lake_Water_" + lake.id);
        obj.transform.SetParent(waterParent);

        float waterY = lake.groundY - SurfaceDrop(lake.depth);
        obj.transform.position = new Vector3(lake.center.x, waterY, lake.center.z);

        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

        int segments = 48;
        float rx = lake.width * 0.5f;
        float rz = lake.length * 0.5f;

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float cx = Mathf.Cos(a);
            float sz = Mathf.Sin(a);

            vertices[i + 1] = new Vector3(cx * rx, 0f, sz * rz);
            uvs[i + 1] = new Vector2(0.5f + cx * 0.5f, 0.5f + sz * 0.5f);

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % segments + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Lake_Water_Mesh_" + lake.id;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;

        ApplyWaterMaterial(meshRenderer, lake.depth);
        return obj;
    }


    // =========================================================
    // MATERIAL + DEPTH COLOUR
    // =========================================================

    private void ApplyWaterMaterial(MeshRenderer renderer, float depth)
    {
        if (waterMaterial == null)
        {
            Debug.LogWarning("WaterManager: No water material assigned.");
            return;
        }

        if (!tintWaterByDepth)
        {
            renderer.sharedMaterial = waterMaterial;
            return;
        }

        // A separate material instance per water body, so each one
        // can show its own depth.
        Material instance = new Material(waterMaterial);
        float t = Mathf.Clamp01(depth / Mathf.Max(depthForDeepestColor, 0.1f));
        Color color = Color.Lerp(shallowWaterColor, deepWaterColor, t);

        if (instance.HasProperty("_BaseColor")) instance.SetColor("_BaseColor", color);
        if (instance.HasProperty("_Color")) instance.SetColor("_Color", color);

        renderer.sharedMaterial = instance;
    }

    private void DestroyWaterObject(GameObject obj)
    {
        if (obj == null)
            return;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();

        if (renderer != null && tintWaterByDepth && renderer.sharedMaterial != null &&
            renderer.sharedMaterial != waterMaterial)
        {
            Destroy(renderer.sharedMaterial);
        }

        Destroy(obj);
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
    // DELETE WATER (all)
    // =========================================================

    public void DeleteWater()
    {
        // Undo each carve in reverse order, so other terrain edits
        // (for example mountains created later) are kept.
        for (int i = generatedRivers.Count - 1; i >= 0; i--)
            if (generatedRivers[i] != null) UndoCarve(generatedRivers[i].carve);

        for (int i = generatedLakes.Count - 1; i >= 0; i--)
            if (generatedLakes[i] != null) UndoCarve(generatedLakes[i].carve);

        DeleteWaterObjects();

        generatedLakes.Clear();
        generatedRivers.Clear();

        RefreshTerrain();

        Debug.Log("WaterManager: All water deleted.");
    }


    // =========================================================
    // DELETE WATER OBJECTS
    // =========================================================

    private void DeleteWaterObjects()
    {
        foreach (RiverInfo river in generatedRivers)
            if (river != null) DestroyWaterObject(river.waterObject);

        foreach (LakeInfo lake in generatedLakes)
            if (lake != null) DestroyWaterObject(lake.waterObject);

        if (waterParent != null)
        {
            Destroy(waterParent.gameObject);
            waterParent = null;
        }

        GameObject existing = GameObject.Find("Generated_Water");

        if (existing != null)
            Destroy(existing);
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

    [ContextMenu("Make Last Lake Deeper")]
    private void DebugDeeperLake()
    {
        if (generatedLakes.Count > 0)
        {
            LakeInfo lake = generatedLakes[generatedLakes.Count - 1];
            ModifyLake(lake.id, lake.width, lake.length, lake.depth + 1f);
        }
    }

    [ContextMenu("Make Last River Deeper")]
    private void DebugDeeperRiver()
    {
        if (generatedRivers.Count > 0)
        {
            RiverInfo river = generatedRivers[generatedRivers.Count - 1];
            ModifyRiver(river.id, river.width, river.depth + 1f);
        }
    }

    [ContextMenu("Delete Water")]
    private void DebugDeleteWater()
    {
        DeleteWater();
    }
}