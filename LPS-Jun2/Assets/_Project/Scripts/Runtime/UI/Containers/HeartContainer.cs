using System.Collections.Generic;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using UnityEngine.UI;

public sealed class HeartContainer : MonoBehaviour
{
    private ContainerView _container;

    private readonly List<Image> _targetHeartImages = new();

    private void Awake()
    {
        _container = GetComponent<ContainerView>();
    }

    private void Update()
    {
        HandleTargetHeartImage();
    }

    public void RefreshHeartDisplay(int currentLife, int maxLife, int spendingLife)
    {
        _targetHeartImages.Clear();
        _container.Clear();
        for (var i = 0; i < maxLife; i++)
        {
            var view = _container.AddStatefulComponent();
            var available = currentLife > i;
            var icon = view.GetImage(ImageRole.Icon);
            icon.color = available ? Color.white : Color.white.ChangeA(.1f);

            if (!available) continue;
            if (currentLife - spendingLife > i) continue;

            _targetHeartImages.Add(icon);
        }
    }

    private void HandleTargetHeartImage()
    {
        if (_targetHeartImages.Count == 0) return;

        foreach (var targetHeartImage in _targetHeartImages)
        {
            var from = Color.white;
            var to = Color.white.ChangeA(.1f);
            var speed = 1.5f;
            var time = Time.unscaledTime * speed;
            var t = Mathf.PingPong(time, 1f);
            targetHeartImage.color = Color.Lerp(from, to, t);
        }
    }
}