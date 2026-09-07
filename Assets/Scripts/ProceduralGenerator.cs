using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    // ============================================================
    // TERRAIN GENERATOR
    // ============================================================
    //
    // FEATURES
    // --------
    // 1. Natural single mountains
    // 2. Connected mountain ranges
    // 3. Multiple separate mountains
    // 4. Automatic grass / rock texturing
    // 5. Stable generated mountain IDs
    // 6. Find mountain by ID
    // 7. Find generated mountain at world position
    // 8. Modify mountain by ID
    // 9. Modify mountain at pointed world position
    // 10. Mountain collision detection
    // 11. Water collision detection
    // 12. Reset terrain
    // 13. Refresh TerrainCollider
    //
    // REMOVED
    // --------
    // Hills
    // Valleys
    // Lakes
    // Rivers
    // Snow
    //
    // ============================================================


    // ============================================================
    // TERRAIN
    // ============================================================

    [Header("Terrain")]

    [SerializeField]
    private Terrain targetTerrain;


    // ============================================================
    // WATER COLLISION
    // ============================================================

    [Header("Water Collision")]

    [Tooltip(
        "WaterManager used to prevent mountains from overlapping lakes and rivers."
    )]
    [SerializeField]
    private WaterManager waterManager;

    [Tooltip(
        "Extra safety space between mountains and water."
    )]
    [SerializeField]
    private float minimumMountainWaterSpacing = 8f;


    // ============================================================
    // TEXTURES
    // ============================================================

    [Header("Terrain Textures")]

    [SerializeField]
    private Texture2D grassTexture;

    [SerializeField]
    private Texture2D rockTexture;

    [SerializeField]
    private bool automaticallyTextureTerrain = true;

    [SerializeField]
    [Range(0f, 1f)]
    private float rockStartHeight = 0.35f;

    [SerializeField]
    [Range(0f, 1f)]
    private float rockStartSlope = 0.45f;

    [SerializeField]
    [Range(0f, 1f)]
    private float grassStrength = 1f;


    // ============================================================
    // SINGLE MOUNTAIN SETTINGS
    // ============================================================

    [Header("Single Mountain")]

    [SerializeField]
    private float minimumMountainRadius = 20f;

    [SerializeField]
    private float maximumMountainRadius = 40f;

    [SerializeField]
    private float minimumMountainHeight = 8f;

    [SerializeField]
    private float maximumMountainHeight = 18f;

    [SerializeField]
    [Range(0.05f, 1f)]
    private float singleMountainPeakWidth = 0.42f;

    [SerializeField]
    private float singleMountainPeakSharpness = 1.35f;

    [SerializeField]
    [Range(0f, 1f)]
    private float singleMountainIrregularity = 0.10f;

    [SerializeField]
    private float singleMountainBaseExponent = 0.85f;


    // ============================================================
    // MOUNTAIN RANGE SETTINGS
    // ============================================================

    [Header("Mountain Range")]

    [SerializeField]
    private int mountainRangePeakCount = 5;

    [SerializeField]
    [Range(0.05f, 1f)]
    private float mountainRangePeakWidth = 0.30f;

    [SerializeField]
    [Range(0f, 1f)]
    private float mountainRangePeakOverlap = 0.58f;

    [SerializeField]
    [Range(0f, 1f)]
    private float mountainRangeCurve = 0.16f;

    [SerializeField]
    [Range(0f, 1f)]
    private float mountainRangePeakWander = 0.10f;

    [SerializeField]
    [Range(0f, 1f)]
    private float mountainRangeBaseStrength = 0.40f;

    [SerializeField]
    [Range(0f, 1f)]
    private float mountainRangeNoiseStrength = 0.08f;

    [SerializeField]
    [Range(0f, 1f)]
    private float mountainRangeHeightVariation = 0.25f;

    [SerializeField]
    private float minimumMountainRangeRadius = 45f;

    [SerializeField]
    private float maximumMountainRangeRadius = 70f;

    [SerializeField]
    private float minimumMountainRangeHeight = 10f;

    [SerializeField]
    private float maximumMountainRangeHeight = 22f;


    // ============================================================
    // MOUNTAIN SPACING
    // ============================================================

    [Header("Mountain Spacing")]

    [Tooltip(
        "Minimum empty space between mountain footprints."
    )]
    [SerializeField]
    private float minimumMountainSpacing = 5f;

    [Tooltip(
        "Number of random positions tested before giving up."
    )]
    [SerializeField]
    private int maximumPlacementAttempts = 50;


    // ============================================================
    // MOUNTAIN TYPES
    // ============================================================

    public enum MountainType
    {
        Single,
        Range
    }


    // ============================================================
    // MOUNTAIN INFORMATION
    // ============================================================

    [System.Serializable]
    public class MountainInfo
    {
        public int id;

        public string name;

        public Vector2 terrainPosition;

        public float radius;

        public MountainType type;


        public Vector3 GetWorldPosition(
            Terrain terrain)
        {
            if (terrain == null)
            {
                return Vector3.zero;
            }

            return new Vector3(
                terrain.transform.position.x +
                terrainPosition.x,

                terrain.transform.position.y,

                terrain.transform.position.z +
                terrainPosition.y
            );
        }
    }


    // ============================================================
    // GENERATED MOUNTAINS
    // ============================================================

    [SerializeField]
    private List<MountainInfo> generatedMountains =
        new List<MountainInfo>();

    private int nextMountainId = 1;


    // ============================================================
    // ORIGINAL TERRAIN
    // ============================================================

    private float[,] originalHeights;

    private bool originalTerrainSaved = false;


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        FindTerrain();

        FindWaterManager();

        SaveOriginalTerrain();

        SetupTerrainTextures();
    }


    // ============================================================
    // FIND TERRAIN
    // ============================================================

    private void FindTerrain()
    {
        if (targetTerrain == null)
        {
            targetTerrain =
                Terrain.activeTerrain;
        }

        if (targetTerrain == null)
        {
            targetTerrain =
                FindObjectOfType<Terrain>();
        }

        if (targetTerrain == null)
        {
            Debug.LogError(
                "[TerrainGenerator] No Terrain found."
            );
        }
    }


    // ============================================================
    // FIND WATER MANAGER
    // ============================================================

    private void FindWaterManager()
    {
        if (waterManager != null)
        {
            return;
        }

        waterManager =
            FindObjectOfType<WaterManager>();
    }


    // ============================================================
    // PUBLIC TERRAIN ACCESS
    // ============================================================

    public Terrain GetGeneratedTerrain()
    {
        return targetTerrain;
    }


    public Terrain GetTargetTerrain()
    {
        return targetTerrain;
    }


    // ============================================================
    // PUBLIC MOUNTAIN ACCESS
    // ============================================================

    public List<MountainInfo>
        GetGeneratedMountains()
    {
        return generatedMountains;
    }


    public bool TryGetMountainById(
        int id,
        out MountainInfo mountain)
    {
        mountain = null;

        for (int i = 0;
             i < generatedMountains.Count;
             i++)
        {
            MountainInfo info =
                generatedMountains[i];

            if (info != null &&
                info.id == id)
            {
                mountain = info;
                return true;
            }
        }

        return false;
    }


    public bool TryGetMountainAtWorldPosition(
        Vector3 worldPosition,
        out MountainInfo mountain)
    {
        mountain = null;

        if (targetTerrain == null)
        {
            return false;
        }

        Vector2 terrainPosition =
            new Vector2(
                worldPosition.x -
                targetTerrain.transform.position.x,

                worldPosition.z -
                targetTerrain.transform.position.z
            );

        float closestDistance =
            float.MaxValue;

        MountainInfo closestMountain =
            null;

        for (int i = 0;
             i < generatedMountains.Count;
             i++)
        {
            MountainInfo info =
                generatedMountains[i];

            if (info == null)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    terrainPosition,
                    info.terrainPosition
                );

            if (distance <= info.radius &&
                distance < closestDistance)
            {
                closestDistance =
                    distance;

                closestMountain =
                    info;
            }
        }

        if (closestMountain != null)
        {
            mountain =
                closestMountain;

            return true;
        }

        return false;
    }


    // ============================================================
    // SAVE ORIGINAL TERRAIN
    // ============================================================

    private void SaveOriginalTerrain()
    {
        if (targetTerrain == null)
        {
            return;
        }

        if (originalTerrainSaved)
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

        originalTerrainSaved = true;
    }


    // ============================================================
    // VALID TERRAIN
    // ============================================================

    private bool HasValidTerrain()
    {
        if (targetTerrain == null)
        {
            FindTerrain();
        }

        if (targetTerrain == null)
        {
            return false;
        }

        if (targetTerrain.terrainData == null)
        {
            return false;
        }

        return true;
    }


    // ============================================================
    // RANDOM TERRAIN POSITION
    // ============================================================

    private Vector2 GetRandomTerrainPosition(
        float radius)
    {
        TerrainData data =
            targetTerrain.terrainData;

        Vector3 terrainSize =
            data.size;

        float padding =
            Mathf.Max(
                radius + minimumMountainSpacing,
                1f
            );

        float x =
            Random.Range(
                padding,
                terrainSize.x -
                padding
            );

        float z =
            Random.Range(
                padding,
                terrainSize.z -
                padding
            );

        return new Vector2(
            x,
            z
        );
    }


    // ============================================================
    // MOUNTAIN POSITION COLLISION
    // ============================================================

    private bool IsMountainPositionAvailable(
        Vector2 position,
        float radius)
    {
        if (radius <= 0f)
        {
            return false;
        }

        float newRadius =
            Mathf.Max(
                radius,
                1f
            );

        float spacing =
            Mathf.Max(
                minimumMountainSpacing,
                0f
            );

        // --------------------------------------------------------
        // CHECK OTHER MOUNTAINS
        // --------------------------------------------------------

        for (int i = 0;
             i < generatedMountains.Count;
             i++)
        {
            MountainInfo existing =
                generatedMountains[i];

            if (existing == null)
            {
                continue;
            }

            float existingRadius =
                Mathf.Max(
                    existing.radius,
                    1f
                );

            float minimumCenterDistance =
                newRadius +
                existingRadius +
                spacing;

            float actualCenterDistance =
                Vector2.Distance(
                    position,
                    existing.terrainPosition
                );

            if (actualCenterDistance <
                minimumCenterDistance)
            {
                return false;
            }
        }


        // --------------------------------------------------------
        // CHECK WATER
        // --------------------------------------------------------

        FindWaterManager();

        if (waterManager != null)
        {
            float waterRadius =
                newRadius +
                minimumMountainWaterSpacing;

            bool waterAvailable =
                waterManager.IsMountainPositionAvailable(
                    position,
                    waterRadius
                );

            if (!waterAvailable)
            {
                return false;
            }
        }

        return true;
    }


    // ============================================================
    // FIND AVAILABLE MOUNTAIN POSITION
    // ============================================================

    private bool TryGetAvailableMountainPosition(
        float radius,
        out Vector2 position)
    {
        position =
            Vector2.zero;

        if (!HasValidTerrain())
        {
            return false;
        }

        radius =
            Mathf.Max(
                radius,
                1f
            );

        int attempts =
            Mathf.Max(
                maximumPlacementAttempts,
                1
            );

        for (int attempt = 0;
             attempt < attempts;
             attempt++)
        {
            Vector2 candidate =
                GetRandomTerrainPosition(
                    radius
                );

            if (IsMountainPositionAvailable(
                    candidate,
                    radius))
            {
                position =
                    candidate;

                return true;
            }
        }

        Debug.LogWarning(
            "[TerrainGenerator] Could not find a free position " +
            "for a mountain/range after " +
            attempts +
            " attempts. No mountain was created."
        );

        return false;
    }


    // ============================================================
    // CREATE RANDOM SINGLE MOUNTAIN
    // ============================================================

    public void CreateRandomMountain()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        SaveOriginalTerrain();

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

        Vector2 randomPosition;

        if (!TryGetAvailableMountainPosition(
                radius,
                out randomPosition))
        {
            Debug.LogWarning(
                "[TerrainGenerator] Single mountain was not created " +
                "because there is not enough free space."
            );

            return;
        }

        CreateMountainInternal(
            randomPosition,
            radius,
            height,
            MountainType.Single
        );

        Debug.Log(
            "[TerrainGenerator] Natural single mountain created."
        );
    }


    // ============================================================
    // CREATE RANDOM MOUNTAIN RANGE
    // ============================================================

    public void CreateRandomMountainRange()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        SaveOriginalTerrain();

        float radius =
            Random.Range(
                minimumMountainRangeRadius,
                maximumMountainRangeRadius
            );

        float height =
            Random.Range(
                minimumMountainRangeHeight,
                maximumMountainRangeHeight
            );

        Vector2 randomPosition;

        if (!TryGetAvailableMountainPosition(
                radius,
                out randomPosition))
        {
            Debug.LogWarning(
                "[TerrainGenerator] Mountain range was not created " +
                "because there is not enough free space."
            );

            return;
        }

        CreateMountainInternal(
            randomPosition,
            radius,
            height,
            MountainType.Range
        );

        Debug.Log(
            "[TerrainGenerator] Natural mountain range created."
        );
    }


    // ============================================================
    // CREATE MULTIPLE SEPARATE MOUNTAINS
    // ============================================================

    public void CreateRandomMountains(
        int count)
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

        int successfullyCreated =
            0;

        for (int i = 0;
             i < count;
             i++)
        {
            int beforeCount =
                generatedMountains.Count;

            CreateRandomMountain();

            if (generatedMountains.Count >
                beforeCount)
            {
                successfullyCreated++;
            }
        }

        Debug.Log(
            "[TerrainGenerator] Requested " +
            count +
            " separate mountain(s). Successfully created " +
            successfullyCreated +
            "."
        );
    }


    // ============================================================
    // CREATE MOUNTAIN AT WORLD POSITION
    // ============================================================

    public void CreateMountainAtWorldPosition(
        Vector3 worldPosition)
    {
        if (!HasValidTerrain())
        {
            return;
        }

        Vector2 terrainPosition =
            WorldToTerrainPosition(
                worldPosition
            );

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

        if (!IsMountainPositionAvailable(
                terrainPosition,
                radius))
        {
            Debug.LogWarning(
                "[TerrainGenerator] Cannot create mountain at " +
                "the requested position because it overlaps " +
                "another mountain or water."
            );

            return;
        }

        CreateMountainInternal(
            terrainPosition,
            radius,
            height,
            MountainType.Single
        );
    }


    // ============================================================
    // CREATE MOUNTAIN IN FRONT OF USER
    // ============================================================

    public void CreateMountainInFrontOfUser(
        Vector3 worldPosition)
    {
        if (!HasValidTerrain())
        {
            return;
        }

        Vector3 forward =
            Vector3.forward;

        Camera mainCamera =
            Camera.main;

        if (mainCamera != null)
        {
            forward =
                mainCamera.transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude <
                0.001f)
            {
                forward =
                    Vector3.forward;
            }

            forward.Normalize();
        }

        Vector3 targetPosition =
            worldPosition +
            forward * 35f;

        targetPosition.y =
            targetTerrain.SampleHeight(
                targetPosition
            ) +
            targetTerrain.transform.position.y;

        CreateMountainAtWorldPosition(
            targetPosition
        );
    }


    // ============================================================
    // CREATE MOUNTAIN INTERNAL
    // ============================================================

    private void CreateMountainInternal(
        Vector2 terrainPosition,
        float radius,
        float height,
        MountainType type)
    {
        if (!HasValidTerrain())
        {
            return;
        }

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

        Vector3 terrainSize =
            data.size;

        float[,] original =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        Vector2 normalizedCenter =
            new Vector2(
                terrainPosition.x /
                terrainSize.x,

                terrainPosition.y /
                terrainSize.z
            );

        int id =
            nextMountainId++;

        MountainInfo mountain =
            new MountainInfo();

        mountain.id =
            id;

        mountain.name =
            type == MountainType.Range
                ? "Mountain Range " + id
                : "Mountain " + id;

        mountain.terrainPosition =
            terrainPosition;

        mountain.radius =
            radius;

        mountain.type =
            type;

        generatedMountains.Add(
            mountain
        );

        if (type ==
            MountainType.Single)
        {
            ModifyTerrainArea(
                heights,
                original,
                normalizedCenter,
                radius,
                height,
                true
            );
        }
        else
        {
            ModifyTerrainArea(
                heights,
                original,
                normalizedCenter,
                radius,
                height,
                false
            );
        }

        data.SetHeights(
            0,
            0,
            heights
        );

        PaintTerrainTextures();

        RefreshTerrainCollider();
    }


    // ============================================================
    // CALCULATE SINGLE MOUNTAIN SHAPE
    // ============================================================

    private float CalculateSingleMountainShape(
        float distance01,
        float angle,
        float seed)
    {
        distance01 =
            Mathf.Clamp01(
                distance01
            );

        float baseShape =
            1f -
            Mathf.Pow(
                distance01,
                singleMountainBaseExponent
            );

        baseShape =
            Mathf.Clamp01(
                baseShape
            );

        float peakStart =
            Mathf.Clamp01(
                singleMountainPeakWidth
            );

        float peak =
            1f -
            Mathf.Clamp01(
                distance01 /
                Mathf.Max(
                    peakStart,
                    0.001f
                )
            );

        peak =
            Mathf.Pow(
                peak,
                singleMountainPeakSharpness
            );

        float noise =
            Mathf.PerlinNoise(
                Mathf.Cos(angle) *
                2f +
                seed,

                Mathf.Sin(angle) *
                2f +
                seed
            );

        noise =
            Mathf.Lerp(
                1f,
                0.75f +
                noise * 0.5f,
                singleMountainIrregularity
            );

        float shape =
            baseShape *
            Mathf.Lerp(
                0.65f,
                1f,
                peak
            ) *
            noise;

        return Mathf.Clamp01(
            shape
        );
    }


    // ============================================================
    // CALCULATE MOUNTAIN RANGE SHAPE
    // ============================================================

    private float CalculateMountainRangeShape(
        float distance01,
        float along01,
        float angle,
        float seed)
    {
        distance01 =
            Mathf.Clamp01(
                distance01
            );

        along01 =
            Mathf.Clamp01(
                along01
            );

        int peaks =
            Mathf.Max(
                2,
                mountainRangePeakCount
            );

        float peakPosition =
            along01 *
            (peaks - 1);

        float nearestPeak =
            Mathf.Round(
                peakPosition
            );

        float peakDistance =
            Mathf.Abs(
                peakPosition -
                nearestPeak
            );

        float peakShape =
            1f -
            Mathf.Clamp01(
                peakDistance /
                Mathf.Max(
                    mountainRangePeakWidth,
                    0.001f
                )
            );

        peakShape =
            Mathf.Pow(
                peakShape,
                1.5f
            );

        float radialShape =
            1f -
            Mathf.Pow(
                distance01,
                0.8f
            );

        radialShape =
            Mathf.Clamp01(
                radialShape
            );

        float curve =
            Mathf.Sin(
                along01 *
                Mathf.PI
            ) *
            mountainRangeCurve;

        float noise =
            Mathf.PerlinNoise(
                along01 * 3f +
                seed,

                distance01 * 4f +
                seed
            );

        noise =
            Mathf.Lerp(
                1f -
                mountainRangeNoiseStrength,

                1f +
                mountainRangeNoiseStrength,

                noise
            );

        float shape =
            radialShape *
            (
                mountainRangeBaseStrength +
                peakShape *
                (1f -
                 mountainRangeBaseStrength)
            );

        shape *=
            noise;

        shape *=
            1f +
            curve;

        return Mathf.Clamp01(
            shape
        );
    }


    // ============================================================
    // MODIFY TERRAIN AREA
    // ============================================================

    private void ModifyTerrainArea(
        float[,] heights,
        float[,] original,
        Vector2 normalizedCenter,
        float radius,
        float height,
        bool singleMountain)
    {
        TerrainData data =
            targetTerrain.terrainData;

        int resolution =
            data.heightmapResolution;

        Vector3 terrainSize =
            data.size;

        float centerX =
            normalizedCenter.x *
            terrainSize.x;

        float centerZ =
            normalizedCenter.y *
            terrainSize.z;

        float seed =
            Random.Range(
                0f,
                10000f
            );

        Vector2 rangeDirection =
            new Vector2(
                Mathf.Cos(seed),
                Mathf.Sin(seed)
            );

        if (rangeDirection.sqrMagnitude <
            0.001f)
        {
            rangeDirection =
                Vector2.right;
        }

        rangeDirection.Normalize();

        float rangeLength =
            radius * 2f;

        for (int y = 0;
             y < resolution;
             y++)
        {
            float worldZ =
                y /
                (float)(resolution - 1) *
                terrainSize.z;

            for (int x = 0;
                 x < resolution;
                 x++)
            {
                float worldX =
                    x /
                    (float)(resolution - 1) *
                    terrainSize.x;

                float dx =
                    worldX -
                    centerX;

                float dz =
                    worldZ -
                    centerZ;

                float distance =
                    Mathf.Sqrt(
                        dx * dx +
                        dz * dz
                    );

                if (distance >
                    radius)
                {
                    continue;
                }

                float distance01 =
                    distance /
                    radius;

                float angle =
                    Mathf.Atan2(
                        dz,
                        dx
                    );

                float shape;

                if (singleMountain)
                {
                    shape =
                        CalculateSingleMountainShape(
                            distance01,
                            angle,
                            seed
                        );
                }
                else
                {
                    Vector2 offset =
                        new Vector2(
                            dx,
                            dz
                        );

                    float along =
                        Vector2.Dot(
                            offset,
                            rangeDirection
                        );

                    float along01 =
                        Mathf.InverseLerp(
                            -rangeLength * 0.5f,
                            rangeLength * 0.5f,
                            along
                        );

                    shape =
                        CalculateMountainRangeShape(
                            distance01,
                            along01,
                            angle,
                            seed
                        );
                }

                float originalHeight =
                    original[y, x];

                float worldHeight =
                    originalHeight *
                    terrainSize.y;

                float addedHeight =
                    height *
                    shape;

                float finalWorldHeight =
                    worldHeight +
                    addedHeight;

                heights[y, x] =
                    Mathf.Clamp01(
                        finalWorldHeight /
                        terrainSize.y
                    );
            }
        }
    }


    // ============================================================
    // MODIFY MOUNTAIN
    // ============================================================

    public bool ModifyMountain(
        int mountainId,
        float heightChange,
        float radiusOverride = -1f)
    {
        if (!HasValidTerrain())
        {
            return false;
        }

        MountainInfo mountain;

        if (!TryGetMountainById(
                mountainId,
                out mountain))
        {
            return false;
        }

        float radius =
            radiusOverride > 0f
                ? radiusOverride
                : mountain.radius;

        float currentHeight =
            mountain.type ==
            MountainType.Range
                ? maximumMountainRangeHeight
                : maximumMountainHeight;

        float change =
            Mathf.Clamp(
                heightChange,
                -currentHeight,
                currentHeight
            );

        Vector2 center =
            mountain.terrainPosition;

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

        float[,] current =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        Vector3 terrainSize =
            data.size;

        Vector2 normalizedCenter =
            new Vector2(
                center.x /
                terrainSize.x,

                center.y /
                terrainSize.z
            );

        ModifyTerrainArea(
            heights,
            current,
            normalizedCenter,
            radius,
            change,
            mountain.type ==
            MountainType.Single
        );

        data.SetHeights(
            0,
            0,
            heights
        );

        mountain.radius =
            radius;

        PaintTerrainTextures();

        RefreshTerrainCollider();

        return true;
    }


    // ============================================================
    // MODIFY MOUNTAIN AT WORLD POSITION
    // ============================================================

    public bool ModifyMountainAtWorldPosition(
        Vector3 worldPosition,
        float heightChange)
    {
        if (!HasValidTerrain())
        {
            return false;
        }

        MountainInfo mountain;

        if (TryGetMountainAtWorldPosition(
                worldPosition,
                out mountain))
        {
            return ModifyMountain(
                mountain.id,
                heightChange
            );
        }

        Vector2 terrainPosition =
            WorldToTerrainPosition(
                worldPosition
            );

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

        float[,] current =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        Vector3 terrainSize =
            data.size;

        Vector2 normalizedCenter =
            new Vector2(
                terrainPosition.x /
                terrainSize.x,

                terrainPosition.y /
                terrainSize.z
            );

        ModifyTerrainArea(
            heights,
            current,
            normalizedCenter,
            20f,
            heightChange,
            true
        );

        data.SetHeights(
            0,
            0,
            heights
        );

        PaintTerrainTextures();

        RefreshTerrainCollider();

        return true;
    }


    // ============================================================
    // WORLD -> TERRAIN POSITION
    // ============================================================

    private Vector2 WorldToTerrainPosition(
        Vector3 worldPosition)
    {
        if (targetTerrain == null)
        {
            return Vector2.zero;
        }

        Vector3 terrainPosition =
            targetTerrain.transform.position;

        return new Vector2(
            worldPosition.x -
            terrainPosition.x,

            worldPosition.z -
            terrainPosition.z
        );
    }


    // ============================================================
    // TERRAIN LOCAL -> WORLD
    // ============================================================

    private Vector3 TerrainLocalToWorld(
        Vector2 localPosition)
    {
        if (targetTerrain == null)
        {
            return Vector3.zero;
        }

        return new Vector3(
            targetTerrain.transform.position.x +
            localPosition.x,

            targetTerrain.transform.position.y,

            targetTerrain.transform.position.z +
            localPosition.y
        );
    }


    // ============================================================
    // TEXTURE SETUP
    // ============================================================

    private void SetupTerrainTextures()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        if (!automaticallyTextureTerrain)
        {
            return;
        }

        TerrainData data =
            targetTerrain.terrainData;

        List<TerrainLayer> layers =
            new List<TerrainLayer>();

        if (grassTexture != null)
        {
            TerrainLayer grassLayer =
                new TerrainLayer();

            grassLayer.diffuseTexture =
                grassTexture;

            grassLayer.tileSize =
                new Vector2(
                    15f,
                    15f
                );

            layers.Add(
                grassLayer
            );
        }

        if (rockTexture != null)
        {
            TerrainLayer rockLayer =
                new TerrainLayer();

            rockLayer.diffuseTexture =
                rockTexture;

            rockLayer.tileSize =
                new Vector2(
                    12f,
                    12f
                );

            layers.Add(
                rockLayer
            );
        }

        if (layers.Count > 0)
        {
            data.terrainLayers =
                layers.ToArray();
        }

        PaintTerrainTextures();
    }


    // ============================================================
    // PAINT TERRAIN TEXTURES
    // ============================================================

    private void PaintTerrainTextures()
    {
        if (!automaticallyTextureTerrain)
        {
            return;
        }

        if (targetTerrain == null ||
            targetTerrain.terrainData == null)
        {
            return;
        }

        TerrainData data =
            targetTerrain.terrainData;

        TerrainLayer[] layers =
            data.terrainLayers;

        if (layers == null ||
            layers.Length == 0)
        {
            return;
        }

        int resolution =
            data.alphamapResolution;

        float[,,] alpha =
            new float[
                resolution,
                resolution,
                layers.Length
            ];

        for (int y = 0;
             y < resolution;
             y++)
        {
            for (int x = 0;
                 x < resolution;
                 x++)
            {
                float normalizedX =
                    x /
                    (float)(resolution - 1);

                float normalizedZ =
                    y /
                    (float)(resolution - 1);

                float height =
                    data.GetInterpolatedHeight(
                        normalizedX,
                        normalizedZ
                    );

                float normalizedHeight =
                    Mathf.InverseLerp(
                        data.bounds.min.y,
                        data.bounds.max.y,
                        height
                    );

                float steepness =
                    data.GetSteepness(
                        normalizedX,
                        normalizedZ
                    ) /
                    90f;

                float rockAmount =
                    Mathf.Max(
                        Mathf.InverseLerp(
                            rockStartHeight,
                            1f,
                            normalizedHeight
                        ),

                        Mathf.InverseLerp(
                            rockStartSlope,
                            1f,
                            steepness
                        )
                    );

                rockAmount =
                    Mathf.Clamp01(
                        rockAmount
                    );

                rockAmount =
                    Mathf.Lerp(
                        0f,
                        rockAmount,
                        grassStrength
                    );

                if (layers.Length == 1)
                {
                    alpha[y, x, 0] =
                        1f;
                }
                else
                {
                    alpha[y, x, 0] =
                        1f -
                        rockAmount;

                    alpha[y, x, 1] =
                        rockAmount;

                    for (int layer = 2;
                         layer < layers.Length;
                         layer++)
                    {
                        alpha[y, x, layer] =
                            0f;
                    }
                }
            }
        }

        data.SetAlphamaps(
            0,
            0,
            alpha
        );
    }


    // ============================================================
    // REFRESH TERRAIN COLLIDER
    // ============================================================

    private void RefreshTerrainCollider()
    {
        if (targetTerrain == null)
        {
            return;
        }

        TerrainCollider collider =
            targetTerrain.GetComponent<TerrainCollider>();

        if (collider != null)
        {
            collider.terrainData =
                null;

            collider.terrainData =
                targetTerrain.terrainData;
        }
    }


    // ============================================================
    // RESET TERRAIN
    // ============================================================

    public void ResetTerrain()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        SaveOriginalTerrain();

        if (originalHeights == null)
        {
            return;
        }

        targetTerrain.terrainData.SetHeights(
            0,
            0,
            originalHeights
        );

        generatedMountains.Clear();

        nextMountainId =
            1;

        PaintTerrainTextures();

        RefreshTerrainCollider();

        Debug.Log(
            "[TerrainGenerator] Terrain reset. " +
            "All generated mountains removed."
        );
    }


    // ============================================================
    // KEYBOARD TEST
    // ============================================================

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            CreateRandomMountain();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            CreateRandomMountainRange();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            CreateRandomMountains(3);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ResetTerrain();
        }
    }
}