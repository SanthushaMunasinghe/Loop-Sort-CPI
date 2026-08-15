using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class ConsoleListener
{
    private static readonly LinkedList<string> ErrorLogs = new();
    private const int MaxErrorLogs = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnInitialize()
    {
        Application.logMessageReceived += OnLogReceived;
    }

    private static void OnLogReceived(string message, string stackTrace, LogType type)
    {
        if (type is not (LogType.Error or LogType.Exception)) return;

        if (ErrorLogs.Count >= MaxErrorLogs)
            ErrorLogs.RemoveFirst();

        ErrorLogs.AddLast($"[{type}] {message}\n{stackTrace}");
    }

    public static void GetErrors(StringBuilder sb)
    {
        foreach (var error in ErrorLogs)
            sb.AppendLine(error);
    }
}