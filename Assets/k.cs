using UnityEngine;

public class TerrainGenerators : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int width = 512;
    public int length = 512;
    public int depth = 50; // Chiều cao tối đa của núi

    [Header("Noise Settings")]
    public float scale = 20f;      // Độ rộng/tần số của núi
    public int octaves = 4;        // Số lớp chi tiết xếp chồng
    public float persistence = 0.5f; // Độ ảnh hưởng của các lớp chi tiết nhỏ
    public float lacunarity = 2f;    // Độ dày đặc của các lớp chi tiết nhỏ

    void Start()
    {
        Terrain terrain = GetComponent<Terrain>();
        terrain.terrainData = GenerateTerrain(terrain.terrainData);
    }

    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        terrainData.heightmapResolution = width + 1;
        terrainData.size = new Vector3(width, depth, length);
        terrainData.SetHeights(0, 0, GenerateHeights());
        return terrainData;
    }

    float[,] GenerateHeights()
    {
        float[,] heights = new float[width, length];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                heights[x, y] = CalculateHeight(x, y);
            }
        }

        return heights;
    }

    float CalculateHeight(int x, int y)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;  // D dùng để chuẩn hóa giá trị về khoảng [0, 1]

        // Vòng lặp Octaves tạo chi tiết cho núi
        for (int i = 0; i < octaves; i++)
        {
            float xCoord = (float)x / width * scale * frequency;
            float yCoord = (float)y / length * scale * frequency;

            float perlinValue = Mathf.PerlinNoise(xCoord, yCoord);
            total += perlinValue * amplitude;

            maxValue += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // Chuẩn hóa kết quả
        float finalHeight = total / maxValue;

        // Làm nhọn đỉnh núi và làm phẳng thung lũng (Exponentiation)
        finalHeight = Mathf.Pow(finalHeight, 2.5f);

        return finalHeight;
    }
}