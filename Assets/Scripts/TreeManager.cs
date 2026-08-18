using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// TREE MANAGER
// ============================================================
//
// Responsibilities:
//
// - Plant trees
// - Plant different tree types
// - Remove trees
// - Remove all trees
// - Move individual trees
// - Identify individual trees
// - Keep tree IDs stable
// - Remember tree type
// - Place trees on Unity Terrain
// - Keep moved trees on terrain
// - Ground tree bottom correctly on terrain
// - Prevent excessive overlap
// - Random rotation
// - Random scale
// - Automatically add TreeSelectable
// - Automatically add Collider if required
// - Create trees from mature crops
//
// ============================================================

public class TreeManager : MonoBehaviour
{
    // =========================================================
    // TREE PREFAB ENTRY
    // =========================================================

    [Serializable]
    public class TreePrefabEntry
    {
        [Tooltip(
            "Name used by AI/user. " +
            "Example: Oak, Pine, Palm, Birch."
        )]
        public string treeType;


        [Tooltip(
            "Prefab used when this tree type is planted."
        )]
        public GameObject prefab;
    }


    // =========================================================
    // DEFAULT TREE
    // =========================================================

    [Header("Tree Settings")]

    [Tooltip(
        "Default tree prefab used when no tree type is specified."
    )]
    [SerializeField]
    private GameObject treePrefab;


    // =========================================================
    // TREE TYPES
    // =========================================================

    [Header("Tree Types")]

    [Tooltip(
        "Add tree types here.\n\n" +
        "Example:\n" +
        "Oak -> Oak prefab\n" +
        "Pine -> Pine prefab\n" +
        "Palm -> Palm prefab\n" +
        "Birch -> Birch prefab"
    )]
    [SerializeField]
    private List<TreePrefabEntry> treePrefabs =
        new List<TreePrefabEntry>();


    // =========================================================
    // TERRAIN GENERATOR
    // =========================================================

    [SerializeField]
    private TerrainGenerator terrainGenerator;


    // =========================================================
    // TREE PARENT
    // =========================================================

    [SerializeField]
    private Transform treeParent;


    // =========================================================
    // MAXIMUM TREES
    // =========================================================

    [SerializeField]
    private int maximumTrees = 5000;


    // =========================================================
    // TERRAIN
    // =========================================================

    [Header("Terrain")]

    [SerializeField]
    private Terrain terrain;


    // =========================================================
    // TREE COLLIDER
    // =========================================================

    [Header("Tree Collider")]

    [SerializeField]
    private bool addColliderIfMissing = true;


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
    private bool randomScale = true;


    [SerializeField]
    private float minimumScale = 0.8f;


    [SerializeField]
    private float maximumScale = 1.2f;


    // =========================================================
    // GROUNDING
    // =========================================================

    [Header("Tree Grounding")]

    [Tooltip(
        "Moves the tree so the bottom of its renderer bounds " +
        "touches the terrain."
    )]
    [SerializeField]
    private bool groundTreesOnTerrain = true;


    [Tooltip(
        "Small vertical offset above terrain."
    )]
    [SerializeField]
    private float groundOffset = 0.01f;


    // =========================================================
    // INTERNAL TREE LIST
    // =========================================================

    private readonly List<GameObject> generatedTrees =
        new List<GameObject>();


    // =========================================================
    // NEXT TREE ID
    // =========================================================

    private int nextTreeId = 0;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        CreateTreeParent();

        RefreshTreeList();

        AssignTreeComponents();

        CalculateNextTreeId();


        Debug.Log(
            "[TreeManager] Awake complete. " +
            "Next Tree ID = " +
            nextTreeId
        );
    }


    // =========================================================
    // CREATE TREE PARENT
    // =========================================================

    private void CreateTreeParent()
    {
        if (treeParent != null)
        {
            return;
        }


        GameObject parentObject =
            new GameObject(
                "Generated Trees"
            );


        parentObject.transform.SetParent(
            transform
        );


        treeParent =
            parentObject.transform;
    }


    // =========================================================
    // ORIGINAL PLANT TREES
    // =========================================================

    public void PlantTrees(
        int count,
        float centerX,
        float centerZ,
        float radius,
        float spacing
    )
    {
        PlantTreesInternal(
            count,
            treePrefab,
            "Default",
            centerX,
            centerZ,
            radius,
            spacing
        );
    }


    // =========================================================
    // PLANT SPECIFIC TREE TYPE
    // =========================================================

    public void PlantTrees(
        int count,
        string treeType,
        float centerX,
        float centerZ,
        float radius,
        float spacing
    )
    {
        GameObject prefab =
            GetTreePrefab(
                treeType
            );


        if (prefab == null)
        {
            Debug.LogError(
                "[TreeManager] Could not find tree type: " +
                treeType
            );

            return;
        }


        PlantTreesInternal(
            count,
            prefab,
            treeType,
            centerX,
            centerZ,
            radius,
            spacing
        );
    }


    // =========================================================
    // PLANT MIXED / SPECIFIC
    // =========================================================

    public void PlantTrees(
        int count,
        string treeType,
        float centerX,
        float centerZ,
        float radius,
        float spacing,
        bool mixed
    )
    {
        if (!mixed)
        {
            PlantTrees(
                count,
                treeType,
                centerX,
                centerZ,
                radius,
                spacing
            );

            return;
        }


        PlantMixedTrees(
            count,
            centerX,
            centerZ,
            radius,
            spacing
        );
    }


    // =========================================================
    // PLANT MIXED TREES
    // =========================================================

    public void PlantMixedTrees(
        int count,
        float centerX,
        float centerZ,
        float radius,
        float spacing
    )
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[TreeManager] No terrain found."
            );

            return;
        }


        count =
            Mathf.Clamp(
                count,
                1,
                maximumTrees
            );


        radius =
            Mathf.Max(
                radius,
                1f
            );


        spacing =
            Mathf.Max(
                spacing,
                0.5f
            );


        RefreshTreeList();


        int availableSpace =
            maximumTrees -
            generatedTrees.Count;


        if (availableSpace <= 0)
        {
            Debug.LogWarning(
                "[TreeManager] Maximum tree count reached."
            );

            return;
        }


        count =
            Mathf.Min(
                count,
                availableSpace
            );


        int planted = 0;


        int attempts = 0;


        int maximumAttempts =
            Mathf.Max(
                count * 30,
                30
            );


        while (
            planted < count &&
            attempts < maximumAttempts
        )
        {
            attempts++;


            Vector2 random =
                UnityEngine.Random.insideUnitCircle *
                radius;


            float x =
                centerX +
                random.x;


            float z =
                centerZ +
                random.y;


            if (!IsInsideTerrain(
                    currentTerrain,
                    x,
                    z))
            {
                continue;
            }


            Vector3 position =
                TerrainLocalToWorld(
                    currentTerrain,
                    x,
                    z
                );


            if (IsTooCloseToExistingTree(
                    position,
                    spacing))
            {
                continue;
            }


            TreePrefabEntry entry =
                GetRandomTreeEntry();


            if (
                entry == null ||
                entry.prefab == null
            )
            {
                Debug.LogError(
                    "[TreeManager] No valid tree prefabs configured."
                );

                return;
            }


            GameObject tree =
                Instantiate(
                    entry.prefab,
                    position,
                    Quaternion.identity,
                    treeParent
                );


            if (tree == null)
            {
                continue;
            }


            SetupTree(
                tree,
                entry.treeType
            );


            GroundTreeOnTerrain(
                tree,
                currentTerrain
            );


            generatedTrees.Add(
                tree
            );


            planted++;
        }


        Debug.Log(
            "[TreeManager] Planted " +
            planted +
            " mixed trees."
        );
    }


    // =========================================================
    // INTERNAL PLANTING
    // =========================================================

    private void PlantTreesInternal(
        int count,
        GameObject prefab,
        string treeType,
        float centerX,
        float centerZ,
        float radius,
        float spacing
    )
    {
        if (prefab == null)
        {
            Debug.LogError(
                "[TreeManager] Tree Prefab is not assigned."
            );

            return;
        }


        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[TreeManager] No terrain found."
            );

            return;
        }


        count =
            Mathf.Clamp(
                count,
                1,
                maximumTrees
            );


        radius =
            Mathf.Max(
                radius,
                1f
            );


        spacing =
            Mathf.Max(
                spacing,
                0.5f
            );


        RefreshTreeList();


        int availableSpace =
            maximumTrees -
            generatedTrees.Count;


        if (availableSpace <= 0)
        {
            Debug.LogWarning(
                "[TreeManager] Maximum tree count reached."
            );

            return;
        }


        count =
            Mathf.Min(
                count,
                availableSpace
            );


        int planted = 0;


        int attempts = 0;


        int maximumAttempts =
            Mathf.Max(
                count * 30,
                30
            );


        while (
            planted < count &&
            attempts < maximumAttempts
        )
        {
            attempts++;


            Vector2 random =
                UnityEngine.Random.insideUnitCircle *
                radius;


            float x =
                centerX +
                random.x;


            float z =
                centerZ +
                random.y;


            if (!IsInsideTerrain(
                    currentTerrain,
                    x,
                    z))
            {
                continue;
            }


            Vector3 position =
                TerrainLocalToWorld(
                    currentTerrain,
                    x,
                    z
                );


            if (IsTooCloseToExistingTree(
                    position,
                    spacing))
            {
                continue;
            }


            GameObject tree =
                Instantiate(
                    prefab,
                    position,
                    Quaternion.identity,
                    treeParent
                );


            if (tree == null)
            {
                continue;
            }


            SetupTree(
                tree,
                treeType
            );


            GroundTreeOnTerrain(
                tree,
                currentTerrain
            );


            generatedTrees.Add(
                tree
            );


            planted++;
        }


        Debug.Log(
            "[TreeManager] Planted " +
            planted +
            " " +
            treeType +
            " trees."
        );
    }


    // =========================================================
    // CREATE TREE AT EXACT WORLD POSITION
    // =========================================================
    //
    // IMPORTANT FOR CROP -> TREE
    //
    // CropManager passes a WORLD position.
    //
    // This method:
    //
    // 1. Finds the tree prefab.
    // 2. Converts the requested position onto terrain.
    // 3. Creates the tree.
    // 4. Assigns stable Tree ID.
    // 5. Assigns tree type.
    // 6. Adds collider.
    // 7. Randomizes rotation.
    // 8. Randomizes scale.
    // 9. Grounds the tree.
    //
    // =========================================================

    public GameObject CreateTreeAtWorldPosition(
        string treeType,
        Vector3 worldPosition
    )
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[TreeManager] Cannot create tree. " +
                "No terrain found."
            );

            return null;
        }


        // -----------------------------------------------------
        // FIND PREFAB
        // -----------------------------------------------------

        GameObject prefab =
            GetTreePrefab(
                treeType
            );


        if (prefab == null)
        {
            Debug.LogError(
                "[TreeManager] Cannot create tree. " +
                "Tree type not found = " +
                treeType
            );

            return null;
        }


        // -----------------------------------------------------
        // MAXIMUM
        // -----------------------------------------------------

        RefreshTreeList();


        if (
            generatedTrees.Count >=
            maximumTrees
        )
        {
            Debug.LogWarning(
                "[TreeManager] Maximum tree count reached."
            );

            return null;
        }


        // -----------------------------------------------------
        // TERRAIN DATA
        // -----------------------------------------------------

        TerrainData data =
            currentTerrain.terrainData;


        if (data == null)
        {
            Debug.LogError(
                "[TreeManager] Terrain has no TerrainData."
            );

            return null;
        }


        Vector3 terrainOrigin =
            currentTerrain.transform.position;


        // -----------------------------------------------------
        // WORLD -> TERRAIN LOCAL
        // -----------------------------------------------------

        float localX =
            worldPosition.x -
            terrainOrigin.x;


        float localZ =
            worldPosition.z -
            terrainOrigin.z;


        // -----------------------------------------------------
        // CLAMP
        // -----------------------------------------------------

        localX =
            Mathf.Clamp(
                localX,
                0f,
                data.size.x
            );


        localZ =
            Mathf.Clamp(
                localZ,
                0f,
                data.size.z
            );


        // -----------------------------------------------------
        // WORLD X/Z
        // -----------------------------------------------------

        float worldX =
            terrainOrigin.x +
            localX;


        float worldZ =
            terrainOrigin.z +
            localZ;


        // -----------------------------------------------------
        // TERRAIN HEIGHT
        // -----------------------------------------------------

        Vector3 samplePosition =
            new Vector3(
                worldX,
                terrainOrigin.y,
                worldZ
            );


        float terrainHeight =
            currentTerrain.SampleHeight(
                samplePosition
            );


        float worldY =
            terrainOrigin.y +
            terrainHeight;


        Vector3 finalPosition =
            new Vector3(
                worldX,
                worldY,
                worldZ
            );


        // -----------------------------------------------------
        // CREATE
        // -----------------------------------------------------

        GameObject tree =
            Instantiate(
                prefab,
                finalPosition,
                Quaternion.identity,
                treeParent
            );


        if (tree == null)
        {
            return null;
        }


        // -----------------------------------------------------
        // SETUP
        // -----------------------------------------------------

        SetupTree(
            tree,
            treeType
        );


        // -----------------------------------------------------
        // IMPORTANT
        //
        // Ground AFTER rotation and scale.
        // -----------------------------------------------------

        GroundTreeOnTerrain(
            tree,
            currentTerrain
        );


        // -----------------------------------------------------
        // REGISTER
        // -----------------------------------------------------

        generatedTrees.Add(
            tree
        );


        Debug.Log(
            "[TreeManager] Created tree from crop: " +
            tree.name +
            " | Type = " +
            treeType +
            " | Position = " +
            tree.transform.position
        );


        return tree;
    }


    // =========================================================
    // SETUP TREE
    // =========================================================

    private void SetupTree(
        GameObject tree,
        string treeType
    )
    {
        if (tree == null)
        {
            return;
        }


        // -----------------------------------------------------
        // STABLE ID
        // -----------------------------------------------------

        int treeId =
            nextTreeId;


        nextTreeId++;


        tree.name =
            "Tree_" +
            treeId;


        // -----------------------------------------------------
        // TREE SELECTABLE
        // -----------------------------------------------------

        TreeSelectable selectable =
            tree.GetComponent<TreeSelectable>();


        if (selectable == null)
        {
            selectable =
                tree.AddComponent<TreeSelectable>();
        }


        selectable.SetTreeId(
            treeId
        );


        // -----------------------------------------------------
        // TREE TYPE
        // -----------------------------------------------------

        TreeTypeInfo typeInfo =
            tree.GetComponent<TreeTypeInfo>();


        if (typeInfo == null)
        {
            typeInfo =
                tree.AddComponent<TreeTypeInfo>();
        }


        typeInfo.SetTreeType(
            treeType
        );


        // -----------------------------------------------------
        // COLLIDER
        // -----------------------------------------------------

        EnsureTreeCollider(
            tree
        );


        // -----------------------------------------------------
        // RANDOM ROTATION
        // -----------------------------------------------------

        if (randomRotation)
        {
            tree.transform.rotation =
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


            tree.transform.localScale *=
                scale;
        }


        Debug.Log(
            "[TreeManager] Created " +
            tree.name +
            " | Type = " +
            treeType
        );
    }


    // =========================================================
    // GROUND TREE ON TERRAIN
    // =========================================================

    private void GroundTreeOnTerrain(
        GameObject tree,
        Terrain currentTerrain
    )
    {
        if (!groundTreesOnTerrain)
        {
            return;
        }


        if (tree == null)
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

        Vector3 treePosition =
            tree.transform.position;


        float terrainHeight =
            currentTerrain.SampleHeight(
                treePosition
            );


        float terrainWorldY =
            currentTerrain.transform.position.y +
            terrainHeight;


        // -----------------------------------------------------
        // GET RENDERERS
        // -----------------------------------------------------

        Renderer[] renderers =
            tree.GetComponentsInChildren<Renderer>(
                true
            );


        if (
            renderers == null ||
            renderers.Length == 0
        )
        {
            tree.transform.position =
                new Vector3(
                    treePosition.x,
                    terrainWorldY +
                    groundOffset,
                    treePosition.z
                );

            return;
        }


        // -----------------------------------------------------
        // LOWEST RENDERER POINT
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
            tree.transform.position =
                new Vector3(
                    treePosition.x,
                    terrainWorldY +
                    groundOffset,
                    treePosition.z
                );

            return;
        }


        // -----------------------------------------------------
        // CORRECTION
        // -----------------------------------------------------

        float verticalDifference =
            terrainWorldY -
            lowestPoint;


        tree.transform.position +=
            new Vector3(
                0f,
                verticalDifference +
                groundOffset,
                0f
            );


        Debug.Log(
            "[TreeManager GROUND] " +
            tree.name +
            " | Terrain Y = " +
            terrainWorldY +
            " | Bottom Y BEFORE = " +
            lowestPoint +
            " | Vertical correction = " +
            verticalDifference
        );
    }


    // =========================================================
    // GET TREE PREFAB
    // =========================================================

    public GameObject GetTreePrefab(
        string treeType
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                treeType
            )
        )
        {
            return treePrefab;
        }


        string requestedType =
            treeType.Trim();


        if (
            requestedType.Equals(
                "default",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return treePrefab;
        }


        foreach (
            TreePrefabEntry entry
            in treePrefabs
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
                    entry.treeType
                )
            )
            {
                continue;
            }


            if (
                entry.treeType.Trim().Equals(
                    requestedType,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return entry.prefab;
            }
        }


        Debug.LogWarning(
            "[TreeManager] Tree type not found: " +
            treeType
        );


        return null;
    }


    // =========================================================
    // GET RANDOM TREE ENTRY
    // =========================================================

    private TreePrefabEntry GetRandomTreeEntry()
    {
        List<TreePrefabEntry> validEntries =
            new List<TreePrefabEntry>();


        foreach (
            TreePrefabEntry entry
            in treePrefabs
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
                    entry.treeType
                )
            )
            {
                continue;
            }


            validEntries.Add(
                entry
            );
        }


        if (validEntries.Count == 0)
        {
            return null;
        }


        int index =
            UnityEngine.Random.Range(
                0,
                validEntries.Count
            );


        return validEntries[index];
    }


    // =========================================================
    // MOVE TREE BY TERRAIN LOCAL X/Z
    // =========================================================

    public bool MoveTree(
        int treeIndex,
        float targetX,
        float targetZ
    )
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[TreeManager] Cannot move tree. " +
                "No terrain found."
            );

            return false;
        }


        targetX =
            Mathf.Clamp(
                targetX,
                0f,
                currentTerrain.terrainData.size.x
            );


        targetZ =
            Mathf.Clamp(
                targetZ,
                0f,
                currentTerrain.terrainData.size.z
            );


        Vector3 newPosition =
            TerrainLocalToWorld(
                currentTerrain,
                targetX,
                targetZ
            );


        return MoveTreeToWorldPosition(
            treeIndex,
            newPosition
        );
    }


    // =========================================================
    // MOVE TREE TO WORLD POSITION
    // =========================================================

    public bool MoveTreeToWorldPosition(
        int treeIndex,
        Vector3 worldPosition
    )
    {
        Debug.Log(
            "[TreeManager MOVE] =================================="
        );


        Debug.Log(
            "[TreeManager MOVE] Requested Tree ID = " +
            treeIndex
        );


        Debug.Log(
            "[TreeManager MOVE] Requested World Position = " +
            worldPosition
        );


        // -----------------------------------------------------
        // FIND TREE
        // -----------------------------------------------------

        GameObject tree =
            GetTreeById(
                treeIndex
            );


        if (tree == null)
        {
            Debug.LogError(
                "[TreeManager MOVE] TREE NOT FOUND! ID = " +
                treeIndex
            );

            return false;
        }


        // -----------------------------------------------------
        // TERRAIN
        // -----------------------------------------------------

        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[TreeManager MOVE] NO TERRAIN FOUND."
            );

            return false;
        }


        TerrainData terrainData =
            currentTerrain.terrainData;


        if (terrainData == null)
        {
            Debug.LogError(
                "[TreeManager MOVE] Terrain has no TerrainData."
            );

            return false;
        }


        Vector3 terrainOrigin =
            currentTerrain.transform.position;


        // -----------------------------------------------------
        // WORLD -> LOCAL
        // -----------------------------------------------------

        float localX =
            worldPosition.x -
            terrainOrigin.x;


        float localZ =
            worldPosition.z -
            terrainOrigin.z;


        // -----------------------------------------------------
        // CLAMP
        // -----------------------------------------------------

        localX =
            Mathf.Clamp(
                localX,
                0f,
                terrainData.size.x
            );


        localZ =
            Mathf.Clamp(
                localZ,
                0f,
                terrainData.size.z
            );


        // -----------------------------------------------------
        // WORLD POSITION
        // -----------------------------------------------------

        float finalWorldX =
            terrainOrigin.x +
            localX;


        float finalWorldZ =
            terrainOrigin.z +
            localZ;


        // -----------------------------------------------------
        // TERRAIN HEIGHT
        // -----------------------------------------------------

        Vector3 samplePosition =
            new Vector3(
                finalWorldX,
                terrainOrigin.y,
                finalWorldZ
            );


        float terrainHeight =
            currentTerrain.SampleHeight(
                samplePosition
            );


        float finalWorldY =
            terrainOrigin.y +
            terrainHeight;


        Vector3 finalPosition =
            new Vector3(
                finalWorldX,
                finalWorldY,
                finalWorldZ
            );


        // -----------------------------------------------------
        // MOVE
        // -----------------------------------------------------

        tree.transform.position =
            finalPosition;


        // -----------------------------------------------------
        // GROUND
        // -----------------------------------------------------

        GroundTreeOnTerrain(
            tree,
            currentTerrain
        );


        // -----------------------------------------------------
        // VERIFY
        // -----------------------------------------------------

        Debug.Log(
            "[TreeManager MOVE] Tree moved successfully: " +
            tree.name +
            " -> " +
            tree.transform.position
        );


        return true;
    }


    // =========================================================
    // GET TREE
    // =========================================================

    public GameObject GetTree(
        int treeIndex
    )
    {
        return GetTreeById(
            treeIndex
        );
    }


    // =========================================================
    // GET TREE BY ID
    // =========================================================

    public GameObject GetTreeById(
        int treeId
    )
    {
        RefreshTreeList();


        foreach (
            GameObject tree
            in generatedTrees
        )
        {
            if (tree == null)
            {
                continue;
            }


            TreeSelectable selectable =
                tree.GetComponent<TreeSelectable>();


            if (selectable == null)
            {
                continue;
            }


            if (
                selectable.GetTreeId() ==
                treeId
            )
            {
                return tree;
            }
        }


        return null;
    }


    // =========================================================
    // GET TREE TYPE
    // =========================================================

    public string GetTreeType(
        int treeId
    )
    {
        GameObject tree =
            GetTreeById(
                treeId
            );


        if (tree == null)
        {
            return null;
        }


        TreeTypeInfo typeInfo =
            tree.GetComponent<TreeTypeInfo>();


        if (typeInfo == null)
        {
            return "Unknown";
        }


        return typeInfo.GetTreeType();
    }


    // =========================================================
    // REMOVE TREES BY AREA
    // =========================================================

    public void RemoveTrees(
        float centerX,
        float centerZ,
        float radius
    )
    {
        Terrain currentTerrain =
            GetTerrain();


        if (currentTerrain == null)
        {
            Debug.LogError(
                "[TreeManager] No terrain found."
            );

            return;
        }


        Vector3 center =
            TerrainLocalToWorld(
                currentTerrain,
                centerX,
                centerZ
            );


        List<GameObject> treesToRemove =
            new List<GameObject>();


        foreach (
            GameObject tree
            in generatedTrees
        )
        {
            if (tree == null)
            {
                continue;
            }


            Vector3 treePosition =
                tree.transform.position;


            float distance =
                Vector2.Distance(
                    new Vector2(
                        treePosition.x,
                        treePosition.z
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
                treesToRemove.Add(
                    tree
                );
            }
        }


        foreach (
            GameObject tree
            in treesToRemove
        )
        {
            generatedTrees.Remove(
                tree
            );


            if (tree != null)
            {
                Destroy(
                    tree
                );
            }
        }


        Debug.Log(
            "[TreeManager] Removed " +
            treesToRemove.Count +
            " trees."
        );
    }


    // =========================================================
    // REMOVE TREE BY ID
    // =========================================================

    public bool RemoveTree(
        int treeId
    )
    {
        GameObject tree =
            GetTreeById(
                treeId
            );


        if (tree == null)
        {
            Debug.LogWarning(
                "[TreeManager] Cannot remove tree. " +
                "Tree ID not found = " +
                treeId
            );

            return false;
        }


        generatedTrees.Remove(
            tree
        );


        Debug.Log(
            "[TreeManager] Removing " +
            tree.name
        );


        Destroy(
            tree
        );


        return true;
    }


    // =========================================================
    // REMOVE ALL TREES
    // =========================================================

    public void RemoveAllTrees()
    {
        foreach (
            GameObject tree
            in generatedTrees
        )
        {
            if (tree != null)
            {
                Destroy(
                    tree
                );
            }
        }


        generatedTrees.Clear();


        Debug.Log(
            "[TreeManager] Removed all trees."
        );
    }


    // =========================================================
    // TREE COUNT
    // =========================================================

    public int GetTreeCount()
    {
        RefreshTreeList();


        return generatedTrees.Count;
    }


    // =========================================================
    // REFRESH LIST
    // =========================================================

    private void RefreshTreeList()
    {
        generatedTrees.Clear();


        if (treeParent == null)
        {
            return;
        }


        for (
            int i = 0;
            i < treeParent.childCount;
            i++
        )
        {
            Transform child =
                treeParent.GetChild(i);


            if (child != null)
            {
                generatedTrees.Add(
                    child.gameObject
                );
            }
        }
    }


    // =========================================================
    // ASSIGN COMPONENTS
    // =========================================================

    private void AssignTreeComponents()
    {
        RefreshTreeList();


        Terrain currentTerrain =
            GetTerrain();


        foreach (
            GameObject tree
            in generatedTrees
        )
        {
            if (tree == null)
            {
                continue;
            }


            // -------------------------------------------------
            // TREE SELECTABLE
            // -------------------------------------------------

            TreeSelectable selectable =
                tree.GetComponent<TreeSelectable>();


            if (selectable == null)
            {
                selectable =
                    tree.AddComponent<TreeSelectable>();
            }


            // -------------------------------------------------
            // ID
            // -------------------------------------------------

            if (
                selectable.GetTreeId() <
                0
            )
            {
                selectable.SetTreeId(
                    nextTreeId
                );


                nextTreeId++;


                tree.name =
                    "Tree_" +
                    selectable.GetTreeId();
            }


            // -------------------------------------------------
            // TREE TYPE
            // -------------------------------------------------

            TreeTypeInfo typeInfo =
                tree.GetComponent<TreeTypeInfo>();


            if (typeInfo == null)
            {
                typeInfo =
                    tree.AddComponent<TreeTypeInfo>();


                typeInfo.SetTreeType(
                    "Unknown"
                );
            }


            // -------------------------------------------------
            // COLLIDER
            // -------------------------------------------------

            EnsureTreeCollider(
                tree
            );


            // -------------------------------------------------
            // GROUND
            // -------------------------------------------------

            if (currentTerrain != null)
            {
                GroundTreeOnTerrain(
                    tree,
                    currentTerrain
                );
            }
        }
    }


    // =========================================================
    // CALCULATE NEXT ID
    // =========================================================

    private void CalculateNextTreeId()
    {
        int highestId =
            -1;


        foreach (
            GameObject tree
            in generatedTrees
        )
        {
            if (tree == null)
            {
                continue;
            }


            TreeSelectable selectable =
                tree.GetComponent<TreeSelectable>();


            if (selectable == null)
            {
                continue;
            }


            highestId =
                Mathf.Max(
                    highestId,
                    selectable.GetTreeId()
                );
        }


        nextTreeId =
            highestId + 1;
    }


    // =========================================================
    // COLLIDER
    // =========================================================

    private void EnsureTreeCollider(
        GameObject tree
    )
    {
        if (!addColliderIfMissing)
        {
            return;
        }


        if (
            tree.GetComponentInChildren<Collider>() !=
            null
        )
        {
            return;
        }


        CapsuleCollider collider =
            tree.AddComponent<CapsuleCollider>();


        collider.center =
            new Vector3(
                0f,
                1f,
                0f
            );


        collider.radius =
            0.5f;


        collider.height =
            2f;
    }


    // =========================================================
    // FIND TERRAIN
    // =========================================================

    private Terrain GetTerrain()
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
    // TERRAIN BOUNDS
    // =========================================================

    private bool IsInsideTerrain(
        Terrain currentTerrain,
        float localX,
        float localZ
    )
    {
        if (currentTerrain == null)
        {
            return false;
        }


        TerrainData data =
            currentTerrain.terrainData;


        if (data == null)
        {
            return false;
        }


        return
            localX >= 0f &&
            localZ >= 0f &&
            localX <= data.size.x &&
            localZ <= data.size.z;
    }


    // =========================================================
    // TERRAIN LOCAL -> WORLD
    // =========================================================

    private Vector3 TerrainLocalToWorld(
        Terrain currentTerrain,
        float localX,
        float localZ
    )
    {
        if (currentTerrain == null)
        {
            return Vector3.zero;
        }


        TerrainData data =
            currentTerrain.terrainData;


        if (data == null)
        {
            return Vector3.zero;
        }


        localX =
            Mathf.Clamp(
                localX,
                0f,
                data.size.x
            );


        localZ =
            Mathf.Clamp(
                localZ,
                0f,
                data.size.z
            );


        float worldX =
            currentTerrain.transform.position.x +
            localX;


        float worldZ =
            currentTerrain.transform.position.z +
            localZ;


        float terrainY =
            currentTerrain.SampleHeight(
                new Vector3(
                    worldX,
                    currentTerrain.transform.position.y,
                    worldZ
                )
            );


        return new Vector3(
            worldX,
            currentTerrain.transform.position.y +
            terrainY,
            worldZ
        );
    }


    // =========================================================
    // SPACING
    // =========================================================

    private bool IsTooCloseToExistingTree(
        Vector3 position,
        float spacing
    )
    {
        float spacingSquared =
            spacing *
            spacing;


        foreach (
            GameObject tree
            in generatedTrees
        )
        {
            if (tree == null)
            {
                continue;
            }


            Vector3 difference =
                tree.transform.position -
                position;


            difference.y = 0f;


            if (
                difference.sqrMagnitude <
                spacingSquared
            )
            {
                return true;
            }
        }


        return false;
    }
}


// ============================================================
// TREE TYPE INFO
// ============================================================
//
// Stores the type of an individual tree.
//
// Example:
//
// Tree_0 -> Oak
// Tree_1 -> Pine
// Tree_2 -> Palm
// Tree_3 -> Birch
//
// ============================================================

public class TreeTypeInfo :
    MonoBehaviour
{
    [SerializeField]
    private string treeType =
        "Unknown";


    // =========================================================
    // SET TYPE
    // =========================================================

    public void SetTreeType(
        string type
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                type
            )
        )
        {
            treeType =
                "Unknown";
        }
        else
        {
            treeType =
                type;
        }
    }


    // =========================================================
    // GET TYPE
    // =========================================================

    public string GetTreeType()
    {
        return treeType;
    }
}