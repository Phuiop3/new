using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CROP INSTANCE
// ============================================================
//
// Represents ONE individual crop.
//
// Responsibilities:
//
// - Unique crop ID
// - Crop name/type
// - Growth stage
// - Mature state
// - Grow one stage
// - Grow to maturity
// - Update visual growth prefab
// - Provide position information
//
// IMPORTANT
// ----------
//
// CropType belongs to CropManager:
//
//     CropManager.CropType
//
// Therefore this script uses:
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
    // CURRENT GROWTH STAGE
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
    // WORLD POSITION
    // =========================================================

    public Vector3 WorldPosition
    {
        get
        {
            return transform.position;
        }
    }


    // =========================================================
    // CROP DEFINITION
    // =========================================================

    private CropManager.CropType definition;


    // =========================================================
    // CROP MANAGER
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
        cropId =
            id;


        cropName =
            name;


        definition =
            cropDefinition;


        manager =
            cropManager;


        // -----------------------------------------------------
        // STARTING STAGE
        // -----------------------------------------------------

        CurrentStage =
            0;


        // -----------------------------------------------------
        // SINGLE-STAGE CROP
        // -----------------------------------------------------

        IsMature =
            definition != null &&
            definition.growthStages <= 1;


        // -----------------------------------------------------
        // CREATE VISUAL
        // -----------------------------------------------------

        UpdateVisual();
    }


    // =========================================================
    // GROW ONE STAGE
    // =========================================================
    //
    // Example:
    //
    // Stage 0
    //    ↓
    // Grow()
    //    ↓
    // Stage 1
    //    ↓
    // Grow()
    //    ↓
    // Stage 2
    //    ↓
    // Mature
    //
    // =========================================================

    public bool Grow()
    {
        // -----------------------------------------------------
        // CHECK DEFINITION
        // -----------------------------------------------------

        if (definition == null)
        {
            Debug.LogWarning(
                "[CropInstance] Cannot grow crop " +
                cropId +
                ". Crop definition is missing."
            );

            return false;
        }


        // -----------------------------------------------------
        // ALREADY MATURE
        // -----------------------------------------------------

        if (IsMature)
        {
            Debug.Log(
                "[CropInstance] Crop " +
                cropId +
                " is already mature."
            );

            return false;
        }


        // -----------------------------------------------------
        // INCREASE STAGE
        // -----------------------------------------------------

        CurrentStage++;


        // -----------------------------------------------------
        // FINAL STAGE
        // -----------------------------------------------------

        int finalStage =
            Mathf.Max(
                definition.growthStages - 1,
                0
            );


        // -----------------------------------------------------
        // CHECK MATURITY
        // -----------------------------------------------------

        if (
            CurrentStage >=
            finalStage
        )
        {
            CurrentStage =
                finalStage;


            IsMature =
                true;
        }


        // -----------------------------------------------------
        // UPDATE VISUAL
        // -----------------------------------------------------

        UpdateVisual();


        Debug.Log(
            "[CropInstance] Crop " +
            cropId +
            " (" +
            cropName +
            ") grew to stage " +
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
    //
    // Example:
    //
    // "Grow crop 3 fully"
    //
    // =========================================================

    public void GrowToMaturity()
    {
        // -----------------------------------------------------
        // CHECK DEFINITION
        // -----------------------------------------------------

        if (definition == null)
        {
            Debug.LogWarning(
                "[CropInstance] Cannot mature crop " +
                cropId +
                ". Crop definition is missing."
            );

            return;
        }


        // -----------------------------------------------------
        // FINAL STAGE
        // -----------------------------------------------------

        CurrentStage =
            Mathf.Max(
                definition.growthStages - 1,
                0
            );


        // -----------------------------------------------------
        // MATURE
        // -----------------------------------------------------

        IsMature =
            true;


        // -----------------------------------------------------
        // UPDATE VISUAL
        // -----------------------------------------------------

        UpdateVisual();


        Debug.Log(
            "[CropInstance] Crop " +
            cropId +
            " (" +
            cropName +
            ") is now MATURE."
        );
    }


    // =========================================================
    // GET GROWTH INFORMATION
    // =========================================================

    public string GetGrowthInformation()
    {
        if (definition == null)
        {
            return
                "Crop definition missing.";
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
    // UPDATE VISUAL
    // =========================================================

    private void UpdateVisual()
    {
        if (definition == null)
        {
            return;
        }


        // -----------------------------------------------------
        // NO GROWTH PREFABS
        // -----------------------------------------------------

        if (
            definition.growthStagePrefabs == null ||
            definition.growthStagePrefabs.Length == 0
        )
        {
            return;
        }


        // -----------------------------------------------------
        // SELECT PREFAB
        // -----------------------------------------------------

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
        // REMOVE OLD VISUAL
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
            Destroy(
                child
            );
        }


        // -----------------------------------------------------
        // CREATE NEW VISUAL
        // -----------------------------------------------------

        GameObject visual =
            Instantiate(
                prefab,
                transform.position,
                transform.rotation,
                transform
            );


        if (visual == null)
        {
            return;
        }


        // -----------------------------------------------------
        // LOCAL POSITION
        // -----------------------------------------------------

        visual.transform.localPosition =
            Vector3.zero;


        visual.transform.localRotation =
            Quaternion.identity;
    }


    // =========================================================
    // DEBUG
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