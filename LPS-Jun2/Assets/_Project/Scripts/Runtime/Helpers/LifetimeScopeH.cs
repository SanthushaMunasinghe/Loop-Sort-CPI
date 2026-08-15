using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

public static class LifetimeScopeH
{
    private static readonly Dictionary<Type, LifetimeScope> ScopeByType = new();

    public static LifetimeScope FindScope<T>() where T : LifetimeScope
    {
        var scopeType = typeof(T);
        if (!ScopeByType.TryGetValue(scopeType, out var scope) || scope == null)
        {
            scope = LifetimeScope.Find<T>();
            ScopeByType[scopeType] = scope;
        }

        return scope;
    }

    public static bool InjectIfNotScoped<T>(Object target) where T : LifetimeScope
    {
        var scope = FindScope<T>();
        if (scope == null) return false;
        if (scope.Container == null) return false;
        if (target is GameObject targetGameObject)
            scope.Container.InjectGameObject(targetGameObject);
        else
            scope.Container.Inject(target);
        return true;
    }

    public static bool InjectIfNotScoped<T>(object target) where T : LifetimeScope
    {
        var scope = FindScope<T>();
        if (scope == null) return false;
        if (scope.Container == null) return false;
        scope.Container.Inject(target);
        return true;
    }
}