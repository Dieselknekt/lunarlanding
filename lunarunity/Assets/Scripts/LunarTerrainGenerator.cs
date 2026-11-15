using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class LunarTerrainGenerator : MonoBehaviour
{
    [Header("Mesh Resolution")]
    [Tooltip("Subdivisions per cube face (6 faces). 128 = ~196k tris (fast & detailed), 180 = ~388k tris.")]
    [Range(32, 200)]
    public int resolution = 128;

    [Header("NASA Elevation Data")]
    [Tooltip("Optional grayscale equirectangular heightmap (enable Read/Write in Texture Import Settings).")]
    public Texture2D heightMap;
    [Tooltip("Max elevation displacement in Unity units (Moon radius = 1000 units = 1737.4 km -> 1 unit ~ 1.74 km).")]
    public float elevationScale = 18f;
    public float seaLevelOffset = 0.5f;

    [Header("Procedural Micro-Crater Detail")]
    [Tooltip("Adds realistic crater rims and ruggedness even if no heightmap is assigned yet.")]
    public bool addProceduralDetail = true;
    public float proceduralAmplitude = 6f;
    public int seed = 2026;

    void Awake()
    {
        GenerateMoonMesh();
    }

    [ContextMenu("Generate Moon Terrain Now")]
    public void GenerateMoonMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        Mesh mesh = new Mesh();
        mesh.name = "ProceduralLunarSphere";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Allows > 65k vertices

        Vector3[] directions = {
            Vector3.up, Vector3.down,
            Vector3.left, Vector3.right,
            Vector3.forward, Vector3.back
        };

        int vertsPerFace = resolution * resolution;
        int totalVerts = vertsPerFace * 6;
        int trisPerFace = (resolution - 1) * (resolution - 1) * 6;
        int totalTris = trisPerFace * 6;

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        int[] triangles = new int[totalTris];

        int vertIndex = 0;
        int triIndex = 0;

        for (int f = 0; f < 6; f++)
        {
            Vector3 localUp = directions[f];
            Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            Vector3 axisB = Vector3.Cross(localUp, axisA);

            int faceStartVert = vertIndex;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 percent = new Vector2(x, y) / (resolution - 1);
                    Vector3 pointOnUnitCube = localUp + (percent.x - 0.5f) * 2f * axisA + (percent.y - 0.5f) * 2f * axisB;

                    // Spherify cube point uniformly to avoid polar pinching
                    Vector3 pointOnUnitSphere = SpherifyPoint(pointOnUnitCube);

                    // Compute selenographic UVs (equirectangular) matching CelestialSystem coordinates
                    float lat = Mathf.Asin(Mathf.Clamp(pointOnUnitSphere.y, -1f, 1f));
                    float lon = Mathf.Atan2(pointOnUnitSphere.x, pointOnUnitSphere.z);
                    float u = (lon / (2f * Mathf.PI)) + 0.5f;
                    float v = (lat / Mathf.PI) + 0.5f;
                    uvs[vertIndex] = new Vector2(u, v);

                    // Base radius is 0.5 so that Transform Scale (2000, 2000, 2000) gives radius 1000
                    float radiusOffset = EvaluateElevation(pointOnUnitSphere, u, v);
                    vertices[vertIndex] = pointOnUnitSphere * (0.5f + radiusOffset);

                    if (x != resolution - 1 && y != resolution - 1)
                    {
                        int i = faceStartVert + x + y * resolution;
                        triangles[triIndex] = i;
                        triangles[triIndex + 1] = i + resolution + 1;
                        triangles[triIndex + 2] = i + resolution;

                        triangles[triIndex + 3] = i;
                        triangles[triIndex + 4] = i + 1;
                        triangles[triIndex + 5] = i + resolution + 1;
                        triIndex += 6;
                    }

                    vertIndex++;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    private Vector3 SpherifyPoint(Vector3 p)
    {
        float x2 = p.x * p.x;
        float y2 = p.y * p.y;
        float z2 = p.z * p.z;

        float sx = p.x * Mathf.Sqrt(1f - y2 * 0.5f - z2 * 0.5f + (y2 * z2) / 3f);
        float sy = p.y * Mathf.Sqrt(1f - z2 * 0.5f - x2 * 0.5f + (z2 * x2) / 3f);
        float sz = p.z * Mathf.Sqrt(1f - x2 * 0.5f - y2 * 0.5f + (x2 * y2) / 3f);

        return new Vector3(sx, sy, sz).normalized;
    }

    private float EvaluateElevation(Vector3 unitDir, float u, float v)
    {
        // Convert world unit scale offset back to local mesh space (divided by ~2000)
        float localScaleFactor = 1f / Mathf.Max(1f, transform.localScale.x);
        float heightUnits = 0f;

        if (heightMap != null)
        {
            float sample = heightMap.GetPixelBilinear(u, v).grayscale;
            heightUnits += (sample - seaLevelOffset) * elevationScale;
        }

        if (addProceduralDetail)
        {
            // Multi-octave ridged crater-like noise for realistic slopes and crater rims
            Vector3 p = unitDir * 6.5f + new Vector3(seed * 0.13f, seed * 0.27f, seed * 0.41f);
            float macro = RidgedNoise3D(p);
            float micro = RidgedNoise3D(p * 4.2f) * 0.35f;
            heightUnits += (macro + micro - 0.5f) * proceduralAmplitude;
        }

        return heightUnits * localScaleFactor;
    }

    private float RidgedNoise3D(Vector3 p)
    {
        float xy = Mathf.PerlinNoise(p.x, p.y);
        float yz = Mathf.PerlinNoise(p.y, p.z);
        float xz = Mathf.PerlinNoise(p.x, p.z);
        float n = (xy + yz + xz) / 3f;
        // Invert absolute ridge to form bowl-shaped craters with raised rims
        float bowl = 1f - Mathf.Abs(n * 2f - 1f);
        return bowl * bowl;
    }
}