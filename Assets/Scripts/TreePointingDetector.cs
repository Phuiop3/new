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
// Move ray to ground
//      ↓
// selectedTree = Tree_3       ← REMAINS SELECTED
// groundPosition = new point
//
// Move ray to another object
//      ↓
// selectedTree = Tree_3       ← REMAINS SELECTED
//
// Move ray into empty space
//      ↓
// selectedTree = Tree_3       ← REMAINS SELECTED
//
// Say:
// "Move this tree over there."
//
// DemoChat receives:
//
// selectedTree
// selectedTreeIndex
// selectedTreeName
// lastGroundPosition
//
// Selection is ONLY cleared when:
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
    //
    // IMPORTANT:
    //
    // This is NOT the same as pointedTree.
    //
    // pointedTree:
    //     What the ray is pointing at RIGHT NOW.
    //
    // selectedTree:
    //     The tree the user selected previously.
    //
    // selectedTree DOES NOT become null just because the
    // ray moves away from the tree.
    //

    private TreeSelectable selectedTree;


    // =========================================================
    // LAST GROUND POSITION
    // =========================================================

    private Vector3 groundPosition;

    private bool hasGroundPosition;


    // =========================================================
    // DEBUG STATE
    // =========================================================
    //
    // These are only used to avoid repeating the same
    // information in the Console every frame.
    //

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
        // pointedTree is the CURRENT ray target.
        //
        // selectedTree is NOT touched here unless the ray
        // actually hits another TreeSelectable.
        //

        pointedTree = null;


        // -----------------------------------------------------
        // NO RAY
        // -----------------------------------------------------

        if (rayInteractor == null)
        {
            return;
        }


        // -----------------------------------------------------
        // GET CURRENT RAY HIT
        // -----------------------------------------------------

        if (!rayInteractor.TryGetCurrent3DRaycastHit(
                out RaycastHit hit))
        {
            wasRayHittingSomething = false;
            wasPointingAtGround = false;

            // IMPORTANT:
            //
            // Do NOT clear selectedTree.
            //
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
            // CURRENTLY POINTING AT THIS TREE
            // -------------------------------------------------

            pointedTree = tree;


            // -------------------------------------------------
            // SELECT TREE
            // -------------------------------------------------
            //
            // Only change selectedTree if it is actually
            // a different tree.
            //

            if (selectedTree != tree)
            {
                selectedTree = tree;


                Debug.Log(
                    "[TreePointingDetector] Selected: " +
                    selectedTree.GetTreeName() +
                    " | ID=" +
                    selectedTree.treeIndex
                );
            }


            wasPointingAtGround = false;

            return;
        }


        // =====================================================
        // CHECK TERRAIN
        // =====================================================

        Terrain terrain =
            hit.collider.GetComponent<Terrain>();


        if (terrain != null)
        {
            // -------------------------------------------------
            // REMEMBER GROUND POSITION
            // -------------------------------------------------

            groundPosition =
                hit.point;


            hasGroundPosition =
                true;


            // -------------------------------------------------
            // ONLY LOG WHEN WE FIRST ENTER GROUND
            // -------------------------------------------------
            //
            // NOT EVERY FRAME.
            //

            if (!wasPointingAtGround)
            {
                Debug.Log(
                    "[TreePointingDetector] Ground target acquired: " +
                    groundPosition
                );
            }


            wasPointingAtGround = true;


            // -------------------------------------------------
            // VERY IMPORTANT
            // -------------------------------------------------
            //
            // DO NOT DO THIS:
            //
            // selectedTree = null;
            //
            // The selected tree must remain remembered.
            //

            return;
        }


        // =====================================================
        // OTHER OBJECT
        // =====================================================
        //
        // The ray may hit:
        //
        // - UI
        // - another collider
        // - controller-related object
        // - house
        // - rock
        // - etc.
        //
        // DO NOTHING.
        //
        // Most importantly:
        //
        // DO NOT CLEAR selectedTree.
        // DO NOT CLEAR groundPosition.
        //
        // Also DO NOT print a message every frame.
        //

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
    // IS TREE SELECTED
    // =========================================================

    public bool HasSelectedTree()
    {
        return selectedTree != null;
    }


    // =========================================================
    // IS CURRENTLY POINTING AT TREE
    // =========================================================

    public bool IsPointingAtTree()
    {
        return pointedTree != null;
    }


    // =========================================================
    // IS CURRENTLY POINTING AT GROUND
    // =========================================================

    public bool IsPointingAtGround()
    {
        if (rayInteractor == null)
        {
            return false;
        }


        if (!rayInteractor.TryGetCurrent3DRaycastHit(
                out RaycastHit hit))
        {
            return false;
        }


        Terrain terrain =
            hit.collider.GetComponent<Terrain>();


        return terrain != null;
    }


    // =========================================================
    // GET GROUND POSITION
    // =========================================================

    public Vector3 GetGroundPosition()
    {
        return groundPosition;
    }


    // =========================================================
    // TRY GET GROUND POSITION
    // =========================================================
    //
    // Returns the LAST remembered terrain position.
    //
    // The user does NOT need to continue pointing at the
    // ground after the position has been recorded.
    //

    public bool TryGetGroundPosition(
        out Vector3 position
    )
    {
        position =
            groundPosition;


        return hasGroundPosition;
    }


    // =========================================================
    // CLEAR TREE SELECTION
    // =========================================================
    //
    // This is the ONLY normal operation that should remove
    // the selected tree.
    //

    public void ClearSelection()
    {
        if (selectedTree != null)
        {
            Debug.Log(
                "[TreePointingDetector] Selection cleared: " +
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
        hasGroundPosition = false;

        groundPosition =
            Vector3.zero;

        wasPointingAtGround =
            false;


        Debug.Log(
            "[TreePointingDetector] Ground position cleared."
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
    //
    // Useful for checking the state without generating
    // messages every frame.
    //

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
            "Pointed Tree: " + pointed +
            "\nSelected Tree: " + selected +
            "\nGround Position: " + ground;
    }
}