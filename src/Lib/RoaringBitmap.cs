using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lib;

public sealed class RoaringBitmap : IDisposable, IEnumerable<uint>
{
    private int _disposedCount;

    /*
     * Notes:
     * Synchronous API only will allow us to use Span<T> within methods and keep some things on the stack (maybe?)
     * Using ArrayPool/MemoryPool may be beneficial? https://www.infoworld.com/article/3596289/how-to-use-arraypool-and-memorypool-in-c.html
     *
     * One argument/use for stackalloc is data locality, which could be useful for checking contiguous blocks
     * From https://learn.microsoft.com/en-us/archive/msdn-magazine/2018/january/csharp-all-about-span-exploring-a-new-net-mainstay#what-about-the-c-language-and-compiler
     * > This is also extremely useful in situations where you need some scratch space to perform an operation, but want to avoid allocating heap memory for relatively small sizes
     *
     * We should also look at the performance of using record structs vs records/classes should we store anything in classes or records
     * 
     * We can probably handle multiple threads if we're careful, will need to consider tradeoffs there
     * https://vcsjones.dev/stackalloc/ <- dangers of stackalloc
     *
     * https://stebet.net/real-world-example-of-reducing-allocations-using-span-t-and-memory-t/
     *
     * In this article about Pipelines it mentions ReadOnlySequence<T> which is a view over one or more
     * segments of ReadOnlyMemory<T> which could be super handy for reading the containers https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/
     * 
     * Keep in mind we only need to get it working first, then we can make it faster
     */

    public ulong Cardinality => (ulong)_containers.Sum(c => c.Value.Cardinality);
    public bool IsEmpty => !_containers.Any(c => c.Value.Cardinality > 0);
    public uint Min
    {
        get
        {
            var lowestHigh = _containers.Keys.Min();
            var lowestLow = _containers[lowestHigh] switch
            {
                { SortedSet: { } sorted } => sorted.FirstOrDefault(),
                { Bitmap: { } bitmap } => (ushort)bitmap.Cast<bool>().Zip(Enumerable.Range(0, bitmap.Count)).FirstOrDefault(pair => pair.First).Second,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (uint)lowestHigh << 16 | lowestLow;
        }
    }

    public uint Max
    {
        get
        {
            var highestHigh = _containers.Keys.Max();
            var highestLow = _containers[highestHigh] switch
            {
                { SortedSet: { } sorted } => sorted.LastOrDefault(),
                { Bitmap: { } bitmap } => (ushort)bitmap.Cast<bool>().Zip(Enumerable.Range(0, bitmap.Count - 1)).LastOrDefault(pair => pair.First).Second,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (uint)highestHigh << 16 | highestLow;
        }
    }

    // TODO: Needed? Does it make sense?
    public int SerializedBytes => throw new NotImplementedException();
    public int PortableSerializedBytes => throw new NotImplementedException();

    private record Container
    {
        public uint Cardinality => SortedSet is { } ? (uint)SortedSet.Count : ushort.MaxValue;

        public SortedSet<ushort>? SortedSet { get; set; }
        public BitArray? Bitmap { get; set; }
        // TODO: Any other container types we want

        public static Container Empty() => new() { SortedSet = new SortedSet<ushort>() };
        public static Container WithBitmap(BitArray bitmap) => new() { Bitmap = bitmap };
    }

    private readonly ConcurrentDictionary<ushort, Container> _containers = new();

    public RoaringBitmap()
    {
    }

    public RoaringBitmap(uint capacity) : this()
    {
        throw new NotImplementedException();
    }

    #region Factories
    public static RoaringBitmap FromRange(uint min, uint max, uint step = 1) => FromValues(Generate(min, max, step).ToArray());

    public static RoaringBitmap FromValues(params uint[] values) => FromValues(values, 0, values.Length - 1);

    public static RoaringBitmap FromValues(uint[] values, int offset, int count)
    {
        var bitmap = new RoaringBitmap();
        Add(bitmap._containers, ref values, (uint)offset, (uint)count);
        return bitmap;
    }
    #endregion

    #region Public Methods
    // TODO: I'm not sure we should bother delegating to private static methods, just save the extra .call instead
    public void Add(uint value)
        => Add(_containers, value);

    public void AddMany(params uint[] values)
        => Add(_containers, ref values, 0, (uint)values.Length - 1);

    public void AddMany(uint[] values, uint offset, uint count)
        => Add(_containers, ref values, offset, count);

    public void Remove(uint value)
        => Remove(_containers, value);

    public void RemoveMany(params uint[] values)
        => Remove(_containers, ref values, 0, (uint)values.Length - 1);

    public void RemoveMany(uint[] values, uint offset, uint count)
        => Remove(_containers, ref values, offset, count);

    public bool Contains(uint value)
    {
        var (key, shortValue) = GetHighLow(value);
        if (!_containers.TryGetValue(key, out var container)) return false;
        return container switch
        {
            { SortedSet: { } sorted } => sorted.Contains(shortValue),
            { Bitmap: { } bitArray } => bitArray.Get(shortValue),
            _ => false
        };
    }

    public bool Equals(RoaringBitmap bitmap)
        => Equals(_containers, bitmap._containers);

    public bool IsSubset(RoaringBitmap bitmap, bool isStrict = false)
        => IsSubset(_containers, bitmap._containers, isStrict);

    public bool Select(uint rank, out uint element)
        => throw new NotImplementedException(); // => NativeMethods.roaring_bitmap_select(_pointer, rank, out element);
    
    public RoaringBitmap Not(ulong start, ulong end)
        => throw new NotImplementedException();

    public void ApplyNot(ulong start, ulong end)
        => throw new NotImplementedException();

    public RoaringBitmap And(RoaringBitmap bitmap)
        => And(_containers, bitmap._containers);

    public void ApplyAnd(RoaringBitmap bitmap)
        => throw new NotImplementedException();

    public ulong AndCardinality(RoaringBitmap bitmap)
        => throw new NotImplementedException();
    
    public RoaringBitmap AndNot(RoaringBitmap bitmap)
        => throw new NotImplementedException();

    public void ApplyAndNot(RoaringBitmap bitmap)
        => throw new NotImplementedException();

    public ulong AndNotCardinality(RoaringBitmap bitmap)
        => throw new NotImplementedException();

    public RoaringBitmap Or(RoaringBitmap bitmap)
        => throw new NotImplementedException();
    
    public void ApplyOr(RoaringBitmap bitmap)
        => throw new NotImplementedException();
    
    public ulong OrCardinality(RoaringBitmap bitmap)
        => throw new NotImplementedException();

    public RoaringBitmap Xor(RoaringBitmap bitmap)
        => throw new NotImplementedException();
    
    public void ApplyXor(RoaringBitmap bitmap)
        => throw new NotImplementedException();
    
    public ulong XorCardinality(RoaringBitmap bitmap)
        => throw new NotImplementedException();
    
    public static RoaringBitmap OrMany(params RoaringBitmap[] bitmaps)
        => throw new NotImplementedException();
    
    public static RoaringBitmap OrManyHeap(params RoaringBitmap[] bitmaps)
        => throw new NotImplementedException();
    
    public static RoaringBitmap XorMany(params RoaringBitmap[] bitmaps)
        => throw new NotImplementedException();

    public RoaringBitmap LazyOr(RoaringBitmap bitmap, bool bitsetConversion)
        => throw new NotImplementedException();

    public void ApplyLazyOr(RoaringBitmap bitmap, bool bitsetConversion)
        => throw new NotImplementedException();

    public RoaringBitmap LazyXor(RoaringBitmap bitmap, bool bitsetConversion)
        => throw new NotImplementedException();

    public void ApplyLazyXor(RoaringBitmap bitmap, bool bitsetConversion)
        => throw new NotImplementedException();

    public void RepairAfterLazy()
        => throw new NotImplementedException();

    public bool Intersects(RoaringBitmap bitmap)
        => throw new NotImplementedException();

    public double GetJaccardIndex(RoaringBitmap bitmap)
        => throw new NotImplementedException();
    
#pragma warning disable CA1822
    // ReSharper disable MemberCanBeMadeStatic.Global
    public bool Optimize() => true; // TODO: Make real

    public bool RemoveRunCompression() => true; // TODO: Make real
    // ReSharper enable MemberCanBeMadeStatic.Global
#pragma warning restore CA1822
    
    public int ShrinkToFit() => throw new NotImplementedException();
    
    public ReadOnlySpan<byte> Serialize(SerializationFormat format = SerializationFormat.Normal)
        => throw new NotImplementedException();
    
    public static RoaringBitmap Deserialize(ReadOnlySpan<byte> buffer, SerializationFormat format = SerializationFormat.Normal)
        => throw new NotImplementedException();

    public Statistics GetStatistics()
        => throw new NotImplementedException();

    /// <inheritdoc />
    public IEnumerator<uint> GetEnumerator() => new Enumerator(this);

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Increment(ref _disposedCount) == 1)
        {
            // TODO
        }
    }
    #endregion

    // TODO: Move private static methods to another class and make public, then make private RoaringBitmap properties internal
    private static IEnumerable<uint> Generate(uint min, uint max, uint step)
    {
        for (var i = min; i < max; i += step) yield return i;
    }
    
    private static (ushort high, ushort low) GetHighLow(uint value) =>
        ((ushort)(value >>> 16), (ushort)(value & ushort.MaxValue));
    
    private static void Add(ConcurrentDictionary<ushort, Container> containers, ref uint[] values, uint offset,
        uint count)
    {
        if (values.Length < 1)
            return;
        // TODO: BitArray has a faster method than this for setting many values, so we shouldn't take this path generically
        for (var i = offset; i <= count; i++) Add(containers, values[i]);
    }
    
    private static void Add(ConcurrentDictionary<ushort, Container> containers, uint value)
    {
        var (high, low) = GetHighLow(value);
        // TODO: Using SortedSet may not perform well as it's using a red/black tree rather than a packed array
        var container = containers.GetOrAdd(high, _ => Container.Empty());
        // Add to container, possibly swapping SortedSet for a bitmap
        if (container.Cardinality >= 4096)
        {
            switch (container)
            {
                // If sparse array, allocate full bitmap first, set bit, then swap containers
                // TODO: This is poor logic encapsulation...
                case { SortedSet: { } sorted }:
                    var newBitmap = CreateBitmap(sorted);
                    newBitmap.Set(low, true);
                    if (!containers.TryUpdate(high, Container.WithBitmap(newBitmap), container))
                        // TODO: threading
                        throw new InvalidOperationException("Inconsistent container state");
                    break;
                case { Bitmap: { } bitmap }:
                    bitmap.Set(low, true);
                    break;
                default:
                    throw new NotImplementedException("Add not implemented for type");
            }
        }
        else
        {
            // TODO: At some point think about thread safety
            Debug.Assert(container.SortedSet != null, "container.SortedSet != null");
            container.SortedSet.Add(low);
        }
    }
    
    private static void Remove(ConcurrentDictionary<ushort, Container> containers, ref uint[] values, uint offset,
        uint count)
    {
        if (values.Length < 1)
            return;
        // TODO: BitArray has a faster method than this for setting many values, so we shouldn't take this path generically
        for (var i = offset; i <= count; i++) Remove(containers, values[i]);
    }

    private static void Remove(ConcurrentDictionary<ushort, Container> containers, uint value)
    {
        var (key, shortValue) = GetHighLow(value);
        if (!containers.TryGetValue(key, out var container)) return;
        switch (container)
        {
            case { SortedSet: { } sorted }:
                sorted.Remove(shortValue);
                break;
            case { Bitmap: {} bitmap }:
                bitmap.Set(shortValue, false);
                break;
            default:
                throw new NotImplementedException("Remove not implemented for type");
        }
        // TODO: Convert back to SortedSet if < 4096 automatically or require manual repack?
    }

    private static bool Equals(ConcurrentDictionary<ushort, Container> containersA, ConcurrentDictionary<ushort, Container> containersB)
    {
        if (containersA.Count != containersB.Count)
            return false;

        if (!containersA.Keys.SequenceEqual(containersB.Keys))
            return false;

        foreach (var (key, container) in containersA)
        {
            if (!containersB.TryGetValue(key, out var containerB))
                return false; // Should not happen unless there's concurrent access
            // NB strict equality shortcut
            if (container == containerB)
                return true;
            if (container.Cardinality != containerB.Cardinality)
                return false;

            // NB Exhaustive check
            switch (container)
            {
                case { SortedSet: { } sorted } when containerB is { SortedSet: { } sortedB }:
                    if (!sorted.SetEquals(sortedB)) return false;
                    break;
                case { Bitmap: { } bitmap } when containerB is { Bitmap: { } bitmapB }:
                    // TODO: Is there a faster way?
                    if (!bitmap.Cast<bool>().SequenceEqual(bitmapB.Cast<bool>())) return false;
                    break;
                default:
                    throw new NotImplementedException("Need to implement comparing between container types");
            }
        }

        return true;
    }

    // https://github.com/RoaringBitmap/CRoaring/blob/bc51a40aa276053fef531ff45f47c4f548f84986/include/roaring/containers/containers.h#L682
    /// <summary>
    /// Returns true if all elements in <see cref="maybeSubset"/> are also in <see cref="containers"/>
    /// </summary>
    /// <param name="maybeSubset"></param>
    /// <param name="containers"></param>
    /// <param name="isStrict">Return true only if <see cref="containers"/> is strictly greater than <see cref="maybeSubset"/></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private static bool IsSubset(ConcurrentDictionary<ushort, Container> maybeSubset, ConcurrentDictionary<ushort, Container> containers, bool isStrict = false)
    {
        // TODO: We need to support maybeSubset being a different (smaller) type than containers

        var maybeSubsetCount = maybeSubset.Count(NonEmptyContainers);
        var containersCount = containers.Count(NonEmptyContainers);
        if ((isStrict && maybeSubsetCount >= containersCount) || maybeSubsetCount > containersCount)
            return false;
        
        foreach (var (key, subsetContainer) in maybeSubset)
        {
            if (subsetContainer.Cardinality < 1) continue;

            if (!containers.TryGetValue(key, out var container)) return false;

            if (subsetContainer.Cardinality > container.Cardinality)
                return false;
            
            switch (subsetContainer)
            {
                case { SortedSet: { } sorted } when container is { SortedSet: { } sortedB }:
                    if (!SetIsSubset(sorted, sortedB)) return false;
                    break;
                case { Bitmap: { } bitmap } when container is { Bitmap: { } bitmapB }:
                    // iterate both and track whether bitmapB is a superset (when isStrict)
                    // while also ensuring that any set in bitmap are set in bitmapB
                    var bIsSuperSet = false;
                    for (var i = 0; i < bitmap.Count; i++)
                    {
                        if (!bIsSuperSet && bitmapB.Get(i) && !bitmap.Get(i))
                            bIsSuperSet = true;
                        if (bitmap.Get(i) && !bitmapB.Get(i))
                            return false;
                    }

                    if (isStrict && !bIsSuperSet)
                        return false;
                    break;
                default:
                    throw new NotImplementedException("Need to implement comparing between container types");
            }
        }

        return true;

        bool NonEmptyContainers(KeyValuePair<ushort, Container> kvp) => kvp.Value.Cardinality > 0;

        bool SetIsSubset(IReadOnlySet<ushort> a, IReadOnlyCollection<ushort> b) =>
            (isStrict && a.IsProperSubsetOf(b)) || a.IsSubsetOf(b);
    }

    private static RoaringBitmap And(ConcurrentDictionary<ushort, Container> containersA, ConcurrentDictionary<ushort, Container> containersB)
    {
        RoaringBitmap? result = default;
        // Match up all matching keys in both dictionaries, non-matching are ignored since we're ANDing
        // AND together each individual container, so only values present in both are added to new container
        var matching = containersA.Keys.Intersect(containersB.Keys);
        foreach (var key in matching)
        {
            var containerA = containersA[key];
            var containerB = containersB[key];
            switch (containerA)
            {
                case { SortedSet: { } sortedA } when containerB is { SortedSet: { } sortedB }:
                    result = FromValues(sortedA.Intersect(sortedB).Select(low => (uint)key << 16 | low)
                        .ToArray());
                    break;
                case { Bitmap: { } bitmapA } when containerB is { Bitmap: { } bitmapB }:
                    var overlap = bitmapA.And(bitmapB);
                    result = FromValues(overlap.Cast<bool>().Zip(Enumerable.Range(0, overlap.Count - 1))
                        .Where(pair => pair.First).Select(pair => (uint)key << 16 | (ushort)pair.Second).ToArray());
                    break;
                default:
                    throw new NotImplementedException("Need to implement cross-type AND");
            }
        }

        return result ?? throw new InvalidOperationException();
    }

    private static BitArray CreateBitmap(SortedSet<ushort> values)
    {
        // TODO: Check my math here, but we should be able to just allocate ushort max / 8 to get bytes we need
        var bitmap = new BitArray(ushort.MaxValue);
        foreach (var value in values)
            bitmap.Set(value, true);

        return bitmap;
    }

    private static SortedSet<ushort> ConvertToSortedSet(BitArray bitmap)
    {
        var sorted = new SortedSet<ushort>();
        for (var i = 0; i < bitmap.Count; i++)
        {
            if (!bitmap.Get(i)) continue;
            sorted.Add((ushort)i);
        }

        return sorted;
    }

    private class Enumerator : IEnumerator<uint>
    {
        private readonly IEnumerator<KeyValuePair<ushort, Container>> _containersEnumerator;
        private IEnumerator? _currentContainerEnumerator;
        private int _disposedCount;
        
        public Enumerator(RoaringBitmap bitmap)
        {
            // TODO: GetEnumerator is thread-safe, but moving after modification may 
            _containersEnumerator = bitmap._containers.GetEnumerator();
            _containersEnumerator.MoveNext();
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (GetCurrentContainerEnumerator().MoveNext()) return true;
            
            if (!_containersEnumerator.MoveNext())
                return false;
            ClearCurrentContainerEnumerator();

            return true;
        }

        private void ClearCurrentContainerEnumerator() => _currentContainerEnumerator = null;
        
        private IEnumerator GetCurrentContainerEnumerator()
        {
            _currentContainerEnumerator ??= _containersEnumerator.Current.Value switch
            {
                { SortedSet: { } sorted } => sorted.GetEnumerator(),
                { Bitmap: { } bitmap } => new BitArrayPositionEnumerator(bitmap),
                _ => throw new InvalidOperationException()
            };

            return _currentContainerEnumerator;
        }

        /// <inheritdoc />
        public void Reset()
        {
            ClearCurrentContainerEnumerator();
            _containersEnumerator.Reset();
        }

        /// <inheritdoc />
        public uint Current
        {
            get
            {
                var high = _containersEnumerator.Current.Key;
                var low = (ushort)GetCurrentContainerEnumerator().Current;
                return (uint)high << 16 | low;
            }
        }

        /// <inheritdoc />
        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposedCount) != 1) return;
            _currentContainerEnumerator = null;
            _containersEnumerator.Dispose();
        }

        private class BitArrayPositionEnumerator : IEnumerator<ushort>
        {
            private readonly ushort _length;
            private ushort _index;

            public BitArrayPositionEnumerator(ICollection bitArray)
            {
                if (bitArray.Count > ushort.MaxValue)
                    throw new InvalidOperationException("BitArray is larger than ushort");
                _length = (ushort)(bitArray.Count - 1);
            }
            
            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_index >= _length)
                    return false;
                _index++;
                return true;
            }

            /// <inheritdoc />
            public void Reset()
            {
                _index = 0;
            }

            /// <inheritdoc />
            public ushort Current => _index;

            /// <inheritdoc />
            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
