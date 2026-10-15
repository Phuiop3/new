using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;



public class TreePointingDetector : MonoBehaviour
{
    // =========================================================
    // XR RAY
    // =========================================================

    [Header("XR Ray")]

    [SerializeField]
    private XRRayInteractor rayInteractor;


    // =========================================================
    // CURRENTLY POINTED TREE
    // =========================================================

    private TreeSelectable pointedTree;


    // =========================================================
    // SELECTED TREE
    // =========================================================

    private TreeSelectable selectedTree;


    // =========================================================
    // LAST GROUND POSITION
    // =========================================================

    private Vector3 groundPosition;

    private bool hasGroundPosition;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]

    [SerializeField]
    private bool debugGroundPosition = true;

    [SerializeField]
    private bool debugTreeSelection = true;


    // =========================================================
    // DISTANT TREE SELECTION ASSIST
    // =========================================================
    //
    // A distant tree covers only a few pixels, so the ray often
    // passes it and hits the terrain behind it. When the ray does
    // not hit a tree directly, the assist checks for a tree inside
    // a narrow cone around the ray. Because the cone has a fixed
    // ANGLE, the selection area grows with distance.
    //
    // The assist is used only for trees further away than
    // assistMinDistance, so near selection is unchanged.
    //
    // =========================================================

    [Header("Distant Tree Selection Assist")]

    [Tooltip("Turn the assist on or off.")]
    [SerializeField]
    private bool useDistantSelectionAssist = true;

    [Tooltip("Half-angle of the selection cone around the ray, in degrees.")]
    [SerializeField]
    private float assistConeAngle = 1.5f;

    [Tooltip("Only trees further away than this (metres) can be selected by the assist.")]
    [SerializeField]
    private float assistMinDistance = 60f;

    [Tooltip("How often (seconds) the list of trees in the scene is refreshed.")]
    [SerializeField]
    private float treeListRefreshInterval = 1f;

    private readonly List<TreeSelectable> allTrees = new List<TreeSelectable>();
    private float nextTreeListRefresh;

    // Used only to avoid repeated logs
    private bool wasPointingAtGround;

    private bool wasRayHittingSomething;


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        DetectRayTarget();
    }


    // =========================================================
    // DETECT RAY TARGET
    // =========================================================

    private void DetectRayTarget()
    {
        // -----------------------------------------------------
        // IMPORTANT
        // -----------------------------------------------------
        //
        // pointedTree is reset every frame because it means:
        //
        // "What am I pointing at RIGHT NOW?"
        //
        // selectedTree is NOT reset.
        //
        // -----------------------------------------------------

        pointedTree = null;


        // =====================================================
        // NO RAY
        // =====================================================

        if (rayInteractor == null)
        {
            return;
        }


        // =====================================================
        // GET CURRENT 3D RAYCAST HIT
        // =====================================================

        RaycastHit hit;

        if (!rayInteractor.TryGetCurrent3DRaycastHit(
                out hit))
        {
            wasRayHittingSomething = false;
            wasPointingAtGround = false;

            // IMPORTANT:
            //
            // DO NOT clear selectedTree.
            // DO NOT clear groundPosition.

            return;
        }


        wasRayHittingSomething = true;


        // =====================================================
        // CHECK TREE
        // =====================================================

        TreeSelectable tree =
            hit.collider.GetComponentInParent<TreeSelectable>();


        if (tree != null)
        {
            // -------------------------------------------------
            // CURRENTLY POINTING AT TREE
            // -------------------------------------------------

            SelectPointedTree(tree, false);


            wasPointingAtGround = false;

            return;
        }


        // =====================================================
        // CHECK TERRAIN
        // =====================================================

        Terrain terrain =
            hit.collider.GetComponent<Terrain>();


        // -----------------------------------------------------
        // If collider itself does not have Terrain,
        // check its parent.
        // -----------------------------------------------------

        if (terrain == null)
        {
            terrain =
                hit.collider.GetComponentInParent<Terrain>();
        }


        if (terrain != null)
        {
            // =================================================
            // DISTANT TREE ASSIST
            // =================================================
            //
            // The ray hit the terrain. If a distant tree lies
            // inside the selection cone in front of the hit
            // point, treat it as the pointed tree.
            //
            // =================================================

            TreeSelectable assisted =
                FindTreeInCone(hit.distance);

            if (assisted != null)
            {
                SelectPointedTree(assisted, true);

                wasPointingAtGround = false;

                return;
            }


            // =================================================
            // ALWAYS UPDATE GROUND POSITION
            // =================================================
            //
            // THIS IS THE IMPORTANT FIX.
            //
            // Every time the ray moves to another point on the
            // terrain, groundPosition is replaced.
            //
            // =================================================

            Vector3 previousPosition =
                groundPosition;


            groundPosition =
                hit.point;


            hasGroundPosition =
                true;


            // =================================================
            // DEBUG GROUND POSITION
            // =================================================
            //
            // Log only when the position actually changes.
            //
            // =================================================

            if (debugGroundPosition)
            {
                float distance =
                    Vector3.Distance(
                        previousPosition,
                        groundPosition
                    );


                if (!wasPointingAtGround ||
                    distance > 0.01f)
                {
                    Debug.Log(
                        "[TreePointingDetector] " +
                        "Ground position updated: " +
                        groundPosition
                    );
                }
            }


            wasPointingAtGround = true;


            // =================================================
            // VERY IMPORTANT
            // =================================================
            //
            // DO NOT DO:
            //
            // selectedTree = null;
            //
            // selected tree remains selected.
            //
            // =================================================

            return;
        }


        // =====================================================
        // OTHER OBJECT
        // =====================================================
        //
        // Examples:
        //
        // - House
        // - Rock
        // - UI
        // - Another collider
        // - Object
        //
        // DO NOT clear selectedTree.
        // DO NOT clear groundPosition.
        //
        // =====================================================

        wasPointingAtGround = false;
    }


    // =========================================================
    // SELECT POINTED TREE
    // =========================================================

    private void SelectPointedTree(TreeSelectable tree, bool assisted)
    {
        pointedTree = tree;

        if (selectedTree != tree)
        {
            selectedTree = tree;

            if (debugTreeSelection)
            {
                Debug.Log(
                    "[TreePointingDetector] Selected: " +
                    selectedTree.GetTreeName() +
                    " | ID=" +
                    selectedTree.treeIndex +
                    (assisted ? " | assisted (distant tree)" : "")
                );
            }
        }
    }


    // =========================================================
    // FIND A DISTANT TREE INSIDE THE SELECTION CONE
    // =========================================================
    //
    // Returns the tree whose centre is closest in angle to the
    // ray, provided that:
    //   - the angle is within assistConeAngle,
    //   - the tree is further than assistMinDistance,
    //   - the tree is not behind the terrain hit point.
    //
    // =========================================================

    private TreeSelectable FindTreeInCone(float hitDistance)
    {
        if (!useDistantSelectionAssist || rayInteractor == null)
            return null;

        RefreshTreeList();

        Transform origin =
            rayInteractor.rayOriginTransform != null
                ? rayInteractor.rayOriginTransform
                : rayInteractor.transform;

        Vector3 rayStart = origin.position;
        Vector3 rayDir = origin.forward;

        TreeSelectable best = null;
        float bestAngle = assistConeAngle;

        foreach (TreeSelectable t in allTrees)
        {
            if (t == null || !t.isActiveAndEnabled)
                continue;

            Vector3 centre = GetTreeCentre(t);
            Vector3 toTree = centre - rayStart;
            float distance = toTree.magnitude;

            if (distance < assistMinDistance)
                continue;

            // Ignore trees behind the point where the ray hit the terrain
            if (distance > hitDistance + 5f)
                continue;

            float angle = Vector3.Angle(rayDir, toTree);

            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = t;
            }
        }

        return best;
    }


    private Vector3 GetTreeCentre(TreeSelectable tree)
    {
        Collider col = tree.GetComponentInChildren<Collider>();

        if (col != null)
            return col.bounds.center;

        Renderer rend = tree.GetComponentInChildren<Renderer>();

        if (rend != null)
            return rend.bounds.center;

        return tree.transform.position;
    }


    private void RefreshTreeList()
    {
        if (Time.time < nextTreeListRefresh)
            return;

        nextTreeListRefresh = Time.time + treeListRefreshInterval;

        allTrees.Clear();
        allTrees.AddRange(
            FindObjectsByType<TreeSelectable>(FindObjectsSortMode.None));
    }


    // =========================================================
    // CURRENTLY POINTED TREE
    // =========================================================

    public TreeSelectable GetPointedTree()
    {
        return pointedTree;
    }


    // =========================================================
    // SELECTED TREE
    // =========================================================

    public TreeSelectable GetSelectedTree()
    {
        return selectedTree;
    }


    // =========================================================
    // SELECTED TREE INDEX
    // =========================================================

    public int GetSelectedTreeIndex()
    {
        if (selectedTree == null)
        {
            return -1;
        }

        return selectedTree.treeIndex;
    }


    // =========================================================
    // SELECTED TREE NAME
    // =========================================================

    public string GetSelectedTreeName()
    {
        if (selectedTree == null)
        {
            return "";
        }

        return selectedTree.GetTreeName();
    }


    // =========================================================
    // HAS SELECTED TREE
    // =========================================================

    public bool HasSelectedTree()
    {
        return selectedTree != null;
    }


    // =========================================================
    // CURRENTLY POINTING AT TREE
    // =========================================================

    public bool IsPointingAtTree()
    {
        return pointedTree != null;
    }


    // =========================================================
    // CURRENTLY POINTING AT GROUND
    // =========================================================

    public bool IsPointingAtGround()
    {
        if (rayInteractor == null)
        {
            return false;
        }


        RaycastHit hit;

        if (!rayInteractor.TryGetCurrent3DRaycastHit(
                out hit))
        {
            return false;
        }


        Terrain terrain =
            hit.collider.GetComponent<Terrain>();


        if (terrain == null)
        {
            terrain =
                hit.collider.GetComponentInParent<Terrain>();
        }


        return terrain != null;
    }


    // =========================================================
    // GET CURRENT GROUND POSITION
    // =========================================================

    public Vector3 GetGroundPosition()
    {
        return groundPosition;
    }


    // =========================================================
    // TRY GET GROUND POSITION
    // =========================================================
    //
    // Returns the LAST terrain position detected.
    //
    // Example:
    //
    // Point at:
    //
    // (10, 0, 20)
    //
    // groundPosition = (10, 0, 20)
    //
    // Move ray to:
    //
    // (30, 0, 50)
    //
    // groundPosition = (30, 0, 50)
    //
    // =========================================================

    public bool TryGetGroundPosition(
        out Vector3 position)
    {
        position =
            groundPosition;

        return hasGroundPosition;
    }


    // =========================================================
    // CLEAR TREE SELECTION
    // =========================================================

    public void ClearSelection()
    {
        if (selectedTree != null)
        {
            Debug.Log(
                "[TreePointingDetector] " +
                "Selection cleared: " +
                selectedTree.GetTreeName()
            );
        }


        selectedTree = null;
    }


    // =========================================================
    // CLEAR GROUND POSITION
    // =========================================================

    public void ClearGroundPosition()
    {
        hasGroundPosition =
            false;

        groundPosition =
            Vector3.zero;

        wasPointingAtGround =
            false;


        Debug.Log(
            "[TreePointingDetector] " +
            "Ground position cleared."
        );
    }


    // =========================================================
    // CLEAR EVERYTHING
    // =========================================================

    public void ClearAll()
    {
        ClearSelection();

        ClearGroundPosition();
    }


    // =========================================================
    // DEBUG INFORMATION
    // =========================================================

    public string GetDebugState()
    {
        string selected =
            selectedTree != null
                ? selectedTree.GetTreeName()
                : "NONE";


        string pointed =
            pointedTree != null
                ? pointedTree.GetTreeName()
                : "NONE";


        string ground =
            hasGroundPosition
                ? groundPosition.ToString("F3")
                : "NONE";


        return
            "Pointed Tree: " +
            pointed +

            "\nSelected Tree: " +
            selected +

            "\nGround Position: " +
            ground;
    }
}