
using UnityEngine;
using System.Collections.Generic;



public class BuildingGenerator : MonoBehaviour
{
    // =========================================================
    // EXISTING TERRAIN
    // =========================================================

    [Header("Existing Terrain")]

    [Tooltip(
        "Drag your existing Unity Terrain here."
    )]
    [SerializeField]
    private Terrain targetTerrain;


    // =========================================================
    // BUILDING PREFABS
    // =========================================================

    [Header("Building Prefabs")]

    [Tooltip(
        "Prefab used when creating houses."
    )]
    [SerializeField]
    private GameObject housePrefab;


    [Tooltip(
        "Prefab used when creating temples."
    )]
    [SerializeField]
    private GameObject templePrefab;


    // =========================================================
    // BUILDING PLACEMENT
    // =========================================================

    [Header("Building Placement")]

    [Tooltip(
        "Minimum empty distance between buildings."
    )]
    [SerializeField]
    private float minimumBuildingDistance = 12f;


    [Tooltip(
        "Maximum number of attempts to find a valid position."
    )]
    [SerializeField]
    private int maximumPlacementAttempts = 100;


    [Tooltip(
        "Maximum allowed terrain slope in degrees."
    )]
    [SerializeField]
    private float maximumSlope = 15f;


    [Tooltip(
        "Extra distance from the terrain edge."
    )]
    [SerializeField]
    private float terrainEdgeMargin = 5f;


    [Tooltip(
        "Vertical offset applied after placing the building " +
        "on the terrain. Use this if the prefab appears " +
        "slightly underground or floating."
    )]
    [SerializeField]
    private float buildingHeightOffset = 0f;


    // =========================================================
    // HOUSE SETTINGS
    // =========================================================

    [Header("House Settings")]

    [Tooltip(
        "Approximate radius of a house for overlap checking."
    )]
    [SerializeField]
    private float houseRadius = 5f;


    [Tooltip(
        "Minimum random house rotation."
    )]
    [SerializeField]
    private float houseMinimumRotation = 0f;


    [Tooltip(
        "Maximum random house rotation."
    )]
    [SerializeField]
    private float houseMaximumRotation = 360f;


    // =========================================================
    // TEMPLE SETTINGS
    // =========================================================

    [Header("Temple Settings")]

    [Tooltip(
        "Approximate radius of a temple for overlap checking."
    )]
    [SerializeField]
    private float templeRadius = 8f;


    [Tooltip(
        "Minimum random temple rotation."
    )]
    [SerializeField]
    private float templeMinimumRotation = 0f;


    [Tooltip(
        "Maximum random temple rotation."
    )]
    [SerializeField]
    private float templeMaximumRotation = 360f;


    // =========================================================
    // GENERATED BUILDINGS
    // =========================================================

    [Header("Generated Buildings")]

    [Tooltip(
        "Parent object for all generated buildings."
    )]
    [SerializeField]
    private Transform generatedBuildingsParent;


    // =========================================================
    // INTERNAL BUILDING LIST
    // =========================================================

    private readonly List<GeneratedBuilding> generatedBuildings =
        new List<GeneratedBuilding>();


    // =========================================================
    // GENERATED BUILDING DATA
    // =========================================================

    private class GeneratedBuilding
    {
        public GameObject gameObject;

        public Vector3 position;

        public float radius;

        public string buildingType;
    }


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public Terrain GetTargetTerrain()
    {
        return targetTerrain;
    }


    public int GetBuildingCount()
    {
        CleanupNullBuildings();

        return generatedBuildings.Count;
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        CreateBuildingParent();
    }


    // =========================================================
    // CREATE BUILDING PARENT
    // =========================================================

    private void CreateBuildingParent()
    {
        if (generatedBuildingsParent != null)
        {
            return;
        }

        GameObject parentObject =
            new GameObject(
                "Generated Buildings"
            );

        generatedBuildingsParent =
            parentObject.transform;

        generatedBuildingsParent.SetParent(
            transform
        );

        // Keep the parent transform neutral.
        generatedBuildingsParent.localPosition =
            Vector3.zero;

        generatedBuildingsParent.localRotation =
            Quaternion.identity;

        generatedBuildingsParent.localScale =
            Vector3.one;
    }


    // =========================================================
    // CHECK TERRAIN
    // =========================================================

    private bool HasValidTerrain()
    {
        if (targetTerrain == null)
        {
            Debug.LogWarning(
                "BuildingGenerator: No Terrain assigned.\n" +
                "Please drag your Terrain into Target Terrain."
            );

            return false;
        }

        if (targetTerrain.terrainData == null)
        {
            Debug.LogWarning(
                "BuildingGenerator: Target Terrain has no TerrainData."
            );

            return false;
        }

        return true;
    }


    // =========================================================
    // CHECK BUILDING PREFAB
    // =========================================================

    private bool HasValidPrefab(
        GameObject prefab,
        string buildingType
    )
    {
        if (prefab == null)
        {
            Debug.LogWarning(
                "BuildingGenerator: " +
                buildingType +
                " Prefab is not assigned."
            );

            return false;
        }

        return true;
    }


    // =========================================================
    // CREATE RANDOM HOUSE
    // =========================================================

    public void CreateRandomHouse()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        if (!HasValidPrefab(
            housePrefab,
            "House"
        ))
        {
            return;
        }

        Vector3 position;

        bool foundPosition =
            FindValidBuildingPosition(
                houseRadius,
                out position
            );

        if (!foundPosition)
        {
            Debug.LogWarning(
                "BuildingGenerator: Could not find a valid " +
                "location for the house."
            );

            return;
        }

        float rotation =
            Random.Range(
                houseMinimumRotation,
                houseMaximumRotation
            );

        GameObject house =
            Instantiate(
                housePrefab,
                position,
                Quaternion.Euler(
                    0f,
                    rotation,
                    0f
                ),
                generatedBuildingsParent
            );

        house.name =
            "House_" +
            (generatedBuildings.Count + 1);

        RegisterBuilding(
            house,
            position,
            houseRadius,
            "House"
        );

        Debug.Log(
            "=================================================\n" +
            "HOUSE CREATED\n" +
            "=================================================\n" +
            $"Position X: {position.x:F1}\n" +
            $"Position Y: {position.y:F1}\n" +
            $"Position Z: {position.z:F1}\n" +
            $"Radius: {houseRadius:F1}\n" +
            $"Slope: {GetTerrainSlopeAtWorldPosition(position):F1}°\n" +
            $"Building ID: {generatedBuildings.Count}\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "================================================="
        );
    }


    // =========================================================
    // CREATE RANDOM TEMPLE
    // =========================================================

    public void CreateRandomTemple()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        if (!HasValidPrefab(
            templePrefab,
            "Temple"
        ))
        {
            return;
        }

        Vector3 position;

        bool foundPosition =
            FindValidBuildingPosition(
                templeRadius,
                out position
            );

        if (!foundPosition)
        {
            Debug.LogWarning(
                "BuildingGenerator: Could not find a valid " +
                "location for the temple."
            );

            return;
        }

        float rotation =
            Random.Range(
                templeMinimumRotation,
                templeMaximumRotation
            );

        GameObject temple =
            Instantiate(
                templePrefab,
                position,
                Quaternion.Euler(
                    0f,
                    rotation,
                    0f
                ),
                generatedBuildingsParent
            );

        temple.name =
            "Temple_" +
            (generatedBuildings.Count + 1);

        RegisterBuilding(
            temple,
            position,
            templeRadius,
            "Temple"
        );

        Debug.Log(
            "=================================================\n" +
            "TEMPLE CREATED\n" +
            "=================================================\n" +
            $"Position X: {position.x:F1}\n" +
            $"Position Y: {position.y:F1}\n" +
            $"Position Z: {position.z:F1}\n" +
            $"Radius: {templeRadius:F1}\n" +
            $"Slope: {GetTerrainSlopeAtWorldPosition(position):F1}°\n" +
            $"Building ID: {generatedBuildings.Count}\n" +
            $"Terrain: {targetTerrain.name}\n" +
            "================================================="
        );
    }


    // =========================================================
    // CREATE RANDOM BUILDING
    // =========================================================
    //
    // 70% chance = House
    // 30% chance = Temple
    //
    // =========================================================

    public void CreateRandomBuilding()
    {
        if (!HasValidTerrain())
        {
            return;
        }

        if (Random.value > 0.7f)
        {
            CreateRandomTemple();
        }
        else
        {
            CreateRandomHouse();
        }
    }


    // =========================================================
    // CREATE MULTIPLE RANDOM BUILDINGS
    // =========================================================

    public void CreateRandomBuildings(
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
                100
            );

        Debug.Log(
            "=================================================\n" +
            "CREATING RANDOM BUILDINGS\n" +
            "=================================================\n" +
            $"Requested: {count}\n" +
            "================================================="
        );

        int buildingsCreated = 0;

        for (
            int i = 0;
            i < count;
            i++
        )
        {
            int beforeCount =
                generatedBuildings.Count;

            CreateRandomBuilding();

            if (
                generatedBuildings.Count >
                beforeCount
            )
            {
                buildingsCreated++;
            }
        }

        Debug.Log(
            "=================================================\n" +
            "BUILDING GENERATION COMPLETE\n" +
            "=================================================\n" +
            $"Requested: {count}\n" +
            $"Created: {buildingsCreated}\n" +
            $"Total Buildings: {generatedBuildings.Count}\n" +
            "================================================="
        );
    }


    // =========================================================
    // FIND VALID BUILDING POSITION
    // =========================================================

    private bool FindValidBuildingPosition(
        float buildingRadius,
        out Vector3 validPosition
    )
    {
        validPosition =
            Vector3.zero;

        if (!HasValidTerrain())
        {
            return false;
        }

        TerrainData terrainData =
            targetTerrain.terrainData;

        float terrainWidth =
            terrainData.size.x;

        float terrainLength =
            terrainData.size.z;


        // -----------------------------------------------------
        // Safe area inside terrain.
        // -----------------------------------------------------

        float minimumX =
            terrainEdgeMargin +
            buildingRadius;

        float maximumX =
            terrainWidth -
            terrainEdgeMargin -
            buildingRadius;

        float minimumZ =
            terrainEdgeMargin +
            buildingRadius;

        float maximumZ =
            terrainLength -
            terrainEdgeMargin -
            buildingRadius;


        // -----------------------------------------------------
        // Check terrain size.
        // -----------------------------------------------------

        if (
            maximumX <= minimumX ||
            maximumZ <= minimumZ
        )
        {
            Debug.LogWarning(
                "BuildingGenerator: Terrain is too small " +
                "for this building."
            );

            return false;
        }


        // =====================================================
        // RANDOM POSITION ATTEMPTS
        // =====================================================

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
            // -------------------------------------------------
            // Random terrain-local position.
            // -------------------------------------------------

            float localX =
                Random.Range(
                    minimumX,
                    maximumX
                );

            float localZ =
                Random.Range(
                    minimumZ,
                    maximumZ
                );


            // -------------------------------------------------
            // Check slope BEFORE converting final position.
            // -------------------------------------------------

            float slopeAngle =
                GetTerrainSlope(
                    localX,
                    localZ
                );

            if (
                slopeAngle >
                maximumSlope
            )
            {
                continue;
            }


            // -------------------------------------------------
            // Convert local terrain coordinates to world.
            // -------------------------------------------------

            Vector3 worldPosition =
                TerrainLocalToWorld(
                    localX,
                    localZ
                );


            // -------------------------------------------------
            // Check overlap.
            // -------------------------------------------------

            if (
                !IsBuildingPositionAvailable(
                    worldPosition,
                    buildingRadius
                )
            )
            {
                continue;
            }


            // -------------------------------------------------
            // Valid position found.
            // -------------------------------------------------

            validPosition =
                worldPosition;

            return true;
        }

        return false;
    }


    // =========================================================
    // GET TERRAIN SLOPE
    // =========================================================
    //
    // IMPORTANT:
    // We use TerrainData.GetInterpolatedNormal().
    //
    // We DO NOT use:
    //
    // Terrain.SampleNormal()
    //
    // because Unity Terrain does not provide SampleNormal().
    //
    // =========================================================

    private float GetTerrainSlope(
        float localX,
        float localZ
    )
    {
        if (!HasValidTerrain())
        {
            return 90f;
        }

        TerrainData terrainData =
            targetTerrain.terrainData;


        // -----------------------------------------------------
        // Convert local position to normalized terrain coords.
        // -----------------------------------------------------

        float normalizedX =
            localX /
            terrainData.size.x;

        float normalizedZ =
            localZ /
            terrainData.size.z;


        normalizedX =
            Mathf.Clamp01(
                normalizedX
            );

        normalizedZ =
            Mathf.Clamp01(
                normalizedZ
            );


        // -----------------------------------------------------
        // Get terrain surface normal.
        // -----------------------------------------------------

        Vector3 terrainNormal =
            terrainData.GetInterpolatedNormal(
                normalizedX,
                normalizedZ
            );


        // -----------------------------------------------------
        // Calculate slope.
        // -----------------------------------------------------

        float slopeAngle =
            Vector3.Angle(
                terrainNormal,
                Vector3.up
            );

        return slopeAngle;
    }


    // =========================================================
    // GET SLOPE FROM WORLD POSITION
    // =========================================================

    private float GetTerrainSlopeAtWorldPosition(
        Vector3 worldPosition
    )
    {
        if (!HasValidTerrain())
        {
            return 90f;
        }

        Vector3 terrainPosition =
            targetTerrain.transform.position;

        float localX =
            worldPosition.x -
            terrainPosition.x;

        float localZ =
            worldPosition.z -
            terrainPosition.z;

        return GetTerrainSlope(
            localX,
            localZ
        );
    }


    // =========================================================
    // CHECK BUILDING OVERLAP
    // =========================================================

    private bool IsBuildingPositionAvailable(
        Vector3 position,
        float radius
    )
    {
        CleanupNullBuildings();

        Vector2 currentPosition =
            new Vector2(
                position.x,
                position.z
            );


        for (
            int i = 0;
            i < generatedBuildings.Count;
            i++
        )
        {
            GeneratedBuilding building =
                generatedBuildings[i];

            if (building == null)
            {
                continue;
            }


            float requiredDistance =
                radius +
                building.radius +
                minimumBuildingDistance;


            Vector2 existingPosition =
                new Vector2(
                    building.position.x,
                    building.position.z
                );


            float distance =
                Vector2.Distance(
                    currentPosition,
                    existingPosition
                );


            if (
                distance <
                requiredDistance
            )
            {
                return false;
            }
        }

        return true;
    }


    // =========================================================
    // REGISTER BUILDING
    // =========================================================

    private void RegisterBuilding(
        GameObject buildingObject,
        Vector3 position,
        float radius,
        string buildingType
    )
    {
        GeneratedBuilding building =
            new GeneratedBuilding();

        building.gameObject =
            buildingObject;

        building.position =
            position;

        building.radius =
            radius;

        building.buildingType =
            buildingType;

        generatedBuildings.Add(
            building
        );
    }


    // =========================================================
    // TERRAIN LOCAL → WORLD
    // =========================================================
    //
    // localX/localZ:
    //     Position relative to the Terrain.
    //
    // worldPosition:
    //     Correct world-space position including the terrain's
    //     transform position and terrain height.
    //
    // =========================================================

    private Vector3 TerrainLocalToWorld(
        float localX,
        float localZ
    )
    {
        Vector3 terrainPosition =
            targetTerrain.transform.position;


        // -----------------------------------------------------
        // Horizontal world position.
        // -----------------------------------------------------

        Vector3 worldPosition =
            terrainPosition +
            new Vector3(
                localX,
                0f,
                localZ
            );


        // -----------------------------------------------------
        // Terrain height.
        //
        // SampleHeight returns height relative to the terrain
        // object's world Y position, so we add terrainPosition.y.
        // -----------------------------------------------------

        float terrainHeight =
            targetTerrain.SampleHeight(
                worldPosition
            );


        worldPosition.y =
            terrainPosition.y +
            terrainHeight +
            buildingHeightOffset;


        return worldPosition;
    }


    // =========================================================
    // CLEAR BUILDINGS
    // =========================================================

    public void ClearBuildings()
    {
        // -----------------------------------------------------
        // Delete generated GameObjects.
        // -----------------------------------------------------

        if (
            generatedBuildingsParent != null
        )
        {
            for (
                int i =
                    generatedBuildingsParent.childCount - 1;

                i >= 0;

                i--
            )
            {
                GameObject child =
                    generatedBuildingsParent
                        .GetChild(i)
                        .gameObject;

                if (Application.isPlaying)
                {
                    Destroy(
                        child
                    );
                }
                else
                {
                    DestroyImmediate(
                        child
                    );
                }
            }
        }


        // -----------------------------------------------------
        // Clear internal list.
        // -----------------------------------------------------

        generatedBuildings.Clear();


        Debug.Log(
            "=================================================\n" +
            "BUILDINGS CLEARED\n" +
            "=================================================\n" +
            "All generated buildings have been removed.\n" +
            "================================================="
        );
    }


    // =========================================================
    // CLEAN NULL BUILDINGS
    // =========================================================

    private void CleanupNullBuildings()
    {
        for (
            int i =
                generatedBuildings.Count - 1;

            i >= 0;

            i--
        )
        {
            GeneratedBuilding building =
                generatedBuildings[i];

            if (
                building == null ||
                building.gameObject == null
            )
            {
                generatedBuildings.RemoveAt(
                    i
                );
            }
        }
    }


    // =========================================================
    // KEYBOARD TEST
    // =========================================================

    private void Update()
    {
        // =====================================================
        // H = HOUSE
        // =====================================================

        if (
            Input.GetKeyDown(
                KeyCode.H
            )
        )
        {
            Debug.Log(
                "TEST: Creating random house..."
            );

            CreateRandomHouse();
        }


        // =====================================================
        // T = TEMPLE
        // =====================================================

        if (
            Input.GetKeyDown(
                KeyCode.T
            )
        )
        {
            Debug.Log(
                "TEST: Creating random temple..."
            );

            CreateRandomTemple();
        }


        // =====================================================
        // B = RANDOM BUILDING
        // =====================================================

        if (
            Input.GetKeyDown(
                KeyCode.B
            )
        )
        {
            Debug.Log(
                "TEST: Creating random building..."
            );

            CreateRandomBuilding();
        }


        // =====================================================
        // N = 5 RANDOM BUILDINGS
        // =====================================================

        if (
            Input.GetKeyDown(
                KeyCode.N
            )
        )
        {
            Debug.Log(
                "TEST: Creating 5 random buildings..."
            );

            CreateRandomBuildings(
                5
            );
        }


        // =====================================================
        // C = CLEAR
        // =====================================================

        if (
            Input.GetKeyDown(
                KeyCode.C
            )
        )
        {
            Debug.Log(
                "TEST: Clearing generated buildings..."
            );

            ClearBuildings();
        }
    }
}

