using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public class OutlineRenderer : MonoBehaviour
{
    [field: SerializeField] public OutlineRenderMode Mode { get; set; }
    [field: SerializeField] public Color Color { get; set; } = Color.black;
    [field: SerializeField, Range(0f, 10f)] public float Width { get; set; } = 2f;

    private OutlineRenderMode _mode;
    private Color _color;
    private float _width;

    private List<MeshFilter> _meshFilters;
    private List<OutlineEntity> _outlineEntities;

    private static OutlineModule _outlineModule;

    private void Awake()
    {
        if (_outlineModule == null)
        {
            var scope = LifetimeScopeH.FindScope<SceneScope>();
            if (scope != null && scope.Container != null)
                scope.Container.TryResolve(out _outlineModule);
        }

        _meshFilters = ListPool<MeshFilter>.Get();
        _outlineEntities = ListPool<OutlineEntity>.Get();

        GetComponentsInChildren(_meshFilters);
    }

    private void OnEnable() => Add();

    private void OnDisable() => Remove();

    private void OnDestroy()
    {
        ListPool<MeshFilter>.Release(_meshFilters);
        ListPool<OutlineEntity>.Release(_outlineEntities);
    }

    private void Update()
    {
        if (!HasChanges()) return;
        _mode = Mode;
        _color = Color;
        _width = Width;
        Remove();
        Add();
    }

    private bool HasChanges()
    {
        return _mode != Mode || _color != Color || !Mathf.Approximately(_width, Width);
    }

    private void Add()
    {
        if (_outlineModule == null) return;

        foreach (var meshFilter in _meshFilters)
        {
            var outlineEntity = _outlineModule.Add(meshFilter.gameObject, meshFilter.sharedMesh, _color, Width, _mode);
            _outlineEntities.Add(outlineEntity);
        }
    }

    private void Remove()
    {
        if (_outlineModule == null) return;

        foreach (var outlineEntity in _outlineEntities)
        {
            _outlineModule.Remove(outlineEntity);
        }
        _outlineEntities.Clear();
    }
}