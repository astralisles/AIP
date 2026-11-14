namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Generated runtime helpers for the std list functions that take a function value (T-0976).</summary>
    const string RtFn = """
        public static partial class Rt
        {
            public static Aip.Persistent.PList ListSort(Aip.Persistent.PList l, System.Func<object[], object> less)
            {
                var items = new System.Collections.Generic.List<object>(l);
                var sorted = System.Linq.Enumerable.OrderBy(items, x => x, System.Collections.Generic.Comparer<object>.Create((a, b) =>
                    (bool)less(new object[] { a, b }) ? -1 : (bool)less(new object[] { b, a }) ? 1 : 0)); // OrderBy is stable
                var r = Aip.Persistent.PList.Empty;
                foreach (var x in sorted) r = r.Append(x);
                return r;
            }
        }
        """;
}
