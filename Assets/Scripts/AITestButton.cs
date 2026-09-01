using UnityEngine;

// ============================================================
// ENVIRONMENT MANAGER
// ============================================================
//
// Controls the appearance of the dynamically generated
// Unity Terrain.
//
// Responsibilities:
//
// - Automatically find TerrainGenerator
// - Automatically find generated Terrain
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
// Therefore this script does NOT require a Terrain
// to exist when the scene starts.
//
// It repeatedly checks:
//
// EnvironmentManager
//       ↓
// TerrainGenerator
//       ↓
// GetGeneratedTerrain()
//       ↓
// Generated Terrain
//
// ============================================================

public class EnvironmentManager : MonoBehaviour
{
    // =========================================================
    // TERRAIN GENERATOR
    // =========================================================

    [Header("Terrain Generator")]

    [SerializeField]
    private TerrainGenerator terrainGenerator;


    // =========================================================
    // CURRENT GENERATED TERRAIN
    // =========================================================

    [Header("Runtime Terrain")]

    [SerializeField]
    private Terrain terrain;


    // =========================================================
    // TERRAIN LAYER
    // =========================================================

    [Header("Terrain Appearance")]

    [Tooltip(
        "Colour applied to the generated terrain surface."
    )]
    [SerializeField]
    private Color baseTerrainColor =
        new Color(
            0.45f,
            0.55f,
            0.30f,
            1f
        );


    // =========================================================
    // CURRENT GREEN AMOUNT
    // =========================================================

    [SerializeField]
    private float greenAmount = 0f;


    // =========================================================
    // BRIGHTNESS
    // =========================================================

    [Header("Brightness")]

    [SerializeField]
    private float brightness = 1f;


    // =========================================================
    // WARMTH
    // =========================================================

    [Header("Warmth")]

    [SerializeField]
    private float warmth = 0f;


    // =========================================================
    // SUN
    // =========================================================

    [Header("Sun / Directional Light")]

    [SerializeField]
    private Light sunLight;


    // =========================================================
    // ORIGINAL SUN VALUES
    // =========================================================

    private float originalSunIntensity = 1f;

    private Color originalSunColor =
        Color.white;

    private Quaternion originalSunRotation =
        Quaternion.identity;

    private LightShadows originalShadowType =
        LightShadows.Soft;

    private float originalShadowStrength =
        1f;

    private bool originalSunCaptured =
        false;


    // =========================================================
    // ORIGINAL TERRAIN LAYERS
    // =========================================================

    private TerrainLayer[] originalTerrainLayers;

    private bool originalTerrainCaptured =
        false;


    // =========================================================
    // LIMITS
    // =========================================================

    private const float MIN_GREEN =
        -1f;

    private const float MAX_GREEN =
        1f;

    private const float MIN_BRIGHTNESS =
        0.3f;

    private const float MAX_BRIGHTNESS =
        2f;

    private const float MIN_WARMTH =
        -1f;

    private const float MAX_WARMTH =
        1f;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        Debug.Log(
            "[EnvironmentManager] Starting."
        );

        FindTerrainGenerator();

        FindSun();

        CaptureSunOriginalValues();

        // Terrain may not exist yet.
        //
        // Try once immediately.
        RefreshTerrain();

        Debug.Log(
            "[EnvironmentManager] Start complete."
        );
    }


    // =========================================================
    // UPDATE
    // =========================================================
    //
    // IMPORTANT
    //
    // TerrainGenerator may generate terrain AFTER
    // EnvironmentManager.Start().
    //
    // Therefore we check for it continuously.
    //
    // =========================================================

    private void Update()
    {
        Terrain generatedTerrain =
            GetTerrain();

        if (generatedTerrain == null)
        {
            return;
        }

        // -----------------------------------------------------
        // If TerrainGenerator generated a new Terrain,
        // automatically reconnect.
        // -----------------------------------------------------

        if (terrain != generatedTerrain)
        {
            terrain =
                generatedTerrain;

            originalTerrainCaptured =
                false;

            CaptureOriginalTerrainLayers();

            Debug.Log(
                "[EnvironmentManager] " +
                "New generated terrain detected: " +
                terrain.name
            );

            ApplyCurrentEnvironment();
        }
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
                "TerrainGenerator found: " +
                terrainGenerator.name
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

            if (light.type ==
                LightType.Directional)
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
    // REFRESH TERRAIN
    // =========================================================

    private void RefreshTerrain()
    {
        Terrain currentTerrain =
            GetTerrain();

        if (currentTerrain == null)
        {
            Debug.Log(
                "[EnvironmentManager] " +
                "Terrain not generated yet."
            );

            return;
        }

        if (terrain != currentTerrain)
        {
            terrain =
                currentTerrain;

            originalTerrainCaptured =
                false;

            CaptureOriginalTerrainLayers();
        }

        ApplyCurrentEnvironment();
    }


    // =========================================================
    // GET TERRAIN
    // =========================================================
    //
    // Priority:
    //
    // 1. TerrainGenerator
    // 2. Assigned Terrain
    // 3. Terrain.activeTerrain
    // 4. Scene search
    //
    // =========================================================

    private Terrain GetTerrain()
    {
        // -----------------------------------------------------
        // TERRAIN GENERATOR
        // -----------------------------------------------------

        if (terrainGenerator == null)
        {
            FindTerrainGenerator();
        }

        if (terrainGenerator != null)
        {
            Terrain generatedTerrain =
                terrainGenerator.GetGeneratedTerrain();

            if (
                generatedTerrain != null &&
                generatedTerrain.terrainData != null
            )
            {
                return generatedTerrain;
            }
        }


        // -----------------------------------------------------
        // ASSIGNED TERRAIN
        // -----------------------------------------------------

        if (
            terrain != null &&
            terrain.terrainData != null
        )
        {
            return terrain;
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
                    return foundTerrain;
                }
            }
        }


        return null;
    }


    // =========================================================
    // CAPTURE ORIGINAL TERRAIN LAYERS
    // =========================================================

    private void CaptureOriginalTerrainLayers()
    {
        Terrain currentTerrain =
            GetTerrain();

        if (currentTerrain == null)
        {
            return;
        }

        TerrainData data =
            currentTerrain.terrainData;

        if (data == null)
        {
            return;
        }


        TerrainLayer[] layers =
            data.terrainLayers;

        if (
            layers == null ||
            layers.Length == 0
        )
        {
            Debug.Log(
                "[EnvironmentManager] " +
                "Terrain has no TerrainLayers. " +
                "Creating one."
            );

            CreateTerrainLayer();

            layers =
                data.terrainLayers;
        }


        if (
            layers != null &&
            layers.Length > 0
        )
        {
            originalTerrainLayers =
                new TerrainLayer[layers.Length];

            for (
                int i = 0;
                i < layers.Length;
                i++
            )
            {
                originalTerrainLayers[i] =
                    layers[i];
            }

            originalTerrainCaptured =
                true;

            Debug.Log(
                "[EnvironmentManager] " +
                "Captured " +
                layers.Length +
                " terrain layer(s)."
            );
        }
    }


    // =========================================================
    // CREATE TERRAIN LAYER
    // =========================================================
    //
    // Your TerrainGenerator currently does not create
    // TerrainLayers.
    //
    // This creates one automatically at runtime.
    //
    // =========================================================

    private void CreateTerrainLayer()
    {
        Terrain currentTerrain =
            GetTerrain();

        if (currentTerrain == null)
        {
            return;
        }

        TerrainData data =
            currentTerrain.terrainData;

        if (data == null)
        {
            return;
        }


        TerrainLayer layer =
            new TerrainLayer();

        layer.diffuseTexture =
            CreateWhiteTexture();

        layer.tileSize =
            new Vector2(
                20f,
                20f
            );

        layer.tileOffset =
            Vector2.zero;


        data.terrainLayers =
            new TerrainLayer[]
            {
                layer
            };


        Debug.Log(
            "[EnvironmentManager] " +
            "Created runtime TerrainLayer."
        );
    }


    // =========================================================
    // CREATE WHITE TEXTURE
    // =========================================================
    //
    // A white texture allows the TerrainLayer colour to
    // control the visible terrain colour.
    //
    // =========================================================

    private Texture2D CreateWhiteTexture()
    {
        Texture2D texture =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false
            );

        texture.SetPixel(
            0,
            0,
            Color.white
        );

        texture.SetPixel(
            1,
            0,
            Color.white
        );

        texture.SetPixel(
            0,
            1,
            Color.white
        );

        texture.SetPixel(
            1,
            1,
            Color.white
        );

        texture.Apply();

        return texture;
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
                    ? 0.20f
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
                    ? 0.20f
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
                FindSun();
            }

            if (sunLight == null)
            {
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
                FindSun();
            }

            if (sunLight == null)
            {
                return false;
            }

            sunLight.shadows =
                LightShadows.Soft;

            sunLight.shadowStrength =
                0.35f;

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
                0.01f;

            RenderSettings.fogColor =
                new Color(
                    0.75f,
                    0.80f,
                    0.85f
                );

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
                "No generated terrain yet."
            );

            return;
        }


        TerrainData data =
            currentTerrain.terrainData;

        if (data == null)
        {
            return;
        }


        // -----------------------------------------------------
        // Make sure a TerrainLayer exists.
        // -----------------------------------------------------

        TerrainLayer[] layers =
            data.terrainLayers;

        if (
            layers == null ||
            layers.Length == 0
        )
        {
            CreateTerrainLayer();

            layers =
                data.terrainLayers;
        }


        if (
            layers == null ||
            layers.Length == 0
        )
        {
            Debug.LogWarning(
                "[EnvironmentManager] " +
                "Could not create TerrainLayer."
            );

            return;
        }


        // -----------------------------------------------------
        // Calculate colour.
        // -----------------------------------------------------

        Color color =
            baseTerrainColor;


        // More green:
        //
        // greenAmount = +1
        //
        // Less green:
        //
        // greenAmount = -1
        //

        color.r =
            Mathf.Clamp01(
                baseTerrainColor.r -
                greenAmount * 0.25f
            );

        color.g =
            Mathf.Clamp01(
                baseTerrainColor.g +
                greenAmount * 0.45f
            );

        color.b =
            Mathf.Clamp01(
                baseTerrainColor.b -
                greenAmount * 0.15f
            );


        // -----------------------------------------------------
        // Apply to every terrain layer.
        // -----------------------------------------------------

        foreach (
            TerrainLayer layer
            in layers
        )
        {
            if (layer == null)
            {
                continue;
            }

            layer.diffuseRemapMax =
                new Vector4(
                    color.r,
                    color.g,
                    color.b,
                    1f
                );

            // TerrainLayer does not have a universal
            // direct tint property in all Unity versions.
            //
            // Therefore we modify the diffuse texture
            // through the layer's material settings where
            // possible.

            layer.tileSize =
                layer.tileSize == Vector2.zero
                    ? new Vector2(
                        20f,
                        20f
                    )
                    : layer.tileSize;
        }


        // -----------------------------------------------------
        // IMPORTANT
        //
        // Create a colourized texture and assign it.
        // -----------------------------------------------------

        Texture2D colourTexture =
            CreateColourTexture(
                color
            );


        foreach (
            TerrainLayer layer
            in layers
        )
        {
            if (layer == null)
            {
                continue;
            }

            layer.diffuseTexture =
                colourTexture;
        }


        data.terrainLayers =
            layers;


        Debug.Log(
            "[EnvironmentManager] " +
            "Terrain colour updated. " +
            "Green Amount = " +
            greenAmount
        );
    }


    // =========================================================
    // CREATE COLOUR TEXTURE
    // =========================================================

    private Texture2D CreateColourTexture(
        Color color
    )
    {
        Texture2D texture =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false
            );


        texture.SetPixel(
            0,
            0,
            color
        );

        texture.SetPixel(
            1,
            0,
            color
        );

        texture.SetPixel(
            0,
            1,
            color
        );

        texture.SetPixel(
            1,
            1,
            color
        );


        texture.wrapMode =
            TextureWrapMode.Repeat;


        texture.Apply();


        return texture;
    }


    // =========================================================
    // APPLY LIGHTING
    // =========================================================

    private void ApplyLighting()
    {
        if (sunLight == null)
        {
            FindSun();
        }

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
            FindSun();
        }

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
    // CAPTURE SUN
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


        originalShadowType =
            sunLight.shadows;


        originalShadowStrength =
            sunLight.shadowStrength;


        originalSunCaptured =
            true;
    }


    // =========================================================
    // RESET
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


        if (currentTerrain != null)
        {
            TerrainData data =
                currentTerrain.terrainData;


            if (data != null)
            {
                // Recreate a neutral terrain appearance.
                //
                // This is preferable to restoring destroyed
                // runtime TerrainLayer references.

                TerrainLayer[] layers =
                    data.terrainLayers;


                if (
                    layers != null &&
                    layers.Length > 0
                )
                {
                    Texture2D resetTexture =
                        CreateColourTexture(
                            baseTerrainColor
                        );


                    foreach (
                        TerrainLayer layer
                        in layers
                    )
                    {
                        if (layer == null)
                        {
                            continue;
                        }

                        layer.diffuseTexture =
                            resetTexture;
                    }


                    data.terrainLayers =
                        layers;
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
                originalShadowType;


            sunLight.shadowStrength =
                originalShadowStrength;
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