using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed class MainMenuCarrier : GameBehaviourBase
{
    [SerializeField] private Transform BlockParent;
    [SerializeField] private Transform BlockPoint;
    [SerializeField] private StatefulComponent View;

    [Inject] private GameMachine _gameMachine;
    [Inject] private AudioModule _audioModule;
    [Inject] private HapticModule _hapticModule;

    private readonly List<Transform> _blocks = new();

    protected override void Awake()
    {
        base.Awake();

        HandleAnimation().Forget();
    }

    private async UniTaskVoid HandleAnimation()
    {
        View.SetText(TextRole.CurrentLevel, $"Level\n{(Prefs.Level + 1).ToString()}");

        transform.position = Vector3.forward * 20f;

        for (var i = 0; i < BlockParent.childCount; i++)
        {
            var block = BlockParent.GetChild(i);
            _blocks.Add(block);
            block.gameObject.SetActive(false);
        }
        _blocks.Reverse();

        await UniTask.DelayFrame(4);

        await LMotion.Create(20f, 0f, 1.5f)
            .WithEase(Ease.InOutBack)
            .BindToPositionZ(transform)
            .AddTo(this)
            .ToUniTask();

        BlockParent.parent = transform;

        foreach (var block in _blocks)
        {
            block.gameObject.SetActive(true);
            ApplyBlockMotion(block.transform, BlockPoint.position, block.transform.position);
            await UniTask.Delay(TimeSpan.FromMilliseconds(10));
        }

        await UniTask.WaitUntil(() => _gameMachine.HasPendingTransition);

        await LMotion.Create(0f, -20f, 1.5f)
            .WithEase(Ease.InOutBack)
            .BindToPositionZ(transform)
            .AddTo(this)
            .ToUniTask();

        _gameMachine.StateCanExit();
    }

    public void ApplyBlockMotion(Transform blockT, Vector3 from, Vector3 to)
    {
        var matrix = BlockParent.worldToLocalMatrix;
        from = matrix.MultiplyPoint3x4(from);
        to = matrix.MultiplyPoint3x4(to);
        LMotion.Create(from, to, .6f)
            .WithEase(Ease.InOutSine)
            .BindToLocalPosition(blockT)
            .AddTo(this);

        // _audioModule.GetPlayer().Play(_audioModule.Sounds.AddBlock);
        // _hapticModule.PlaySoft();
    }
}