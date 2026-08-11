using System;
using System.Collections.Generic;
using UnityEngine;

//
// ============================================================
// SHARED AI / HOUSE DESIGN DATA
// ============================================================
// Keep these classes in ONE file only.
// Recommended filename:
//
// AIToolData.cs
//
// Do NOT define ToolArguments, WallPoint, etc. again
// inside UnityToolManager.cs or DemoChat.cs.
//


// ============================================================
// WALL POINT
// ============================================================

[Serializable]
public class WallPoint
{
    public float x;
    public float z;
}


// ============================================================
// TOOL ARGUMENTS
// ============================================================

[Serializable]
public class ToolArguments
{
    // General
    public string objectType;
    public string name;
    public string targetName;

    public string color;
    public string material;

    // Transform
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;

    // --------------------------------------------------------
    // Wall
    // --------------------------------------------------------

    public float startX;
    public float startZ;

    public float endX;
    public float endZ;

    public float height;
    public float thickness;

    // --------------------------------------------------------
    // Generic dimensions
    // --------------------------------------------------------

    public float width;
    public float depth;

    // --------------------------------------------------------
    // Window
    // --------------------------------------------------------

    public float sillHeight;

    // --------------------------------------------------------
    // Room / floor
    // --------------------------------------------------------

    public float minX;
    public float minZ;

    public float maxX;
    public float maxZ;

    public float y;

    // --------------------------------------------------------
    // Modification
    // --------------------------------------------------------

    public float moveX;
    public float moveY;
    public float moveZ;

    public float scaleX;
    public float scaleY;
    public float scaleZ;
}


// ============================================================
// WALL DATA
// ============================================================

[Serializable]
public class WallData
{
    public string name;

    public WallPoint start;
    public WallPoint end;

    public float height;
    public float thickness;
}


// ============================================================
// DOOR DATA
// ============================================================

[Serializable]
public class DoorData
{
    public string name;

    public WallPoint position;

    public float width;
    public float height;

    public float rotation;

    public bool doubleDoor;
}


// ============================================================
// WINDOW DATA
// ============================================================

[Serializable]
public class WindowData
{
    public string name;

    public WallPoint position;

    public float width;
    public float height;

    public float sillHeight;

    public float rotation;
}


// ============================================================
// FLOOR DATA
// ============================================================

[Serializable]
public class FloorData
{
    public string name;

    public float minX;
    public float minZ;

    public float maxX;
    public float maxZ;

    public float y;
}


// ============================================================
// ROOM DATA
// ============================================================

[Serializable]
public class RoomData
{
    public string name;

    public float minX;
    public float minZ;

    public float maxX;
    public float maxZ;

    public string type;
}


// ============================================================
// STAIRS DATA
// ============================================================

[Serializable]
public class StairData
{
    public string name;

    public WallPoint position;

    public float width;
    public float length;
    public float height;

    public float rotation;
}


// ============================================================
// KITCHEN DATA
// ============================================================

[Serializable]
public class KitchenData
{
    public string name;

    public WallPoint position;

    public float width;
    public float depth;
    public float height;

    public float rotation;
}


// ============================================================
// ROOF DATA
// ============================================================

[Serializable]
public class RoofData
{
    public string name;

    public float minX;
    public float minZ;

    public float maxX;
    public float maxZ;

    public float height;
}


// ============================================================
// HOUSE LAYOUT
// ============================================================

[Serializable]
public class HouseLayout
{
    public List<WallData> walls =
        new List<WallData>();

    public List<DoorData> doors =
        new List<DoorData>();

    public List<WindowData> windows =
        new List<WindowData>();

    public List<FloorData> floors =
        new List<FloorData>();

    public List<RoomData> rooms =
        new List<RoomData>();

    public List<StairData> stairs =
        new List<StairData>();

    public List<KitchenData> kitchens =
        new List<KitchenData>();

    public List<RoofData> roofs =
        new List<RoofData>();
}