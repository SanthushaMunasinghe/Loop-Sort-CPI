using System;
using UnityEngine;

public sealed class BlockTrigger : MonoBehaviour
{
    private Action<Block> _listener;

    private void OnDisable()
    {
        _listener = null;
    }

    public void AddListener(Action<Block> listener)
    {
        _listener = listener;
    }

    private void OnTriggerEnter(Collider other)
    {
        var block = other.GetComponent<Block>();
        if (block == null) return;
        _listener?.Invoke(block);
    }
}