using UnityEngine;

[System.Serializable]
public class ToolArguments
{
    public string objectType;
    public string color;

    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
}