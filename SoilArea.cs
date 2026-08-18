using UnityEngine;

// ============================================================
// SOIL AREA
// ============================================================
//
// Represents one farming/soil area.
//
// SoilArea:
// - Belongs to generated Terrain
// - Has a stable soil ID
// - Has width/depth
// - Creates a visible soil surface
// - Keeps itself on Terrain
// - Provides bounds for CropManager
//
// IMPORTANT:
// SoilArea does NOT generate Terrain.
// It only uses the Terrain that SoilManager detects.
//
// ============================================================

public class SoilArea : MonoBehaviour
{
    // =========================================================
    // ID
    // =========================================================

    [SerializeField]
    private int soilId = -1;


    // =========================================================
    // TERRAIN
    // =========================================================

    [SerializeField]
    private Terrain terrain;


    // =========================================================
    // SIZE
    // =========================================================

    [SerializeField]
    private float width = 10f;

    [SerializeField]
    private float depth = 10f;


    // =========================================================
    // SOIL VISUAL
    // =========================================================

    [Header("Soil Visual")]

    [SerializeField]
    private Material soilMaterial;


    [SerializeField]
    private float surfaceOffset = 0.025f;


    // =========================================================
    // INTERNAL
    // =========================================================

    private GameObject soilVisual;


    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize(
        int id,
        Terrain targetTerrain,
        Vector3 center,
        float areaWidth,
        float areaDepth,
        Material material
    )
    {
        soilId = id;

        terrain = targetTerrain;

        width = Mathf.Max(areaWidth, 1f);

        depth = Mathf.Max(areaDepth, 1f);

        soilMaterial = material;

        CreateSoilVisual(center);
    }


    // =========================================================
    // CREATE SOIL VISUAL
    // =========================================================

    private void CreateSoilVisual(
        Vector3 center
    )
    {
        if (terrain == null)
        {
            Debug.LogError(
                "[SoilArea] Cannot create soil. " +
                "Terrain is null."
            );

            return;
        }


        if (soilVisual != null)
        {
            Destroy(soilVisual);
        }


        // -----------------------------------------------------
        // Clamp center to terrain
        // -----------------------------------------------------

        TerrainData data =
            terrain.terrainData;


        Vector3 terrainOrigin =
            terrain.transform.position;


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


        float terrainHeight =
            terrain.SampleHeight(
                new Vector3(
                    worldX,
                    terrainOrigin.y,
                    worldZ
                )
            );


        float worldY =
            terrainOrigin.y +
            terrainHeight +
            surfaceOffset;


        Vector3 finalCenter =
            new Vector3(
                worldX,
                worldY,
                worldZ
            );


        // -----------------------------------------------------
        // CREATE QUAD
        // -----------------------------------------------------

        soilVisual =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );


        soilVisual.name =
            "SoilSurface_" +
            soilId;


        soilVisual.transform.SetParent(
            transform
        );


        soilVisual.transform.position =
            finalCenter;


        // -----------------------------------------------------
        // Small thickness
        // -----------------------------------------------------

        soilVisual.transform.localScale =
            new Vector3(
                width,
                0.05f,
                depth
            );


        // -----------------------------------------------------
        // Remove collider
        // -----------------------------------------------------

        Collider collider =
            soilVisual.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(collider);
        }


        // -----------------------------------------------------
        // Material
        // -----------------------------------------------------

        Renderer renderer =
            soilVisual.GetComponent<Renderer>();


        if (
            renderer != null &&
            soilMaterial != null
        )
        {
            renderer.material =
                soilMaterial;
        }


        Debug.Log(
            "[SoilArea] Created Soil_" +
            soilId +
            " | Width = " +
            width +
            " | Depth = " +
            depth +
            " | Center = " +
            finalCenter
        );
    }


    // =========================================================
    // GET ID
    // =========================================================

    public int GetSoilId()
    {
        return soilId;
    }


    // =========================================================
    // GET TERRAIN
    // =========================================================

    public Terrain GetTerrain()
    {
        return terrain;
    }


    // =========================================================
    // GET WIDTH
    // =========================================================

    public float GetWidth()
    {
        return width;
    }


    // =========================================================
    // GET DEPTH
    // =========================================================

    public float GetDepth()
    {
        return depth;
    }


    // =========================================================
    // GET CENTER
    // =========================================================

    public Vector3 GetCenter()
    {
        return transform.position;
    }


    // =========================================================
    // CONTAINS WORLD POSITION
    // =========================================================

    public bool ContainsWorldPosition(
        Vector3 worldPosition
    )
    {
        Vector3 center =
            transform.position;


        float halfWidth =
            width * 0.5f;


        float halfDepth =
            depth * 0.5f;


        return
            worldPosition.x >=
            center.x - halfWidth &&

            worldPosition.x <=
            center.x + halfWidth &&

            worldPosition.z >=
            center.z - halfDepth &&

            worldPosition.z <=
            center.z + halfDepth;
    }


    // =========================================================
    // GET RANDOM POSITION
    // =========================================================

    public Vector3 GetRandomPosition(
        float edgePadding = 0.5f
    )
    {
        if (terrain == null)
        {
            return transform.position;
        }


        float halfWidth =
            Mathf.Max(
                0.1f,
                width * 0.5f -
                edgePadding
            );


        float halfDepth =
            Mathf.Max(
                0.1f,
                depth * 0.5f -
                edgePadding
            );


        float x =
            Random.Range(
                -halfWidth,
                halfWidth
            );


        float z =
            Random.Range(
                -halfDepth,
                halfDepth
            );


        Vector3 position =
            transform.position +
            new Vector3(
                x,
                0f,
                z
            );


        return GetTerrainSurfacePosition(
            position
        );
    }


    // =========================================================
    // TERRAIN SURFACE POSITION
    // =========================================================

    public Vector3 GetTerrainSurfacePosition(
        Vector3 worldPosition
    )
    {
        if (terrain == null)
        {
            return worldPosition;
        }


        float terrainHeight =
            terrain.SampleHeight(
                worldPosition
            );


        return new Vector3(
            worldPosition.x,
            terrain.transform.position.y +
            terrainHeight,
            worldPosition.z
        );
    }
}