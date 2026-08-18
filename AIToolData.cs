
using System;

//
// ============================================================
// TERRAIN DATA
// ============================================================

[Serializable]
public class TerrainRequest
{
    public TerrainSettings terrain;
}

[Serializable]
public class TerrainSettings
{
    public int width = 200;
    public int depth = 200;
    public int height = 30;
    public string terrainType = "hills";
    public float roughness = 0.5f;
    public float detailScale = 0.03f;
    public int octaves = 4;
    public int seed = 0;
}

[Serializable]
public class TreeCommand
{
    public string action;
    public int count = 10;
    public float centerX = 0f;
    public float centerZ = 0f;
    public float radius = 20f;
    public float spacing = 4f;
}
