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