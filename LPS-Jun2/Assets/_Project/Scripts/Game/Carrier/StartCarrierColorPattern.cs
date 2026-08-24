using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StartCarrierColorPattern", menuName = "Carriers/Start Carrier Color Pattern")]
public sealed class StartCarrierColorPattern : ScriptableObject
{
    [Serializable]
    public struct ColorBlock
    {
        public ColorType Color;
        [Min(1)] public int ConsecutiveGroupCount;
    }

    [Tooltip("Sequential color blocks a Start carrier's groups are painted with, starting from the " +
             "carrier's last group (the front, the first one it dispenses) and working backward — " +
             "e.g. 2 Red groups then 1 Blue group paints the last group Red, the second-last Red, and " +
             "the third-last Blue. Groups this pattern doesn't reach fall back to the carrier's " +
             "Override Start Color / Max Consecutive Same Color Groups / random draw instead of " +
             "looping back to the first block.")]
    [SerializeField] private ColorBlock[] _blocks;

    public IReadOnlyList<ColorBlock> Blocks => _blocks;

    /// <summary>Expands this pattern's blocks into colors, one per ConsecutiveGroupCount, in order —
    /// stopping once the blocks are exhausted or `maxCount` is reached, whichever comes first. Does
    /// not loop back to the first block. Index 0 is the color for the carrier's last group (its
    /// caller applies these back-to-front — see SceneScope.ApplyRandomCarrierColors). Empty when this
    /// pattern has no blocks.</summary>
    public List<ColorType> GetColors(int maxCount)
    {
        var colorTypes = new List<ColorType>(maxCount);
        if (_blocks == null) return colorTypes;

        foreach (var block in _blocks)
        {
            var count = Mathf.Max(0, block.ConsecutiveGroupCount);
            for (var i = 0; i < count; i++)
            {
                if (colorTypes.Count >= maxCount) return colorTypes;
                colorTypes.Add(block.Color);
            }
        }

        return colorTypes;
    }
}
