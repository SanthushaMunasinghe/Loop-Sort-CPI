using System.Collections.Generic;
using UnityEngine;

public static partial class Extensions
{
    /// <summary>
    /// Returns a random element from the given collection.
    /// </summary>
    /// <returns> A random element from the collection.</returns>
    public static T GetRandom<T>(this IList<T> collection)
    {
        if (collection == null || collection.Count == 0) return default;
        var randomIndex = Random.Range(0, collection.Count);
        return collection[randomIndex];
    }

    /// <summary>
    /// Returns a random element from the given collection.
    /// </summary>
    /// <returns> A random element from the collection.</returns>
    public static T GetRandom<T>(this T[] collection)
    {
        if (collection == null || collection.Length == 0) return default;
        var randomIndex = Random.Range(0, collection.Length);
        return collection[randomIndex];
    }

    /// <summary>
    /// Shuffles the elements of the list in a random order using the Fisher-Yates algorithm.
    /// </summary>
    public static void Shuffle<T>(this IList<T> list)
    {
        if (list == null || list.Count <= 1) return;

        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Range(0, i + 1);
            Swap(list, i, j);
        }
    }

    /// <summary>
    /// Shuffles the elements of the array in a random order using the Fisher-Yates algorithm.
    /// </summary>
    public static void Shuffle<T>(this T[] array)
    {
        if (array == null || array.Length <= 1) return;

        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = Random.Range(0, i + 1);
            Swap(array, i, j);
        }
    }

    /// <summary>
    /// Shuffles the elements of the list ensuring no element remains in its original position (derangement).
    /// Note: Requires at least 2 elements.
    /// </summary>
    public static void ShuffleDeranged<T>(this IList<T> list)
    {
        if (list == null || list.Count < 2) return;

        // Use rejection sampling: keep shuffling until we get a valid derangement
        const int maxAttempts = 100;
        var attempts = 0;
        bool isDerangement;

        // Store original positions
        var originalPositions = new T[list.Count];
        for (var i = 0; i < list.Count; i++) originalPositions[i] = list[i];

        do
        {
            list.Shuffle();

            // Check if it's a derangement (no element in original position)
            isDerangement = true;
            for (var i = 0; i < list.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(list[i], originalPositions[i]))
                {
                    isDerangement = false;
                    break;
                }
            }

            attempts++;
        } while (!isDerangement && attempts < maxAttempts);

        // Fallback: if rejection sampling fails, use deterministic approach
        if (!isDerangement)
        {
            // Simple swap: move each element to next position (cycling)
            var temp = list[0];
            for (var i = 0; i < list.Count - 1; i++)
            {
                list[i] = list[i + 1];
            }
            list[^1] = temp;
        }
    }

    /// <summary>
    /// Shuffles the elements of the array ensuring no element remains in its original position (derangement).
    /// Note: Requires at least 2 elements.
    /// </summary>
    public static void ShuffleDeranged<T>(this T[] array)
    {
        if (array == null || array.Length < 2) return;

        // Use rejection sampling: keep shuffling until we get a valid derangement
        const int maxAttempts = 100;
        var attempts = 0;
        bool isDerangement;

        // Store original positions
        var originalPositions = new T[array.Length];
        for (var i = 0; i < array.Length; i++) originalPositions[i] = array[i];

        do
        {
            array.Shuffle();

            // Check if it's a derangement (no element in original position)
            isDerangement = true;
            for (var i = 0; i < array.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(array[i], originalPositions[i]))
                {
                    isDerangement = false;
                    break;
                }
            }

            attempts++;
        } while (!isDerangement && attempts < maxAttempts);

        // Fallback: if rejection sampling fails, use deterministic approach
        if (!isDerangement)
        {
            // Simple swap: move each element to next position (cycling)
            var temp = array[0];
            for (var i = 0; i < array.Length - 1; i++)
            {
                array[i] = array[i + 1];
            }
            array[^1] = temp;
        }
    }

    /// <summary>
    /// Swaps the elements at the specified indices in the list.
    /// </summary>
    public static void Swap<T>(this IList<T> list, int i, int j)
    {
        (list[i], list[j]) = (list[j], list[i]);
    }

    /// <summary>
    /// Swaps the elements at the specified indices in the array.
    /// </summary>
    public static void Swap<T>(this T[] array, int i, int j)
    {
        (array[i], array[j]) = (array[j], array[i]);
    }

    /// <summary>
    /// Returns the element at the specified index, or the last element if the index is out of range.
    /// </summary>
    public static T GetClamped<T>(this IList<T> list, int index)
    {
        if (list == null || list.Count == 0) return default;
        return list[index >= list.Count ? ^1 : 0 > index ? 0 : index];
    }

    /// <summary>
    /// Returns the element at the specified index, or the last element if the index is out of range.
    /// </summary>
    public static T GetClamped<T>(this T[] array, int index)
    {
        if (array == null || array.Length == 0) return default;
        return array[index >= array.Length ? ^1 : 0 > index ? 0 : index];
    }

    /// <summary>
    /// Returns the element at the specified index, wrapping around if index exceeds collection size.
    /// </summary>
    public static T GetWrapped<T>(this IList<T> list, int index)
    {
        if (list == null || list.Count == 0 || 0 > index) return default;
        return list[index % list.Count];
    }

    /// <summary>
    /// Returns the element at the specified index, wrapping around if index exceeds array length.
    /// </summary>
    public static T GetWrapped<T>(this T[] array, int index)
    {
        if (array == null || array.Length == 0 || 0 > index) return default;
        return array[index % array.Length];
    }

    public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> collection)
    {
        if (hashSet == null) return;
        using var enumerator = collection.GetEnumerator();
        while (enumerator.MoveNext()) hashSet.Add(enumerator.Current);
    }
}