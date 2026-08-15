using System.Globalization;
using Cysharp.Threading.Tasks;
using Freya;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed partial class PlayingMonitor : MonitorBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private SceneModule _sceneModule;
    [Inject] private EconomyMonitor _economyMonitor;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private EconomyModule _economyModule;

    [Inject] private IPublisher<ReviveMessage> _revivePub;

    private IObjectResolver _sceneResolver;
    private IObjectResolver _appliedThemeResolver;
    private CapacityConfig _capacityConfig;
    private CoinConfig _coinConfig;
    private BoosterConfig _boosterConfig;
    private BoosterContainer _boosterContainer;
    private StatefulComponent _giveUpView;
    private AutoResetUniTaskCompletionSource<bool> _giveUpFlowContinue;
    private ConveyorSpeedSystem _conveyorSpeedSystem;
    private PaceConfig _paceConfig;
    private EGPConfig _egpConfig;

    private PlayerPrefsInt _level = Prefs.Level;

    private bool _isHolding;
    private CanvasGroup _holdTutorial;
    private int _lastHoldLevel;
    private bool _isHoldTutorialActive;
    private readonly CompositeMotionHandle _holdVignetteMotions = new();
    private readonly CompositeMotionHandle _holdTutorialMotions = new();

    public override void Setup()
    {
        base.Setup();

        HandleRemoteConfig();

        _boosterContainer = GetInnerComponent(InnerComponentRole.Booster).GetComponent<BoosterContainer>();
        _giveUpView = GetInnerComponent(InnerComponentRole.GiveUp);

        SetButtonListener(ButtonRole.Settings, OnSettingsClicked);
        SetupDebug();
        SetupGiveUp();

        GetImage(ImageRole.Dark).gameObject.SetActive(false);

        var goldView = GetInnerComponent(InnerComponentRole.Gold);
        _economyMonitor.AddItemView(Item.Gold, goldView);

        GetImage(ImageRole.Vignette).color = Color.white.WithAlpha(0f);
        _holdTutorial = GetObject(ObjectRole.Hold).GetComponent<CanvasGroup>();
        _holdTutorial.alpha = 0f;
        _lastHoldLevel = _level.Value;
    }

    private void SetupGiveUp()
    {
        _giveUpView.SetButtonListener(ButtonRole.Continue, OnGiveUpContinueClicked);
        _giveUpView.SetButtonListener(ButtonRole.GiveUp, OnGiveUpClicked);
    }

    public override void OnActivated()
    {
        base.OnActivated();

        GetInnerComponent(InnerComponentRole.HardLevel).gameObject.SetActive(false);
        GetInnerComponent(InnerComponentRole.SuperHardLevel).gameObject.SetActive(false);
        _giveUpView.gameObject.SetActive(false);

        _sceneResolver = _sceneModule.Container;

        _debugView.GetButton(ButtonRole.PreviousLevel).gameObject.SetActive(_level.Value > 0);
        GetText(TextRole.CurrentLevel).SetFormattedText((_level.Value + 1).ToString());
        ActivateDebug();

        _boosterContainer.gameObject.SetActive(true);
        _boosterContainer.RefreshElements();

        HandleTheme();

        _conveyorSpeedSystem = _sceneResolver.Resolve<ConveyorSpeedSystem>();
    }

    public override void OnDeactivated()
    {
        base.OnDeactivated();

        ResetHoldInput();
    }

    public override void Render()
    {
        base.Render();

        HandleHoldInput();
    }

    private void OnSettingsClicked()
    {
        if (_gameMachine.IsInState<GamePlayingState>())
            _gameMachine.RequestStateChange<GameSettingsState>();
    }

    private void HandleRemoteConfig()
    {
        _capacityConfig = _remoteConfigModule.GetDataClassNew<CapacityConfig>();
        _coinConfig = _remoteConfigModule.GetDataClassNew<CoinConfig>();
        _boosterConfig = _remoteConfigModule.GetDataClassNew<BoosterConfig>();
        _paceConfig = _remoteConfigModule.GetDataClassNew<PaceConfig>();
        _egpConfig = _remoteConfigModule.GetDataClassNew<EGPConfig>();
        SetState(StateRole.Reset);
        if (_capacityConfig.ShowInGame) SetState(StateRole.Capacity);
        if (_coinConfig.ShowInGame) SetState(StateRole.Gold);
    }

    private async UniTaskVoid HandleTheme()
    {
        if (_appliedThemeResolver == _sceneResolver) return;
        _appliedThemeResolver = _sceneResolver;

        while (_gameMachine.IsInState<GameTutorialState>()) await UniTask.Yield();

        var theme = _sceneResolver.Resolve<ThemeType>();
        if (theme == ThemeType.Hard)
        {
            var view = GetInnerComponent(InnerComponentRole.HardLevel);
            ApplyLevelPreviewMotion(view).Forget();
        }
        else if (theme == ThemeType.SuperHard)
        {
            var view = GetInnerComponent(InnerComponentRole.SuperHardLevel);
            ApplyLevelPreviewMotion(view).Forget();
        }
    }

    public async UniTask<bool> StartGiveUpFlow()
    {
        var boosterSystem = _sceneResolver.Resolve<BoosterSystem>();
        if (!boosterSystem.CanSelect(BoosterType.Capacity)) return true;

        var loseSystem = _sceneResolver.Resolve<LoseSystem>();
        if (loseSystem.GetLoseReason() != LoseReason.ConveyorFull) return true;

        _giveUpView.gameObject.SetActive(true);
        _boosterContainer.gameObject.SetActive(false);

        UpdateGiveUpBoosterView();

        _giveUpFlowContinue = AutoResetUniTaskCompletionSource<bool>.Create();
        return await _giveUpFlowContinue.Task;
    }

    private void OnGiveUpClicked()
    {
        _giveUpFlowContinue.TrySetResult(true);
    }

    private void OnGiveUpContinueClicked()
    {
        var capacityBooster = _sheetContainer.Boosters.Find(BoosterType.Capacity);
        if (!_egpConfig.UseBooster)
        {
            var goldConsumeArgs = new EconomyModule.Transaction
            {
                Item = Item.Gold,
                Units = capacityBooster.Cost
            };
            if (_economyModule.TryConsume(goldConsumeArgs))
            {
                _revivePub.Publish(new ReviveMessage { LoseReason = LoseReason.ConveyorFull });
            }
            else
            {
                _economyModule.GrantMissingGold(goldConsumeArgs);
                return;
            }
        }
        else if (capacityBooster.IsAvailable())
        {
            _revivePub.Publish(new ReviveMessage { LoseReason = LoseReason.ConveyorFull });
            _economyModule.TryConsume(new EconomyModule.Transaction
            {
                Units = 1,
                Item = Item.CapacityBooster,
            });
        }
        else
        {
            _economyModule.Add(new EconomyModule.Transaction
            {
                Item = Item.CapacityBooster,
                Units = 1,
                ItemUsed = Item.None
            });

            _revivePub.Publish(new ReviveMessage { LoseReason = LoseReason.ConveyorFull });
            _economyModule.TryConsume(new EconomyModule.Transaction
            {
                Units = 1,
                Item = Item.CapacityBooster,
            });
        }

        _giveUpFlowContinue.TrySetResult(false);
        _gameMachine.RequestStateChange<GamePlayingState>();
    }

    private void UpdateGiveUpBoosterView()
    {
        var capacityBooster = _sheetContainer.Boosters.Find(BoosterType.Capacity);
        var giveUpBoosterView = _giveUpView.GetInnerComponent(InnerComponentRole.Booster);
        var giveUpCoinView = _giveUpView.GetInnerComponent(InnerComponentRole.Coin);
        _boosterContainer.OnRefreshElement(giveUpBoosterView, capacityBooster);
        _boosterContainer.OnRefreshElement(giveUpCoinView, capacityBooster);

        if (!_egpConfig.UseBooster)
        {
            giveUpBoosterView.SetState(StateRole.None);
            giveUpCoinView.SetState(StateRole.None);
        }

        _giveUpView.SetState(StateRole.Reset);
    }

    private void HandleHoldInput()
    {
        if (!_paceConfig.HoldToSpeed) return;
        if (!_gameMachine.IsInState<GamePlayingState>() && !_gameMachine.IsInState<GameTutorialState>()) return;

        var vignette = GetImage(ImageRole.Vignette);
        var isHoldInputActive = _conveyorSpeedSystem.IsHoldInputActive();

        var currentLevel = _level.Value;
        var isTutorialRequired = 3 > currentLevel || currentLevel - _lastHoldLevel >= 5;
        if (isTutorialRequired && !isHoldInputActive && _conveyorSpeedSystem.IsLastCarrier())
        {
            if (!_isHoldTutorialActive)
            {
                _holdTutorialMotions.Cancel();
                _isHoldTutorialActive = true;
                LMotion.Create(0f, 1f, .25f)
                    .WithEase(Ease.OutQuad)
                    .BindToAlpha(_holdTutorial)
                    .AddTo(_holdTutorialMotions);
            }
        }
        else
        {
            if (_isHoldTutorialActive)
            {
                _holdTutorialMotions.Cancel();
                _isHoldTutorialActive = false;
                LMotion.Create(_holdTutorial.alpha, 0f, .25f)
                    .WithEase(Ease.OutQuad)
                    .BindToAlpha(_holdTutorial)
                    .AddTo(_holdTutorialMotions);
            }
        }

        if (isHoldInputActive)
        {
            if (_isHolding) return;
            _isHolding = true;
            _lastHoldLevel = currentLevel;

            _holdVignetteMotions.Cancel();
            LMotion.Create(0f, .5f, .25f)
                .WithEase(Ease.OutQuad)
                .WithOnComplete(() =>
                {
                    LMotion.Create(.5f, .2f, .5f)
                        .WithEase(Ease.OutQuad)
                        .WithLoops(-1, LoopType.Yoyo)
                        .BindToColorA(vignette)
                        .AddTo(_holdVignetteMotions);
                })
                .BindToColorA(vignette)
                .AddTo(_holdVignetteMotions);
        }
        else if (_isHolding)
        {
            _isHolding = false;
            _holdVignetteMotions.Cancel();
            LMotion.Create(vignette.color.a, 0f, .25f)
                .WithEase(Ease.OutQuad)
                .BindToColorA(vignette)
                .AddTo(_holdVignetteMotions);
        }
    }

    private void ResetHoldInput()
    {
        _holdVignetteMotions?.Cancel();
        _holdTutorialMotions?.Cancel();
        _isHolding = false;
        _isHoldTutorialActive = false;
        _holdTutorial.alpha = 0f;
        var vignette = GetImage(ImageRole.Vignette);
        vignette.color = Color.white.WithAlpha(0f);
    }
}

public struct ToggleDebuggerModeMessage
{
}

public partial class PlayingMonitor
{
    [Inject] private IPublisher<ToggleDebuggerModeMessage> _toggleDebuggerModePub;

    private StatefulComponent _debugView;

    private void SetupDebug()
    {
        SetButtonListener(ButtonRole.Debug, OnDebugClicked);
        _debugView = GetInnerComponent(InnerComponentRole.Debug);
        _debugView.SetButtonListener(ButtonRole.PreviousLevel, OnDebugPreviousLevelClicked);
        _debugView.SetButtonListener(ButtonRole.NextLevel, OnDebugNextLevelClicked);
        _debugView.SetButtonListener(ButtonRole.SheetReload, OnDebugSheetReloadClicked);
        _debugView.SetButtonListener(ButtonRole.TimeScale, OnDebugTimeScaleClicked);
        _debugView.SetButtonListener(ButtonRole.Console, OnDebugConsoleClicked);
        _debugView.SetButtonListener(ButtonRole.Win, OnDebugWinClicked);
        _debugView.SetButtonListener(ButtonRole.Lose, OnDebugLoseClicked);
        _debugView.GetTextInput(TextInputRole.Level).InputFieldTMP.onSubmit.AddListener(OnDebugLevelSubmit);
    }

    private void OnDebugClicked()
    {
        var debugView = GetInnerComponent(InnerComponentRole.Debug);
        var isActive = !debugView.gameObject.activeSelf;
        debugView.gameObject.SetActive(isActive);
    }

    private void ActivateDebug()
    {
        _debugView.SetText(TextRole.TimeScale, $"{Time.timeScale.ToString(CultureInfo.InvariantCulture)}x");
        _debugView.SetText(TextRole.Build, "");
    }

    private void OnDebugPreviousLevelClicked()
    {
        _level.Set(_level.Value - 1);
        _gameMachine.RequestStateChange<GameStartState>();
    }

    private void OnDebugNextLevelClicked()
    {
        _level.Set(_level.Value + 1);
        _gameMachine.RequestStateChange<GameStartState>();
    }

    private void OnDebugSheetReloadClicked()
    {
        ReloadSheetContainer().Forget();
    }

    private void OnDebugTimeScaleClicked()
    {
        var timeScale = Time.timeScale switch
        {
            1f => 2f,
            2f => 3f,
            0f => .5f,
            .5f => 1f,
            _ => 0f
        };
        Time.timeScale = timeScale;
        _debugView.SetText(TextRole.TimeScale, $"{Time.timeScale.ToString(CultureInfo.InvariantCulture)}x");
    }

    private void OnDebugConsoleClicked()
    {
        _toggleDebuggerModePub.Publish(new ToggleDebuggerModeMessage());
    }

    private void OnDebugWinClicked()
    {
        _gameMachine.RequestStateChange<GameWinState>();
    }

    private void OnDebugLoseClicked()
    {
        _gameMachine.RequestStateChange<GameReviveState>();
    }

    private void OnDebugLevelSubmit(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)) return;
        _level.Set(level - 1);
        _gameMachine.RequestStateChange<GameStartState>();
        _debugView.GetTextInput(TextInputRole.Level).InputFieldTMP.text = string.Empty;
    }

    private async UniTaskVoid ReloadSheetContainer()
    {
        if (_sheetContainer.Reloading)
            return;

        await _sheetContainer.Reload();
        _gameMachine.RequestStateChange<GameStartState>();
    }
}
