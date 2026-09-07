
using UnityEngine;

// ============================================================
// MOUNTAIN SELECTABLE
// ============================================================
//
// Attached to every generated mountain.
//
// Provides:
// • Stable mountain ID
// • Mountain name
// • Information for MountainPointingDetector
//
// IMPORTANT
// ------------------------------------------------------------
// The mountainIndex should be assigned by TerrainGenerator
// when the mountain is created.
//
// Do NOT generate a random ID here.
// The ID must remain stable for the lifetime of the mountain.
// ============================================================

public class MountainSelectable : MonoBehaviour
{
    // ========================================================
    // MOUNTAIN ID
    // ========================================================

    [Header("Mountain Identity")]

    [Tooltip(
        "Stable ID assigned to this mountain by TerrainGenerator."
    )]
    public int mountainIndex;


    // ========================================================
    // MOUNTAIN NAME
    // ========================================================

    [SerializeField]
    private string mountainName = "";


    // ========================================================
    // INITIALIZE
    // ========================================================

    public void Initialize(
        int id,
        string name
    )
    {
        mountainIndex = id;

        if (string.IsNullOrWhiteSpace(name))
        {
            mountainName =
                "Mountain_" + id;
        }
        else
        {
            mountainName =
                name;
        }

        gameObject.name =
            mountainName;
    }


    // ========================================================
    // GET MOUNTAIN NAME
    // ========================================================

    public string GetMountainName()
    {
        if (string.IsNullOrWhiteSpace(mountainName))
        {
            return "Mountain_" + mountainIndex;
        }

        return mountainName;
    }


    // ========================================================
    // GET MOUNTAIN ID
    // ========================================================

    public int GetMountainID()
    {
        return mountainIndex;
    }


    // ========================================================
    // SET MOUNTAIN NAME
    // ========================================================

    public void SetMountainName(
        string name
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            mountainName =
                "Mountain_" + mountainIndex;
        }
        else
        {
            mountainName =
                name;
        }

        gameObject.name =
            mountainName;
    }
}

