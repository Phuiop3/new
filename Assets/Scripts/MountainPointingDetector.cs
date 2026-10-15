using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MountainPointingDetector : MonoBehaviour
{
    [Header("XR Ray")]
    [SerializeField] private XRRayInteractor rayInteractor;

    [Header("Terrain")]
    [SerializeField] private TerrainGenerator terrainGenerator;

    [Header("Mountain Detection")]
    [Tooltip("Minimum local terrain relief required before a terrain hit is considered to be on a mountain.")]
    [SerializeField] private float minimumMountainRelief = 1.0f;

    [Tooltip("Distance around the ray hit used to examine local terrain relief.")]
    [SerializeField] private float reliefSampleDistance = 4.0f;

    [Tooltip("Number of surrounding terrain samples used for relief detection.")]
    [SerializeField]
    [Range(4, 16)]
    private int reliefSampleCount = 8;

    [Tooltip("If enabled, terrain relief must pass the minimum threshold.")]
    [SerializeField] private bool requireTerrainRelief = true;

    [Tooltip("Seconds the selection is kept after the ray leaves the mountain. Set to a negative value to keep it forever (Round 1 behaviour).")]
    [SerializeField] private float selectionHoldTime = 1.5f;

    [Tooltip("Allows a mountain to be detected close to its footprint edge.")]
    [SerializeField] private float edgeTolerance = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool debugSelection = true;


    // =========================================================
    // CURRENT POINTING STATE
    // =========================================================

    private int pointedMountainId = -1;
    private bool isPointingAtMountain = false;


    // =========================================================
    // SELECTED MOUNTAIN STATE
    // =========================================================

    private int selectedMountainId = -1;
    private bool hasSelectedMountain = false;
    private float lastTimeOnMountain = -1f;


    // =========================================================
    // LAST HIT POINT
    // =========================================================

    private Vector3 lastMountainHitPoint;
    private bool hasMountainHitPoint = false;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        ClearSelection();

        pointedMountainId = -1;
        isPointingAtMountain = false;

        hasMountainHitPoint = false;
        lastMountainHitPoint = Vector3.zero;
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        DetectMountain();
        ReleaseStaleSelection();
        ValidateSelectedMountain();

        // Diagnostic: press M while pointing to explain the detection result.
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            DiagnoseCurrentHit();
    }


    // =========================================================
    // DIAGNOSTIC (press M)
    // =========================================================

    private void DiagnoseCurrentHit()
    {
        string prefix = "[MountainPointingDetector] DIAGNOSE: ";

        if (rayInteractor == null || terrainGenerator == null)
        {
            Debug.Log(prefix + "rayInteractor or terrainGenerator is NOT assigned in the Inspector.");
            return;
        }

        if (!rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            Debug.Log(prefix + "the ray is not hitting anything.");
            return;
        }

        Terrain terrain = hit.collider.GetComponent<Terrain>();
        if (terrain == null)
            terrain = hit.collider.GetComponentInParent<Terrain>();

        if (terrain == null)
        {
            Debug.Log(prefix + "the ray hits '" + hit.collider.name + "', which is not a Terrain.");
            return;
        }

        Terrain targetTerrain = terrainGenerator.GetTargetTerrain();
        Terrain generatedTerrain = terrainGenerator.GetGeneratedTerrain();

        if (targetTerrain != null && terrain != targetTerrain && terrain != generatedTerrain)
        {
            Debug.Log(prefix + "the ray hits terrain '" + terrain.name +
                      "', which is not the TerrainGenerator's terrain.");
            return;
        }

        List<TerrainGenerator.MountainInfo> mountains = terrainGenerator.GetGeneratedMountains();
        int count = mountains == null ? 0 : mountains.Count;

        Debug.Log(prefix + "hit terrain at distance " + hit.distance.ToString("F1") +
                  " m. Recorded mountains: " + count +
                  (hasSelectedMountain ? " | currently selected ID = " + selectedMountainId : " | nothing selected"));

        if (count == 0)
        {
            Debug.Log(prefix + "NO mountains are recorded. Only mountains created by the system " +
                      "(\"create a mountain\") can be selected.");
            return;
        }

        Vector3 local = terrain.transform.InverseTransformPoint(hit.point);
        Vector2 hitPos = new Vector2(local.x, local.z);

        foreach (TerrainGenerator.MountainInfo m in mountains)
        {
            if (m == null) continue;

            float distance = Vector2.Distance(hitPos, new Vector2(m.terrainPosition.x, m.terrainPosition.y));
            float allowed = m.radius + edgeTolerance;
            float relief = CalculateLocalRelief(hit.point, terrain, m.radius);

            string verdict =
                distance > allowed ? "OUTSIDE footprint" :
                (requireTerrainRelief && relief < minimumMountainRelief) ? "inside footprint but relief too LOW" :
                "OK (should be selectable)";

            Debug.Log(prefix + "ID " + m.id + " '" + m.name + "': distance to centre " +
                      distance.ToString("F1") + " m (allowed " + allowed.ToString("F1") +
                      "), relief " + relief.ToString("F2") + " m (minimum " +
                      minimumMountainRelief.ToString("F2") + ") -> " + verdict);
        }
    }


    // =========================================================
    // DETECT MOUNTAIN
    // =========================================================

    private void DetectMountain()
    {
        // Reset current pointing state every frame.
        pointedMountainId = -1;
        isPointingAtMountain = false;

        if (rayInteractor == null)
            return;

        if (terrainGenerator == null)
            return;


        // -----------------------------------------------------
        // Get current XR raycast hit
        // -----------------------------------------------------

        RaycastHit hit;

        if (!rayInteractor.TryGetCurrent3DRaycastHit(out hit))
            return;


        // -----------------------------------------------------
        // Find Terrain from collider
        // -----------------------------------------------------

        Terrain terrain =
            hit.collider.GetComponent<Terrain>();

        if (terrain == null)
        {
            terrain =
                hit.collider.GetComponentInParent<Terrain>();
        }

        if (terrain == null)
            return;


        // -----------------------------------------------------
        // Check that this is one of our terrains
        // -----------------------------------------------------

        Terrain targetTerrain =
            terrainGenerator.GetTargetTerrain();

        Terrain generatedTerrain =
            terrainGenerator.GetGeneratedTerrain();

        if (targetTerrain != null &&
            terrain != targetTerrain &&
            terrain != generatedTerrain)
        {
            return;
        }


        // -----------------------------------------------------
        // Find mountain at hit position
        // -----------------------------------------------------

        TerrainGenerator.MountainInfo mountain =
            FindMountainAtWorldPosition(
                hit.point,
                terrain
            );

        if (mountain == null)
            return;


        // -----------------------------------------------------
        // Mountain detected
        // -----------------------------------------------------

        pointedMountainId =
            mountain.id;

        isPointingAtMountain =
            true;

        lastTimeOnMountain =
            Time.time;


        // Save last hit position.
        lastMountainHitPoint =
            hit.point;

        hasMountainHitPoint =
            true;


        // -----------------------------------------------------
        // Select mountain
        // -----------------------------------------------------

        if (!hasSelectedMountain ||
            selectedMountainId != pointedMountainId)
        {
            selectedMountainId =
                pointedMountainId;

            hasSelectedMountain =
                true;


            if (debugSelection)
            {
                Debug.Log(
                    "[MountainPointingDetector] " +
                    "Selected Mountain ID = " +
                    selectedMountainId +
                    " | Name = " +
                    mountain.name
                );
            }
        }
    }


    // =========================================================
    // FIND MOUNTAIN AT WORLD POSITION
    // =========================================================

    private TerrainGenerator.MountainInfo
        FindMountainAtWorldPosition(
            Vector3 worldPosition,
            Terrain terrain)
    {
        if (terrain == null)
            return null;


        // -----------------------------------------------------
        // Get saved/generated mountains
        // -----------------------------------------------------

        List<TerrainGenerator.MountainInfo> mountains =
            terrainGenerator.GetGeneratedMountains();

        if (mountains == null ||
            mountains.Count == 0)
        {
            return null;
        }


        // -----------------------------------------------------
        // Terrain data
        // -----------------------------------------------------

        TerrainData terrainData =
            terrain.terrainData;

        if (terrainData == null)
            return null;


        // -----------------------------------------------------
        // Convert world position to terrain local position
        // -----------------------------------------------------

        Vector3 localPosition =
            terrain.transform.InverseTransformPoint(
                worldPosition
            );


        TerrainGenerator.MountainInfo bestMountain =
            null;

        float bestScore =
            float.MinValue;


        // =====================================================
        // CHECK EVERY MOUNTAIN
        // =====================================================

        foreach (
            TerrainGenerator.MountainInfo mountain
            in mountains)
        {
            if (mountain == null)
                continue;


            // -------------------------------------------------
            // Mountain centre
            //
            // IMPORTANT:
            // terrainPosition is already stored in LOCAL
            // METRES.
            //
            // Do NOT multiply by TerrainData.size.
            // -------------------------------------------------

            Vector2 mountainCenter =
                new Vector2(
                    mountain.terrainPosition.x,
                    mountain.terrainPosition.y
                );


            // -------------------------------------------------
            // Hit position
            // -------------------------------------------------

            Vector2 hitPosition =
                new Vector2(
                    localPosition.x,
                    localPosition.z
                );


            // -------------------------------------------------
            // Distance from mountain centre
            // -------------------------------------------------

            float distance =
                Vector2.Distance(
                    hitPosition,
                    mountainCenter
                );


            // -------------------------------------------------
            // Allowed radius
            // -------------------------------------------------

            float allowedRadius =
                mountain.radius +
                edgeTolerance;


            if (distance > allowedRadius)
                continue;


            // -------------------------------------------------
            // Calculate local terrain relief
            // -------------------------------------------------

            float relief =
                CalculateLocalRelief(
                    worldPosition,
                    terrain,
                    mountain.radius
                );


            // -------------------------------------------------
            // Check relief requirement
            // -------------------------------------------------

            if (requireTerrainRelief &&
                relief < minimumMountainRelief)
            {
                continue;
            }


            // -------------------------------------------------
            // Distance score
            // -------------------------------------------------

            float normalizedDistance =
                mountain.radius > 0.01f
                    ? distance / mountain.radius
                    : 1f;

            normalizedDistance =
                Mathf.Clamp01(
                    normalizedDistance
                );


            float distanceScore =
                1f -
                normalizedDistance;


            // -------------------------------------------------
            // Relief score
            // -------------------------------------------------

            float reliefScore =
                Mathf.Clamp01(
                    relief /
                    Mathf.Max(
                        minimumMountainRelief * 4f,
                        0.01f
                    )
                );


            // -------------------------------------------------
            // Combined score
            // -------------------------------------------------

            float score =
                distanceScore * 0.35f +
                reliefScore * 0.65f;


            // -------------------------------------------------
            // Best mountain
            // -------------------------------------------------

            if (score > bestScore)
            {
                bestScore =
                    score;

                bestMountain =
                    mountain;
            }
        }


        return bestMountain;
    }


    // =========================================================
    // CALCULATE LOCAL TERRAIN RELIEF
    // =========================================================

    private float CalculateLocalRelief(
        Vector3 worldPosition,
        Terrain terrain,
        float mountainRadius)
    {
        if (terrain == null)
            return 0f;


        TerrainData terrainData =
            terrain.terrainData;

        if (terrainData == null)
            return 0f;


        // -----------------------------------------------------
        // Height at ray hit
        // -----------------------------------------------------

        float hitHeight =
            terrain.SampleHeight(
                worldPosition
            ) +
            terrain.transform.position.y;


        // -----------------------------------------------------
        // Determine sampling distance
        // -----------------------------------------------------

        float sampleDistance =
            reliefSampleDistance;


        if (mountainRadius > 0f)
        {
            sampleDistance =
                Mathf.Min(
                    sampleDistance,
                    Mathf.Max(
                        mountainRadius * 0.25f,
                        1f
                    )
                );
        }


        sampleDistance =
            Mathf.Max(
                sampleDistance,
                0.5f
            );


        // -----------------------------------------------------
        // Number of surrounding samples
        // -----------------------------------------------------

        int samples =
            Mathf.Max(
                reliefSampleCount,
                4
            );


        float totalHeight =
            0f;

        float minimumHeight =
            float.MaxValue;


        // IMPORTANT:
        // Count only samples that are actually inside
        // the terrain.
        int validSampleCount =
            0;


        // =====================================================
        // SAMPLE SURROUNDING TERRAIN
        // =====================================================

        for (int i = 0; i < samples; i++)
        {
            float angle =
                (360f / samples) * i;

            float radians =
                angle * Mathf.Deg2Rad;


            Vector3 offset =
                new Vector3(
                    Mathf.Cos(radians) *
                    sampleDistance,

                    0f,

                    Mathf.Sin(radians) *
                    sampleDistance
                );


            Vector3 sampleWorld =
                worldPosition +
                offset;


            // -------------------------------------------------
            // Convert sample to local terrain coordinates
            // -------------------------------------------------

            Vector3 sampleLocal =
                terrain.transform.InverseTransformPoint(
                    sampleWorld
                );


            // -------------------------------------------------
            // Ignore samples outside terrain
            // -------------------------------------------------

            if (sampleLocal.x < 0f ||
                sampleLocal.z < 0f ||
                sampleLocal.x > terrainData.size.x ||
                sampleLocal.z > terrainData.size.z)
            {
                continue;
            }


            // -------------------------------------------------
            // Get terrain height
            // -------------------------------------------------

            float sampleHeight =
                terrain.SampleHeight(
                    sampleWorld
                ) +
                terrain.transform.position.y;


            totalHeight +=
                sampleHeight;


            if (sampleHeight < minimumHeight)
            {
                minimumHeight =
                    sampleHeight;
            }


            // -------------------------------------------------
            // Count valid sample
            // -------------------------------------------------

            validSampleCount++;
        }


        // -----------------------------------------------------
        // No valid samples
        // -----------------------------------------------------

        if (validSampleCount == 0)
            return 0f;


        // -----------------------------------------------------
        // Average surrounding height
        // -----------------------------------------------------

        float averageHeight =
            totalHeight /
            validSampleCount;


        // -----------------------------------------------------
        // Calculate relief
        // -----------------------------------------------------

        float averageRelief =
            hitHeight -
            averageHeight;


        float minimumRelief =
            hitHeight -
            minimumHeight;


        // -----------------------------------------------------
        // Return stronger of the two measurements
        // -----------------------------------------------------

        return Mathf.Max(
            averageRelief,
            minimumRelief * 0.5f
        );
    }


    // =========================================================
    // RELEASE STALE SELECTION (Round 2)
    // =========================================================

    // In Round 1 a mountain stayed selected after the ray moved away,
    // so "make this mountain taller" changed a mountain that was no
    // longer pointed at. The selection is now kept only briefly, which
    // still allows small hand movements while speaking.
    private void ReleaseStaleSelection()
    {
        if (!hasSelectedMountain || isPointingAtMountain || selectionHoldTime < 0f)
            return;

        if (Time.time - lastTimeOnMountain < selectionHoldTime)
            return;

        if (debugSelection)
        {
            Debug.Log(
                "[MountainPointingDetector] Ray left mountain ID " +
                selectedMountainId +
                " for " + selectionHoldTime.ToString("F1") +
                " s. Selection cleared."
            );
        }

        ClearSelection();
    }


    // =========================================================
    // VALIDATE SELECTED MOUNTAIN
    // =========================================================

    private void ValidateSelectedMountain()
    {
        if (!hasSelectedMountain)
            return;

        if (terrainGenerator == null)
            return;


        // -----------------------------------------------------
        // Check whether selected mountain still exists
        // -----------------------------------------------------

        if (!terrainGenerator.HasMountain(
                selectedMountainId))
        {
            if (debugSelection)
            {
                Debug.Log(
                    "[MountainPointingDetector] " +
                    "Selected mountain ID " +
                    selectedMountainId +
                    " no longer exists. " +
                    "Selection cleared."
                );
            }


            ClearSelection();
        }
    }


    // =========================================================
    // PUBLIC: CURRENT POINTING
    // =========================================================

    public bool IsPointingAtMountain()
    {
        return isPointingAtMountain;
    }


    public int GetPointedMountainId()
    {
        return pointedMountainId;
    }


    public bool TryGetPointedMountainId(
        out int mountainId)
    {
        mountainId =
            pointedMountainId;

        return
            isPointingAtMountain &&
            pointedMountainId >= 0;
    }


    // =========================================================
    // PUBLIC: SELECTED MOUNTAIN
    // =========================================================

    public int GetSelectedMountainId()
    {
        if (!hasSelectedMountain)
            return -1;

        return selectedMountainId;
    }


    public bool TryGetSelectedMountainId(
        out int mountainId)
    {
        mountainId =
            selectedMountainId;

        return
            hasSelectedMountain &&
            selectedMountainId >= 0;
    }


    public bool HasSelectedMountain()
    {
        return
            hasSelectedMountain &&
            selectedMountainId >= 0;
    }


    public TerrainGenerator.MountainInfo
        GetSelectedMountain()
    {
        if (!hasSelectedMountain)
            return null;

        if (terrainGenerator == null)
            return null;


        TerrainGenerator.MountainInfo mountain;


        if (terrainGenerator.TryGetMountainById(
                selectedMountainId,
                out mountain))
        {
            return mountain;
        }


        return null;
    }


    // =========================================================
    // CLEAR SELECTION
    // =========================================================

    public void ClearSelection()
    {
        selectedMountainId =
            -1;

        hasSelectedMountain =
            false;


        pointedMountainId =
            -1;

        isPointingAtMountain =
            false;


        lastMountainHitPoint =
            Vector3.zero;

        hasMountainHitPoint =
            false;
    }


    // =========================================================
    // LAST MOUNTAIN HIT POINT
    // =========================================================

    public Vector3 GetLastMountainHitPoint()
    {
        return lastMountainHitPoint;
    }


    public bool TryGetLastMountainHitPoint(
        out Vector3 position)
    {
        position =
            lastMountainHitPoint;

        return hasMountainHitPoint;
    }


    // =========================================================
    // DEBUG STATE
    // =========================================================

    public string GetDebugState()
    {
        string pointed =
            isPointingAtMountain
                ? pointedMountainId.ToString()
                : "NONE";


        string selected =
            hasSelectedMountain
                ? selectedMountainId.ToString()
                : "NONE";


        return
            "Pointed Mountain ID: " +
            pointed +

            "\nSelected Mountain ID: " +
            selected +

            "\nHas Selected Mountain: " +
            hasSelectedMountain +

            "\nIs Pointing At Mountain: " +
            isPointingAtMountain;
    }
}