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
// selectedTree = Tree_3
// groundPosition = new point
//
// Move ray to another object
//      ↓
// selectedTree = Tree_3
//
// Say:
//
// "Move this tree over there."
//
// DemoChat receives:
//
// selected tree
// selected tree ID
// selected tree name
// last ground position
//
// Selection is cleared ONLY by:
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
        // Current ray target is recalculated every frame.

        pointedTree = null;


        // -----------------------------------------------------
        // NO RAY
        // -----------------------------------------------------

        if (rayInteractor == null)
        {
            return;
        }


        // -----------------------------------------------------
        // GET RAYCAST HIT
        // -----------------------------------------------------

        if (!rayInteractor.TryGetCurrent3DRaycastHit(
            out RaycastHit hit))
        {
            wasRayHittingSomething = false;
            wasPointingAtGround = false;

            // IMPORTANT:
            //
            // Do NOT clear selectedTree.

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
            pointedTree = tree;


            // Select this tree.

            if (selectedTree != tree)
            {
                selectedTree = tree;

                Debug.Log(
                    "[TreePointingDetector] Selected: " +
                    selectedTree.GetTreeName() +
                    " | ID=" +
                    selectedTree.GetTreeId()
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
            groundPosition = hit.point;

            hasGroundPosition = true;


            if (!wasPointingAtGround)
            {
                Debug.Log(
                    "[TreePointingDetector] Ground target acquired: " +
                    groundPosition
                );
            }


            wasPointingAtGround = true;


            // IMPORTANT:
            //
            // selectedTree stays selected.

            return;
        }


        // =====================================================
        // OTHER OBJECT
        // =====================================================

        // Do nothing.
        //
        // The selected tree remains selected.
        // The last ground position remains stored.

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
    // SELECTED TREE ID
    // =========================================================

    public int GetSelectedTreeIndex()
    {
        if (selectedTree == null)
        {
            return -1;
        }

        return selectedTree.GetTreeId();
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
    // HAS GROUND POSITION
    // =========================================================
    //
    // This is included specifically for DemoChat.
    //
    // =========================================================

    public bool HasGroundPosition()
    {
        return hasGroundPosition;
    }


    // =========================================================
    // GET GROUND POSITION
    // =========================================================

    public Vector3 GetGroundPosition()
    {
        return groundPosition;
    }


    // =========================================================
    // GET LAST GROUND POSITION
    // =========================================================
    //
    // Compatibility method.
    //
    // DemoChat can use either:
    //
    // GetGroundPosition()
    //
    // or:
    //
    // GetLastGroundPosition()
    //
    // =========================================================

    public Vector3 GetLastGroundPosition()
    {
        return groundPosition;
    }


    // =========================================================
    // TRY GET GROUND POSITION
    // =========================================================

    public bool TryGetGroundPosition(
        out Vector3 position
    )
    {
        position = groundPosition;

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

        groundPosition = Vector3.zero;

        wasPointingAtGround = false;

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