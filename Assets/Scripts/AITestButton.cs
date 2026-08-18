using UnityEngine;

// ============================================================
// ENVIRONMENT MANAGER
// ============================================================
//
// Responsibilities:
//
// - Modify generated terrain appearance
// - Make terrain greener
// - Make terrain less green
// - Make environment brighter
// - Make environment darker
// - Make environment warmer
// - Make environment cooler
// - Stronger shadows
// - Softer shadows
// - Add fog
// - Remove fog
// - Reset environment
//
// IMPORTANT
// ----------
//
// Terrain is generated dynamically.
//
// Therefore this manager does NOT require a Terrain
// to already exist when the scene starts.
//
// It uses the same approach as TreeManager:
//
//     GetTerrain()
//          ↓
//     TerrainGenerator
//          ↓
//     GetGeneratedTerrain()
//
// If the terrain does not exist yet, environment commands
// simply wait until the terrain becomes available.
//
// Architecture:
//
// DemoChat
//    ↓
// EnvironmentCommand
//    ↓
// EnvironmentManager
//    ↓
// GetTerrain()
//    ↓
// TerrainGenerator
//    ↓
// Generated Terrain
//
// ============================================================

public class EnvironmentManager : MonoBehaviour
{
    // =========================================================
    // TERRAIN GENERATOR
    // =========================================================

    [Header("Terrain Generator")]

    [Tooltip(
        "TerrainGenerator responsible for creating the runtime terrain."
    )]
    [SerializeField]
    private TerrainGenerator terrainGenerator;


    // =========================================================
    // TERRAIN
    // =========================================================

    [Header("Terrain")]

    [Tooltip(
        "Runtime generated terrain. " +
        "This is automatically found from TerrainGenerator."
    )]
    [SerializeField]
    private Terrain terrain;


    // =========================================================
    // ORIGINAL TERRAIN COLOUR
    // =========================================================

    private Color originalTerrainColor =
        Color.white;

    private bool originalTerrainColorCaptured =
        false;


    // =========================================================
    // SUN / LIGHT
    // =========================================================

    [Header("Sun / Directional Light")]

    [SerializeField]
    private Light sunLight;


    // =========================================================
    // ORIGINAL SUN VALUES
    // =========================================================

    private float originalSunIntensity =
        1f;

    private Color originalSunColor =
        Color.white;

    private Quaternion originalSunRotation =
        Quaternion.identity;

    private bool originalSunCaptured =
        false;


    // =========================================================
    // ENVIRONMENT
    // =========================================================

    [Header("Environment")]

    [SerializeField]
    private Color environmentTint =
        Color.white;

    [SerializeField]
    private float greenAmount =
        0f;

    [SerializeField]
    private float brightness =
        1f;

    [SerializeField]
    private float warmth =
        0f;


    // =========================================================
    // FOG
    // =========================================================

    [Header("Fog")]

    [SerializeField]
    private float defaultFogDensity =
        0.01f;

    [SerializeField]
    private Color defaultFogColor =
        new Color(
            0.75f,
            0.80f,
            0.85f
        );


    // =========================================================
    // LIMITS
    // =========================================================

    private const float MIN_BRIGHTNESS =
        0.30f;

    private const float MAX_BRIGHTNESS =
        2.00f;

    private const float MIN_GREEN =
        -1.00f;

    private const float MAX_GREEN =
        1.00f;

    private const float MIN_WARMTH =
        -1.00f;

    private const float MAX_WARMTH =
        1.00f;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // -----------------------------------------------------
        // Find TerrainGenerator if not assigned.
        // -----------------------------------------------------

        FindTerrainGenerator();

        // -----------------------------------------------------
        // Find directional light.
        // -----------------------------------------------------

        FindSun();

        // -----------------------------------------------------
        // Capture sun.
        // -----------------------------------------------------

        CaptureSunOriginalValues();

        // -----------------------------------------------------
        // Try to find terrain.
        //
        // It may not exist yet.
        // -----------------------------------------------------

        Terrain currentTerrain =
            GetTerrain();

        if (currentTerrain != null)
        {
            CaptureTerrainOriginalColor();

            ApplyCurrentEnvironment();
        }

        Debug.Log(
            "[EnvironmentManager] Start complete."
        );
    }


    // =========================================================
    // FIND TERRAIN GENERATOR
    // =========================================================

    private void FindTerrainGenerator()
    {
        if (terrainGenerator != null)
        {
            return;
        }

        terrainGenerator =
            FindFirstObjectByType<TerrainGenerator>();

        if (terrainGenerator != null)
        {
            Debug.Log(
                "[EnvironmentManager] " +
                "TerrainGenerator found."
            );
        }
        else
        {
            Debug.LogWarning(
                "[EnvironmentManager] " +
                "TerrainGenerator not found."
            );
        }
    }


    // =========================================================
    // FIND SUN
    // =========================================================

    private void FindSun()
    {
        if (sunLight != null)
        {
            return;
        }

        Light[] lights =
            FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        foreach (Light light in lights)
        {
            if (light == null)
            {
                continue;
            }

            if (light.type == LightType.Directional)
            {
                sunLight =
                    light;

                Debug.Log(
                    "[EnvironmentManager] " +
                    "Directional light found: " +
                    light.name
                );

                return;
            }
        }

        Debug.LogWarning(
            "[EnvironmentManager] " +
            "No directional light found."
        );
    }


    // =========================================================
    // GET TERRAIN
    // =========================================================
    //
    // Same approach as TreeManager.
    //
    // Priority:
    //
    // 1. Assigned terrain
    // 2. TerrainGenerator generated terrain
    // 3. Active terrain
    // 4. Search scene
    //
    // =========================================================

    private Terrain GetTerrain()
    {
        // -----------------------------------------------------
        // ASSIGNED TERRAIN
        // -----------------------------------------------------

        if (
            terrain != null &&
            terrain.terrainData != null
        )
        {
            EnsureTerrainOriginalColorCaptured();

            return terrain;
        }


        // -----------------------------------------------------
        // TERRAIN GENERATOR
        // -----------------------------------------------------

        if (terrainGenerator != null)
        {
            Terrain generatedTerrain =
                terrainGenerator.GetGeneratedTerrain();

            if (
                generatedTerrain != null &&
                generatedTerrain.terrainData != null
            )
            {
                terrain =
                    generatedTerrain;

                EnsureTerrainOriginalColorCaptured();

                Debug.Log(
                    "[EnvironmentManager] " +
                    "Generated terrain found: " +
                    terrain.name
                );

                return generatedTerrain;
            }
        }


        // -----------------------------------------------------
        // ACTIVE TERRAIN
        // -----------------------------------------------------

        Terrain activeTerrain =
            Terrain.activeTerrain;

        if (
            activeTerrain != null &&
            activeTerrain.terrainData != null
        )
        {
            terrain =
                activeTerrain;

            EnsureTerrainOriginalColorCaptured();

            return activeTerrain;
        }


        // -----------------------------------------------------
        // SEARCH SCENE
        // -----------------------------------------------------

        Terrain[] terrains =
            FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        if (terrains != null)
        {
            foreach (
                Terrain foundTerrain
                in terrains
            )
            {
                if (
                    foundTerrain != null &&
                    foundTerrain.terrainData != null &&
                    foundTerrain.gameObject.activeInHierarchy
                )
                {
                    terrain =
                        foundTerrain;

                    EnsureTerrainOriginalColorCaptured();

                    return foundTerrain;
                }
            }
        }


        // -----------------------------------------------------
        // NO TERRAIN YET
        // -----------------------------------------------------

        return null;
    }


    // =========================================================
    // ENSURE ORIGINAL TERRAIN COLOUR
    // =========================================================

    private void EnsureTerrainOriginalColorCaptured()
    {
        if (originalTerrainColorCaptured)
        {
            return;
        }

        CaptureTerrainOriginalColor();
    }


    // =========================================================
    // CAPTURE TERRAIN ORIGINAL COLOUR
    // =========================================================

    private void CaptureTerrainOriginalColor()
    {
        if (terrain == null)
        {
            return;
        }

        Material terrainMaterial =
            terrain.materialTemplate;

        if (terrainMaterial == null)
        {
            Debug.LogWarning(
                "[EnvironmentManager] " +
                "Terrain has no material template."
            );

            return;
        }


        // -----------------------------------------------------
        // URP
        // -----------------------------------------------------

        if (
            terrainMaterial.HasProperty(
                "_BaseColor"
            )
        )
        {
            originalTerrainColor =
                terrainMaterial.GetColor(
                    "_BaseColor"
                );

            originalTerrainColorCaptured =
                true;

            Debug.Log(
                "[EnvironmentManager] " +
                "Captured terrain original _BaseColor."
            );

            return;
        }


        // -----------------------------------------------------
        // BUILT-IN
        // -----------------------------------------------------

        if (
            terrainMaterial.HasProperty(
                "_Color"
            )
        )
        {
            originalTerrainColor =
                terrainMaterial.GetColor(
                    "_Color"
                );

            originalTerrainColorCaptured =
                true;

            Debug.Log(
                "[EnvironmentManager] " +
                "Captured terrain original _Color."
            );

            return;
        }


        Debug.LogWarning(
            "[EnvironmentManager] " +
            "Could not find terrain colour property."
        );
    }


    // =========================================================
    // CAPTURE SUN ORIGINAL VALUES
    // =========================================================

    private void CaptureSunOriginalValues()
    {
        if (sunLight == null)
        {
            return;
        }

        originalSunIntensity =
            sunLight.intensity;

        originalSunColor =
            sunLight.color;

        originalSunRotation =
            sunLight.transform.rotation;

        originalSunCaptured =
            true;
    }


    // =========================================================
    // APPLY CURRENT ENVIRONMENT
    // =========================================================

    private void ApplyCurrentEnvironment()
    {
        ApplyTerrainGreen();

        ApplyLighting();

        ApplyEnvironmentTint();
    }


    // =========================================================
    // PUBLIC ENVIRONMENT COMMAND
    // =========================================================

    public bool ApplyEnvironmentCommand(
        string action,
        float amount
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                action
            )
        )
        {
            Debug.LogWarning(
                "[EnvironmentManager] " +
                "Empty environment action."
            );

            return false;
        }


        action =
            action.Trim()
                  .ToLowerInvariant();


        Debug.Log(
            "[EnvironmentManager] " +
            "Action = " +
            action +
            " | Amount = " +
            amount
        );


        // =====================================================
        // GREENER
        // =====================================================

        if (
            action == "greener" ||
            action == "more_green" ||
            action == "make_green" ||
            action == "increase_green"
        )
        {
            float change =
                amount <= 0f
                    ? 0.25f
                    : amount;

            greenAmount =
                Mathf.Clamp(
                    greenAmount + change,
                    MIN_GREEN,
                    MAX_GREEN
                );

            ApplyTerrainGreen();

            return true;
        }


        // =====================================================
        // LESS GREEN
        // =====================================================

        if (
            action == "less_green" ||
            action == "reduce_green" ||
            action == "less_greenery"
        )
        {
            float change =
                amount <= 0f
                    ? 0.25f
                    : amount;

            greenAmount =
                Mathf.Clamp(
                    greenAmount - change,
                    MIN_GREEN,
                    MAX_GREEN
                );

            ApplyTerrainGreen();

            return true;
        }


        // =====================================================
        // BRIGHTER
        // =====================================================

        if (
            action == "brighter" ||
            action == "increase_brightness" ||
            action == "more_light"
        )
        {
            float change =
                amount <= 0f
                    ? 0.20f
                    : amount;

            brightness =
                Mathf.Clamp(
                    brightness + change,
                    MIN_BRIGHTNESS,
                    MAX_BRIGHTNESS
                );

            ApplyLighting();

            return true;
        }


        // =====================================================
        // DARKER
        // =====================================================

        if (
            action == "darker" ||
            action == "decrease_brightness" ||
            action == "less_light"
        )
        {
            float change =
                amount <= 0f
                    ? 0.20f
                    : amount;

            brightness =
                Mathf.Clamp(
                    brightness - change,
                    MIN_BRIGHTNESS,
                    MAX_BRIGHTNESS
                );

            ApplyLighting();

            return true;
        }


        // =====================================================
        // WARMER
        // =====================================================

        if (
            action == "warmer" ||
            action == "warm"
        )
        {
            float change =
                amount <= 0f
                    ? 0.20f
                    : amount;

            warmth =
                Mathf.Clamp(
                    warmth + change,
                    MIN_WARMTH,
                    MAX_WARMTH
                );

            ApplyEnvironmentTint();

            return true;
        }


        // =====================================================
        // COOLER
        // =====================================================

        if (
            action == "cooler" ||
            action == "cool"
        )
        {
            float change =
                amount <= 0f
                    ? 0.20f
                    : amount;

            warmth =
                Mathf.Clamp(
                    warmth - change,
                    MIN_WARMTH,
                    MAX_WARMTH
                );

            ApplyEnvironmentTint();

            return true;
        }


        // =====================================================
        // STRONGER SHADOWS
        // =====================================================

        if (
            action == "stronger_shadows" ||
            action == "more_shadows"
        )
        {
            if (sunLight == null)
            {
                Debug.LogWarning(
                    "[EnvironmentManager] " +
                    "No directional light found."
                );

                return false;
            }

            sunLight.shadows =
                LightShadows.Soft;

            sunLight.shadowStrength =
                0.90f;

            return true;
        }


        // =====================================================
        // SOFTER SHADOWS
        // =====================================================

        if (
            action == "softer_shadows" ||
            action == "less_shadows"
        )
        {
            if (sunLight == null)
            {
                return false;
            }

            sunLight.shadows =
                LightShadows.Soft;

            sunLight.shadowStrength =
                0.40f;

            return true;
        }


        // =====================================================
        // ADD FOG
        // =====================================================

        if (
            action == "add_fog" ||
            action == "fog"
        )
        {
            RenderSettings.fog =
                true;

            RenderSettings.fogMode =
                FogMode.ExponentialSquared;

            RenderSettings.fogDensity =
                defaultFogDensity;

            RenderSettings.fogColor =
                defaultFogColor;

            return true;
        }


        // =====================================================
        // REMOVE FOG
        // =====================================================

        if (
            action == "remove_fog" ||
            action == "no_fog"
        )
        {
            RenderSettings.fog =
                false;

            return true;
        }


        // =====================================================
        // RESET
        // =====================================================

        if (
            action == "reset" ||
            action == "reset_environment"
        )
        {
            ResetEnvironment();

            return true;
        }


        // =====================================================
        // UNKNOWN
        // =====================================================

        Debug.LogWarning(
            "[EnvironmentManager] " +
            "Unknown action: " +
            action
        );

        return false;
    }


    // =========================================================
    // APPLY TERRAIN GREEN
    // =========================================================

    private void ApplyTerrainGreen()
    {
        Terrain currentTerrain =
            GetTerrain();

        if (currentTerrain == null)
        {
            Debug.LogWarning(
                "[EnvironmentManager] " +
                "Terrain has not been generated yet."
            );

            return;
        }


        Material material =
            currentTerrain.materialTemplate;

        if (material == null)
        {
            Debug.LogWarning(
                "[EnvironmentManager] " +
                "Terrain has no material template."
            );

            return;
        }


        // -----------------------------------------------------
        // Original colour
        // -----------------------------------------------------

        Color baseColor =
            originalTerrainColorCaptured
                ? originalTerrainColor
                : Color.white;


        // -----------------------------------------------------
        // GREEN
        // -----------------------------------------------------

        float green =
            Mathf.Clamp01(
                baseColor.g +
                greenAmount
            );


        // -----------------------------------------------------
        // RED
        // -----------------------------------------------------

        float red =
            Mathf.Clamp01(
                baseColor.r -
                greenAmount * 0.25f
            );


        // -----------------------------------------------------
        // BLUE
        // -----------------------------------------------------

        float blue =
            Mathf.Clamp01(
                baseColor.b
            );


        Color newColor =
            new Color(
                red,
                green,
                blue,
                baseColor.a
            );


        // -----------------------------------------------------
        // URP
        // -----------------------------------------------------

        if (
            material.HasProperty(
                "_BaseColor"
            )
        )
        {
            material.SetColor(
                "_BaseColor",
                newColor
            );

            return;
        }


        // -----------------------------------------------------
        // BUILT-IN
        // -----------------------------------------------------

        if (
            material.HasProperty(
                "_Color"
            )
        )
        {
            material.SetColor(
                "_Color",
                newColor
            );

            return;
        }


        Debug.LogWarning(
            "[EnvironmentManager] " +
            "Terrain material has no supported colour property."
        );
    }


    // =========================================================
    // APPLY LIGHTING
    // =========================================================

    private void ApplyLighting()
    {
        if (sunLight == null)
        {
            return;
        }

        if (!originalSunCaptured)
        {
            CaptureSunOriginalValues();
        }

        if (!originalSunCaptured)
        {
            return;
        }

        sunLight.intensity =
            originalSunIntensity *
            brightness;
    }


    // =========================================================
    // APPLY ENVIRONMENT TINT
    // =========================================================

    private void ApplyEnvironmentTint()
    {
        if (sunLight == null)
        {
            return;
        }

        if (!originalSunCaptured)
        {
            CaptureSunOriginalValues();
        }

        if (!originalSunCaptured)
        {
            return;
        }


        Color baseColor =
            originalSunColor;


        // -----------------------------------------------------
        // Positive warmth:
        // more red / less blue
        //
        // Negative warmth:
        // more blue / less red
        // -----------------------------------------------------

        float red =
            baseColor.r +
            warmth * 0.20f;

        float green =
            baseColor.g;

        float blue =
            baseColor.b -
            warmth * 0.20f;


        sunLight.color =
            new Color(
                Mathf.Clamp01(red),
                Mathf.Clamp01(green),
                Mathf.Clamp01(blue),
                baseColor.a
            );
    }


    // =========================================================
    // RESET ENVIRONMENT
    // =========================================================

    public void ResetEnvironment()
    {
        greenAmount =
            0f;

        brightness =
            1f;

        warmth =
            0f;


        // -----------------------------------------------------
        // TERRAIN
        // -----------------------------------------------------

        Terrain currentTerrain =
            GetTerrain();

        if (
            currentTerrain != null &&
            originalTerrainColorCaptured
        )
        {
            Material material =
                currentTerrain.materialTemplate;

            if (material != null)
            {
                if (
                    material.HasProperty(
                        "_BaseColor"
                    )
                )
                {
                    material.SetColor(
                        "_BaseColor",
                        originalTerrainColor
                    );
                }
                else if (
                    material.HasProperty(
                        "_Color"
                    )
                )
                {
                    material.SetColor(
                        "_Color",
                        originalTerrainColor
                    );
                }
            }
        }


        // -----------------------------------------------------
        // SUN
        // -----------------------------------------------------

        if (
            sunLight != null &&
            originalSunCaptured
        )
        {
            sunLight.intensity =
                originalSunIntensity;

            sunLight.color =
                originalSunColor;

            sunLight.transform.rotation =
                originalSunRotation;

            sunLight.shadows =
                LightShadows.Soft;

            sunLight.shadowStrength =
                1f;
        }


        // -----------------------------------------------------
        // FOG
        // -----------------------------------------------------

        RenderSettings.fog =
            false;


        Debug.Log(
            "[EnvironmentManager] " +
            "Environment reset."
        );
    }


    // =========================================================
    // PUBLIC INFORMATION
    // =========================================================

    public float GetGreenAmount()
    {
        return greenAmount;
    }


    public float GetBrightness()
    {
        return brightness;
    }


    public float GetWarmth()
    {
        return warmth;
    }


    public Terrain GetCurrentTerrain()
    {
        return GetTerrain();
    }


    public bool HasTerrain()
    {
        return GetTerrain() != null;
    }
}