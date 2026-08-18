using System.Collections.Generic;
using UnityEngine;

// ============================================================
// SOIL MANAGER
// ============================================================
//
// Responsibilities:
//
// - Detect generated Terrain
// - Wait if Terrain has not been generated yet
// - Remember generated Terrain
// - Create soil ONLY when requested
// - Remove soil
// - Remove all soil
// - Provide soil areas to CropManager
//
// IMPORTANT:
//
// Terrain does NOT need to exist when SoilManager starts.
//
// SoilManager automatically detects the terrain later.
//
// Example:
//
// Unity starts
//      ↓
// No Terrain yet
//      ↓
// TerrainGenerator generates Terrain
//      ↓
// SoilManager detects it
//      ↓
// User says "create soil"
//      ↓
// SoilArea is created
//
// ============================================================

public class SoilManager : MonoBehaviour
{
    // =========================================================
    // TERRAIN GENERATOR
    // =========================================================

    [Header("Terrain")]

    [SerializeField]
    private TerrainGenerator terrainGenerator;


    [SerializeField]
    private Terrain terrain;


    // =========================================================
    // SOIL PARENT
    // =========================================================

    [Header("Soil Parent")]

    [SerializeField]
    private Transform soilParent;


    // =========================================================
    // SOIL MATERIAL
    // =========================================================

    [Header("Soil Appearance")]

    [SerializeField]
    private Material soilMaterial;


    // =========================================================
    // DEFAULT SOIL SIZE
    // =========================================================

    [Header("Default Soil Size")]

    [SerializeField]
    private float defaultWidth = 10f;


    [SerializeField]
    private float defaultDepth = 10f;


    // =========================================================
    // SOIL LIST
    // =========================================================

    private readonly List<SoilArea> soilAreas =
        new List<SoilArea>();


    // =========================================================
    // NEXT ID
    // =========================================================

    private int nextSoilId = 0;


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        DetectGeneratedTerrain();
    }


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        CreateSoilParent();

        RefreshSoilList();

        CalculateNextSoilId();
    }


    // =========================================================
    // CREATE PARENT
    // =========================================================

    private void CreateSoilParent()
    {
        if (soilParent != null)
        {
            return;
        }


        GameObject parent =
            new GameObject(
                "Generated Soil"
            );


        parent.transform.SetParent(
            transform
        );


        soilParent =
            parent.transform;
    }


    // =========================================================
    // DETECT GENERATED TERRAIN
    // =========================================================

    private void DetectGeneratedTerrain()
    {
        // -----------------------------------------------------
        // Already have valid terrain
        // -----------------------------------------------------

        if (
            terrain != null &&
            terrain.terrainData != null
        )
        {
            return;
        }


        // -----------------------------------------------------
        // Ask TerrainGenerator
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


                Debug.Log(
                    "[SoilManager] Generated terrain detected: " +
                    terrain.name
                );


                return;
            }
        }


        // -----------------------------------------------------
        // Active Terrain fallback
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


            Debug.Log(
                "[SoilManager] Active terrain detected: " +
                terrain.name
            );


            return;
        }
    }


    // =========================================================
    // GET TERRAIN
    // =========================================================

    public Terrain GetTerrain()
    {
        DetectGeneratedTerrain();

        return terrain;
    }


    // =========================================================
    // CREATE SOIL AREA
    // =========================================================

    public SoilArea CreateSoilArea(
        Vector3 center,
        float width,
        float depth
    )
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogWarning(
                "[SoilManager] Cannot create soil yet. " +
                "Generated terrain has not been detected."
            );

            return null;
        }


        width =
            Mathf.Max(
                width,
                1f
            );


        depth =
            Mathf.Max(
                depth,
                1f
            );


        // -----------------------------------------------------
        // Clamp center to terrain
        // -----------------------------------------------------

        Vector3 terrainOrigin =
            currentTerrain.transform.position;


        TerrainData data =
            currentTerrain.terrainData;


        float localX =
            center.x -
            terrainOrigin.x;


        float localZ =
            center.z -
            terrainOrigin.z;


        float halfWidth =
            width * 0.5f;


        float halfDepth =
            depth * 0.5f;


        if (
            data.size.x <
            width
        )
        {
            width =
                data.size.x;

            halfWidth =
                width * 0.5f;
        }


        if (
            data.size.z <
            depth
        )
        {
            depth =
                data.size.z;

            halfDepth =
                depth * 0.5f;
        }


        localX =
            Mathf.Clamp(
                localX,
                halfWidth,
                data.size.x - halfWidth
            );


        localZ =
            Mathf.Clamp(
                localZ,
                halfDepth,
                data.size.z - halfDepth
            );


        float worldX =
            terrainOrigin.x +
            localX;


        float worldZ =
            terrainOrigin.z +
            localZ;


        float terrainY =
            currentTerrain.SampleHeight(
                new Vector3(
                    worldX,
                    terrainOrigin.y,
                    worldZ
                )
            );


        Vector3 finalCenter =
            new Vector3(
                worldX,
                terrainOrigin.y +
                terrainY,
                worldZ
            );


        // -----------------------------------------------------
        // CREATE OBJECT
        // -----------------------------------------------------

        GameObject soilObject =
            new GameObject(
                "Soil_" +
                nextSoilId
            );


        soilObject.transform.SetParent(
            soilParent
        );


        soilObject.transform.position =
            finalCenter;


        // -----------------------------------------------------
        // CREATE COMPONENT
        // -----------------------------------------------------

        SoilArea soil =
            soilObject.AddComponent<SoilArea>();


        soil.Initialize(
            nextSoilId,
            currentTerrain,
            finalCenter,
            width,
            depth,
            soilMaterial
        );


        soilAreas.Add(
            soil
        );


        Debug.Log(
            "[SoilManager] Created Soil_" +
            nextSoilId +
            " on generated terrain."
        );


        nextSoilId++;


        return soil;
    }


    // =========================================================
    // CREATE DEFAULT SOIL
    // =========================================================

    public SoilArea CreateDefaultSoil()
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            return null;
        }


        Vector3 center =
            currentTerrain.transform.position +
            new Vector3(
                currentTerrain.terrainData.size.x * 0.5f,
                0f,
                currentTerrain.terrainData.size.z * 0.5f
            );


        return CreateSoilArea(
            center,
            defaultWidth,
            defaultDepth
        );
    }


    // =========================================================
    // GET SOIL COUNT
    // =========================================================

    public int GetSoilCount()
    {
        RefreshSoilList();

        return soilAreas.Count;
    }


    // =========================================================
    // GET SOIL
    // =========================================================

    public SoilArea GetSoil(
        int soilId
    )
    {
        RefreshSoilList();


        foreach (
            SoilArea soil
            in soilAreas
        )
        {
            if (soil == null)
            {
                continue;
            }


            if (
                soil.GetSoilId() ==
                soilId
            )
            {
                return soil;
            }
        }


        return null;
    }


    // =========================================================
    // GET FIRST SOIL
    // =========================================================

    public SoilArea GetFirstSoil()
    {
        RefreshSoilList();


        if (soilAreas.Count == 0)
        {
            return null;
        }


        return soilAreas[0];
    }


    // =========================================================
    // GET SOIL AREAS
    // =========================================================

    public List<SoilArea> GetSoilAreas()
    {
        RefreshSoilList();

        return soilAreas;
    }


    // =========================================================
    // FIND SOIL AT POSITION
    // =========================================================

    public SoilArea GetSoilAtWorldPosition(
        Vector3 position
    )
    {
        RefreshSoilList();


        foreach (
            SoilArea soil
            in soilAreas
        )
        {
            if (soil == null)
            {
                continue;
            }


            if (
                soil.ContainsWorldPosition(
                    position
                )
            )
            {
                return soil;
            }
        }


        return null;
    }


    // =========================================================
    // REMOVE SOIL
    // =========================================================

    public bool RemoveSoil(
        int soilId
    )
    {
        SoilArea soil =
            GetSoil(
                soilId
            );


        if (soil == null)
        {
            Debug.LogWarning(
                "[SoilManager] Soil not found: " +
                soilId
            );

            return false;
        }


        soilAreas.Remove(
            soil
        );


        Destroy(
            soil.gameObject
        );


        Debug.Log(
            "[SoilManager] Removed Soil_" +
            soilId
        );


        return true;
    }


    // =========================================================
    // REMOVE ALL
    // =========================================================

    public void RemoveAllSoil()
    {
        RefreshSoilList();


        foreach (
            SoilArea soil
            in soilAreas
        )
        {
            if (soil != null)
            {
                Destroy(
                    soil.gameObject
                );
            }
        }


        soilAreas.Clear();


        Debug.Log(
            "[SoilManager] Removed all soil."
        );
    }


    // =========================================================
    // REFRESH
    // =========================================================

    private void RefreshSoilList()
    {
        soilAreas.Clear();


        if (soilParent == null)
        {
            return;
        }


        for (
            int i = 0;
            i < soilParent.childCount;
            i++
        )
        {
            Transform child =
                soilParent.GetChild(i);


            if (child == null)
            {
                continue;
            }


            SoilArea soil =
                child.GetComponent<SoilArea>();


            if (soil != null)
            {
                soilAreas.Add(
                    soil
                );
            }
        }
    }


    // =========================================================
    // CALCULATE NEXT ID
    // =========================================================

    private void CalculateNextSoilId()
    {
        int highest =
            -1;


        foreach (
            SoilArea soil
            in soilAreas
        )
        {
            if (soil == null)
            {
                continue;
            }


            highest =
                Mathf.Max(
                    highest,
                    soil.GetSoilId()
                );
        }


        nextSoilId =
            highest + 1;
    }
}