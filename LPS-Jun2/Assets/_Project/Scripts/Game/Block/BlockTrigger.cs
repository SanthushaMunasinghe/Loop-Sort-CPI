using System;
using UnityEngine;

public sealed class BlockTrigger : MonoBehaviour
{
    private Action<Block> _listener;
    private bool _isActive = true;

    public bool IsActive => _isActive;

    public void AddListener(Action<Block> listener)
    {
        _listener = listener;
    }

    public void SetActive(bool isActive)
    {
        _isActive = isActive;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive) return;

        var block = other.GetComponent<Block>();
        if (block == null) return;
        _listener?.Invoke(block);
    }
}