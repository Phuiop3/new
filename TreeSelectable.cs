using UnityEngine;

// ============================================================
// TREE SELECTABLE
// ============================================================
//
// Represents ONE individual tree.
//
// treeId is a STABLE ID.
//
// Example:
//
// Tree_0 -> ID 0
// Tree_1 -> ID 1
// Tree_2 -> ID 2
//
// If Tree_1 is removed:
//
// Tree_0 -> ID 0
// Tree_2 -> ID 2
//
// Tree_2 stays ID 2.
//
// IMPORTANT
// ----------
//
// TreeSelectable does NOT manage selection.
//
// TreePointingDetector manages which tree is selected.
//
// TreeSelectable only stores:
// - tree ID
// - tree name
//
// ============================================================

public class TreeSelectable : MonoBehaviour
{
    // =========================================================
    // TREE ID
    // =========================================================

    [Header("Tree Identity")]

    [SerializeField]
    private int treeId = -1;


    // =========================================================
    // COMPATIBILITY PROPERTY
    // =========================================================

    public int treeIndex
    {
        get
        {
            return treeId;
        }

        set
        {
            treeId = value;
        }
    }


    // =========================================================
    // TREE NAME
    // =========================================================

    public string GetTreeName()
    {
        if (!string.IsNullOrWhiteSpace(gameObject.name))
        {
            return gameObject.name;
        }

        return "Tree_" + treeId;
    }


    // =========================================================
    // GET ID
    // =========================================================

    public int GetTreeId()
    {
        return treeId;
    }


    // =========================================================
    // SET ID
    // =========================================================

    public void SetTreeId(int id)
    {
        treeId = id;
    }
}