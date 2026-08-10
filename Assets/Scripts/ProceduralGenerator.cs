using UnityEngine;

public class ProceduralGenerator : MonoBehaviour
{
    public void CreatePrimitive(ToolArguments args)
    {
        PrimitiveType primitiveType = PrimitiveType.Cube;

        // Decide which Unity primitive to create
        switch (args.objectType.ToLower())
        {
            case "cube":
                primitiveType = PrimitiveType.Cube;
                break;

            case "sphere":
                primitiveType = PrimitiveType.Sphere;
                break;

            case "cylinder":
                primitiveType = PrimitiveType.Cylinder;
                break;

            case "capsule":
                primitiveType = PrimitiveType.Capsule;
                break;

            case "plane":
                primitiveType = PrimitiveType.Plane;
                break;

            case "quad":
                primitiveType = PrimitiveType.Quad;
                break;

            default:
                Debug.LogWarning(
                    "Unknown primitive: " + args.objectType +
                    ". Creating cube instead."
                );
                break;
        }

        // Create object
        GameObject obj = GameObject.CreatePrimitive(primitiveType);

        // Position
        obj.transform.position = args.position;

        // Rotation
        obj.transform.rotation = Quaternion.Euler(args.rotation);

        // Scale
        obj.transform.localScale = args.scale;

        // Name
        obj.name = args.objectType;

        // Color
        if (!string.IsNullOrEmpty(args.color))
        {
            if (ColorUtility.TryParseHtmlString(args.color, out Color color))
            {
                Renderer renderer = obj.GetComponent<Renderer>();

                if (renderer != null)
                {
                    renderer.material.color = color;
                }
            }
            else
            {
                Debug.LogWarning(
                    "Could not parse color: " + args.color
                );
            }
        }

        Debug.Log(
            $"Created {args.objectType} at {args.position}"
        );
    }

    public void CreateCustomObject(ToolArguments args)
    {
        Debug.Log("Custom object requested");
    }
}