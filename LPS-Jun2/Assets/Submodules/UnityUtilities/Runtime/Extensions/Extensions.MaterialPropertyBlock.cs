using UnityEngine;

public static partial class Extensions
{
    public static void SetProperty(this MaterialPropertyBlock b, int id, float v) => b.SetFloat(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, int v) => b.SetInt(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Vector2 v) => b.SetVector(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Vector3 v) => b.SetVector(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Vector4 v) => b.SetVector(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Color v) => b.SetColor(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Matrix4x4 v) => b.SetMatrix(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Texture v) => b.SetTexture(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, RenderTexture v) => b.SetTexture(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, ComputeBuffer v) => b.SetBuffer(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, float[] v) => b.SetFloatArray(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Vector4[] v) => b.SetVectorArray(id, v);
    public static void SetProperty(this MaterialPropertyBlock b, int id, Matrix4x4[] v) => b.SetMatrixArray(id, v);
}