using System;
using UnityEngine;

public sealed class LoadNextScene : MonoBehaviour
{
    private void Start()
    {
        SceneManagerH.LoadNextScene();
    }
}