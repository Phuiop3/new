using System;
using UnityEngine;

[Serializable]
public class ToolArguments
{
    public string objectType;
    public string color;

    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;

    // Wall data
    public float startX;
    public float startZ;

    public float endX;
    public float endZ;

    public float height;
    public float thickness;
}