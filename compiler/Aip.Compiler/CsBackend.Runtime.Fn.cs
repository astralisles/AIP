namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Generated runtime helpers for the std list functions that take a function value (T-0976).</summary>
    const string RtFn = """
        public static partial class Rt
        {
            static object Call1(System.Func<object[], object> f, object a) => f(new object[] { a });
            public static Aip.Persistent.PList ListMap(Aip.Persistent.PList l, System.Func<object[], object> f)
            {
                var r = Aip.Persistent.PList.Empty;
                foreach (var x in l) r = r.Append(Call1(f, x));
                return r;
            }
            public static Aip.Persistent.PList ListFilter(Aip.Persistent.PList l, System.Func<object[], object> f)
            {
                var r = Aip.Persistent.PList.Empty;
                foreach (var x in l) if ((bool)Call1(f, x)) r = r.Append(x);
                return r;
            }
            public static object ListFold(Aip.Persistent.PList l, object start, System.Func<object[], object> f)
            {
                var acc = start;
                foreach (var x in l) acc = f(new object[] { acc, x });
                return acc;
            }
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
