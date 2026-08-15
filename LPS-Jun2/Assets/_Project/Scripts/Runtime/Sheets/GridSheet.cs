using System.Collections.Generic;
using Cathei.BakingSheet;

public sealed class GridSheet : Sheet<int, GridSheet.Layouts>
{
    public class Layouts : SheetRow<int>
    {
        public Dictionary<int, VerticalList<string>> A { get; private set; }
        public Dictionary<int, VerticalList<string>> B { get; private set; }
        public Dictionary<int, VerticalList<string>> C { get; private set; }
        public Dictionary<int, VerticalList<string>> D { get; private set; }
        public Dictionary<int, VerticalList<string>> E { get; private set; }

        private readonly Dictionary<LayoutType, Layout> _layoutByType = new();

        public override void PostLoad(SheetConvertingContext context)
        {
            base.PostLoad(context);

            foreach (var (_, rows) in A) rows.Reverse();
            foreach (var (_, rows) in B) rows.Reverse();
            foreach (var (_, rows) in C) rows.Reverse();
            foreach (var (_, rows) in D) rows.Reverse();
            foreach (var (_, rows) in E) rows.Reverse();

            _layoutByType[LayoutType.A] = Layout.Create(A);
            _layoutByType[LayoutType.B] = Layout.Create(B);
            _layoutByType[LayoutType.C] = Layout.Create(C);
            _layoutByType[LayoutType.D] = Layout.Create(D);
            _layoutByType[LayoutType.E] = Layout.Create(E);
        }


        public Layout GetLayout(LayoutType layoutType)
        {
            return _layoutByType.GetValueOrDefault(layoutType);
        }

        public Dictionary<LayoutType, Layout>.ValueCollection.Enumerator GetLayouts()
        {
            return _layoutByType.Values.GetEnumerator();
        }
    }

    public struct Layout
    {
        public Cell[,] Cells;

        public static Layout Create(Dictionary<int, VerticalList<string>> dict)
        {
            var layout = new Layout();
            var horizontal = dict.Count;
            var vertical = dict[0].Count;
            layout.Cells = new Cell[horizontal, vertical];
            for (var x = 0; x < horizontal; x++)
            {
                for (var y = 0; y < vertical; y++)
                {
                    var cell = layout.Cells[x, y];
                    cell.Data = dict[x][y];
                    layout.Cells[x, y] = cell;
                }
            }

            return layout;
        }
    }

    public struct Cell
    {
        public string Data;
    }

    public enum LayoutType
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3,
        E = 4,
    }
}