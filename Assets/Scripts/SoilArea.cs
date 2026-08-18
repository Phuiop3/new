using System.Collections.Generic;
using UnityEngine;

// ============================================================
// SOIL AREA
// ============================================================
//
// Creates a visible soil/farming area on top of Unity Terrain.
//
// Responsibilities:
//
// - Define soil area size
// - Snap soil to Terrain
// - Create a soil mesh that follows Terrain height
// - Allow crops to be planted only inside soil
// - Provide soil bounds
// - Automatically rebuild when requested
//
// Example:
//
//              TERRAIN
//      __________________________
//
//          ┌────────────────┐
//          │      SOIL      │
//          │ 🌱  🌱  🌱  🌱 │
//          │ 🌱  🌱  🌱  🌱 │
//          └────────────────┘
//
// Crops are planted only inside this area.
//
// ============================================================

public class SoilArea : MonoBehaviour
{
    // =========================================================
    // SOIL SETTINGS
    // =========================================================

    [Header("Soil Settings")]

    [SerializeField]
    private Vector2 size = new Vector2(10f, 10f);


    [Tooltip("How many terrain samples are used along each direction.")]
    [SerializeField]
    [Min(2)]
    private int resolution = 16;


    [Tooltip("Small height offset above the terrain.")]
    [SerializeField]
    private float heightOffset = 0.025f;


    // =========================================================
    // VISUAL SETTINGS
    // =========================================================

    [Header("Soil Visual")]

    [SerializeField]
    private Material soilMaterial;


    [SerializeField]
    private bool createVisual = true;


    // =========================================================
    // GIZMO
    // =========================================================

    [Header("Debug")]

    [SerializeField]
    private bool showGizmo = true;


    [SerializeField]
    private Color gizmoColor =
        new Color(
            0.35f,
            0.2f,
            0.1f,
            0.5f
        );


    // =========================================================
    // INTERNAL
    // =========================================================

    private Terrain terrain;

    private GameObject soilVisual;

    private Mesh soilMesh;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        terrain =
            GetTerrain();


        if (terrain == null)
        {
            Debug.LogWarning(
                "[SoilArea] No terrain found."
            );

            return;
        }


        SnapToTerrain();


        if (createVisual)
        {
            CreateSoilVisual();
        }
    }


    // =========================================================
    // GET TERRAIN
    // =========================================================

    private Terrain GetTerrain()
    {
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
            return activeTerrain;
        }


        // -----------------------------------------------------
        // SEARCH SCENE
        // -----------------------------------------------------

        Terrain[] terrains =
            FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude
            );


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
                return foundTerrain;
            }
        }


        return null;
    }


    // =========================================================
    // CAN PLANT
    // =========================================================
    //
    // Returns true if the world position is inside this soil
    // area.
    //
    // =========================================================

    public bool CanPlantAt(
        Vector3 worldPosition
    )
    {
        Vector3 local =
            transform.InverseTransformPoint(
                worldPosition
            );


        float halfX =
            size.x * 0.5f;


        float halfZ =
            size.y * 0.5f;


        return
            local.x >= -halfX &&
            local.x <= halfX &&
            local.z >= -halfZ &&
            local.z <= halfZ;
    }


    // =========================================================
    // GET RANDOM POSITION
    // =========================================================
    //
    // Returns a random position inside the soil.
    //
    // The Y value is corrected to Terrain height.
    //
    // =========================================================

    public Vector3 GetRandomPosition()
    {
        float x =
            Random.Range(
                -size.x * 0.5f,
                size.x * 0.5f
            );


        float z =
            Random.Range(
                -size.y * 0.5f,
                size.y * 0.5f
            );


        Vector3 worldPosition =
            transform.TransformPoint(
                new Vector3(
                    x,
                    0f,
                    z
                )
            );


        return SnapPositionToTerrain(
            worldPosition
        );
    }


    // =========================================================
    // SNAP POSITION TO TERRAIN
    // =========================================================

    public Vector3 SnapPositionToTerrain(
        Vector3 worldPosition
    )
    {
        if (terrain == null)
        {
            terrain =
                GetTerrain();
        }


        if (terrain == null)
        {
            return worldPosition;
        }


        float terrainHeight =
            terrain.SampleHeight(
                worldPosition
            );


        float worldY =
            terrain.transform.position.y +
            terrainHeight;


        return new Vector3(
            worldPosition.x,
            worldY + heightOffset,
            worldPosition.z
        );
    }


    // =========================================================
    // SNAP SOIL AREA TO TERRAIN
    // =========================================================

    public void SnapToTerrain()
    {
        if (terrain == null)
        {
            terrain =
                GetTerrain();
        }


        if (terrain == null)
        {
            Debug.LogWarning(
                "[SoilArea] Cannot snap soil. " +
                "No terrain found."
            );

            return;
        }


        transform.position =
            SnapPositionToTerrain(
                transform.position
            );


        if (soilVisual != null)
        {
            CreateSoilVisual();
        }
    }


    // =========================================================
    // CREATE SOIL VISUAL
    // =========================================================
    //
    // Creates a mesh conforming to the Terrain.
    //
    // This means the soil does NOT remain as a flat plane
    // when the Terrain is uneven.
    //
    // =========================================================

    public void CreateSoilVisual()
    {
        if (!createVisual)
        {
            return;
        }


        if (terrain == null)
        {
            terrain =
                GetTerrain();
        }


        if (terrain == null)
        {
            Debug.LogWarning(
                "[SoilArea] Cannot create soil visual. " +
                "No terrain found."
            );

            return;
        }


        // -----------------------------------------------------
        // REMOVE OLD VISUAL
        // -----------------------------------------------------

        if (soilVisual != null)
        {
            Destroy(
                soilVisual
            );
        }


        // -----------------------------------------------------
        // CREATE OBJECT
        // -----------------------------------------------------

        soilVisual =
            new GameObject(
                "Soil Visual"
            );


        soilVisual.transform.SetParent(
            transform
        );


        soilVisual.transform.localPosition =
            Vector3.zero;


        soilVisual.transform.localRotation =
            Quaternion.identity;


        soilVisual.transform.localScale =
            Vector3.one;


        // -----------------------------------------------------
        // MESH
        // -----------------------------------------------------

        MeshFilter meshFilter =
            soilVisual.AddComponent<MeshFilter>();


        MeshRenderer meshRenderer =
            soilVisual.AddComponent<MeshRenderer>();


        soilMesh =
            CreateTerrainFollowingMesh();


        meshFilter.sharedMesh =
            soilMesh;


        // -----------------------------------------------------
        // MATERIAL
        // -----------------------------------------------------

        if (soilMaterial != null)
        {
            meshRenderer.sharedMaterial =
                soilMaterial;
        }


        Debug.Log(
            "[SoilArea] Created soil visual. " +
            "Size = " +
            size
        );
    }


    // =========================================================
    // CREATE TERRAIN FOLLOWING MESH
    // =========================================================

    private Mesh CreateTerrainFollowingMesh()
    {
        int verticesPerSide =
            Mathf.Max(
                resolution,
                2
            );


        int vertexCount =
            verticesPerSide *
            verticesPerSide;


        Vector3[] vertices =
            new Vector3[
                vertexCount
            ];


        Vector2[] uv =
            new Vector2[
                vertexCount
            ];


        int quadCount =
            (verticesPerSide - 1) *
            (verticesPerSide - 1);


        int[] triangles =
            new int[
                quadCount * 6
            ];


        // -----------------------------------------------------
        // VERTICES
        // -----------------------------------------------------

        int vertexIndex = 0;


        for (
            int z = 0;
            z < verticesPerSide;
            z++
        )
        {
            float normalizedZ =
                (float)z /
                (verticesPerSide - 1);


            float localZ =
                Mathf.Lerp(
                    -size.y * 0.5f,
                    size.y * 0.5f,
                    normalizedZ
                );


            for (
                int x = 0;
                x < verticesPerSide;
                x++
            )
            {
                float normalizedX =
                    (float)x /
                    (verticesPerSide - 1);


                float localX =
                    Mathf.Lerp(
                        -size.x * 0.5f,
                        size.x * 0.5f,
                        normalizedX
                    );


                Vector3 worldPosition =
                    transform.TransformPoint(
                        new Vector3(
                            localX,
                            0f,
                            localZ
                        )
                    );


                float terrainHeight =
                    terrain.SampleHeight(
                        worldPosition
                    );


                float terrainWorldY =
                    terrain.transform.position.y +
                    terrainHeight;


                Vector3 localPosition =
                    transform.InverseTransformPoint(
                        new Vector3(
                            worldPosition.x,
                            terrainWorldY +
                            heightOffset,
                            worldPosition.z
                        )
                    );


                vertices[vertexIndex] =
                    localPosition;


                uv[vertexIndex] =
                    new Vector2(
                        normalizedX,
                        normalizedZ
                    );


                vertexIndex++;
            }
        }


        // -----------------------------------------------------
        // TRIANGLES
        // -----------------------------------------------------

        int triangleIndex = 0;


        for (
            int z = 0;
            z < verticesPerSide - 1;
            z++
        )
        {
            for (
                int x = 0;
                x < verticesPerSide - 1;
                x++
            )
            {
                int bottomLeft =
                    z *
                    verticesPerSide +
                    x;


                int bottomRight =
                    bottomLeft + 1;


                int topLeft =
                    bottomLeft +
                    verticesPerSide;


                int topRight =
                    topLeft + 1;


                triangles[triangleIndex++] =
                    bottomLeft;


                triangles[triangleIndex++] =
                    topLeft;


                triangles[triangleIndex++] =
                    topRight;


                triangles[triangleIndex++] =
                    bottomLeft;


                triangles[triangleIndex++] =
                    topRight;


                triangles[triangleIndex++] =
                    bottomRight;
            }
        }


        // -----------------------------------------------------
        // CREATE MESH
        // -----------------------------------------------------

        Mesh mesh =
            new Mesh();


        mesh.name =
            "Soil Area Mesh";


        mesh.vertices =
            vertices;


        mesh.uv =
            uv;


        mesh.triangles =
            triangles;


        mesh.RecalculateNormals();


        mesh.RecalculateBounds();


        return mesh;
    }


    // =========================================================
    // GET SIZE
    // =========================================================

    public Vector2 GetSize()
    {
        return size;
    }


    // =========================================================
    // GET CENTER
    // =========================================================

    public Vector3 GetCenter()
    {
        return transform.position;
    }


    // =========================================================
    // SET SIZE
    // =========================================================

    public void SetSize(
        Vector2 newSize
    )
    {
        size =
            new Vector2(
                Mathf.Max(
                    newSize.x,
                    0.5f
                ),
                Mathf.Max(
                    newSize.y,
                    0.5f
                )
            );


        SnapToTerrain();


        if (createVisual)
        {
            CreateSoilVisual();
        }
    }


    // =========================================================
    // GIZMO
    // =========================================================

    private void OnDrawGizmos()
    {
        if (!showGizmo)
        {
            return;
        }


        Gizmos.color =
            gizmoColor;


        Gizmos.matrix =
            transform.localToWorldMatrix;


        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(
                size.x,
                0.05f,
                size.y
            )
        );
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (soilMesh != null)
        {
            Destroy(
                soilMesh
            );
        }
    }
}