using UnityEngine;


// Captures the mountain position from the CURRENT XR Terrain ray hit
// when DemoChat receives a voice/text command.
//
// IMPORTANT:
// A mountain is part of the Unity Terrain heightmap. It is NOT a
// separate GameObject. Therefore the selected mountain position is
// simply the Terrain world position hit by the XR ray.
//
// The detector does NOT remember a mountain from an earlier pointing
// moment. DemoChat explicitly calls CaptureCurrentMountainPosition()
// when the command is received.
public class MountainPointingDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainGenerator terrainGenerator;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

    // Unity-owned mountain information identified at command time.
    private int selectedMountainId = -1;
    private string selectedMountainName = "";
    private Vector3 selectedMountainPosition;

    // Latest Terrain hit is still maintained for general terrain
    // pointing/debugging, but it is NOT used as a remembered mountain.
    private Vector3 lastGroundPosition;
    private bool hasGroundPosition;

    public bool HasSelectedMountain => selectedMountainId >= 0;
    public int SelectedMountainId => selectedMountainId;
    public string SelectedMountainName => selectedMountainName;
    public Vector3 SelectedMountainPosition => selectedMountainPosition;

    // Kept for compatibility with existing code.
    public Vector3 SelectedMountainCenter => selectedMountainPosition;

    private void Awake()
    {
        if (terrainGenerator == null)
            terrainGenerator = FindFirstObjectByType<TerrainGenerator>();

        if (rayInteractor == null)
            rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
    }

    private void Update()
    {
        if (terrainGenerator == null || rayInteractor == null)
            return;

        RaycastHit hit;

        if (!rayInteractor.TryGetCurrent3DRaycastHit(out hit))
            return;

        Terrain terrain = terrainGenerator.GetGeneratedTerrain();

        // Keep the latest Terrain position available for other systems.
        if (terrain != null &&
            hit.collider != null &&
            hit.collider.GetComponent<Terrain>() == terrain)
        {
            lastGroundPosition = hit.point;
            hasGroundPosition = true;
        }
    }

    // =========================================================
    // CAPTURE CURRENT MOUNTAIN POSITION
    // =========================================================
    // Called by DemoChat when the voice/text command is received.
    // This is the ONLY operation that selects a mountain.
    // =========================================================
    public bool CaptureCurrentMountainPosition()
    {
        // Always clear the previous command-time mountain first.
        selectedMountainId = -1;
        selectedMountainName = "";
        selectedMountainPosition = Vector3.zero;

        if (terrainGenerator == null || rayInteractor == null)
        {
            Debug.LogWarning(
                "[MountainPointingDetector] TerrainGenerator or XRRayInteractor is not assigned."
            );
            return false;
        }

        RaycastHit hit;

        if (!rayInteractor.TryGetCurrent3DRaycastHit(out hit))
        {
            Debug.Log(
                "[MountainPointingDetector] No current XR Terrain ray hit."
            );
            return false;
        }

        Terrain terrain = terrainGenerator.GetGeneratedTerrain();

        if (terrain == null ||
            hit.collider == null ||
            hit.collider.GetComponent<Terrain>() != terrain)
        {
            Debug.Log(
                "[MountainPointingDetector] Current ray is not hitting the existing Terrain."
            );
            return false;
        }

        // IMPORTANT: this is the position at the exact moment
        // DemoChat asks us to capture it.
        Vector3 currentTerrainPosition = hit.point;

        TerrainGenerator.MountainInfo mountain;

        if (!terrainGenerator.TryGetMountainAtWorldPosition(
                currentTerrainPosition,
                out mountain))
        {
            Debug.Log(
                "[MountainPointingDetector] No mountain found at current Terrain position: " +
                currentTerrainPosition
            );
            return false;
        }

        selectedMountainId = mountain.id;
        selectedMountainName = mountain.name;

        // Store the ACTUAL Terrain ray hit, not the mountain's
        // pre-recorded centre. This is the user's current pointing position.
        selectedMountainPosition = currentTerrainPosition;

        Debug.Log(
            "[MountainPointingDetector] Captured mountain NOW | " +
            selectedMountainName +
            " | ID = " + selectedMountainId +
            " | Position = " + selectedMountainPosition
        );

        return true;
    }

    public bool TryGetGroundPosition(out Vector3 position)
    {
        position = lastGroundPosition;
        return hasGroundPosition;
    }

    public void ClearSelection()
    {
        selectedMountainId = -1;
        selectedMountainName = "";
        selectedMountainPosition = Vector3.zero;
    }
}
