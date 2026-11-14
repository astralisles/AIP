namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Higher-order list helpers and tasks. A function value is a Func taking its arguments in an array.
    /// Tasks run on their own thread with the same 512 MB stack as the main thread, so deep recursion works there too.</summary>
    const string RtTasks = """
        public static partial class Rt
        {
            public static Aip.Persistent.PList ListMap(Aip.Persistent.PList items, System.Func<object[], object> f)
            {
                var r = Aip.Persistent.PList.Empty;
                foreach (var x in items) r = r.Append(f(new[] { x }));
                return r;
            }
            public static Aip.Persistent.PList ListFilter(Aip.Persistent.PList items, System.Func<object[], object> f)
            {
                var r = Aip.Persistent.PList.Empty;
                foreach (var x in items) if ((bool)f(new[] { x })) r = r.Append(x);
                return r;
            }
            public static object ListFold(Aip.Persistent.PList items, object start, System.Func<object[], object> f)
            {
                var acc = start;
                foreach (var x in items) acc = f(new[] { acc, x });
                return acc;
            }
            /// <summary>Same result as ListMap, always in input order; items are computed on several threads.</summary>
            public static Aip.Persistent.PList ListMapPar(Aip.Persistent.PList items, System.Func<object[], object> f)
            {
                var src = new System.Collections.Generic.List<object>(items).ToArray();
                var res = new object[src.Length];
                var tasks = new System.Collections.Generic.List<RtTask>();
                int workers = System.Math.Max(1, System.Math.Min(src.Length, System.Environment.ProcessorCount));
                for (int w = 0; w < workers; w++)
                {
                    int from = (int)((long)src.Length * w / workers), to = (int)((long)src.Length * (w + 1) / workers);
                    tasks.Add(new RtTask(() => { for (int i = from; i < to; i++) res[i] = f(new[] { src[i] }); return null; }));
                }
                foreach (var t in tasks) t.Await(); // the first failure (in input order) is rethrown
                return Aip.Persistent.PList.From(res);
            }
            public static object TaskRun(System.Func<object[], object> f, object arg) => new RtTask(() => f(new[] { arg }));
            public static object TaskAwait(object task) => ((RtTask)task).Await();
        }

        public sealed class RtTask
        {
            readonly System.Threading.Thread thread;
            object result;
            System.Exception error;
            public RtTask(System.Func<object> work)
            {
                thread = new System.Threading.Thread(() => { try { result = work(); } catch (System.Exception e) { error = e; } }, 512 * 1024 * 1024);
                thread.Start();
            }
            public object Await()
            {
                thread.Join();
                if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                return result;
            }
        }
        """;
}
