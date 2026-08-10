using UnityEngine;

public class UnityToolManager : MonoBehaviour
{
    public ProceduralGenerator generator;

    public void ExecuteTool(string tool, ToolArguments args)
    {
        switch (tool)
        {
            case "create_primitive":
                generator.CreatePrimitive(args);
                break;

            case "create_custom_object":
                generator.CreateCustomObject(args);
                break;

            case "move_object":
                MoveObject(args);
                break;

            case "delete_object":
                DeleteObject(args);
                break;

            default:
                Debug.LogWarning("Unknown tool: " + tool);
                break;
        }
    }

    private void MoveObject(ToolArguments args)
    {
        Debug.Log("Move object requested");
    }

    private void DeleteObject(ToolArguments args)
    {
        Debug.Log("Delete object requested");
    }
}