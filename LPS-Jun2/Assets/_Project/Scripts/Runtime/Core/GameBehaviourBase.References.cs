using UnityEngine;
using VContainer;

public partial class GameBehaviourBase
{
    [Inject] protected Colors Colors;
    [Inject] protected PrefabModule PrefabModule;
    [Inject] protected SceneModule SceneModule;
    [Inject] protected ThemeType Theme;
    [Inject] protected MaterialPropertyBlock PropertyBlock;
}