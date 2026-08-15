using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using VContainer.Unity;

public sealed class OutlineModule : ModuleBase, IStartable, ITickable
{
    [Inject] private Camera _camera;
    [Inject] private ISubscriber<ScenePostLoadMessage> _scenePostLoad;

    private uint _nextEntityIdx;
    private bool _isRenderBlocked;

    private readonly List<OutlineEntity> _entities = new();
    private readonly HashSet<Mesh> _bakedMeshes = new();
    private readonly Dictionary<Mesh, List<Vector3>> _smoothNormalsByMesh = new();

    private MaterialPropertyBlock _propertyBlock;
    private Shader _fillShader;
    private Shader _maskShader;

    private Material _fillAlwaysMaterial;
    private Material _fillLessEqualMaterial;
    private Material _fillGreaterMaterial;
    private Material _maskAlwaysMaterial;
    private Material _maskLessEqualMaterial;

    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
    private static readonly int ZTest = Shader.PropertyToID("_ZTest");

    public void Start()
    {
        _propertyBlock = new MaterialPropertyBlock();
        _fillShader = Shader.Find("Custom/Outline Fill");
        _maskShader = Shader.Find("Custom/Outline Mask");

        _fillAlwaysMaterial = new Material(_fillShader);
        _fillLessEqualMaterial = new Material(_fillShader);
        _fillGreaterMaterial = new Material(_fillShader);
        _maskAlwaysMaterial = new Material(_maskShader);
        _maskLessEqualMaterial = new Material(_maskShader);

        _fillAlwaysMaterial.SetFloat(ZTest, (float)CompareFunction.Always);
        _fillLessEqualMaterial.SetFloat(ZTest, (float)CompareFunction.LessEqual);
        _fillGreaterMaterial.SetFloat(ZTest, (float)CompareFunction.Greater);
        _maskAlwaysMaterial.SetFloat(ZTest, (float)CompareFunction.Always);
        _maskLessEqualMaterial.SetFloat(ZTest, (float)CompareFunction.LessEqual);

        _scenePostLoad.Subscribe(OnScenePostLoad);
    }

    public void Tick()
    {
        if (_isRenderBlocked) return;

        foreach (var entity in _entities)
        {
            if (entity.Target == null) continue;
            if (!entity.Target.activeSelf) continue;
            var t = entity.Target.transform;
            var matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);
            var width = entity.RenderMode == OutlineRenderMode.SilhouetteOnly ? 0f : entity.Width;
            _propertyBlock.Clear();
            _propertyBlock.SetColor(OutlineColor, entity.Color);
            _propertyBlock.SetFloat(OutlineWidth, width);
            var fillMaterial = GetFillMaterial(entity.RenderMode);
            var maskMaterial = GetMaskMaterial(entity.RenderMode);
            var submeshIdx = entity.Mesh.subMeshCount - 1;
            Graphics.DrawMesh(entity.Mesh, matrix, fillMaterial, 0, _camera, submeshIdx, _propertyBlock);
            Graphics.DrawMesh(entity.Mesh, matrix, maskMaterial, 0, _camera, submeshIdx, _propertyBlock);
        }
    }

    public OutlineEntity Add(GameObject target, Mesh mesh, Color color, float width, OutlineRenderMode renderMode)
    {
        LoadSmoothNormals(mesh);
        var outlineEntity = new OutlineEntity
        {
            Index = _nextEntityIdx++,
            Target = target,
            Mesh = mesh,
            Color = color,
            Width = width,
            RenderMode = renderMode
        };
        _entities.Add(outlineEntity);
        return outlineEntity;
    }

    public void Remove(OutlineEntity outlineEntity)
    {
        _entities.Remove(outlineEntity);
    }

    private Material GetMaskMaterial(OutlineRenderMode renderMode)
    {
        var zTest = renderMode switch
        {
            OutlineRenderMode.All => CompareFunction.Always,
            OutlineRenderMode.Visible => CompareFunction.Always,
            OutlineRenderMode.Hidden => CompareFunction.Always,
            OutlineRenderMode.OutlineAndSilhouette => CompareFunction.LessEqual,
            OutlineRenderMode.SilhouetteOnly => CompareFunction.LessEqual,
            _ => throw new ArgumentOutOfRangeException()
        };

        return zTest switch
        {
            CompareFunction.Always => _maskAlwaysMaterial,
            CompareFunction.LessEqual => _maskLessEqualMaterial,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private Material GetFillMaterial(OutlineRenderMode renderMode)
    {
        var zTest = renderMode switch
        {
            OutlineRenderMode.All => CompareFunction.Always,
            OutlineRenderMode.Visible => CompareFunction.LessEqual,
            OutlineRenderMode.Hidden => CompareFunction.Greater,
            OutlineRenderMode.OutlineAndSilhouette => CompareFunction.Always,
            OutlineRenderMode.SilhouetteOnly => CompareFunction.Greater,
            _ => throw new ArgumentOutOfRangeException()
        };

        return zTest switch
        {
            CompareFunction.Always => _fillAlwaysMaterial,
            CompareFunction.LessEqual => _fillLessEqualMaterial,
            CompareFunction.Greater => _fillGreaterMaterial,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void LoadSmoothNormals(Mesh mesh)
    {
        if (!_bakedMeshes.Add(mesh)) return;
        var smoothNormals = GetSmoothNormals(mesh);
        mesh.SetUVs(3, smoothNormals);
        CombineSubmeshes(mesh);
    }

    private List<Vector3> GetSmoothNormals(Mesh mesh)
    {
        if (_smoothNormalsByMesh.TryGetValue(mesh, out var cache))
            return cache;

        var ids = new int[mesh.vertexCount];
        int numIDs;
        {
            var idMap = new Dictionary<Vector3, int>();
            var vertices = mesh.vertices;
            for (int i = 0, n = mesh.vertexCount; i < n; ++i)
            {
                if (!idMap.TryGetValue(vertices[i], out var id))
                {
                    id = idMap.Count;
                    idMap.Add(vertices[i], id);
                }

                ids[i] = id;
            }

            numIDs = idMap.Count;
        }

        var normals = mesh.normals;
        var averagedNormals = new Vector3[numIDs];
        var counts = new int[numIDs];
        for (int i = 0, n = mesh.vertexCount; i < n; ++i)
        {
            var id = ids[i];
            int count = counts[id];
            if (count == 0)
            {
                counts[id] = 1;
                averagedNormals[id] = normals[i];
            }
            else
            {
                counts[id] = count + 1;
                averagedNormals[id] += normals[i];
            }
        }

        for (int id = 0, n = averagedNormals.Length; id < n; ++id)
        {
            var count = counts[id];
            if (count != 1) averagedNormals[id] /= count;
        }

        var smoothNormals = new List<Vector3>(mesh.vertexCount);
        for (int i = 0, n = mesh.vertexCount; i < n; ++i)
            smoothNormals.Add(averagedNormals[ids[i]]);

        _smoothNormalsByMesh[mesh] = smoothNormals;
        return smoothNormals;
    }

    private void CombineSubmeshes(Mesh mesh)
    {
        if (mesh.subMeshCount == 1) return;
        mesh.subMeshCount++;
        mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    }

    public void ActivateRender()
    {
        _isRenderBlocked = false;
    }

    public void DeactivateRender()
    {
        _isRenderBlocked = true;
    }

    private void OnScenePostLoad(ScenePostLoadMessage m)
    {
        _entities.Clear();
    }
}

public struct OutlineEntity : IEquatable<OutlineEntity>
{
    public uint Index;
    public GameObject Target;
    public Mesh Mesh;
    public Color Color;
    public float Width;
    public OutlineRenderMode RenderMode;

    public bool Equals(OutlineEntity other) => Index == other.Index;
    public override bool Equals(object obj) => obj is OutlineEntity other && Equals(other);
    public override int GetHashCode() => (int)Index;
}

public enum OutlineRenderMode
{
    All,
    Visible,
    Hidden,
    OutlineAndSilhouette,
    SilhouetteOnly
}