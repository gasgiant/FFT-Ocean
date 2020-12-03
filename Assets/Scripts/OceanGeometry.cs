using System.Collections.Generic;
using UnityEngine;

public class OceanGeometry : MonoBehaviour
{
    [SerializeField] 
    WavesGenerator wavesGenerator;
    [SerializeField]
    Transform viewer;
    [SerializeField]
    Material oceanMaterial;
    [SerializeField]
    bool updateMaterialProperties;
    [SerializeField]
    bool showMaterialLods;

    [SerializeField]
    float lengthScale = 10;
    [SerializeField, Range(1, 40)]
    int vertexDensity = 30;
    [SerializeField, Range(0, 8)]
    int clipLevels = 8;
    [SerializeField, Range(0, 100)]
    float skirtSize = 50;

    List<Element> rings = new List<Element>();
    List<Element> trims = new List<Element>();
    Element center;
    Element skirt;
    Quaternion[] trimRotations;
    int previousVertexDensity;
    float previousSkirtSize;

    Material[] materials;

    private void Start()
    {
        if (viewer == null)
            viewer = Camera.main.transform;

        oceanMaterial.SetTexture("_Displacement_c0", wavesGenerator.cascade0.Displacement);
        oceanMaterial.SetTexture("_Derivatives_c0", wavesGenerator.cascade0.Derivatives);
        oceanMaterial.SetTexture("_Turbulence_c0", wavesGenerator.cascade0.Turbulence);

        oceanMaterial.SetTexture("_Displacement_c1", wavesGenerator.cascade1.Displacement);
        oceanMaterial.SetTexture("_Derivatives_c1", wavesGenerator.cascade1.Derivatives);
        oceanMaterial.SetTexture("_Turbulence_c1", wavesGenerator.cascade1.Turbulence);

        oceanMaterial.SetTexture("_Displacement_c2", wavesGenerator.cascade2.Displacement);
        oceanMaterial.SetTexture("_Derivatives_c2", wavesGenerator.cascade2.Derivatives);
        oceanMaterial.SetTexture("_Turbulence_c2", wavesGenerator.cascade2.Turbulence);


        materials = new Material[3];
        materials[0] = new Material(oceanMaterial);
        materials[0].EnableKeyword("CLOSE");

        materials[1] = new Material(oceanMaterial);
        materials[1].EnableKeyword("MID");
        materials[1].DisableKeyword("CLOSE");

        materials[2] = new Material(oceanMaterial);
        materials[2].DisableKeyword("MID");
        materials[2].DisableKeyword("CLOSE");

        trimRotations = new Quaternion[]
        {
            Quaternion.AngleAxis(180, Vector3.up),
            Quaternion.AngleAxis(90, Vector3.up),
            Quaternion.AngleAxis(270, Vector3.up),
            Quaternion.identity,
        };

        InstantiateMeshes();
    }

    private void Update()
    {
        if (rings.Count != clipLevels || trims.Count != clipLevels
            || previousVertexDensity != vertexDensity || !Mathf.Approximately(previousSkirtSize, skirtSize))
        {
            InstantiateMeshes();
            previousVertexDensity = vertexDensity;
            previousSkirtSize = skirtSize;
        }

        UpdatePositions();
        UpdateMaterials();
    }

    void UpdateMaterials()
    {
        if (updateMaterialProperties && !showMaterialLods)
        {
            for (int i = 0; i < 3; i++)
            {
                materials[i].CopyPropertiesFromMaterial(oceanMaterial);
            }
            materials[0].EnableKeyword("CLOSE");
            materials[1].EnableKeyword("MID");
            materials[1].DisableKeyword("CLOSE");
            materials[2].DisableKeyword("MID");
            materials[2].DisableKeyword("CLOSE");
        }
        if (showMaterialLods)
        {
            materials[0].SetColor("_Color", Color.red * 0.6f);
            materials[1].SetColor("_Color", Color.green * 0.6f);
            materials[2].SetColor("_Color", Color.blue * 0.6f);
        }

        int activeLevels = ActiveLodlevels();
        center.MeshRenderer.material = GetMaterial(clipLevels - activeLevels - 1);

        for (int i = 0; i < rings.Count; i++)
        {
            rings[i].MeshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
            trims[i].MeshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
        }
    }

    Material GetMaterial(int lodLevel)
    {
        if (lodLevel - 2 <= 0)
            return materials[0];

        if (lodLevel - 2 <= 2)
            return materials[1];

        return materials[2];
    }

    void UpdatePositions()
    {
        int k = GridSize();
        int activeLevels = ActiveLodlevels();

        float scale = ClipLevelScale(-1, activeLevels);
        Vector3 previousSnappedPosition = Snap(viewer.position, scale * 2);
        center.Transform.position = previousSnappedPosition + OffsetFromCenter(-1, activeLevels);
        center.Transform.localScale = new Vector3(scale, 1, scale);

        for (int i = 0; i < clipLevels; i++)
        {
            rings[i].Transform.gameObject.SetActive(i < activeLevels);
            trims[i].Transform.gameObject.SetActive(i < activeLevels);
            if (i >= activeLevels) continue;

            scale = ClipLevelScale(i, activeLevels);
            Vector3 centerOffset = OffsetFromCenter(i, activeLevels);
            Vector3 snappedPosition = Snap(viewer.position, scale * 2);

            Vector3 trimPosition = centerOffset + snappedPosition + scale * (k - 1) / 2 * new Vector3(1, 0, 1);
            int shiftX = previousSnappedPosition.x - snappedPosition.x < float.Epsilon ? 1 : 0;
            int shiftZ = previousSnappedPosition.z - snappedPosition.z < float.Epsilon ? 1 : 0;
            trimPosition += shiftX * (k + 1) * scale * Vector3.right;
            trimPosition += shiftZ * (k + 1) * scale * Vector3.forward;
            trims[i].Transform.position = trimPosition;
            trims[i].Transform.rotation = trimRotations[shiftX + 2 * shiftZ];
            trims[i].Transform.localScale = new Vector3(scale, 1, scale);

            rings[i].Transform.position = snappedPosition + centerOffset;
            rings[i].Transform.localScale = new Vector3(scale, 1, scale);
            previousSnappedPosition = snappedPosition;
        }

        scale = lengthScale * 2 * Mathf.Pow(2, clipLevels);
        skirt.Transform.position = new Vector3(-1, 0, -1) * scale * (skirtSize + 0.5f - 0.5f / GridSize()) + previousSnappedPosition;
        skirt.Transform.localScale = new Vector3(scale, 1, scale);
    }

    int ActiveLodlevels()
    {
        return clipLevels - Mathf.Clamp((int)Mathf.Log((1.7f * Mathf.Abs(viewer.position.y) + 1) / lengthScale, 2), 0, clipLevels);
    }

    float ClipLevelScale(int level, int activeLevels)
    {
        return lengthScale / GridSize() * Mathf.Pow(2, clipLevels - activeLevels + level + 1);
    }

    Vector3 OffsetFromCenter(int level, int activeLevels)
    {
        return (Mathf.Pow(2, clipLevels) + GeometricProgressionSum(2, 2, clipLevels - activeLevels + level + 1, clipLevels - 1))
               * lengthScale / GridSize() * (GridSize() - 1) / 2 * new Vector3(-1, 0, -1);
    }

    float GeometricProgressionSum(float b0, float q, int n1, int n2)
    {
        return b0 / (1 - q) * (Mathf.Pow(q, n2) - Mathf.Pow(q, n1));
    }

    int GridSize()
    {
        return 4 * vertexDensity + 1;
    }

    Vector3 Snap(Vector3 coords, float scale)
    {
        if (coords.x >= 0)
            coords.x = Mathf.Floor(coords.x / scale) * scale;
        else
            coords.x = Mathf.Ceil((coords.x - scale + 1) / scale) * scale;

        if (coords.z < 0)
            coords.z = Mathf.Floor(coords.z / scale) * scale;
        else
            coords.z = Mathf.Ceil((coords.z - scale + 1) / scale) * scale;

        coords.y = 0;
        return coords;
    }

    void InstantiateMeshes()
    {
        foreach (var child in gameObject.GetComponentsInChildren<Transform>())
        {
            if (child != transform)
                Destroy(child.gameObject);
        }
        rings.Clear();
        trims.Clear();

        int k = GridSize();
        center = InstantiateElement("Center", CreatePlaneMesh(2 * k, 2 * k, 1, Seams.All), materials[materials.Length - 1]);
        Mesh ring = CreateRingMesh(k, 1);
        Mesh trim = CreateTrimMesh(k, 1);
        for (int i = 0; i < clipLevels; i++)
        {
            rings.Add(InstantiateElement("Ring " + i, ring, materials[materials.Length - 1]));
            trims.Add(InstantiateElement("Trim " + i, trim, materials[materials.Length - 1]));
        }
        skirt = InstantiateElement("Skirt", CreateSkirtMesh(k, skirtSize), materials[materials.Length - 1]);
    }

    Element InstantiateElement(string name, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject();
        go.name = name;
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        meshRenderer.material = mat;
        meshRenderer.allowOcclusionWhenDynamic = false;
        return new Element(go.transform, meshRenderer);
    }

    Mesh CreateSkirtMesh(int k, float outerBorderScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap skirt";
        CombineInstance[] combine = new CombineInstance[8];

        Mesh quad = CreatePlaneMesh(1, 1, 1);
        Mesh hStrip = CreatePlaneMesh(k, 1, 1);
        Mesh vStrip = CreatePlaneMesh(1, k, 1);


        Vector3 cornerQuadScale = new Vector3(outerBorderScale, 1, outerBorderScale);
        Vector3 midQuadScaleVert = new Vector3(1f / k, 1, outerBorderScale);
        Vector3 midQuadScaleHor = new Vector3(outerBorderScale, 1, 1f / k);

        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, cornerQuadScale);
        combine[0].mesh = quad;

        combine[1].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale, Quaternion.identity, midQuadScaleVert);
        combine[1].mesh = hStrip;

        combine[2].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[2].mesh = quad;

        combine[3].transform = Matrix4x4.TRS(Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
        combine[3].mesh = vStrip;

        combine[4].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
            + Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
        combine[4].mesh = vStrip;

        combine[5].transform = Matrix4x4.TRS(Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[5].mesh = quad;

        combine[6].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale
            + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, midQuadScaleVert);
        combine[6].mesh = hStrip;

        combine[7].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
            + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[7].mesh = quad;
        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    Mesh CreateTrimMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap trim";
        CombineInstance[] combine = new CombineInstance[2];

        combine[0].mesh = CreatePlaneMesh(k + 1, 1, lengthScale, Seams.None, 1);
        combine[0].transform = Matrix4x4.TRS(new Vector3(-k - 1, 0, -1) * lengthScale, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePlaneMesh(1, k, lengthScale, Seams.None, 1);
        combine[1].transform = Matrix4x4.TRS(new Vector3(-1, 0, -k - 1) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    Mesh CreateRingMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap ring";
        if ((2 * k + 1) * (2 * k + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        CombineInstance[] combine = new CombineInstance[4];

        combine[0].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left);
        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Top | Seams.Right | Seams.Left);
        combine[1].transform = Matrix4x4.TRS(new Vector3(0, 0, k + 1 + (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[2].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Left);
        combine[2].transform = Matrix4x4.TRS(new Vector3(0, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[3].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Right);
        combine[3].transform = Matrix4x4.TRS(new Vector3(k + 1 + (k - 1) / 2, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    Mesh CreatePlaneMesh(int width, int height, float lengthScale, Seams seams = Seams.None, int trianglesShift = 0)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap plane";
        if ((width + 1) * (height + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        int[] triangles = new int[width * height * 2 * 3];
        Vector3[] normals = new Vector3[(width + 1) * (height + 1)];

        for (int i = 0; i < height + 1; i++)
        {
            for (int j = 0; j < width + 1; j++)
            {
                int x = j;
                int z = i;

                if ((i == 0 && seams.HasFlag(Seams.Bottom)) || (i == height && seams.HasFlag(Seams.Top)))
                    x = x / 2 * 2;
                if ((j == 0 && seams.HasFlag(Seams.Left)) || (j == width && seams.HasFlag(Seams.Right)))
                    z = z / 2 * 2;

                vertices[j + i * (width + 1)] = new Vector3(x, 0, z) * lengthScale;
                normals[j + i * (width + 1)] = Vector3.up;
            }
        }

        int tris = 0;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                int k = j + i * (width + 1);
                if ((i + j + trianglesShift) % 2 == 0)
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;

                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 2;
                    triangles[tris++] = k + 1;
                }
                else
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + 1;

                    triangles[tris++] = k + 1;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        return mesh;
    }

    class Element
    {
        public Transform Transform;
        public MeshRenderer MeshRenderer;

        public Element(Transform transform, MeshRenderer meshRenderer)
        {
            Transform = transform;
            MeshRenderer = meshRenderer;
        }
    }


    [System.Flags]
    enum Seams
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        All = Left | Right | Top | Bottom
    };
}


