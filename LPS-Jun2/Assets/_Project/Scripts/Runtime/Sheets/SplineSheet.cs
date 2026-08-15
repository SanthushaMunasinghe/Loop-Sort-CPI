using System;
using System.Collections.Generic;
using Cathei.BakingSheet;
using UnityEngine;
using NonSerialized = Cathei.BakingSheet.NonSerializedAttribute;

public sealed class SplineSheet : Sheet<int, SplineSheet.Layout>
{
    public class Layout : SheetRow<int>
    {
        public SheetVector2 CameraOffset { get; private set; }
        public float CameraRotation { get; private set; }
        public bool Closed { get; private set; }
        public float Spacing { get; private set; }
        public int Subdivide { get; private set; }
        public string Spline { get; private set; }

        [NonSerialized] public Grid Data { get; private set; }

        public override void PostLoad(SheetConvertingContext context)
        {
            base.PostLoad(context);

            try
            {
                Data = Grid.Create(Spline);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing SplineSheet Id: {Id}");
                throw;
            }

#if !RELEASE_BUILD
            CheckCapacityCarriers();
#endif
        }

        private void CheckCapacityCarriers()
        {
            var capacityCarrierCount = 0;
            foreach (var cell in Data.ActiveCells)
            {
                var c = cell.Data[0];
                if (c == 'Y' || c == 'Z')
                {
                    capacityCarrierCount++;
                }
            }

            if (capacityCarrierCount == 2) return;
            Debug.LogWarning($"[SplineSheet] Level {Id} wrong capacity carriers count: {capacityCarrierCount}");
        }
    }

    public struct Grid
    {
        public List<Cell> Cells;
        public List<Cell> ActiveCells;

        public static Grid Create(string data)
        {
            var layout = new Grid
            {
                Cells = new List<Cell>(),
                ActiveCells = new List<Cell>()
            };

            foreach (var cell in data.Split(':'))
            {
                var cellSplit = cell.Split(';');
                var cellData = cellSplit[0];
                var x = int.Parse(cellSplit[1]);
                var y = int.Parse(cellSplit[2]);
                var item = new Cell { Data = cellData, X = x, Y = y };
                layout.Cells.Add(item);
                if (cellData == "-") continue;
                layout.ActiveCells.Add(item);
            }

            return layout;
        }
    }

    public struct Cell
    {
        public string Data;
        public int X;
        public int Y;
    }
}