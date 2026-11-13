/// <summary>
/// The run-time collections of the C# back end, emitted into every built program as Persistent.cs (design: docs/design/memory-management-T-0961.md).
/// All of them are persistent: an update returns a new collection that shares almost all of its memory with the old one,
/// and the old one stays valid. Cost per operation (n = size, 32-way tree, so log32 n is at most 7 for a billion items):
///   List      get, set, append, length  O(log32 n); insert / remove in the middle, concat, slice, reverse O(n)
///   Map, Set  has, get, set, remove     O(log32 n) expected; size O(1); keys in first-added order
///   Sorted*   has, get, set, remove, min O(log2 n); size O(1); keys ascending
///   Queue     push, front, pop          amortised O(log32 n) / O(log32 n) / amortised O(1)
///   PriorityQueue push, pop             O(log2 n); front O(log2 n); ties in the order added
/// </summary>
static class PersistentRt
{
    public const string Source = """
// Generated: persistent collections. Do not edit.
namespace Aip.Persistent
{
    /// <summary>A persistent vector: a 32-way trie plus a tail array (the design of Clojure's vectors).</summary>
    public sealed class PList : System.Collections.Generic.IEnumerable<object>
    {
        const int B = 5, W = 32, M = 31;
        public static readonly PList Empty = new(0, B, new object[W], System.Array.Empty<object>());
        readonly int count, shift;
        readonly object[] root, tail;
        PList(int count, int shift, object[] root, object[] tail) { this.count = count; this.shift = shift; this.root = root; this.tail = tail; }

        public int Count => count;
        int TailOff => count < W ? 0 : ((count - 1) >> B) << B;

        object[] ArrayFor(int i)
        {
            if (i >= TailOff) return tail;
            var n = root;
            for (int l = shift; l > 0; l -= B) n = (object[])n[(i >> l) & M];
            return n;
        }

        public object this[int i]
        {
            get
            {
                if ((uint)i >= (uint)count) throw new System.ArgumentOutOfRangeException("index", "list index " + i + " is outside 0.." + (count - 1));
                return ArrayFor(i)[i & M];
            }
        }

        static object[] NewPath(int level, object[] node)
        {
            if (level == 0) return node;
            var r = new object[W];
            r[0] = NewPath(level - B, node);
            return r;
        }

        object[] PushTail(int level, object[] parent, object[] tailNode)
        {
            int sub = ((count - 1) >> level) & M;
            var r = (object[])parent.Clone();
            r[sub] = level == B ? tailNode : (parent[sub] != null ? PushTail(level - B, (object[])parent[sub], tailNode) : NewPath(level - B, tailNode));
            return r;
        }

        public PList Append(object x)
        {
            if (count - TailOff < W)
            {
                var nt = new object[tail.Length + 1];
                System.Array.Copy(tail, nt, tail.Length);
                nt[tail.Length] = x;
                return new PList(count + 1, shift, root, nt);
            }
            object[] nr;
            int ns = shift;
            if ((count >> B) > (1 << shift)) { nr = new object[W]; nr[0] = root; nr[1] = NewPath(shift, tail); ns += B; }
            else nr = PushTail(shift, root, tail);
            return new PList(count + 1, ns, nr, new object[] { x });
        }

        static object[] DoSet(int level, object[] node, int i, object x)
        {
            var r = (object[])node.Clone();
            if (level == 0) r[i & M] = x;
            else { int sub = (i >> level) & M; r[sub] = DoSet(level - B, (object[])node[sub], i, x); }
            return r;
        }

        public PList Set(int i, object x)
        {
            if ((uint)i >= (uint)count) throw new System.ArgumentOutOfRangeException("index", "list index " + i + " is outside 0.." + (count - 1));
            if (i >= TailOff)
            {
                var nt = (object[])tail.Clone();
                nt[i & M] = x;
                return new PList(count, shift, root, nt);
            }
            return new PList(count, shift, DoSet(shift, root, i, x), tail);
        }

        public static PList From(System.Collections.Generic.IEnumerable<object> items)
        {
            var r = Empty;
            foreach (var x in items) r = r.Append(x);
            return r;
        }

        public PList Insert(int i, object x)
        {
            if ((uint)i > (uint)count) throw new System.ArgumentOutOfRangeException("index", "list index " + i + " is outside 0.." + count);
            if (i == count) return Append(x);
            var r = Empty;
            int k = 0;
            foreach (var y in this) { if (k++ == i) r = r.Append(x); r = r.Append(y); }
            return r;
        }

        public PList RemoveAt(int i)
        {
            if ((uint)i >= (uint)count) throw new System.ArgumentOutOfRangeException("index", "list index " + i + " is outside 0.." + (count - 1));
            var r = Empty;
            int k = 0;
            foreach (var y in this) if (k++ != i) r = r.Append(y);
            return r;
        }

        public PList Concat(PList other)
        {
            var r = this;
            foreach (var y in other) r = r.Append(y);
            return r;
        }

        public PList Slice(long start, long end)
        {
            int a = (int)System.Math.Clamp(start, 0, count), b = (int)System.Math.Clamp(end, a, count);
            var r = Empty;
            for (int i = a; i < b; i++) r = r.Append(this[i]);
            return r;
        }

        public PList Reverse()
        {
            var r = Empty;
            for (int i = count - 1; i >= 0; i--) r = r.Append(this[i]);
            return r;
        }

        public int IndexOf(object x)
        {
            int i = 0;
            foreach (var y in this) { if (y.Equals(x)) return i; i++; }
            return -1;
        }

        public bool Contains(object x) => IndexOf(x) >= 0;

        public System.Collections.Generic.IEnumerator<object> GetEnumerator()
        {
            for (int i = 0; i < count; i += W)
            {
                var chunk = ArrayFor(i);
                int n = System.Math.Min(W, count - i);
                for (int j = 0; j < n; j++) yield return chunk[j];
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // ---- Map and Set: a hash array mapped trie (HAMT) for lookup, plus a vector of keys for first-added order ----

    sealed class HEntry
    {
        public readonly object Key, Val;
        public readonly int Hash, Pos;
        public HEntry(object key, object val, int hash, int pos) { Key = key; Val = val; Hash = hash; Pos = pos; }
    }

    abstract class HNode
    {
        public abstract HEntry? Find(object k, int h, int sh);
        public abstract HNode Set(HEntry e, int sh);
        public abstract HNode? Remove(object k, int h, int sh);
        public abstract void Walk(System.Collections.Generic.List<HEntry> into);
    }

    sealed class HCollision : HNode
    {
        public readonly int Hash;
        public readonly HEntry[] Es;
        public HCollision(int hash, HEntry[] es) { Hash = hash; Es = es; }
        public override HEntry? Find(object k, int h, int sh)
        {
            foreach (var e in Es) if (e.Key.Equals(k)) return e;
            return null;
        }
        public override HNode Set(HEntry e, int sh)
        {
            for (int i = 0; i < Es.Length; i++)
                if (Es[i].Key.Equals(e.Key)) { var c = (HEntry[])Es.Clone(); c[i] = e; return new HCollision(Hash, c); }
            var n = new HEntry[Es.Length + 1];
            System.Array.Copy(Es, n, Es.Length);
            n[Es.Length] = e;
            return new HCollision(Hash, n);
        }
        public override HNode? Remove(object k, int h, int sh)
        {
            var l = new System.Collections.Generic.List<HEntry>(Es);
            l.RemoveAll(e => e.Key.Equals(k));
            return l.Count == 0 ? null : new HCollision(Hash, l.ToArray());
        }
        public override void Walk(System.Collections.Generic.List<HEntry> into) => into.AddRange(Es);
    }

    sealed class HBitmap : HNode
    {
        public readonly uint Bitmap;
        public readonly object[] Items; // HEntry or HNode
        public HBitmap(uint bitmap, object[] items) { Bitmap = bitmap; Items = items; }
        static uint Bit(int h, int sh) => 1u << ((h >> sh) & 31);
        int Index(uint bit) => System.Numerics.BitOperations.PopCount(Bitmap & (bit - 1));

        public override HEntry? Find(object k, int h, int sh)
        {
            var bit = Bit(h, sh);
            if ((Bitmap & bit) == 0) return null;
            var it = Items[Index(bit)];
            if (it is HEntry e) return e.Key.Equals(k) ? e : null;
            return ((HNode)it).Find(k, h, sh + 5);
        }

        public static HNode Merge(HEntry a, HEntry b, int sh)
        {
            if (a.Hash == b.Hash) return new HCollision(a.Hash, new[] { a, b });
            int ia = (a.Hash >> sh) & 31, ib = (b.Hash >> sh) & 31;
            if (ia == ib) return new HBitmap(1u << ia, new object[] { Merge(a, b, sh + 5) });
            return new HBitmap((1u << ia) | (1u << ib), ia < ib ? new object[] { a, b } : new object[] { b, a });
        }

        public override HNode Set(HEntry e, int sh)
        {
            var bit = Bit(e.Hash, sh);
            int idx = Index(bit);
            if ((Bitmap & bit) == 0)
            {
                var n = new object[Items.Length + 1];
                System.Array.Copy(Items, 0, n, 0, idx);
                n[idx] = e;
                System.Array.Copy(Items, idx, n, idx + 1, Items.Length - idx);
                return new HBitmap(Bitmap | bit, n);
            }
            var c = (object[])Items.Clone();
            var it = Items[idx];
            if (it is HEntry old) c[idx] = old.Key.Equals(e.Key) ? e : Merge(old, e, sh + 5);
            else c[idx] = ((HNode)it).Set(e, sh + 5);
            return new HBitmap(Bitmap, c);
        }

        public override HNode? Remove(object k, int h, int sh)
        {
            var bit = Bit(h, sh);
            if ((Bitmap & bit) == 0) return this;
            int idx = Index(bit);
            var it = Items[idx];
            object? repl;
            if (it is HEntry e) repl = e.Key.Equals(k) ? null : e;
            else repl = ((HNode)it).Remove(k, h, sh + 5);
            if (repl != null)
            {
                var c = (object[])Items.Clone();
                c[idx] = repl;
                return new HBitmap(Bitmap, c);
            }
            if (Items.Length == 1) return null;
            var n = new object[Items.Length - 1];
            System.Array.Copy(Items, 0, n, 0, idx);
            System.Array.Copy(Items, idx + 1, n, idx, Items.Length - idx - 1);
            return new HBitmap(Bitmap & ~bit, n);
        }

        public override void Walk(System.Collections.Generic.List<HEntry> into)
        {
            foreach (var it in Items) { if (it is HEntry e) into.Add(e); else ((HNode)it).Walk(into); }
        }
    }

    public sealed class PMap : System.Collections.Generic.IEnumerable<object>
    {
        static readonly object Tomb = new();
        public static readonly PMap Empty = new(null, 0, PList.Empty, 0);
        readonly HNode? root;
        readonly int count, dead;
        readonly PList order; // keys in first-added order; removed keys are Tomb until the vector is compacted

        PMap(HNode? root, int count, PList order, int dead) { this.root = root; this.count = count; this.order = order; this.dead = dead; }

        public int Count => count;
        HEntry? Find(object k) => root?.Find(k, k.GetHashCode(), 0);
        public bool ContainsKey(object k) => Find(k) != null;
        public bool TryGetValue(object k, out object v)
        {
            var e = Find(k);
            v = e!?.Val!;
            return e != null;
        }

        public PMap Set(object k, object v)
        {
            int h = k.GetHashCode();
            var old = root?.Find(k, h, 0);
            if (old != null)
                return new PMap(root!.Set(new HEntry(k, v, h, old.Pos), 0), count, order, dead);
            var e = new HEntry(k, v, h, order.Count);
            var nr = root == null ? new HBitmap(0, System.Array.Empty<object>()).Set(e, 0) : root.Set(e, 0);
            return new PMap(nr, count + 1, order.Append(k), dead);
        }

        public PMap Remove(object k)
        {
            var old = Find(k);
            if (old == null) return this;
            var nr = root!.Remove(k, old.Hash, 0);
            var no = order.Set(old.Pos, Tomb);
            var r = new PMap(nr, count - 1, no, dead + 1);
            return dead + 1 > 32 && (dead + 1) * 2 > no.Count ? r.Compact() : r;
        }

        // Rebuilds the key vector without the removed keys: O(n), but only after n/2 removals.
        PMap Compact()
        {
            var r = Empty;
            foreach (var k in order) if (!ReferenceEquals(k, Tomb)) r = r.Set(k, Find(k)!.Val);
            return r;
        }

        /// <summary>The keys in first-added order.</summary>
        public PList Keys() => dead == 0 ? order : PList.From(this);

        public System.Collections.Generic.IEnumerator<object> GetEnumerator()
        {
            foreach (var k in order) if (!ReferenceEquals(k, Tomb)) yield return k;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // ---- Sorted map / set and priority queue: a persistent AVL tree ----

    public sealed class PSorted : System.Collections.Generic.IEnumerable<object>
    {
        sealed class N
        {
            public readonly object K, V;
            public readonly N? L, R;
            public readonly int H, S;
            public N(object k, object v, N? l, N? r) { K = k; V = v; L = l; R = r; H = 1 + System.Math.Max(Ht(l), Ht(r)); S = 1 + Sz(l) + Sz(r); }
        }
        static int Ht(N? n) => n == null ? 0 : n.H;
        static int Sz(N? n) => n == null ? 0 : n.S;

        static N Bal(object k, object v, N? l, N? r)
        {
            int d = Ht(l) - Ht(r);
            if (d > 1)
            {
                if (Ht(l!.L) >= Ht(l.R)) return new N(l.K, l.V, l.L, new N(k, v, l.R, r));
                var lr = l.R!;
                return new N(lr.K, lr.V, new N(l.K, l.V, l.L, lr.L), new N(k, v, lr.R, r));
            }
            if (d < -1)
            {
                if (Ht(r!.R) >= Ht(r.L)) return new N(r.K, r.V, new N(k, v, l, r.L), r.R);
                var rl = r.L!;
                return new N(rl.K, rl.V, new N(k, v, l, rl.L), new N(r.K, r.V, rl.R, r.R));
            }
            return new N(k, v, l, r);
        }

        readonly N? root;
        readonly System.Collections.Generic.IComparer<object> cmp;
        public PSorted(System.Collections.Generic.IComparer<object> cmp) { this.cmp = cmp; }
        PSorted(N? root, System.Collections.Generic.IComparer<object> cmp) { this.root = root; this.cmp = cmp; }

        public int Count => Sz(root);

        N? Find(object k)
        {
            var n = root;
            while (n != null)
            {
                int c = cmp.Compare(k, n.K);
                if (c == 0) return n;
                n = c < 0 ? n.L : n.R;
            }
            return null;
        }
        public bool ContainsKey(object k) => Find(k) != null;
        public bool TryGetValue(object k, out object v)
        {
            var n = Find(k);
            v = n!?.V!;
            return n != null;
        }

        N Add(N? n, object k, object v)
        {
            if (n == null) return new N(k, v, null, null);
            int c = cmp.Compare(k, n.K);
            if (c == 0) return new N(k, v, n.L, n.R);
            return c < 0 ? Bal(n.K, n.V, Add(n.L, k, v), n.R) : Bal(n.K, n.V, n.L, Add(n.R, k, v));
        }
        public PSorted Set(object k, object v) => new(Add(root, k, v), cmp);

        static N? RemoveMinNode(N n) => n.L == null ? n.R : Bal(n.K, n.V, RemoveMinNode(n.L), n.R);
        static N Min(N n) { while (n.L != null) n = n.L; return n; }

        N? Del(N? n, object k)
        {
            if (n == null) return null;
            int c = cmp.Compare(k, n.K);
            if (c < 0) return Bal(n.K, n.V, Del(n.L, k), n.R);
            if (c > 0) return Bal(n.K, n.V, n.L, Del(n.R, k));
            if (n.L == null) return n.R;
            if (n.R == null) return n.L;
            var m = Min(n.R);
            return Bal(m.K, m.V, n.L, RemoveMinNode(n.R));
        }
        public PSorted Remove(object k) => Find(k) == null ? this : new PSorted(Del(root, k), cmp);

        public bool TryMin(out object key, out object val)
        {
            if (root == null) { key = null!; val = null!; return false; }
            var m = Min(root);
            key = m.K; val = m.V;
            return true;
        }
        public PSorted RemoveMin() => root == null ? this : new PSorted(RemoveMinNode(root), cmp);

        public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object, object>> Pairs()
        {
            var st = new System.Collections.Generic.Stack<N>();
            var n = root;
            while (n != null || st.Count > 0)
            {
                while (n != null) { st.Push(n); n = n.L; }
                n = st.Pop();
                yield return new System.Collections.Generic.KeyValuePair<object, object>(n.K, n.V);
                n = n.R;
            }
        }
        /// <summary>The keys in ascending order.</summary>
        public System.Collections.Generic.IEnumerator<object> GetEnumerator() { foreach (var p in Pairs()) yield return p.Key; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    sealed class PqCmp : System.Collections.Generic.IComparer<object>
    {
        public int Compare(object? a, object? b)
        {
            var (p1, s1) = ((long, long))a!; var (p2, s2) = ((long, long))b!;
            return p1 != p2 ? p1.CompareTo(p2) : s1.CompareTo(s2);
        }
    }

    /// <summary>A priority queue: lowest priority first, ties in the order added.</summary>
    public sealed class PQueue
    {
        static readonly PqCmp Cmp = new();
        public static readonly PQueue Empty = new(new PSorted(Cmp), 0);
        readonly PSorted tree;
        readonly long seq;
        PQueue(PSorted tree, long seq) { this.tree = tree; this.seq = seq; }
        public int Count => tree.Count;
        public PQueue Push(object item, long priority) => new(tree.Set((priority, seq), item), seq + 1);
        public object Front(string fn) => tree.TryMin(out _, out var v) ? v : throw new System.Collections.Generic.KeyNotFoundException(fn + ": the queue is empty");
        public PQueue Pop(string fn) => tree.Count > 0 ? new(tree.RemoveMin(), seq) : throw new System.Collections.Generic.KeyNotFoundException(fn + ": the queue is empty");
    }

    /// <summary>A first-in first-out queue: a vector plus the index of the front; the vector is rebuilt after half of it has been popped.</summary>
    public sealed class PFifo
    {
        public static readonly PFifo Empty = new(PList.Empty, 0);
        readonly PList items;
        readonly int head;
        PFifo(PList items, int head) { this.items = items; this.head = head; }
        public int Count => items.Count - head;
        public PFifo Push(object x) => new(items.Append(x), head);
        public object Front(string fn) => Count > 0 ? items[head] : throw new System.Collections.Generic.KeyNotFoundException(fn + ": the queue is empty");
        public PFifo Pop(string fn)
        {
            if (Count == 0) throw new System.Collections.Generic.KeyNotFoundException(fn + ": the queue is empty");
            int h = head + 1;
            if (h >= 32 && h * 2 >= items.Count) return new(items.Slice(h, items.Count), 0);
            return new(items, h);
        }
    }
}
""";
}
