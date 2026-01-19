using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Alt_mine : MonoBehaviour
{
    // Settings for the terrain | how big the chunk is //
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 10;

    // noise = terrain detail | resolution = size of each voxel //
    [SerializeField] float resolution = 1;
    [SerializeField] float noiseScale = 1;

    // Threshold to determine surface level // - dont change
    [SerializeField] private float heightTresshold = 0.5f;

    [Tooltip("WARNIG: Huge performance impact!")]
    [SerializeField] bool visualizeNoise;
    [SerializeField] bool use3DNoise = true;

    // Internal data structures //
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private float[,,] densities;
    private bool[,,] isModified;

    // For vertex sharing //
    private Dictionary<int, int> vertexIndexMap = new Dictionary<int, int>();
    private Dictionary<int, Vector3> edgeVertices = new Dictionary<int, Vector3>();

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        InitializeTerrain();
        RegenerateMesh();
    }

    public void InitializeTerrain()
    {
        densities = new float[width + 1, height + 1, width + 1];
        isModified = new bool[width + 1, height + 1, width + 1];

        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    if (use3DNoise)
                    {
                        float currentDensity = PerlinNoise3D(
                            (float)x / width * noiseScale,
                            (float)y / height * noiseScale,
                            (float)z / width * noiseScale
                        );
                        densities[x, y, z] = currentDensity;
                    }
                    else
                    {
                        float currentHeight = height * Mathf.PerlinNoise(x * noiseScale, z * noiseScale);
                        float distToSurface;

                        if (y <= currentHeight - 0.5f)
                            distToSurface = 0f;
                        else if (y > currentHeight + 0.5f)
                            distToSurface = 1f;
                        else if (y > currentHeight)
                            distToSurface = y - currentHeight;
                        else
                            distToSurface = currentHeight - y;

                        densities[x, y, z] = distToSurface;
                    }
                }
            }
        }
    }

    
    public void ModifyTerrain(Vector3 worldPosition, float radius, float intensity, bool removeTerrain = true)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        int centerX = Mathf.RoundToInt(localPos.x / resolution);
        int centerY = Mathf.RoundToInt(localPos.y / resolution);
        int centerZ = Mathf.RoundToInt(localPos.z / resolution);

        int effectRadius = Mathf.RoundToInt(radius / resolution);

        for (int x = Mathf.Max(0, centerX - effectRadius); x <= Mathf.Min(width, centerX + effectRadius); x++)
        {
            for (int y = Mathf.Max(0, centerY - effectRadius); y <= Mathf.Min(height, centerY + effectRadius); y++)
            {
                for (int z = Mathf.Max(0, centerZ - effectRadius); z <= Mathf.Min(width, centerZ + effectRadius); z++)
                {
                    float distance = Vector3.Distance(
                        new Vector3(x, y, z),
                        new Vector3(centerX, centerY, centerZ)
                    ) * resolution;

                    if (distance <= radius)
                    {
                        float falloff = 1 - (distance / radius);
                        float modification = intensity * falloff;

                        if (removeTerrain)
                        {
                            densities[x, y, z] += modification;
                        }
                        else
                        {
                            densities[x, y, z] -= modification;
                        }

                        densities[x, y, z] = Mathf.Clamp01(densities[x, y, z]);
                        isModified[x, y, z] = true;
                    }
                }
            }
        }

        RegenerateMesh();
    }

    
    public void AddCrater(Vector3 worldPosition, float radius, float depth)
    {
        ModifyTerrain(worldPosition, radius, depth, true);
    }

    public void RegenerateMesh()
    {
        vertices.Clear();
        triangles.Clear();
        vertexIndexMap.Clear();
        edgeVertices.Clear();

        MarchCubes();
        SetMesh();
    }

    public void ResetTerrain()
    {
        InitializeTerrain();
        RegenerateMesh();
    }

    private void SetMesh()
    {
        Mesh mesh = new Mesh();

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = mesh;
        }
    }

    private float PerlinNoise3D(float x, float y, float z)
    {
        float xy = Mathf.PerlinNoise(x, y);
        float xz = Mathf.PerlinNoise(x, z);
        float yz = Mathf.PerlinNoise(y, z);
        float yx = Mathf.PerlinNoise(y, x);
        float zx = Mathf.PerlinNoise(z, x);
        float zy = Mathf.PerlinNoise(z, y);

        return (xy + xz + yz + yx + zx + zy) / 6;
    }

    private int GetConfigIndex(float[] cubeCorners)
    {
        int configIndex = 0;

        for (int i = 0; i < 8; i++)
        {
            if (cubeCorners[i] > heightTresshold)
            {
                configIndex |= 1 << i;
            }
        }

        return configIndex;
    }

    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();
        vertexIndexMap.Clear();
        edgeVertices.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    float[] cubeCorners = new float[8];

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
                        cubeCorners[i] = densities[corner.x, corner.y, corner.z];
                    }

                    MarchCube(new Vector3(x, y, z), cubeCorners);
                }
            }
        }
    }

    private void MarchCube(Vector3 position, float[] cubeCorners)
    {
        int configIndex = GetConfigIndex(cubeCorners);

        if (configIndex == 0 || configIndex == 255)
        {
            return;
        }

        // Array to store vertex indices for this cube //
        int[] vertexIndices = new int[12];

        // First pass: create or get vertices for each edge//
        for (int edge = 0; edge < 12; edge++)
        {
            if ((MarchingTable.EdgeTable[configIndex] & (1 << edge)) != 0)
            {
                vertexIndices[edge] = GetOrCreateVertex(position, cubeCorners, edge);
            }
        }

        // Second pass: create triangles using the shared vertices //
        int edgeIndex = 0;
        for (int t = 0; t < 5; t++)
        {
            for (int v = 0; v < 3; v++)
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];

                if (triTableValue == -1)
                {
                    return;
                }

                triangles.Add(vertexIndices[triTableValue]);
                edgeIndex++;
            }
        }
    }

    private int GetOrCreateVertex(Vector3 position, float[] cubeCorners, int edge)
    {
        // Generate a unique key for this edge vertex based on cube position and edge  //
        int cornerIndex1 = MarchingTable.EdgeToCorners[edge, 0];
        int cornerIndex2 = MarchingTable.EdgeToCorners[edge, 1];

        // Create a unique key by hashing the positions of the two corners
        Vector3Int corner1 = MarchingTable.Corners[cornerIndex1];
        Vector3Int corner2 = MarchingTable.Corners[cornerIndex2];

        // Use the position of the cube and the edge to create a unique key //
        int key = GetVertexKey(
            Mathf.FloorToInt(position.x) + corner1.x,
            Mathf.FloorToInt(position.y) + corner1.y,
            Mathf.FloorToInt(position.z) + corner1.z,
            Mathf.FloorToInt(position.x) + corner2.x,
            Mathf.FloorToInt(position.y) + corner2.y,
            Mathf.FloorToInt(position.z) + corner2.z
        );

        // Check if already have this vertex //
        if (vertexIndexMap.TryGetValue(key, out int existingIndex))
        {
            return existingIndex;
        }

        // Create new vertex //
        float density1 = cubeCorners[cornerIndex1];
        float density2 = cubeCorners[cornerIndex2];

        float tParam = (heightTresshold - density1) / (density2 - density1);
        tParam = Mathf.Clamp01(tParam);

        Vector3 edgeStart = position + MarchingTable.Edges[edge, 0];
        Vector3 edgeEnd = position + MarchingTable.Edges[edge, 1];

        Vector3 vertex = edgeStart + (edgeEnd - edgeStart) * tParam;
        vertex *= resolution;

        int newIndex = vertices.Count;
        vertices.Add(vertex);
        vertexIndexMap[key] = newIndex;

        return newIndex;
    }

    // Generate a unique key for an edge vert //
    private int GetVertexKey(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        
        if (x1 > x2 || (x1 == x2 && y1 > y2) || (x1 == x2 && y1 == y2 && z1 > z2))
        {
            
            (x2, x1) = (x1, x2);
            (y2, y1) = (y1, y2);
            (z2, z1) = (z1, z2);
        }

        int hash = 17;
        hash = hash * 31 + x1;
        hash = hash * 31 + y1;
        hash = hash * 31 + z1;
        hash = hash * 31 + x2;
        hash = hash * 31 + y2;
        hash = hash * 31 + z2;

        return hash;
    }

    private void OnDrawGizmosSelected()
    {
        if (!visualizeNoise || !Application.isPlaying || densities == null)
        {
            return;
        }

        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    Gizmos.color = new Color(densities[x, y, z], densities[x, y, z], densities[x, y, z], 1);
                    Gizmos.DrawSphere(
                        transform.position + new Vector3(x * resolution, y * resolution, z * resolution),
                        0.2f * resolution
                    );
                }
            }
        }
    }
}