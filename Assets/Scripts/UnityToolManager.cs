using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates and manages Unity geometry generated from AI commands.
///
/// Supports:
/// - Primitive objects
/// - Architectural walls
/// - Clearing generated house
///
/// Uses the shared ToolArguments and WallPoint classes
/// from AIToolData.cs.
/// </summary>
public class UnityToolManager : MonoBehaviour
{
    [Header("Generated House")]
    [SerializeField]
    private Transform generatedHouseParent;

    [Header("Wall Material")]
    [SerializeField]
    private Material wallMaterial;

    [Header("Default Wall Settings")]
    [SerializeField]
    private float defaultWallHeight = 2.7f;

    [SerializeField]
    private float defaultWallThickness = 0.15f;

    private readonly List<GameObject> generatedObjects =
        new List<GameObject>();


    // =========================================================
    // TOOL EXECUTION
    // =========================================================

    public void ExecuteTool(
        string tool,
        ToolArguments args
    )
    {
        if (string.IsNullOrEmpty(tool))
        {
            Debug.LogWarning(
                "UnityToolManager: Tool name is empty."
            );

            return;
        }

        if (args == null)
        {
            Debug.LogWarning(
                "UnityToolManager: Tool arguments are null."
            );

            return;
        }

        switch (tool)
        {
            case "create_primitive":

                CreatePrimitive(args);

                break;


            case "create_wall":

                CreateWallFromArguments(args);

                break;


            case "clear_house":

                ClearGeneratedHouse();

                break;


            default:

                Debug.LogWarning(
                    "UnityToolManager: Unknown tool: " +
                    tool
                );

                break;
        }
    }


    // =========================================================
    // PRIMITIVE CREATION
    // =========================================================

    private void CreatePrimitive(
        ToolArguments args
    )
    {
        PrimitiveType primitiveType;

        switch (
            (args.objectType ?? "").ToLower()
        )
        {
            case "cube":

                primitiveType =
                    PrimitiveType.Cube;

                break;


            case "sphere":

                primitiveType =
                    PrimitiveType.Sphere;

                break;


            case "cylinder":

                primitiveType =
                    PrimitiveType.Cylinder;

                break;


            case "capsule":

                primitiveType =
                    PrimitiveType.Capsule;

                break;


            case "plane":

                primitiveType =
                    PrimitiveType.Plane;

                break;


            case "quad":

                primitiveType =
                    PrimitiveType.Quad;

                break;


            default:

                Debug.LogWarning(
                    "Unknown primitive: " +
                    args.objectType
                );

                return;
        }


        GameObject obj =
            GameObject.CreatePrimitive(
                primitiveType
            );


        obj.name =
            "AI_" +
            args.objectType;


        obj.transform.position =
            args.position;


        obj.transform.rotation =
            Quaternion.Euler(
                args.rotation
            );


        obj.transform.localScale =
            args.scale == Vector3.zero
                ? Vector3.one
                : args.scale;


        ApplyColor(
            obj,
            args.color
        );


        SetParent(
            obj,
            generatedHouseParent
        );


        generatedObjects.Add(obj);


        Debug.Log(
            "Created primitive: " +
            obj.name
        );
    }


    // =========================================================
    // WALL CREATION FROM AI TOOL
    // =========================================================

    private void CreateWallFromArguments(
        ToolArguments args
    )
    {
        CreateWall(
            "AI Wall",

            new WallPoint
            {
                x = args.startX,
                z = args.startZ
            },

            new WallPoint
            {
                x = args.endX,
                z = args.endZ
            },

            args.height > 0
                ? args.height
                : defaultWallHeight,

            args.thickness > 0
                ? args.thickness
                : defaultWallThickness,

            generatedHouseParent
        );
    }


    // =========================================================
    // ARCHITECTURAL WALL
    // =========================================================

    public bool CreateWall(
        string wallName,
        WallPoint start,
        WallPoint end,
        float height,
        float thickness,
        Transform parent
    )
    {
        if (start == null ||
            end == null)
        {
            Debug.LogWarning(
                "Cannot create wall: invalid coordinates."
            );

            return false;
        }


        Vector3 startPosition =
            new Vector3(
                start.x,
                0f,
                start.z
            );


        Vector3 endPosition =
            new Vector3(
                end.x,
                0f,
                end.z
            );


        Vector3 direction =
            endPosition -
            startPosition;


        float length =
            direction.magnitude;


        if (length < 0.05f)
        {
            Debug.LogWarning(
                "Wall is too short: " +
                wallName
            );

            return false;
        }


        Vector3 midpoint =
            (startPosition +
             endPosition) *
            0.5f;


        GameObject wall =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );


        wall.name =
            string.IsNullOrEmpty(wallName)
                ? "AI_Wall"
                : wallName;


        wall.transform.position =
            midpoint +
            Vector3.up *
            (height * 0.5f);


        wall.transform.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );


        wall.transform.localScale =
            new Vector3(
                thickness,
                height,
                length
            );


        SetParent(
            wall,
            parent
        );


        ApplyWallMaterial(
            wall
        );


        generatedObjects.Add(
            wall
        );


        Debug.Log(
            "Created wall: " +
            wall.name +
            " | Length: " +
            length.ToString("F2") +
            "m"
        );


        return true;
    }


    // =========================================================
    // PARENTING
    // =========================================================

    private void SetParent(
        GameObject obj,
        Transform parent
    )
    {
        if (obj == null)
            return;

        if (parent != null)
        {
            obj.transform.SetParent(
                parent,
                true
            );
        }
    }


    // =========================================================
    // WALL MATERIAL
    // =========================================================

    private void ApplyWallMaterial(
        GameObject wall
    )
    {
        Renderer renderer =
            wall.GetComponent<Renderer>();


        if (renderer == null)
            return;


        if (wallMaterial != null)
        {
            renderer.material =
                wallMaterial;

            return;
        }


        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Standard"
                );
        }


        if (shader == null)
        {
            Debug.LogWarning(
                "Could not find a suitable shader."
            );

            return;
        }


        Material material =
            new Material(shader);


        material.color =
            new Color(
                0.75f,
                0.75f,
                0.75f
            );


        renderer.material =
            material;
    }


    // =========================================================
    // PRIMITIVE COLOR
    // =========================================================

    private void ApplyColor(
        GameObject obj,
        string hexColor
    )
    {
        if (obj == null)
            return;


        if (string.IsNullOrEmpty(hexColor))
            return;


        Renderer renderer =
            obj.GetComponent<Renderer>();


        if (renderer == null)
            return;


        if (ColorUtility.TryParseHtmlString(
            hexColor,
            out Color color))
        {
            renderer.material.color =
                color;
        }
    }


    // =========================================================
    // CLEAR HOUSE
    // =========================================================

    public void ClearGeneratedHouse()
    {
        for (
            int i = generatedObjects.Count - 1;
            i >= 0;
            i--
        )
        {
            GameObject obj =
                generatedObjects[i];


            if (obj != null)
            {
                Destroy(obj);
            }
        }


        generatedObjects.Clear();


        Debug.Log(
            "UnityToolManager: Generated house cleared."
        );
    }
}