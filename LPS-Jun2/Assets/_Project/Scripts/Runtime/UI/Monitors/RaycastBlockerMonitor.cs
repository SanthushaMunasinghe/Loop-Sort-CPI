using System.Collections.Generic;
using UnityEngine;

public sealed class RaycastBlockerMonitor : MonitorBase
{
    private RaycastBlockerImage _blocker;
    private readonly HashSet<object> _blockers = new();

    private void Start()
    {
        _blocker = GetComponentInChildren<RaycastBlockerImage>();
        Unblock();
    }

    public void Block(object owner = null)
    {
        if (owner != null) _blockers.Add(owner);
        gameObject.SetActive(true);
    }

    public void Unblock(object owner = null)
    {
        if (owner != null) _blockers.Remove(owner);
        if (_blockers.Count > 0) return;
        _blocker.ResetClickableArea();
        gameObject.SetActive(false);
    }

    public void SetClickableArea(RectTransform clickableArea)
    {
        _blocker.SetClickableArea(clickableArea);
    }
}