using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

public sealed class SplineMeshAutoCount : MonoBehaviour
{
    [SerializeField] private List<Element> Elements;

    [Serializable]
    public struct Element
    {
        public float LengthMultiplier;
        public int TargetChannelIdx;
    }

    private SplineComputer _splineComputer;
    private SplineMesh _splineMesh;

#if UNITY_EDITOR
    /// <summary>
    /// The tile-density mapping this component applies at play time. Exposed so edit-time tooling
    /// (<see cref="ConveyorMeshUpdater"/>) can apply the same formula from a button — Awake never
    /// runs in edit mode, so without that the mesh keeps whatever coarse count was serialized and
    /// only becomes smooth once you press Play.
    /// </summary>
    public IReadOnlyList<Element> EditorElements => Elements;
#endif

    private void Awake()
    {
        _splineComputer = GetComponent<SplineComputer>();
        _splineMesh = GetComponent<SplineMesh>();

        _splineComputer.onRebuild += UpdateMeshCount;
        _splineMesh.onPostBuild += UpdateMeshCount;
    }

    private void OnDestroy()
    {
        _splineComputer.onRebuild -= UpdateMeshCount;
        _splineMesh.onPostBuild -= UpdateMeshCount;
    }

    private void UpdateMeshCount()
    {
        foreach (var element in Elements)
        {
            var length = _splineComputer.CalculateLength();
            var channel = _splineMesh.GetChannel(element.TargetChannelIdx);
            channel.count = Mathf.CeilToInt(length * element.LengthMultiplier);
        }
    }
}