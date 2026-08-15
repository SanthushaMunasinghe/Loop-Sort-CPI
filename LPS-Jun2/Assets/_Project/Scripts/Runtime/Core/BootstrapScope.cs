using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class BootstrapScope : LifetimeScope
{
    public static bool IsInitialized { get; private set; }
    public static Dictionary<string, string> InitializeArgs { get; } = new();

    private readonly List<InstallerBase> _installers = new();

    protected override void Awake()
    {
    }

    private void Start()
    {
        InitializeInstallers().Forget();
    }

    private async UniTaskVoid InitializeInstallers()
    {
        DontDestroyOnLoad(this);

        await UniTask.DelayFrame(4);

        var startTimestamp = Time.realtimeSinceStartup;

        var installerTimes = new StringBuilder();
        var baseInstallerType = typeof(InstallerBase);
        using var p = baseInstallerType.GetDerivedClassTypes(out var derivedInstallerClassTypes);
        foreach (var installerType in derivedInstallerClassTypes)
        {
            var timestamp = Time.realtimeSinceStartup;
            if (Activator.CreateInstance(installerType) is not InstallerBase installer) continue;

            try
            {
                await installer.Initialize(InitializeArgs);
            }
            catch (Exception e)
            {
                Debug.LogError($"{installerType} Initialize error: " + e.Message + "\n" + e.StackTrace + "\n\n");
                throw;
            }

            _installers.Add(installer);
            var initializeTime = Mathf.CeilToInt((Time.realtimeSinceStartup - timestamp) * 1000);
            installerTimes.AppendLine($"  • <i>{installerType.Name}</i> {initializeTime}ms");
            await UniTask.Yield();
        }

        var configureTimestamp = Time.realtimeSinceStartup;
        base.Awake();
        var configureTime = Mathf.CeilToInt((Time.realtimeSinceStartup - configureTimestamp) * 1000);

        var postBuildTimestamp = Time.realtimeSinceStartup;
        foreach (var installer in _installers)
        {
            await installer.PostBuild();
        }
        var postBuildTime = Mathf.CeilToInt((Time.realtimeSinceStartup - postBuildTimestamp) * 1000);

        IsInitialized = true;

        var totalTime = TimeSpan.FromSeconds(Time.realtimeSinceStartup - startTimestamp);
        Debug.Log($"<b>Installer Report</b>: {totalTime.TotalSeconds:F2} seconds\n" +
                  "────────────────────────────────────\n" +
                  $"{installerTimes}" +
                  "────────────────────────────────────\n" +
                  $" <b>Configure Time</b>: {configureTime}ms\n" +
                  $" <b>Post Build Time</b>: {postBuildTime}ms\n" +
                  "────────────────────────────────────\n"
        );
    }

    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        foreach (var installer in _installers)
        {
            try
            {
                installer.Install(builder);
            }
            catch (Exception e)
            {
                Debug.LogError($"{installer.GetType()} Install error: " + e.Message + "\n" + e.StackTrace + "\n\n");
                throw;
            }
        }
    }
}