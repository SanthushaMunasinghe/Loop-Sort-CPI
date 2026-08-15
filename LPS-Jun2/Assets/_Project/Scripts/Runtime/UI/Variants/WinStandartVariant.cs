using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using Spine.Unity;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class WinStandartVariant : MonitorVariantBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private AudioModule _audioModule;
    [Inject] private WinMonitor _winMonitor;
    [Inject] private EconomyModule _economyModule;
    [Inject] private EconomyMonitor _economyMonitor;
    [Inject] private SheetContainer _sheetContainer;

    private SkeletonGraphic _wordArt;
    // private SkeletonGraphic _piggy;
    private SkeletonGraphic _wellDone;
    private VerticalLayoutGroup _resultVerticalLayout;
    private CancellationTokenSource _wordArtToken;
    private StatefulComponent _goldRewardView;

    private int _goldRewardAmount;

    public override void Setup()
    {
        base.Setup();

        SetButtonListener(ButtonRole.Continue, OnContinueClicked);
        SetButtonListener(ButtonRole.RV, OnContinueClicked);

        _wordArt = GetObject(ObjectRole.WordArt).GetComponentInChildren<SkeletonGraphic>();
        // _piggy = GetObject(ObjectRole.Piggy).GetComponent<SkeletonGraphic>();
        _wellDone = GetObject(ObjectRole.Top).GetComponentInChildren<SkeletonGraphic>();
        _resultVerticalLayout = GetObject(ObjectRole.Result).GetComponent<VerticalLayoutGroup>();
        _economyMonitor.AddItemView(Item.Gold, GetInnerComponent(InnerComponentRole.Gold));
    }

    public override void OnActivated()
    {
        base.OnActivated();

        OnActivatedAsync().Forget();
    }

    public override void Render()
    {
        base.Render();

        if (!Input.GetMouseButtonDown(0)) return;
        _wordArtToken?.Cancel();
    }

    private async UniTaskVoid OnActivatedAsync()
    {
        var currentFeatureProgress = _sheetContainer.Features.GetCurrentProgress();
        var continueButton = GetButton(ButtonRole.Continue);
        var rvButton = GetButton(ButtonRole.RV);
        var progressObject = GetObject(ObjectRole.Progress);
        var rewardObject = GetObject(ObjectRole.Reward);
        var wordArtObject = GetObject(ObjectRole.WordArt);
        var resultObject = GetObject(ObjectRole.Result);
        var confettiObject = GetObject(ObjectRole.Confetti);
        GetContainer(ContainerRole.Reward).Clear();

        wordArtObject.SetActive(true);
        wordArtObject.transform.localScale = Vector3.one;
        resultObject.SetActive(false);
        confettiObject.SetActive(false);
        progressObject.transform.localScale = Vector3.zero;
        rewardObject.transform.localScale = Vector3.zero;
        continueButton.transform.localScale = Vector3.zero;
        continueButton.interactable = false;
        continueButton.gameObject.SetActive(true);
        rvButton.gameObject.SetActive(false);
        _wordArtToken = new CancellationTokenSource();
        try
        {
            await ApplyWordArtMotion(confettiObject, wordArtObject, _wordArtToken.Token);
        }
        catch
        {
            // ignored
        }
        _wordArtToken = null;

        wordArtObject.SetActive(false);
        resultObject.SetActive(true);
        progressObject.SetActive(currentFeatureProgress.HasProgress);
        RebuildResultVerticalLayout();
        {
            var isDefaultLanguage = Localization.IsDefaultLanguage();
            var wellDoneState = isDefaultLanguage ? StateRole.Skeleton : StateRole.Text;
            var wellDoneView = GetInnerComponent(InnerComponentRole.WellDone);
            wellDoneView.SetState(wellDoneState);
            if (wellDoneState == StateRole.Text)
                LMotion.Create(Vector3H.AlmostZero, Vector3.one, .4f)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithEase(Ease.InOutBack)
                    .BindToLocalScaleNonNegative(wellDoneView.transform);
            else
                _wellDone.AnimationState.SetAnimation(0, "animation", loop: false);

            FillRewardElements();
            await UniTask.Delay(600, ignoreTimeScale: true);
            if (currentFeatureProgress.HasProgress)
            {
                LMotion.Create(Vector3H.AlmostZero, Vector3.one, .4f)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithEase(Ease.InOutBack)
                    .BindToLocalScaleNonNegative(progressObject.transform);
                var giftBoxElement = GetObject(ObjectRole.Gift).GetComponent<GiftBoxElement>();
                giftBoxElement.ApplyProgressMotion().Forget();
                await UniTask.Delay(600, ignoreTimeScale: true);
            }
            // UpdatePiggyBankView();
            await LMotion.Create(Vector3H.AlmostZero, Vector3.one, .4f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithEase(Ease.InOutBack)
                .BindToLocalScaleNonNegative(rewardObject.transform);
        }

        await UniTask.Delay(300, ignoreTimeScale: true);
        continueButton.transform.localScale = Vector3.one;
        continueButton.interactable = true;

        var state = Prefs.Level.Value + 1 > 10 ? StateRole.RV : StateRole.Reset;
        SetState(state);
    }

    private void FillRewardElements()
    {
        var container = GetContainer(ContainerRole.Reward);
        container.Clear();
        foreach (var reward in _winMonitor.Results.Rewards)
        {
            var view = container.AddStatefulComponent();
            CreateRewardElement(view, reward);
        }
    }

    private void CreateRewardElement(StatefulComponent view, RewardInfo data)
    {
        view.SetText(TextRole.Quantity, data.Quantity.ToString());

        if (data.Item != Item.Gold) return;
        _goldRewardView = view;
        _goldRewardAmount = data.Quantity;
    }

    private async UniTask ApplyWordArtMotion(GameObject confettiObject, GameObject wordArtObject, CancellationToken token)
    {
        _audioModule.GetPlayer().WithVolumeScale(.75f).Play(_audioModule.Sounds.Win);

        var trackEntry = _wordArt.AnimationState.SetAnimation(0, "animation", loop: false);
        await trackEntry.WaitUntilFirstEvent(token);
        confettiObject.SetActive(true);
        await trackEntry.WaitUntilComplete(token);

        await UniTask.Delay(2700, ignoreTimeScale: true, cancellationToken: token);
        await LMotion.Create(Vector3.one, Vector3H.AlmostZero, .450f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.InOutBack)
            .BindToLocalScaleNonNegative(wordArtObject.transform)
            .ToUniTask(token);
    }

    private void OnContinueClicked()
    {
        HandleContinueClickAsync().Forget();
    }

    private async UniTaskVoid HandleContinueClickAsync()
    {
        Monitors.DisableRaycaster();

        GetButton(ButtonRole.Continue).gameObject.SetActive(false);
        GetButton(ButtonRole.RV).gameObject.SetActive(false);
        HandleEconomyUpdate();
        _gameMachine.RequestStateChange<GameStartState>();
    }
    
    private void HandleEconomyUpdate()
    {
        foreach (var rewards in _winMonitor.Results.Rewards)
        {
            if (rewards.Item == Item.CollectionBarToken) continue;
            var addArgs = new EconomyModule.Transaction
            {
                Item = rewards.Item,
                Units = rewards.Quantity,
            };
            if (rewards.Item == Item.Gold)
            {
                var rewardText = _goldRewardView.GetText(TextRole.Quantity).TMP;
                addArgs.WorldPosition = rewardText.transform.position;
                addArgs.View = GetInnerComponent(InnerComponentRole.Gold);
            }
            _economyModule.Add(addArgs);
        }
    }

    private void RebuildResultVerticalLayout()
    {
        _resultVerticalLayout.enabled = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_resultVerticalLayout.transform);
        _resultVerticalLayout.enabled = false;
    }
}
