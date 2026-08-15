using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using VContainer;

public sealed class BoosterContainer : ContainerBase
{
    [Inject] private SceneModule _sceneModule;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private Boosters _boosters;
    [Inject] private HapticModule _hapticModule;
    [Inject] private EconomyModule _economyModule;

    [Inject] private ISubscriber<BoosterSelectedMessage> _boosterSelectedSub;
    [Inject] private ISubscriber<BoosterCanceledMessage> _boosterCanceledSub;
    [Inject] private ISubscriber<BoosterCompletedMessage> _boosterCompletedSub;

    private BoosterSystem _boosterSystem;

    private readonly Dictionary<BoosterType, StatefulComponent> _boosterViews = new();
    private readonly Dictionary<BoosterType, StateRole> _stateByBoosterType = new();

    public override void Setup()
    {
        base.Setup();

        SetButtonListener(ButtonRole.Cancel, OnCancelClicked);

        _boosterSelectedSub.Subscribe(_ => RefreshElements());
        _boosterCanceledSub.Subscribe(_ => RefreshElements());
        _boosterCompletedSub.Subscribe(_ => RefreshElements());
    }

    public override void Render()
    {
        base.Render();

        if (_sheetContainer.Reloading)
            return;

        foreach (var booster in _sheetContainer.Boosters)
        {
            if (!_boosterViews.TryGetValue(booster.Id, out var view)) continue;
            OnUpdateElement(view, booster);
        }
    }

    public void RefreshElements()
    {
        _boosterSystem = _sceneModule.Scope.Container.Resolve<BoosterSystem>();

        var container = GetContainer(ContainerRole.Booster);
        container.Clear();

        var selected = _boosterSystem.Selected;
        SetState(selected ? StateRole.Selected : StateRole.Container);

        var cancelable = selected && _boosterSystem.BoosterInstance.TryCancel();
        // GetButton(ButtonRole.Cancel).gameObject.SetActive(cancelable);

        if (cancelable)
        {
            var selectedBoosterType = _boosterSystem.SelectedType;
            var booster = _sheetContainer.Boosters[selectedBoosterType];
            var descTmp = GetText(TextRole.Desc).TMP;
            descTmp.SetText(booster.Id.ToLocalizedDesc());
            LMotion.Create(0f, 1f, .5f)
                .WithDelay(.1f)
                .BindToColorA(descTmp);
        }

        if (selected) return;
        foreach (var booster in _sheetContainer.Boosters)
        {
            var boosterView = container.AddStatefulComponent();
            _boosterViews[booster.Id] = boosterView;
            OnRefreshElement(boosterView, booster);
        }
    }

    public void OnRefreshElement(StatefulComponent view, BoosterSheet.Booster booster)
    {
        var refs = _boosters.Get(booster.Id);

        view.SetText(TextRole.UnlockLevel, booster.UnlockLevel.ToString());
        view.SetText(TextRole.Cost, booster.Cost.ToString());
        view.SetText(TextRole.Count, booster.GetCount().ToString());
        view.SetImage(ImageRole.Icon, refs.Icon);

        var selectButton = view.GetButton(ButtonRole.Select);
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() =>
        {
            OnSelectClicked(booster, (StateRole)view.StateHistory[^1]);
        });

        _stateByBoosterType.Remove(booster.Id);
        OnUpdateElement(view, booster);
    }

    private void OnUpdateElement(StatefulComponent view, BoosterSheet.Booster booster)
    {
        if (!_sceneModule.IsSceneReady)
            return;

        var refs = _boosters.Get(booster.Id);
        var canSelect = refs.BoosterPrefab.CanSelect(_sceneModule.Scope.Container);
        view.SetImage(ImageRole.Background, canSelect ? refs.Background : refs.BackgroundDisabled);
        var isItemCountUpdateIntercepted = _economyModule.IsUpdateIntercepted(booster.Id.ToEconomyItem());
        if (!isItemCountUpdateIntercepted) view.SetText(TextRole.Count, booster.GetCount().ToString());

        var state = booster.IsUnlocked()
            ? canSelect ? booster.IsAvailable() ? StateRole.Available : StateRole.None : StateRole.Disabled
            : booster.IsAtUnlockLevel() ? StateRole.Unlock : StateRole.Lock;

        var canApplyState = true;
        if (_stateByBoosterType.TryGetValue(booster.Id, out var prevState))
            canApplyState = prevState != state;
        if (canApplyState) view.SetState(state);
        _stateByBoosterType[booster.Id] = state;

        if (state == StateRole.Unlock)
        {
            booster.Unlock();
            _economyModule.Add(new EconomyModule.Transaction
            {
                Units = booster.FreeAtStart,
                Item = booster.Id.ToEconomyItem(),
            });

            // var centerLockT = view.GetImage(ImageRole.CenterLock).transform;
            // await UniTask.Delay(2000);
            // await LMotion.Punch.Create(Vector3.one, Vector3.one * .1f, 1f)
            //     .BindToLocalScale(centerLockT);
            // await LMotion.Create(Vector3.one, VectorHelper.AlmostZero, .5f)
            //     .WithEase(Ease.InOutBack)
            //     .BindToLocalScaleNonNegative(centerLockT);

            state = StateRole.Available;
            view.SetState(state);
        }

        if (booster.IsFreeUseAllowed())
        {
            state = StateRole.Free;
            view.SetState(StateRole.Free);
        }
    }

    private void OnSelectClicked(BoosterSheet.Booster booster, StateRole state)
    {
        var canSelect = state != StateRole.Lock && _boosterSystem.CanSelect(booster.Id);
        switch (state)
        {
            case StateRole.None:
                if (!_boosterSystem.CanSelect(booster.Id)) break;
                var placement = booster.Id.ToInGamePurchasePlacement();
                _economyModule.Add(new EconomyModule.Transaction
                {
                    Units = 1,
                    Item = booster.Id.ToEconomyItem(),
                    ItemUsed = Item.None
                });
                _boosterSystem.Select(booster.Id);
                break;

            case StateRole.Available:
                if (canSelect) _boosterSystem.Select(booster.Id);
                else _hapticModule.PlayWarning();
                break;

            case StateRole.Lock:
                _hapticModule.PlayWarning();
                break;

            case StateRole.Free:
                // booster.Add(count: 2);
                // _boosterSystem.Select(booster.Id);
                break;
        }
    }

    private void OnCancelClicked()
    {
        _boosterSystem.TryCancel();
    }
}
