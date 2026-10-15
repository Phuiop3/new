using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// ============================================================
// WATER POINTING DETECTOR (Round 2)
// ============================================================
//
// Finds which river or lake the XR ray is pointing at.
//
// The water surfaces have no colliders, so the ray hits the
// terrain under the water. The hit position is then resolved to
// the recorded river or lake around it (WaterManager keeps the
// footprint of every river and lake it created), in the same way
// MountainPointingDetector resolves mountains.
//
// Like the mountain detector, the selection is kept for a short
// time after the ray leaves the water, then cleared.
//
// ============================================================

public class WaterPointingDetector : MonoBehaviour
{
    [Header("XR Ray")]
    [SerializeField] private XRRayInteractor rayInteractor;

    [Header("Water")]
    [SerializeField] private WaterManager waterManager;

    [Tooltip("Extra distance (m) around a river or lake that still counts as pointing at it.")]
    [SerializeField] private float pointingTolerance = 2f;

    [Tooltip("Seconds the selection is kept after the ray leaves the water. Negative = keep forever.")]
    [SerializeField] private float selectionHoldTime = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugSelection = true;


    private int selectedRiverId = -1;
    private int selectedLakeId = -1;
    private float lastTimeOnWater = -1f;


    // =========================================================
    // START / UPDATE
    // =========================================================

    private void Start()
    {
        if (waterManager == null)
            waterManager = FindObjectOfType<WaterManager>();

        ClearSelection();
    }

    private void Update()
    {
        bool onWater = DetectWater();

        if (onWater)
        {
            lastTimeOnWater = Time.time;
            return;
        }

        if (HasSelection() && selectionHoldTime >= 0f &&
            Time.time - lastTimeOnWater >= selectionHoldTime)
        {
            if (debugSelection)
                Debug.Log("[WaterPointingDetector] Ray left the water. Selection cleared.");

            ClearSelection();
        }

        ValidateSelection();
    }


    // =========================================================
    // DETECT
    // =========================================================

    private bool DetectWater()
    {
        if (rayInteractor == null || waterManager == null)
            return false;

        RaycastHit hit;

        if (!rayInteractor.TryGetCurrent3DRaycastHit(out hit))
            return false;

        // Lakes first: a lake footprint is a clearer target than a
        // river bank that may pass close to it.
        int lakeId = waterManager.FindLakeAtPosition(hit.point, pointingTolerance);

        if (lakeId >= 0)
        {
            if (selectedLakeId != lakeId || selectedRiverId >= 0)
            {
                selectedLakeId = lakeId;
                selectedRiverId = -1;

                if (debugSelection)
                    Debug.Log("[WaterPointingDetector] Selected Lake ID = " + lakeId);
            }

            return true;
        }

        int riverId = waterManager.FindRiverAtPosition(hit.point, pointingTolerance);

        if (riverId >= 0)
        {
            if (selectedRiverId != riverId || selectedLakeId >= 0)
            {
                selectedRiverId = riverId;
                selectedLakeId = -1;

                if (debugSelection)
                    Debug.Log("[WaterPointingDetector] Selected River ID = " + riverId);
            }

            return true;
        }

        return false;
    }


    // Clears a selection whose river or lake has been deleted.
    private void ValidateSelection()
    {
        if (waterManager == null)
            return;

        if (selectedRiverId >= 0 && waterManager.GetRiverById(selectedRiverId) == null)
            selectedRiverId = -1;

        if (selectedLakeId >= 0 && waterManager.GetLakeById(selectedLakeId) == null)
            selectedLakeId = -1;
    }


    // =========================================================
    // PUBLIC
    // =========================================================

    public int GetSelectedRiverId()
    {
        return selectedRiverId;
    }

    public int GetSelectedLakeId()
    {
        return selectedLakeId;
    }

    public bool TryGetSelectedRiverId(out int id)
    {
        id = selectedRiverId;
        return id >= 0;
    }

    public bool TryGetSelectedLakeId(out int id)
    {
        id = selectedLakeId;
        return id >= 0;
    }

    public bool HasSelection()
    {
        return selectedRiverId >= 0 || selectedLakeId >= 0;
    }

    public void ClearSelection()
    {
        selectedRiverId = -1;
        selectedLakeId = -1;
    }
}
