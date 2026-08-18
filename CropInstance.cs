using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CROP INSTANCE
// ============================================================
//
// Represents ONE individual crop.
//
// Supports:
// - Stable crop ID
// - Crop type
// - Growth stages
// - Mature state
// - Grow()
// - GrowToMaturity()
// - Growth-stage prefabs
// - CanBecomeTree
//
// CropType belongs to CropManager:
//
//     CropManager.CropType
//
// ============================================================

public class CropInstance : MonoBehaviour
{
    // =========================================================
    // CROP ID
    // =========================================================

    public int cropId
    {
        get;
        private set;
    }


    // =========================================================
    // CROP NAME
    // =========================================================

    public string cropName
    {
        get;
        private set;
    }


    // =========================================================
    // CURRENT STAGE
    // =========================================================

    public int CurrentStage
    {
        get;
        private set;
    }


    // =========================================================
    // MATURE
    // =========================================================

    public bool IsMature
    {
        get;
        private set;
    }


    // =========================================================
    // POSITION
    // =========================================================

    public Vector3 WorldPosition
    {
        get
        {
            return transform.position;
        }
    }


    // =========================================================
    // DEFINITION
    // =========================================================

    private CropManager.CropType definition;


    // =========================================================
    // MANAGER
    // =========================================================

    private CropManager manager;


    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize(
        int id,
        string name,
        CropManager.CropType cropDefinition,
        CropManager cropManager
    )
    {
        cropId = id;

        cropName =
            string.IsNullOrWhiteSpace(name)
            ? "Unknown"
            : name;

        definition =
            cropDefinition;

        manager =
            cropManager;

        CurrentStage = 0;

        IsMature =
            definition != null &&
            definition.growthStages <= 1;

        UpdateVisual();
    }


    // =========================================================
    // GET DEFINITION
    // =========================================================

    public CropManager.CropType GetDefinition()
    {
        return definition;
    }


    // =========================================================
    // GROW ONE STAGE
    // =========================================================

    public bool Grow()
    {
        if (definition == null)
        {
            Debug.LogWarning(
                "[CropInstance] Cannot grow crop " +
                cropId +
                ". Definition is missing."
            );

            return false;
        }


        if (IsMature)
        {
            Debug.Log(
                "[CropInstance] Crop " +
                cropId +
                " is already mature."
            );

            return false;
        }


        CurrentStage++;


        int finalStage =
            Mathf.Max(
                definition.growthStages - 1,
                0
            );


        if (CurrentStage >= finalStage)
        {
            CurrentStage =
                finalStage;

            IsMature =
                true;
        }


        UpdateVisual();


        Debug.Log(
            "[CropInstance] " +
            cropName +
            " #" +
            cropId +
            " grew to stage " +
            CurrentStage +
            "/" +
            finalStage +
            " | Mature=" +
            IsMature
        );


        return true;
    }


    // =========================================================
    // GROW TO MATURITY
    // =========================================================

    public void GrowToMaturity()
    {
        if (definition == null)
        {
            Debug.LogWarning(
                "[CropInstance] Cannot mature crop " +
                cropId +
                ". Definition is missing."
            );

            return;
        }


        CurrentStage =
            Mathf.Max(
                definition.growthStages - 1,
                0
            );


        IsMature = true;


        UpdateVisual();


        Debug.Log(
            "[CropInstance] " +
            cropName +
            " #" +
            cropId +
            " is now MATURE."
        );
    }


    // =========================================================
    // CAN BECOME TREE
    // =========================================================

    public bool CanBecomeTree
    {
        get
        {
            return
                definition != null &&
                definition.canBecomeTree;
        }
    }


    // =========================================================
    // GROWTH INFORMATION
    // =========================================================

    public string GetGrowthInformation()
    {
        if (definition == null)
        {
            return "Crop definition missing.";
        }


        int finalStage =
            Mathf.Max(
                definition.growthStages - 1,
                0
            );


        return
            "Crop ID: " +
            cropId +
            "\n" +

            "Crop: " +
            cropName +
            "\n" +

            "Growth stage: " +
            CurrentStage +
            "/" +
            finalStage +
            "\n" +

            "Mature: " +
            IsMature +
            "\n" +

            "Position: " +
            transform.position;
    }


    // =========================================================
    // UPDATE VISUAL
    // =========================================================

    private void UpdateVisual()
    {
        if (definition == null)
        {
            return;
        }


        if (
            definition.growthStagePrefabs == null ||
            definition.growthStagePrefabs.Length == 0
        )
        {
            return;
        }


        int index =
            Mathf.Clamp(
                CurrentStage,
                0,
                definition.growthStagePrefabs.Length - 1
            );


        GameObject prefab =
            definition.growthStagePrefabs[index];


        if (prefab == null)
        {
            Debug.LogWarning(
                "[CropInstance] Missing growth prefab for " +
                cropName +
                " stage " +
                index
            );

            return;
        }


        // -----------------------------------------------------
        // REMOVE OLD VISUALS
        //
        // CropInstance itself is on the parent.
        // Only children are removed.
        // -----------------------------------------------------

        List<GameObject> children =
            new List<GameObject>();


        for (
            int i = 0;
            i < transform.childCount;
            i++
        )
        {
            children.Add(
                transform.GetChild(i).gameObject
            );
        }


        foreach (
            GameObject child
            in children
        )
        {
            if (child != null)
            {
                Destroy(child);
            }
        }


        // -----------------------------------------------------
        // CREATE VISUAL
        // -----------------------------------------------------

        GameObject visual =
            Instantiate(
                prefab,
                transform
            );


        if (visual == null)
        {
            return;
        }


        visual.transform.localPosition =
            Vector3.zero;


        visual.transform.localRotation =
            Quaternion.identity;
    }


    // =========================================================
    // STRING
    // =========================================================

    public override string ToString()
    {
        return
            cropName +
            "_Crop_" +
            cropId +
            " | Stage=" +
            CurrentStage +
            " | Mature=" +
            IsMature;
    }
}