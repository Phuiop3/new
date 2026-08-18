using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CROP MANAGER
// ============================================================
//
// Responsibilities:
//
// - Detect terrain through SoilManager
// - Plant crops inside SoilArea
// - Support multiple crop types
// - Stable crop IDs
// - Crop growth
// - Terrain surface positioning
// - Crop spacing
// - Remove crops
// - Remove all crops
//
// ============================================================

public class CropManager : MonoBehaviour
{
    // =========================================================
    // CROP TYPE
    // =========================================================
    //
    // This is the type required by CropInstance.
    //
    // =========================================================

    [Serializable]
    public class CropType
    {
        public string cropName = "Default";

        [Min(1)]
        public int growthStages = 3;

        public bool canBecomeTree = false;

        public GameObject[] growthStagePrefabs;
    }


    // =========================================================
    // SIMPLE PREFAB ENTRY
    // =========================================================

    [Serializable]
    public class CropPrefabEntry
    {
        public string cropType;

        public GameObject prefab;
    }


    // =========================================================
    // MANAGERS
    // =========================================================

    [Header("Managers")]

    [SerializeField]
    private SoilManager soilManager;


    // =========================================================
    // DEFAULT PREFAB
    // =========================================================

    [Header("Default Crop")]

    [SerializeField]
    private GameObject cropPrefab;


    // =========================================================
    // CROP DEFINITIONS
    // =========================================================

    [Header("Crop Definitions")]

    [SerializeField]
    private List<CropType> cropTypes =
        new List<CropType>();


    // =========================================================
    // LEGACY / SIMPLE PREFABS
    // =========================================================

    [Header("Simple Crop Prefabs")]

    [SerializeField]
    private List<CropPrefabEntry> cropPrefabs =
        new List<CropPrefabEntry>();


    // =========================================================
    // PARENT
    // =========================================================

    [Header("Crop Parent")]

    [SerializeField]
    private Transform cropParent;


    // =========================================================
    // LIMIT
    // =========================================================

    [Header("Limits")]

    [SerializeField]
    private int maximumCrops = 5000;


    // =========================================================
    // DEFAULT SPACING
    // =========================================================

    [Header("Spacing")]

    [SerializeField]
    private float defaultSpacing = 0.8f;


    // =========================================================
    // GENERATED CROPS
    // =========================================================

    private readonly List<GameObject> generatedCrops =
        new List<GameObject>();


    // =========================================================
    // ID
    // =========================================================

    private int nextCropId = 0;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        CreateCropParent();

        RefreshCropList();

        CalculateNextCropId();
    }


    // =========================================================
    // CREATE PARENT
    // =========================================================

    private void CreateCropParent()
    {
        if (cropParent != null)
        {
            return;
        }


        GameObject parent =
            new GameObject(
                "Generated Crops"
            );


        parent.transform.SetParent(
            transform
        );


        cropParent =
            parent.transform;
    }


    // =========================================================
    // PLANT CROPS
    // =========================================================

    public int PlantCrops(
        int count,
        string cropType,
        float spacing
    )
    {
        if (soilManager == null)
        {
            Debug.LogError(
                "[CropManager] SoilManager is not assigned."
            );

            return 0;
        }


        SoilArea soil =
            soilManager.GetFirstSoil();


        if (soil == null)
        {
            Debug.LogWarning(
                "[CropManager] No SoilArea exists. " +
                "Create soil first."
            );

            return 0;
        }


        return PlantCropsInSoil(
            soil,
            count,
            cropType,
            spacing
        );
    }


    // =========================================================
    // PLANT IN SOIL
    // =========================================================

    public int PlantCropsInSoil(
        SoilArea soil,
        int count,
        string cropType,
        float spacing
    )
    {
        if (soil == null)
        {
            Debug.LogWarning(
                "[CropManager] SoilArea is null."
            );

            return 0;
        }


        CropType definition =
            GetCropType(
                cropType
            );


        GameObject prefab =
            GetCropPrefab(
                cropType
            );


        if (definition == null)
        {
            Debug.LogError(
                "[CropManager] Crop type not found: " +
                cropType
            );

            return 0;
        }


        if (prefab == null)
        {
            Debug.LogError(
                "[CropManager] No prefab configured for crop: " +
                cropType
            );

            return 0;
        }


        count =
            Mathf.Clamp(
                count,
                1,
                maximumCrops
            );


        spacing =
            spacing <= 0f
            ? defaultSpacing
            : Mathf.Max(
                spacing,
                0.1f
            );


        RefreshCropList();


        int available =
            maximumCrops -
            generatedCrops.Count;


        if (available <= 0)
        {
            Debug.LogWarning(
                "[CropManager] Maximum crop count reached."
            );

            return 0;
        }


        count =
            Mathf.Min(
                count,
                available
            );


        int planted = 0;

        int attempts = 0;

        int maxAttempts =
            Mathf.Max(
                count * 30,
                30
            );


        while (
            planted < count &&
            attempts < maxAttempts
        )
        {
            attempts++;


            Vector3 position =
                soil.GetRandomPosition(
                    spacing
                );


            if (
                IsTooCloseToExistingCrop(
                    position,
                    spacing
                )
            )
            {
                continue;
            }


            // -------------------------------------------------
            // Make sure the crop sits on the terrain surface.
            // -------------------------------------------------

            position =
                soil.GetTerrainSurfacePosition(
                    position
                );


            GameObject crop =
                CreateCropInstance(
                    prefab,
                    definition,
                    cropType,
                    position,
                    soil
                );


            if (crop == null)
            {
                continue;
            }


            planted++;
        }


        Debug.Log(
            "[CropManager] Planted " +
            planted +
            " " +
            cropType +
            " crops in Soil_" +
            soil.GetSoilId()
        );


        return planted;
    }


    // =========================================================
    // PLANT AT WORLD POSITION
    // =========================================================

    public GameObject PlantCropAtWorldPosition(
        string cropType,
        Vector3 worldPosition
    )
    {
        if (soilManager == null)
        {
            return null;
        }


        SoilArea soil =
            soilManager.GetSoilAtWorldPosition(
                worldPosition
            );


        if (soil == null)
        {
            Debug.LogWarning(
                "[CropManager] Position is not inside soil."
            );

            return null;
        }


        CropType definition =
            GetCropType(
                cropType
            );


        GameObject prefab =
            GetCropPrefab(
                cropType
            );


        if (
            definition == null ||
            prefab == null
        )
        {
            Debug.LogWarning(
                "[CropManager] Crop type not configured: " +
                cropType
            );

            return null;
        }


        Vector3 finalPosition =
            soil.GetTerrainSurfacePosition(
                worldPosition
            );


        return CreateCropInstance(
            prefab,
            definition,
            cropType,
            finalPosition,
            soil
        );
    }


    // =========================================================
    // CREATE CROP INSTANCE
    // =========================================================

    private GameObject CreateCropInstance(
        GameObject prefab,
        CropType definition,
        string cropType,
        Vector3 position,
        SoilArea soil
    )
    {
        if (prefab == null)
        {
            return null;
        }


        GameObject crop =
            Instantiate(
                prefab,
                position,
                Quaternion.identity,
                cropParent
            );


        if (crop == null)
        {
            return null;
        }


        int id =
            nextCropId;


        nextCropId++;


        crop.name =
            "Crop_" +
            id +
            "_" +
            cropType;


        // -----------------------------------------------------
        // CropInstance
        // -----------------------------------------------------

        CropInstance instance =
            crop.GetComponent<CropInstance>();


        if (instance == null)
        {
            instance =
                crop.AddComponent<CropInstance>();
        }


        instance.Initialize(
            id,
            cropType,
            definition,
            this
        );


        // -----------------------------------------------------
        // Crop information
        // -----------------------------------------------------

        CropTypeInfo info =
            crop.GetComponent<CropTypeInfo>();


        if (info == null)
        {
            info =
                crop.AddComponent<CropTypeInfo>();
        }


        info.SetCropInfo(
            id,
            cropType,
            soil != null
                ? soil.GetSoilId()
                : -1
        );


        // -----------------------------------------------------
        // Random rotation
        // -----------------------------------------------------

        crop.transform.rotation =
            Quaternion.Euler(
                0f,
                UnityEngine.Random.Range(
                    0f,
                    360f
                ),
                0f
            );


        generatedCrops.Add(
            crop
        );


        return crop;
    }


    // =========================================================
    // GET CROP TYPE
    // =========================================================

    public CropType GetCropType(
        string cropType
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                cropType
            )
        )
        {
            return GetDefaultCropType();
        }


        if (
            cropType.Equals(
                "Default",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return GetDefaultCropType();
        }


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


            if (
                type.cropName.Trim().Equals(
                    cropType.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return type;
            }
        }


        // -----------------------------------------------------
        // If only the simple prefab list is configured,
        // automatically create a basic definition.
        // -----------------------------------------------------

        GameObject simplePrefab =
            GetSimpleCropPrefab(
                cropType
            );


        if (simplePrefab != null)
        {
            CropType generated =
                new CropType();


            generated.cropName =
                cropType;


            generated.growthStages =
                1;


            generated.canBecomeTree =
                false;


            generated.growthStagePrefabs =
                new GameObject[]
                {
                    simplePrefab
                };


            return generated;
        }


        return null;
    }


    // =========================================================
    // DEFAULT DEFINITION
    // =========================================================

    private CropType GetDefaultCropType()
    {
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
                type.cropName.Equals(
                    "Default",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return type;
            }
        }


        if (cropPrefab != null)
        {
            CropType generated =
                new CropType();


            generated.cropName =
                "Default";


            generated.growthStages =
                1;


            generated.growthStagePrefabs =
                new GameObject[]
                {
                    cropPrefab
                };


            return generated;
        }


        return null;
    }


    // =========================================================
    // GET PREFAB
    // =========================================================

    public GameObject GetCropPrefab(
        string cropType
    )
    {
        CropType definition =
            GetConfiguredCropType(
                cropType
            );


        if (
            definition != null &&
            definition.growthStagePrefabs != null &&
            definition.growthStagePrefabs.Length > 0
        )
        {
            if (
                definition.growthStagePrefabs[0] != null
            )
            {
                return definition.growthStagePrefabs[0];
            }
        }


        return GetSimpleCropPrefab(
            cropType
        );
    }


    // =========================================================
    // GET CONFIGURED TYPE
    // =========================================================

    private CropType GetConfiguredCropType(
        string cropType
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                cropType
            )
        )
        {
            return null;
        }


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


            if (
                type.cropName.Trim().Equals(
                    cropType.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return type;
            }
        }


        return null;
    }


    // =========================================================
    // SIMPLE PREFAB
    // =========================================================

    private GameObject GetSimpleCropPrefab(
        string cropType
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                cropType
            ) ||
            cropType.Equals(
                "Default",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return cropPrefab;
        }


        foreach (
            CropPrefabEntry entry
            in cropPrefabs
        )
        {
            if (entry == null)
            {
                continue;
            }


            if (entry.prefab == null)
            {
                continue;
            }


            if (
                string.IsNullOrWhiteSpace(
                    entry.cropType
                )
            )
            {
                continue;
            }


            if (
                entry.cropType.Trim().Equals(
                    cropType.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return entry.prefab;
            }
        }


        return null;
    }


    // =========================================================
    // CROP COUNT
    // =========================================================

    public int GetCropCount()
    {
        RefreshCropList();

        return generatedCrops.Count;
    }


    // =========================================================
    // REMOVE CROP
    // =========================================================

    public bool RemoveCrop(
        int cropId
    )
    {
        GameObject crop =
            GetCropById(
                cropId
            );


        if (crop == null)
        {
            return false;
        }


        generatedCrops.Remove(
            crop
        );


        Destroy(
            crop
        );


        return true;
    }


    // =========================================================
    // REMOVE ALL
    // =========================================================

    public void RemoveAllCrops()
    {
        foreach (
            GameObject crop
            in generatedCrops
        )
        {
            if (crop != null)
            {
                Destroy(crop);
            }
        }


        generatedCrops.Clear();


        Debug.Log(
            "[CropManager] Removed all crops."
        );
    }


    // =========================================================
    // GET BY ID
    // =========================================================

    private GameObject GetCropById(
        int cropId
    )
    {
        RefreshCropList();


        foreach (
            GameObject crop
            in generatedCrops
        )
        {
            if (crop == null)
            {
                continue;
            }


            CropInstance instance =
                crop.GetComponent<CropInstance>();


            if (instance == null)
            {
                continue;
            }


            if (
                instance.cropId ==
                cropId
            )
            {
                return crop;
            }
        }


        return null;
    }


    // =========================================================
    // SPACING
    // =========================================================

    private bool IsTooCloseToExistingCrop(
        Vector3 position,
        float spacing
    )
    {
        float distanceSquared =
            spacing * spacing;


        foreach (
            GameObject crop
            in generatedCrops
        )
        {
            if (crop == null)
            {
                continue;
            }


            Vector3 difference =
                crop.transform.position -
                position;


            difference.y = 0f;


            if (
                difference.sqrMagnitude <
                distanceSquared
            )
            {
                return true;
            }
        }


        return false;
    }


    // =========================================================
    // REFRESH
    // =========================================================

    private void RefreshCropList()
    {
        generatedCrops.Clear();


        if (cropParent == null)
        {
            return;
        }


        for (
            int i = 0;
            i < cropParent.childCount;
            i++
        )
        {
            Transform child =
                cropParent.GetChild(i);


            if (child != null)
            {
                generatedCrops.Add(
                    child.gameObject
                );
            }
        }
    }


    // =========================================================
    // NEXT ID
    // =========================================================

    private void CalculateNextCropId()
    {
        int highest = -1;


        foreach (
            GameObject crop
            in generatedCrops
        )
        {
            if (crop == null)
            {
                continue;
            }


            CropInstance instance =
                crop.GetComponent<CropInstance>();


            if (instance == null)
            {
                continue;
            }


            highest =
                Mathf.Max(
                    highest,
                    instance.cropId
                );
        }


        nextCropId =
            highest + 1;
    }
}


// ============================================================
// CROP TYPE INFO
// ============================================================

public class CropTypeInfo : MonoBehaviour
{
    [SerializeField]
    private int cropId = -1;


    [SerializeField]
    private string cropType = "Unknown";


    [SerializeField]
    private int soilId = -1;


    public void SetCropInfo(
        int id,
        string type,
        int soil
    )
    {
        cropId = id;


        cropType =
            string.IsNullOrWhiteSpace(type)
            ? "Unknown"
            : type;


        soilId =
            soil;
    }


    public int GetCropId()
    {
        return cropId;
    }


    public string GetCropType()
    {
        return cropType;
    }


    public int GetSoilId()
    {
        return soilId;
    }
}