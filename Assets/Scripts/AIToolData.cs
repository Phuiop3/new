using System;
using System.Collections.Generic;

[Serializable]
public class WallPoint
{
    public float x;
    public float z;
}

[Serializable]
public class WallData
{
    public string name;

    public WallPoint start;
    public WallPoint end;

    public float height;
    public float thickness;
}

[Serializable]
public class HouseLayout
{
    public List<WallData> walls;
}