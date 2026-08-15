using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Pool;

public static partial class Extensions
{
    private static readonly Dictionary<Assembly, Type[]> CachedAssemblyTypes = new();

    /// <summary>
    /// Retrieves all types that are derived from the specified base class type.
    /// </summary>
    public static PooledObject<List<Type>> GetDerivedClassTypes(this Type baseClassType, out List<Type> result)
    {
        result = null;

        if (baseClassType == null || !baseClassType.IsClass)
            return default;

        var p = ListPool<Type>.Get(out result);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            if (!CachedAssemblyTypes.TryGetValue(assembly, out var types))
            {
                try
                {
                    types = assembly.GetTypes();
                }
                // Some Unity editor/IL2CPP assemblies may fail to load all types.
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                CachedAssemblyTypes[assembly] = types;
            }

            if (types == null)
                continue;

            foreach (var type in types)
            {
                if (type != null && type.IsClass && type.IsSubclassOf(baseClassType))
                    result.Add(type);
            }
        }

        return p;
    }
}