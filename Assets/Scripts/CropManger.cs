using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CROP MANAGER
// ============================================================
//
// Responsibilities:
//
// - Plant crops
// - Plant different crop types
// - Plant crops only inside SoilArea
// - Remove crops
// - Remove all crops
// - Grow individual crops
// - Grow all crops
// - Grow crops to maturity
// - Find individual crops
// - Find crops near a position
// - Keep crop IDs stable
// - Remember crop type
// - Remember growth stage
// - Place crops on Unity Terrain
// - Keep crops on terrain
// - Prevent excessive overlap
// - Support multiple growth-stage prefabs
// - Transform mature crops into trees
//
// IMPORTANT
// ----------
//
// Soil creation is handled by SoilArea.
//
// CropManager handles:
//
//     SoilArea
//          ↓
//     CropManager
//          ↓
//     CropInstance
//
// Tree conversion:
//
//     Mature Crop
//          ↓
//     TreeManager
//          ↓
//     Oak / Pine / Palm / Birch / etc.
//
// The LLM does NOT directly manipulate Unity objects.
//
// ============================================================

public class CropManager : MonoBehaviour
{
    // =========================================================
    // TERRAIN
    // =========================================================

    [Header("Terrain")]

    [Tooltip(
        "Optional terrain. " +
        "If empty, CropManager automatically finds the generated terrain."
    )]
    [SerializeField]
    private Terrain terrain;


    // =========================================================
    // TERRAIN GENERATOR
    // =========================================================

    [Header("Terrain Generator")]

    [Tooltip(
        "Optional TerrainGenerator used when the terrain is generated at runtime."
    )]
    [SerializeField]
    private TerrainGenerator terrainGenerator;


    // =========================================================
    // TREE MANAGER
    // =========================================================

    [Header("Tree Manager")]

    [Tooltip(
        "TreeManager used when a mature crop becomes a tree."
    )]
    [SerializeField]
    private TreeManager treeManager;


    // =========================================================
    // SOIL AREAS
    // =========================================================

    [Header("Soil Areas")]

    [Tooltip(
        "Optional list of SoilArea objects. " +
        "If empty, CropManager automatically searches the scene."
    )]
    [SerializeField]
    private List<SoilArea> soilAreas =
        new List<SoilArea>();


    [Tooltip(
        "If enabled, crops can only be planted inside SoilArea."
    )]
    [SerializeField]
    private bool requireSoil = true;


    // =========================================================
    // CROP PARENT
    // =========================================================

    [Header("Crop Parent")]

    [Tooltip(
        "Parent transform used for all generated crops."
    )]
    [SerializeField]
    private Transform cropParent;


    // =========================================================
    // CROP TYPES
    // =========================================================

    [Header("Crop Types")]

    [Tooltip(
        "Configure all crop types here.\n\n" +
        "Example:\n" +
        "Wheat -> Wheat prefab\n" +
        "Corn -> Corn prefab\n" +
        "Rice -> Rice prefab\n" +
        "Carrot -> Carrot prefab"
    )]
    [SerializeField]
    private List<CropType> cropTypes =
        new List<CropType>();


    // =========================================================
    // MAXIMUM CROPS
    // =========================================================

    [Header("Maximum Crops")]

    [SerializeField]
    private int maximumCrops = 5000;


    // =========================================================
    // GROUNDING
    // =========================================================

    [Header("Crop Grounding")]

    [Tooltip(
        "Keeps crops grounded on the terrain."
    )]
    [SerializeField]
    private bool groundCropsOnTerrain = true;


    [Tooltip(
        "Small vertical offset above terrain."
    )]
    [SerializeField]
    private float groundOffset = 0.01f;


    // =========================================================
    // RANDOM ROTATION
    // =========================================================

    [Header("Random Rotation")]

    [SerializeField]
    private bool randomRotation = true;


    // =========================================================
    // RANDOM SCALE
    // =========================================================

    [Header("Random Scale")]

    [SerializeField]
    private bool randomScale = false;


    [SerializeField]
    private float minimumScale = 0.9f;


    [SerializeField]
    private float maximumScale = 1.1f;


    // =========================================================
    // INTERNAL CROP DATA
    // =========================================================

    private readonly Dictionary<int, CropInstance>
        cropsById =
            new Dictionary<int, CropInstance>();


    // =========================================================
    // NEXT CROP ID
    // =========================================================

    private int nextCropId = 1;


    // =========================================================
    // PUBLIC CROP COUNT
    // =========================================================

    public int CropCount
    {
        get
        {
            CleanupCropDictionary();

            return cropsById.Count;
        }
    }


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        CreateCropParent();

        FindTerrain();

        FindTreeManager();

        RefreshSoilAreas();

        RefreshCropDictionary();

        CalculateNextCropId();


        Debug.Log(
            "[CropManager] Awake complete. " +
            "Soil Areas = " +
            soilAreas.Count +
            " | Crops = " +
            cropsById.Count +
            " | Next Crop ID = " +
            nextCropId
        );
    }


    // =========================================================
    // CREATE CROP PARENT
    // =========================================================

    private void CreateCropParent()
    {
        if (cropParent != null)
        {
            return;
        }


        GameObject parentObject =
            new GameObject(
                "Generated Crops"
            );


        parentObject.transform.SetParent(
            transform
        );


        cropParent =
            parentObject.transform;
    }


    // =========================================================
    // FIND TERRAIN
    // =========================================================

    private Terrain FindTerrain()
    {
        // -----------------------------------------------------
        // ASSIGNED TERRAIN
        // -----------------------------------------------------

        if (
            terrain != null &&
            terrain.terrainData != null
        )
        {
            return terrain;
        }


        // -----------------------------------------------------
        // TERRAIN GENERATOR
        // -----------------------------------------------------

        if (terrainGenerator != null)
        {
            Terrain generatedTerrain =
                terrainGenerator.GetGeneratedTerrain();


            if (
                generatedTerrain != null &&
                generatedTerrain.terrainData != null
            )
            {
                terrain =
                    generatedTerrain;

                return generatedTerrain;
            }
        }


        // -----------------------------------------------------
        // ACTIVE TERRAIN
        // -----------------------------------------------------

        Terrain activeTerrain =
            Terrain.activeTerrain;


        if (
            activeTerrain != null &&
            activeTerrain.terrainData != null
        )
        {
            terrain =
                activeTerrain;

            return activeTerrain;
        }


        // -----------------------------------------------------
        // SEARCH SCENE
        // -----------------------------------------------------

        Terrain[] terrains =
            FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude
            );


        if (terrains != null)
        {
            foreach (
                Terrain foundTerrain
                in terrains
            )
            {
                if (
                    foundTerrain != null &&
                    foundTerrain.terrainData != null &&
                    foundTerrain.gameObject.activeInHierarchy
                )
                {
                    terrain =
                        foundTerrain;

                    return foundTerrain;
                }
            }
        }


        return null;
    }


    // =========================================================
    // GET TERRAIN
    // =========================================================

    private Terrain GetTerrain()
    {
        if (
            terrain != null &&
            terrain.terrainData != null
        )
        {
            return terrain;
        }


        return FindTerrain();
    }


    // =========================================================
    // SET TERRAIN
    // =========================================================

    public void SetTerrain(
        Terrain newTerrain
    )
    {
        terrain =
            newTerrain;


        Debug.Log(
            "[CropManager] Terrain assigned: " +
            (
                newTerrain != null
                    ? newTerrain.name
                    : "None"
            )
        );
    }


    // =========================================================
    // SET TERRAIN GENERATOR
    // =========================================================

    public void SetTerrainGenerator(
        TerrainGenerator generator
    )
    {
        terrainGenerator =
            generator;
    }


    // =========================================================
    // SET TREE MANAGER
    // =========================================================

    public void SetTreeManager(
        TreeManager newTreeManager
    )
    {
        treeManager =
            newTreeManager;
    }


    // =========================================================
    // FIND TREE MANAGER
    // =========================================================

    private TreeManager FindTreeManager()
    {
        if (treeManager != null)
        {
            return treeManager;
        }


        treeManager =
            FindFirstObjectByType<TreeManager>();


        return treeManager;
    }


    // =========================================================
    // REGISTER SOIL AREA
    // =========================================================

    public void RegisterSoilArea(
        SoilArea soilArea
    )
    {
        if (soilArea == null)
        {
            return;
        }


        if (!soilAreas.Contains(soilArea))
        {
            soilAreas.Add(
                soilArea
            );
        }


        Debug.Log(
            "[CropManager] Registered SoilArea: " +
            soilArea.name
        );
    }


    // =========================================================
    // REFRESH SOIL AREAS
    // =========================================================

    public void RefreshSoilAreas()
    {
        soilAreas.Clear();


        SoilArea[] foundSoilAreas =
            FindObjectsByType<SoilArea>(
                FindObjectsInactive.Exclude
            );


        if (foundSoilAreas == null)
        {
            return;
        }


        foreach (
            SoilArea soilArea
            in foundSoilAreas
        )
        {
            if (soilArea == null)
            {
                continue;
            }


            soilAreas.Add(
                soilArea
            );
        }


        Debug.Log(
            "[CropManager] Found " +
            soilAreas.Count +
            " SoilArea objects."
        );
    }


    // =========================================================
    // GET SOIL AT POSITION
    // =========================================================

    public SoilArea GetSoilAtPosition(
        Vector3 worldPosition
    )
    {
        if (soilAreas.Count == 0)
        {
            RefreshSoilAreas();
        }


        foreach (
            SoilArea soilArea
            in soilAreas
        )
        {
            if (soilArea == null)
            {
                continue;
            }


            if (
                soilArea.CanPlantAt(
                    worldPosition
                )
            )
            {
                return soilArea;
            }
        }


        return null;
    }


    // =========================================================
    // PLANT SINGLE CROP
    // =========================================================

    public CropInstance PlantCropAtWorldPosition(
        string cropType,
        Vector3 worldPosition
    )
    {
        Debug.Log(
            "[CropManager] =================================="
        );


        Debug.Log(
            "[CropManager] Plant crop request"
        );


        Debug.Log(
            "[CropManager] Type = " +
            cropType
        );


        Debug.Log(
            "[CropManager] Position = " +
            worldPosition
        );


        // -----------------------------------------------------
        // TERRAIN
        // -----------------------------------------------------

        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[CropManager] Cannot plant crop. " +
                "No terrain found."
            );

            return null;
        }


        // -----------------------------------------------------
        // SOIL
        // -----------------------------------------------------

        SoilArea soil =
            GetSoilAtPosition(
                worldPosition
            );


        if (
            requireSoil &&
            soil == null
        )
        {
            Debug.LogWarning(
                "[CropManager] Cannot plant crop. " +
                "Position is outside a SoilArea."
            );

            return null;
        }


        // -----------------------------------------------------
        // CROP TYPE
        // -----------------------------------------------------

        CropType definition =
            GetCropType(
                cropType
            );


        if (definition == null)
        {
            Debug.LogWarning(
                "[CropManager] Crop type not found: " +
                cropType
            );

            return null;
        }


        // -----------------------------------------------------
        // MAXIMUM
        // -----------------------------------------------------

        CleanupCropDictionary();


        if (
            cropsById.Count >=
            maximumCrops
        )
        {
            Debug.LogWarning(
                "[CropManager] Maximum crop count reached."
            );

            return null;
        }


        // -----------------------------------------------------
        // TERRAIN POSITION
        // -----------------------------------------------------

        Vector3 finalPosition =
            SnapToTerrain(
                worldPosition
            );


        // -----------------------------------------------------
        // CREATE
        // -----------------------------------------------------

        CropInstance instance =
            CreateCropInstance(
                definition,
                finalPosition
            );


        if (instance == null)
        {
            return null;
        }


        Debug.Log(
            "[CropManager] Planted crop: " +
            instance.cropName +
            " | ID = " +
            instance.cropId +
            " | Soil = " +
            (
                soil != null
                    ? soil.name
                    : "None"
            ) +
            " | Position = " +
            instance.transform.position
        );


        return instance;
    }


    // =========================================================
    // PLANT CROPS
    // =========================================================
    //
    // Example:
    //
    // PlantCrops(
    //     "Wheat",
    //     20,
    //     soil.transform.position,
    //     5f,
    //     0.5f
    // );
    //
    // =========================================================

    public int PlantCrops(
        string cropType,
        int count,
        Vector3 center,
        float radius,
        float spacing
    )
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[CropManager] Cannot plant crops. " +
                "No terrain found."
            );

            return 0;
        }


        // -----------------------------------------------------
        // CROP TYPE
        // -----------------------------------------------------

        CropType definition =
            GetCropType(
                cropType
            );


        if (definition == null)
        {
            Debug.LogWarning(
                "[CropManager] Crop type not found: " +
                cropType
            );

            return 0;
        }


        // -----------------------------------------------------
        // SOIL
        // -----------------------------------------------------

        SoilArea soil =
            GetSoilAtPosition(
                center
            );


        if (
            requireSoil &&
            soil == null
        )
        {
            Debug.LogWarning(
                "[CropManager] Cannot plant crops. " +
                "Center position is not inside SoilArea."
            );

            return 0;
        }


        // -----------------------------------------------------
        // LIMITS
        // -----------------------------------------------------

        count =
            Mathf.Clamp(
                count,
                1,
                maximumCrops
            );


        radius =
            Mathf.Max(
                radius,
                0.5f
            );


        spacing =
            Mathf.Max(
                spacing,
                0.1f
            );


        CleanupCropDictionary();


        int availableSpace =
            maximumCrops -
            cropsById.Count;


        if (availableSpace <= 0)
        {
            Debug.LogWarning(
                "[CropManager] Maximum crop count reached."
            );

            return 0;
        }


        count =
            Mathf.Min(
                count,
                availableSpace
            );


        int planted =
            0;


        int attempts =
            0;


        int maximumAttempts =
            Mathf.Max(
                count * 30,
                30
            );


        // -----------------------------------------------------
        // PLANT
        // -----------------------------------------------------

        while (
            planted < count &&
            attempts < maximumAttempts
        )
        {
            attempts++;


            // -------------------------------------------------
            // RANDOM POSITION
            // -------------------------------------------------

            Vector2 random =
                UnityEngine.Random.insideUnitCircle *
                radius;


            Vector3 candidate =
                new Vector3(
                    center.x + random.x,
                    center.y,
                    center.z + random.y
                );


            // -------------------------------------------------
            // SOIL CHECK
            // -------------------------------------------------

            if (
                requireSoil &&
                soil != null &&
                !soil.CanPlantAt(
                    candidate
                )
            )
            {
                continue;
            }


            // -------------------------------------------------
            // TERRAIN
            // -------------------------------------------------

            candidate =
                SnapToTerrain(
                    candidate
                );


            // -------------------------------------------------
            // SOIL CHECK AGAIN
            // -------------------------------------------------
            //
            // Important because terrain snapping changes Y.
            //
            // -------------------------------------------------

            if (
                requireSoil &&
                soil != null &&
                !soil.CanPlantAt(
                    candidate
                )
            )
            {
                continue;
            }


            // -------------------------------------------------
            // SPACING
            // -------------------------------------------------

            if (
                !IsCropPositionAvailable(
                    candidate,
                    spacing
                )
            )
            {
                continue;
            }


            // -------------------------------------------------
            // CREATE
            // -------------------------------------------------

            CropInstance instance =
                CreateCropInstance(
                    definition,
                    candidate
                );


            if (instance != null)
            {
                planted++;
            }
        }


        Debug.Log(
            "[CropManager] Planted " +
            planted +
            " / " +
            count +
            " " +
            cropType +
            " crops."
        );


        return planted;
    }


    // =========================================================
    // CREATE CROP INSTANCE
    // =========================================================

    private CropInstance CreateCropInstance(
        CropType definition,
        Vector3 position
    )
    {
        if (definition == null)
        {
            Debug.LogError(
                "[CropManager] Crop definition is null."
            );

            return null;
        }


        if (definition.prefab == null)
        {
            Debug.LogError(
                "[CropManager] Crop prefab is missing for: " +
                definition.cropName
            );

            return null;
        }


        // -----------------------------------------------------
        // MAXIMUM
        // -----------------------------------------------------

        CleanupCropDictionary();


        if (
            cropsById.Count >=
            maximumCrops
        )
        {
            Debug.LogWarning(
                "[CropManager] Maximum crop count reached."
            );

            return null;
        }


        // -----------------------------------------------------
        // CREATE
        // -----------------------------------------------------

        GameObject cropObject =
            Instantiate(
                definition.prefab,
                position,
                Quaternion.identity,
                cropParent
            );


        if (cropObject == null)
        {
            return null;
        }


        // -----------------------------------------------------
        // ID
        // -----------------------------------------------------

        int cropId =
            nextCropId;


        nextCropId++;


        // -----------------------------------------------------
        // NAME
        // -----------------------------------------------------

        cropObject.name =
            definition.cropName +
            "_Crop_" +
            cropId;


        // -----------------------------------------------------
        // COMPONENT
        // -----------------------------------------------------

        CropInstance instance =
            cropObject.GetComponent<CropInstance>();


        if (instance == null)
        {
            instance =
                cropObject.AddComponent<CropInstance>();
        }


        // -----------------------------------------------------
        // INITIALIZE
        // -----------------------------------------------------

        instance.Initialize(
            cropId,
            definition.cropName,
            definition,
            this
        );


        // -----------------------------------------------------
        // RANDOM ROTATION
        // -----------------------------------------------------

        if (randomRotation)
        {
            cropObject.transform.rotation =
                Quaternion.Euler(
                    0f,
                    UnityEngine.Random.Range(
                        0f,
                        360f
                    ),
                    0f
                );
        }


        // -----------------------------------------------------
        // RANDOM SCALE
        // -----------------------------------------------------

        if (randomScale)
        {
            float scale =
                UnityEngine.Random.Range(
                    minimumScale,
                    maximumScale
                );


            cropObject.transform.localScale *=
                scale;
        }


        // -----------------------------------------------------
        // GROUND
        // -----------------------------------------------------

        GroundCropOnTerrain(
            cropObject,
            GetTerrain()
        );


        // -----------------------------------------------------
        // REGISTER
        // -----------------------------------------------------

        cropsById[cropId] =
            instance;


        Debug.Log(
            "[CropManager] Created " +
            cropObject.name +
            " | Type = " +
            definition.cropName +
            " | ID = " +
            cropId
        );


        return instance;
    }


    // =========================================================
    // GROW SINGLE CROP
    // =========================================================

    public bool GrowCrop(
        int cropId
    )
    {
        CropInstance crop =
            GetCrop(
                cropId
            );


        if (crop == null)
        {
            Debug.LogWarning(
                "[CropManager] Crop not found: " +
                cropId
            );

            return false;
        }


        bool result =
            crop.Grow();


        if (result)
        {
            GroundCropOnTerrain(
                crop.gameObject,
                GetTerrain()
            );
        }


        return result;
    }


    // =========================================================
    // GROW TO MATURITY
    // =========================================================

    public bool GrowCropToMaturity(
        int cropId
    )
    {
        CropInstance crop =
            GetCrop(
                cropId
            );


        if (crop == null)
        {
            Debug.LogWarning(
                "[CropManager] Crop not found: " +
                cropId
            );

            return false;
        }


        crop.GrowToMaturity();


        GroundCropOnTerrain(
            crop.gameObject,
            GetTerrain()
        );


        return true;
    }


    // =========================================================
    // GROW ALL CROPS
    // =========================================================

    public int GrowAllCrops()
    {
        CleanupCropDictionary();


        int grown =
            0;


        List<CropInstance> crops =
            new List<CropInstance>(
                cropsById.Values
            );


        foreach (
            CropInstance crop
            in crops
        )
        {
            if (crop == null)
            {
                continue;
            }


            if (crop.Grow())
            {
                GroundCropOnTerrain(
                    crop.gameObject,
                    GetTerrain()
                );


                grown++;
            }
        }


        Debug.Log(
            "[CropManager] Grown " +
            grown +
            " crops."
        );


        return grown;
    }


    // =========================================================
    // GROW ALL TO MATURITY
    // =========================================================

    public int GrowAllCropsToMaturity()
    {
        CleanupCropDictionary();


        int grown =
            0;


        List<CropInstance> crops =
            new List<CropInstance>(
                cropsById.Values
            );


        foreach (
            CropInstance crop
            in crops
        )
        {
            if (crop == null)
            {
                continue;
            }


            crop.GrowToMaturity();


            GroundCropOnTerrain(
                crop.gameObject,
                GetTerrain()
            );


            grown++;
        }


        Debug.Log(
            "[CropManager] Grown " +
            grown +
            " crops to maturity."
        );


        return grown;
    }


    // =========================================================
    // TRANSFORM CROP INTO TREE
    // =========================================================
    //
    // Example:
    //
    // TransformCropIntoTree(
    //     5,
    //     "Oak"
    // );
    //
    // =========================================================

    public bool TransformCropIntoTree(
        int cropId,
        string treeType
    )
    {
        Debug.Log(
            "[CropManager] =================================="
        );


        Debug.Log(
            "[CropManager] Crop -> Tree request"
        );


        Debug.Log(
            "[CropManager] Crop ID = " +
            cropId
        );


        Debug.Log(
            "[CropManager] Tree Type = " +
            treeType
        );


        // -----------------------------------------------------
        // CROP
        // -----------------------------------------------------

        CropInstance crop =
            GetCrop(
                cropId
            );


        if (crop == null)
        {
            Debug.LogWarning(
                "[CropManager] Crop not found: " +
                cropId
            );

            return false;
        }


        // -----------------------------------------------------
        // MATURE
        // -----------------------------------------------------

        if (!crop.IsMature)
        {
            Debug.LogWarning(
                "[CropManager] Crop " +
                cropId +
                " is not mature."
            );

            return false;
        }


        // -----------------------------------------------------
        // CAN BECOME TREE
        // -----------------------------------------------------

        if (
            crop.Definition != null &&
            !crop.Definition.canBecomeTree
        )
        {
            Debug.LogWarning(
                "[CropManager] Crop type " +
                crop.cropName +
                " cannot become a tree."
            );

            return false;
        }


        // -----------------------------------------------------
        // TREE MANAGER
        // -----------------------------------------------------

        TreeManager currentTreeManager =
            FindTreeManager();


        if (currentTreeManager == null)
        {
            Debug.LogError(
                "[CropManager] TreeManager is not assigned."
            );

            return false;
        }


        // -----------------------------------------------------
        // TREE TYPE
        // -----------------------------------------------------

        if (
            string.IsNullOrWhiteSpace(
                treeType
            )
        )
        {
            Debug.LogWarning(
                "[CropManager] Tree type is empty."
            );

            return false;
        }


        // -----------------------------------------------------
        // VERIFY TREE PREFAB
        // -----------------------------------------------------

        GameObject treePrefab =
            currentTreeManager.GetTreePrefab(
                treeType
            );


        if (treePrefab == null)
        {
            Debug.LogError(
                "[CropManager] Tree type not found: " +
                treeType
            );

            return false;
        }


        // -----------------------------------------------------
        // POSITION
        // -----------------------------------------------------

        Vector3 treePosition =
            crop.transform.position;


        // -----------------------------------------------------
        // CREATE TREE
        // -----------------------------------------------------

        GameObject tree =
            currentTreeManager.CreateTreeAtWorldPosition(
                treeType,
                treePosition
            );


        if (tree == null)
        {
            Debug.LogError(
                "[CropManager] Failed to create tree."
            );

            return false;
        }


        // -----------------------------------------------------
        // REMOVE CROP
        // -----------------------------------------------------

        RemoveCrop(
            cropId
        );


        Debug.Log(
            "[CropManager] Crop " +
            cropId +
            " transformed into " +
            treeType +
            " tree."
        );


        return true;
    }


    // =========================================================
    // REMOVE CROP
    // =========================================================

    public bool RemoveCrop(
        int cropId
    )
    {
        CropInstance crop =
            GetCrop(
                cropId
            );


        if (crop == null)
        {
            Debug.LogWarning(
                "[CropManager] Cannot remove crop. " +
                "Crop ID not found = " +
                cropId
            );

            return false;
        }


        cropsById.Remove(
            cropId
        );


        Debug.Log(
            "[CropManager] Removing " +
            crop.name
        );


        if (crop.gameObject != null)
        {
            Destroy(
                crop.gameObject
            );
        }


        return true;
    }


    // =========================================================
    // REMOVE CROPS BY AREA
    // =========================================================

    public int RemoveCrops(
        Vector3 center,
        float radius
    )
    {
        radius =
            Mathf.Max(
                radius,
                0f
            );


        List<int> cropIdsToRemove =
            new List<int>();


        foreach (
            KeyValuePair<int, CropInstance>
            pair
            in cropsById
        )
        {
            CropInstance crop =
                pair.Value;


            if (crop == null)
            {
                continue;
            }


            float distance =
                Vector2.Distance(
                    new Vector2(
                        crop.transform.position.x,
                        crop.transform.position.z
                    ),
                    new Vector2(
                        center.x,
                        center.z
                    )
                );


            if (
                distance <=
                radius
            )
            {
                cropIdsToRemove.Add(
                    pair.Key
                );
            }
        }


        foreach (
            int cropId
            in cropIdsToRemove
        )
        {
            RemoveCrop(
                cropId
            );
        }


        Debug.Log(
            "[CropManager] Removed " +
            cropIdsToRemove.Count +
            " crops."
        );


        return cropIdsToRemove.Count;
    }


    // =========================================================
    // REMOVE ALL CROPS
    // =========================================================

    public void RemoveAllCrops()
    {
        List<CropInstance> crops =
            new List<CropInstance>(
                cropsById.Values
            );


        foreach (
            CropInstance crop
            in crops
        )
        {
            if (
                crop != null &&
                crop.gameObject != null
            )
            {
                Destroy(
                    crop.gameObject
                );
            }
        }


        cropsById.Clear();


        Debug.Log(
            "[CropManager] Removed all crops."
        );
    }


    // =========================================================
    // GET CROP
    // =========================================================

    public CropInstance GetCrop(
        int cropId
    )
    {
        CropInstance crop;


        if (
            cropsById.TryGetValue(
                cropId,
                out crop
            )
        )
        {
            if (crop != null)
            {
                return crop;
            }


            cropsById.Remove(
                cropId
            );
        }


        return null;
    }


    // =========================================================
    // GET ALL CROPS
    // =========================================================

    public List<CropInstance> GetAllCrops()
    {
        CleanupCropDictionary();


        return new List<CropInstance>(
            cropsById.Values
        );
    }


    // =========================================================
    // GET CLOSEST CROP
    // =========================================================

    public CropInstance GetClosestCrop(
        Vector3 position,
        float maximumDistance
    )
    {
        CleanupCropDictionary();


        CropInstance closest =
            null;


        float closestDistance =
            maximumDistance;


        foreach (
            CropInstance crop
            in cropsById.Values
        )
        {
            if (crop == null)
            {
                continue;
            }


            float distance =
                Vector3.Distance(
                    crop.transform.position,
                    position
                );


            if (
                distance <
                closestDistance
            )
            {
                closestDistance =
                    distance;


                closest =
                    crop;
            }
        }


        return closest;
    }


    // =========================================================
    // GET CROP TYPE
    // =========================================================

    public CropType GetCropType(
        string cropName
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                cropName
            )
        )
        {
            return null;
        }


        string requestedName =
            NormalizeCropType(
                cropName
            );


        foreach (
            CropType type
            in cropTypes
        )
        {
            if (type == null)
            {
                continue;
            }


            if (
                string.IsNullOrWhiteSpace(
                    type.cropName
                )
            )
            {
                continue;
            }


            string configuredName =
                NormalizeCropType(
                    type.cropName
                );


            if (
                configuredName ==
                requestedName
            )
            {
                return type;
            }
        }


        return null;
    }


    // =========================================================
    // NORMALIZE CROP TYPE
    // =========================================================
    //
    // Makes AI speech variations easier to handle.
    //
    // Examples:
    //
    // "wheat"       -> "wheat"
    // " Wheat "     -> "wheat"
    // "WHEAT"       -> "wheat"
    //
    // =========================================================

    private string NormalizeCropType(
        string value
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return string.Empty;
        }


        return value
            .Trim()
            .ToLowerInvariant();
    }


    // =========================================================
    // SNAP TO TERRAIN
    // =========================================================

    private Vector3 SnapToTerrain(
        Vector3 position
    )
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            return position;
        }


        TerrainData data =
            currentTerrain.terrainData;


        if (data == null)
        {
            return position;
        }


        Vector3 terrainPosition =
            currentTerrain.transform.position;


        Vector3 terrainSize =
            data.size;


        // -----------------------------------------------------
        // CLAMP X
        // -----------------------------------------------------

        float x =
            Mathf.Clamp(
                position.x,
                terrainPosition.x,
                terrainPosition.x +
                terrainSize.x
            );


        // -----------------------------------------------------
        // CLAMP Z
        // -----------------------------------------------------

        float z =
            Mathf.Clamp(
                position.z,
                terrainPosition.z,
                terrainPosition.z +
                terrainSize.z
            );


        // -----------------------------------------------------
        // TERRAIN HEIGHT
        // -----------------------------------------------------

        float terrainHeight =
            currentTerrain.SampleHeight(
                new Vector3(
                    x,
                    terrainPosition.y,
                    z
                )
            );


        float worldY =
            terrainPosition.y +
            terrainHeight;


        return new Vector3(
            x,
            worldY +
            groundOffset,
            z
        );
    }


    // =========================================================
    // GROUND CROP ON TERRAIN
    // =========================================================

    private void GroundCropOnTerrain(
        GameObject crop,
        Terrain currentTerrain
    )
    {
        if (!groundCropsOnTerrain)
        {
            return;
        }


        if (crop == null)
        {
            return;
        }


        if (currentTerrain == null)
        {
            return;
        }


        // -----------------------------------------------------
        // TERRAIN HEIGHT
        // -----------------------------------------------------

        Vector3 cropPosition =
            crop.transform.position;


        float terrainHeight =
            currentTerrain.SampleHeight(
                cropPosition
            );


        float terrainWorldY =
            currentTerrain.transform.position.y +
            terrainHeight;


        // -----------------------------------------------------
        // RENDERERS
        // -----------------------------------------------------

        Renderer[] renderers =
            crop.GetComponentsInChildren<Renderer>(
                true
            );


        if (
            renderers == null ||
            renderers.Length == 0
        )
        {
            crop.transform.position =
                new Vector3(
                    cropPosition.x,
                    terrainWorldY +
                    groundOffset,
                    cropPosition.z
                );

            return;
        }


        // -----------------------------------------------------
        // FIND LOWEST POINT
        // -----------------------------------------------------

        bool hasBounds =
            false;


        float lowestPoint =
            float.MaxValue;


        foreach (
            Renderer renderer
            in renderers
        )
        {
            if (renderer == null)
            {
                continue;
            }


            Bounds bounds =
                renderer.bounds;


            if (!hasBounds)
            {
                lowestPoint =
                    bounds.min.y;


                hasBounds =
                    true;
            }
            else
            {
                lowestPoint =
                    Mathf.Min(
                        lowestPoint,
                        bounds.min.y
                    );
            }
        }


        // -----------------------------------------------------
        // FALLBACK
        // -----------------------------------------------------

        if (!hasBounds)
        {
            crop.transform.position =
                new Vector3(
                    cropPosition.x,
                    terrainWorldY +
                    groundOffset,
                    cropPosition.z
                );

            return;
        }


        // -----------------------------------------------------
        // CORRECTION
        // -----------------------------------------------------

        float verticalDifference =
            terrainWorldY -
            lowestPoint;


        crop.transform.position +=
            new Vector3(
                0f,
                verticalDifference +
                groundOffset,
                0f
            );
    }


    // =========================================================
    // CROP SPACING
    // =========================================================

    private bool IsCropPositionAvailable(
        Vector3 position,
        float minimumSpacing
    )
    {
        float spacingSquared =
            minimumSpacing *
            minimumSpacing;


        foreach (
            CropInstance crop
            in cropsById.Values
        )
        {
            if (crop == null)
            {
                continue;
            }


            Vector3 difference =
                crop.transform.position -
                position;


            difference.y =
                0f;


            if (
                difference.sqrMagnitude <
                spacingSquared
            )
            {
                return false;
            }
        }


        return true;
    }


    // =========================================================
    // REFRESH CROP DICTIONARY
    // =========================================================
    //
    // This allows crops that already exist underneath
    // Generated Crops to be recovered.
    //
    // =========================================================

    private void RefreshCropDictionary()
    {
        cropsById.Clear();


        if (cropParent == null)
        {
            return;
        }


        CropInstance[] instances =
            cropParent.GetComponentsInChildren<CropInstance>(
                true
            );


        if (instances == null)
        {
            return;
        }


        foreach (
            CropInstance instance
            in instances
        )
        {
            if (instance == null)
            {
                continue;
            }


            if (
                instance.cropId <= 0
            )
            {
                continue;
            }


            if (
                !cropsById.ContainsKey(
                    instance.cropId
                )
            )
            {
                cropsById.Add(
                    instance.cropId,
                    instance
                );
            }
        }
    }


    // =========================================================
    // CLEANUP CROP DICTIONARY
    // =========================================================

    private void CleanupCropDictionary()
    {
        List<int> invalidIds =
            new List<int>();


        foreach (
            KeyValuePair<int, CropInstance>
            pair
            in cropsById
        )
        {
            if (pair.Value == null)
            {
                invalidIds.Add(
                    pair.Key
                );
            }
        }


        foreach (
            int id
            in invalidIds
        )
        {
            cropsById.Remove(
                id
            );
        }
    }


    // =========================================================
    // CALCULATE NEXT CROP ID
    // =========================================================

    private void CalculateNextCropId()
    {
        int highestId =
            0;


        foreach (
            CropInstance crop
            in cropsById.Values
        )
        {
            if (crop == null)
            {
                continue;
            }


            highestId =
                Mathf.Max(
                    highestId,
                    crop.cropId
                );
        }


        nextCropId =
            highestId + 1;
    }


    // =========================================================
    // LOG CROP INFORMATION
    // =========================================================

    public void LogCropInformation(
        int cropId
    )
    {
        CropInstance crop =
            GetCrop(
                cropId
            );


        if (crop == null)
        {
            Debug.Log(
                "[CropManager] Crop not found: " +
                cropId
            );

            return;
        }


        Debug.Log(
            "[CropManager] Crop Information\n" +
            "-----------------------------\n" +
            "ID: " +
            crop.cropId +
            "\n" +
            "Name: " +
            crop.cropName +
            "\n" +
            "Stage: " +
            crop.CurrentStage +
            "\n" +
            "Mature: " +
            crop.IsMature +
            "\n" +
            "Can Become Tree: " +
            crop.CanBecomeTree +
            "\n" +
            "Position: " +
            crop.transform.position
        );
    }


    // =========================================================
    // CROP TYPE
    // =========================================================

    [Serializable]
    public class CropType
    {
        // -----------------------------------------------------
        // CROP NAME
        // -----------------------------------------------------

        [Tooltip(
            "Name used by the AI/user. " +
            "Example: Wheat, Corn, Rice, Carrot."
        )]
        public string cropName;


        // -----------------------------------------------------
        // DEFAULT PREFAB
        // -----------------------------------------------------

        [Tooltip(
            "Default crop prefab."
        )]
        public GameObject prefab;


        // -----------------------------------------------------
        // GROWTH STAGE PREFABS
        // -----------------------------------------------------

        [Tooltip(
            "Optional visual prefabs for each growth stage."
        )]
        public GameObject[] growthStagePrefabs;


        // -----------------------------------------------------
        // GROWTH STAGES
        // -----------------------------------------------------

        [Min(1)]
        public int growthStages = 3;


        // -----------------------------------------------------
        // CAN BECOME TREE
        // -----------------------------------------------------

        [Tooltip(
            "If enabled, this crop can become a tree when mature."
        )]
        public bool canBecomeTree = true;
    }


    // =========================================================
    // CROP INSTANCE
    // =========================================================
    //
    // Represents ONE individual crop.
    //
    // Example:
    //
    // Wheat_Crop_1
    // Wheat_Crop_2
    // Corn_Crop_3
    //
    // Each crop has its own:
    //
    // - ID
    // - Type
    // - Growth stage
    // - Mature state
    //
    // =========================================================

    public class CropInstance :
        MonoBehaviour
    {
        // =====================================================
        // CROP ID
        // =====================================================

        public int cropId
        {
            get;
            private set;
        }


        // =====================================================
        // CROP NAME
        // =====================================================

        public string cropName
        {
            get;
            private set;
        }


        // =====================================================
        // CURRENT STAGE
        // =====================================================

        public int CurrentStage
        {
            get;
            private set;
        }


        // =====================================================
        // MATURE
        // =====================================================

        public bool IsMature
        {
            get;
            private set;
        }


        // =====================================================
        // DEFINITION
        // =====================================================

        public CropType Definition
        {
            get;
            private set;
        }


        // =====================================================
        // CAN BECOME TREE
        // =====================================================

        public bool CanBecomeTree
        {
            get
            {
                return
                    Definition != null &&
                    Definition.canBecomeTree;
            }
        }


        // =====================================================
        // MANAGER
        // =====================================================

        private CropManager manager;


        // =====================================================
        // VISUAL CONTAINER
        // =====================================================

        private Transform generatedVisualContainer;


        // =====================================================
        // INITIALIZE
        // =====================================================

        public void Initialize(
            int id,
            string name,
            CropType cropDefinition,
            CropManager cropManager
        )
        {
            cropId =
                id;


            cropName =
                name;


            Definition =
                cropDefinition;


            manager =
                cropManager;


            CurrentStage =
                0;


            IsMature =
                Definition != null &&
                Definition.growthStages <= 1;


            UpdateVisual();
        }


        // =====================================================
        // GROW
        // =====================================================

        public bool Grow()
        {
            if (Definition == null)
            {
                return false;
            }


            if (IsMature)
            {
                return false;
            }


            int totalStages =
                Mathf.Max(
                    Definition.growthStages,
                    1
                );


            CurrentStage++;


            if (
                CurrentStage >=
                totalStages - 1
            )
            {
                CurrentStage =
                    totalStages - 1;


                IsMature =
                    true;
            }


            UpdateVisual();


            Debug.Log(
                "[CropInstance] " +
                name +
                " grew to stage " +
                CurrentStage +
                " | Mature = " +
                IsMature
            );


            return true;
        }


        // =====================================================
        // GROW TO MATURITY
        // =====================================================

        public void GrowToMaturity()
        {
            if (Definition == null)
            {
                return;
            }


            CurrentStage =
                Mathf.Max(
                    Definition.growthStages - 1,
                    0
                );


            IsMature =
                true;


            UpdateVisual();


            Debug.Log(
                "[CropInstance] " +
                name +
                " is now mature."
            );
        }


        // =====================================================
        // UPDATE VISUAL
        // =====================================================
        //
        // IMPORTANT:
        //
        // Only the generated growth visual is destroyed.
        //
        // Other children belonging to the original crop prefab
        // are preserved.
        //
        // =====================================================

        private void UpdateVisual()
        {
            if (Definition == null)
            {
                return;
            }


            if (
                Definition.growthStagePrefabs == null ||
                Definition.growthStagePrefabs.Length == 0
            )
            {
                return;
            }


            // -------------------------------------------------
            // GET VISUAL CONTAINER
            // -------------------------------------------------

            if (generatedVisualContainer == null)
            {
                Transform existing =
                    transform.Find(
                        "Generated Growth Visual"
                    );


                if (existing != null)
                {
                    generatedVisualContainer =
                        existing;
                }
                else
                {
                    GameObject container =
                        new GameObject(
                            "Generated Growth Visual"
                        );


                    generatedVisualContainer =
                        container.transform;


                    generatedVisualContainer.SetParent(
                        transform
                    );


                    generatedVisualContainer.localPosition =
                        Vector3.zero;


                    generatedVisualContainer.localRotation =
                        Quaternion.identity;


                    generatedVisualContainer.localScale =
                        Vector3.one;
                }
            }


            // -------------------------------------------------
            // REMOVE OLD VISUAL
            // -------------------------------------------------

            List<GameObject> oldVisuals =
                new List<GameObject>();


            for (
                int i = 0;
                i <
                generatedVisualContainer.childCount;
                i++
            )
            {
                Transform child =
                    generatedVisualContainer.GetChild(i);


                if (child != null)
                {
                    oldVisuals.Add(
                        child.gameObject
                    );
                }
            }


            foreach (
                GameObject visual
                in oldVisuals
            )
            {
                if (visual != null)
                {
                    Destroy(
                        visual
                    );
                }
            }


            // -------------------------------------------------
            // SELECT STAGE
            // -------------------------------------------------

            int index =
                Mathf.Clamp(
                    CurrentStage,
                    0,
                    Definition.growthStagePrefabs.Length - 1
                );


            GameObject prefab =
                Definition.growthStagePrefabs[index];


            if (prefab == null)
            {
                return;
            }


            // -------------------------------------------------
            // CREATE VISUAL
            // -------------------------------------------------

            GameObject visualObject =
                Instantiate(
                    prefab,
                    generatedVisualContainer
                );


            if (visualObject == null)
            {
                return;
            }


            visualObject.transform.localPosition =
                Vector3.zero;


            visualObject.transform.localRotation =
                Quaternion.identity;


            visualObject.transform.localScale =
                Vector3.one;
        }
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        cropsById.Clear();
    }
}