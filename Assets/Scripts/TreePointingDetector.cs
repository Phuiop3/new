
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// ============================================================
// TREE POINTING DETECTOR
// ============================================================
//
// Behaviour:
//
// Point at Tree_3
//      ↓
// selectedTree = Tree_3
//
// Move ray to ground position A
//      ↓
// selectedTree = Tree_3
// groundPosition = A
//
// Move ray to ground position B
//      ↓
// selectedTree = Tree_3
// groundPosition = B
//
// Move ray to ground position C
//      ↓
// selectedTree = Tree_3
// groundPosition = C
//
// Move ray to another object
//      ↓
// selectedTree = Tree_3
// groundPosition = last ground position
//
// Move ray into empty space
//      ↓
// selectedTree = Tree_3
// groundPosition = last ground position
//
// Say:
//
// "Move this tree over there."
//
// DemoChat receives:
//
// selectedTree
// selectedTreeIndex
// selectedTreeName
// lastGroundPosition
//
// ============================================================
//
// IMPORTANT:
//
// selectedTree and pointedTree are DIFFERENT.
//
// pointedTree:
//     What the ray is pointing at RIGHT NOW.
//
// selectedTree:
//     The tree selected previously.
//
// selectedTree DOES NOT become null when the ray moves away.
//
// groundPosition:
//     ALWAYS updates when the ray points at Terrain.
//
// Selection is ONLY cleared by:
//
// ClearSelection()
// ClearAll()
//
// ============================================================

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

            pointedTree = tree;


            // -------------------------------------------------
            // SELECT TREE
            // -------------------------------------------------

            if (selectedTree != tree)
            {
                selectedTree = tree;

                if (debugTreeSelection)
                {
                    Debug.Log(
                        "[TreePointingDetector] Selected: " +
                        selectedTree.GetTreeName() +
                        " | ID=" +
                        selectedTree.treeIndex
                    );
                }
            }


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

