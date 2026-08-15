using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class ShaderInstaller : InstallerBase
{
    public override async UniTask Initialize(Dictionary<string, string> args)
    {
        var resourceRequest = Resources.LoadAsync<ShaderVariantCollection>("Core/Shaders");
        var shaders = (ShaderVariantCollection)await resourceRequest;
        shaders.WarmUp();
    }
}