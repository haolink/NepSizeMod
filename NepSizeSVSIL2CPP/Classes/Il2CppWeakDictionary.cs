using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections;

using IDictionary = System.Collections.IDictionary;
using IEnumerable = System.Collections.IEnumerable;
using IEnumerator = System.Collections.IEnumerator;
using ICollection = System.Collections.ICollection;

/// <summary>
/// Il2Cpp Weak reference dictionary.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public sealed class Il2CppWeakDictionary<TKey, TValue>
    : IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable, IDictionary<TKey, TValue>
    where TKey : Il2CppObjectBase
{
    /// <summary>
    /// Internal storage.
    /// </summary>
    private readonly Dictionary<nint, TValue> _dict = new();


    private readonly object _gate = new();
    private int _opsSinceCompact;
    private readonly int _compactEveryNOps;

    // Creator delegate
    private static readonly Func<IntPtr, TKey> _wrap = BuildWrap();

    /// <summary>
    /// Wrapper builder.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static Func<IntPtr, TKey> BuildWrap()
    {
        var ctor = typeof(TKey).GetConstructor(new[] { typeof(IntPtr) })
                   ?? throw new InvalidOperationException(
                       $"{typeof(TKey).Name} has no constructor .ctor(IntPtr).");

        var p = Expression.Parameter(typeof(IntPtr), "ptr");
        var body = Expression.New(ctor, p);
        return Expression.Lambda<Func<IntPtr, TKey>>(body, p).Compile();
    }

    /// <summary>
    /// Constructor wrapper.
    /// </summary>
    /// <param name="ptr"></param>
    /// <returns></returns>
    private static TKey Wrap(nint ptr) => _wrap((IntPtr)ptr);

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="compactEveryNOps"></param>
    public Il2CppWeakDictionary(int compactEveryNOps = 256)
    {
        if (compactEveryNOps < 1) compactEveryNOps = 1;
        _compactEveryNOps = compactEveryNOps;
    }

    /// <summary>
    /// Turn pointer.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private static nint PtrOf(TKey key) => (nint)key.Pointer;

    /// <summary>
    /// Compact.
    /// </summary>
    private void BumpAndMaybeCompact()
    {
        if (++_opsSinceCompact >= _compactEveryNOps)
        {
            Compact();
        }
    }

    /// <summary>
    /// Add an entry to the dictionary.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void Add(TKey key, TValue value)
    {
        var ptr = PtrOf(key);
        lock (_gate)
        {
            _dict[ptr] = value;
        }
        BumpAndMaybeCompact();
    }

    /// <summary>
    /// Get an item.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public TValue this[TKey key]
    {
        get => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    /// <summary>
    /// Get a value by key.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        var ptr = PtrOf(key);

        // Should it be a dead item.. remove it.
        if (key.WasCollected)
        {
            lock (_gate) _dict.Remove(ptr);
            value = default!;
            BumpAndMaybeCompact();
            return false;
        }

        lock (_gate)
        {
            if (!_dict.TryGetValue(ptr, out var entry))
            {
                value = default!;
                BumpAndMaybeCompact();
                return false;
            }

            // Rewrap.
            var probe = Wrap(ptr);
            if (probe.WasCollected)
            {
                _dict.Remove(ptr);
                value = default!;
                BumpAndMaybeCompact();
                return false;
            }

            value = entry;
        }

        BumpAndMaybeCompact();
        return true;
    }

    /// <summary>
    /// Remove a key deliberately.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool Remove(TKey key)
    {
        var ptr = PtrOf(key);
        lock (_gate) return _dict.Remove(ptr);
    }

    /// <summary>
    /// Get by pointer.
    /// </summary>
    /// <param name="ptr"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGetByPointer(nint ptr, out TValue value)
    {
        lock (_gate)
        {
            if (_dict.TryGetValue(ptr, out var entry))
            {
                var probe = Wrap(ptr);
                if (!probe.WasCollected)
                {
                    value = entry;
                    BumpAndMaybeCompact();
                    return true;
                }
                _dict.Remove(ptr);
            }
        }
        value = default!;
        BumpAndMaybeCompact();
        return false;
    }

    /// <summary>
    /// Force compact.
    /// </summary>
    /// <returns></returns>
    public int Compact()
    {
        int removed = 0;
        List<nint> toRemove = null;

        lock (_gate)
        {
            if (_dict.Count == 0) { _opsSinceCompact = 0; return 0; }

            // Snapshot Keys → prüfen → nachher löschen
            foreach (var ptr in _dict.Keys)
            {
                var k = Wrap(ptr);
                if (k.WasCollected)
                    (toRemove ??= new()).Add(ptr);
            }

            if (toRemove != null)
            {
                foreach (var r in toRemove) _dict.Remove(r);
                removed = toRemove.Count;
            }

            _opsSinceCompact = 0;
        }
        return removed;
    }

    /// <summary>
    /// Amount of items.
    /// </summary>
    public int Count
    {
        get { lock (_gate) return _dict.Count; }
    }

    /// <summary>
    /// Clear dictionary.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _dict.Clear();
            _opsSinceCompact = 0;
        }
    }

    /// <summary>
    /// Enumate through the dictionary.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        // Get an output list during the lock and enumerate over it.
        List<KeyValuePair<TKey, TValue>> live = new();
        List<nint> toRemove = null;

        lock (_gate)
        {
            foreach (var kv in _dict)
            {
                var ptr = kv.Key;
                var k = Wrap(ptr);
                if (k.WasCollected)
                {
                    (toRemove ??= new()).Add(ptr);
                    continue;
                }
                live.Add(new KeyValuePair<TKey, TValue>(k, kv.Value));
            }
            if (toRemove != null)
            {
                foreach (var r in toRemove) _dict.Remove(r);
            }

            _opsSinceCompact = 0;
        }

        foreach (var item in live)
        {
            yield return item;
        }
    }

    /// <summary>
    /// Non generic enumerator.
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Get the keys.
    /// </summary>
    /// <returns></returns>
    private List<TKey> GetKeys()
    {
        List<TKey> keys = new List<TKey>();
        foreach (KeyValuePair<TKey, TValue> kv in this)
        {
            keys.Add(kv.Key);
        }
        return keys;
    }

    /// <summary>
    /// Get the values.
    /// </summary>
    /// <returns></returns>
    private List<TValue> GetValues()
    {
        List<TValue> values = new List<TValue>();
        foreach (KeyValuePair<TKey, TValue> kv in this)
        {
            values.Add(kv.Value);
        }
        return values;
    }

    /// <summary>
    /// Keys of the dictionary.
    /// </summary>
    ICollection<TKey> IDictionary<TKey, TValue>.Keys {
        get => this.GetKeys();        
    }

    /// <summary>
    /// All possible values.
    /// </summary>
    ICollection<TValue> IDictionary<TKey, TValue>.Values {
        get => this.GetValues();        
    }

    /// <summary>
    /// We're never readonly.
    /// </summary>
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    /// <summary>
    /// Does it contain a key?
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    bool IDictionary<TKey, TValue>.ContainsKey(TKey key)
    {
        return this.TryGetValue(key, out var _);
    }

    /// <summary>
    /// Add key value pair.
    /// </summary>
    /// <param name="item"></param>
    /// <exception cref="NotImplementedException"></exception>
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
    {
        this.Add(item.Key, item.Value);
    }

    /// <summary>
    /// Check if a key value pair is in the dictionary.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
    {
        if (this.TryGetValue(item.Key, out var v))
        {
            return (v.Equals(item.Value));
        }
        return false;
    }

    /// <summary>
    /// Copy into an array.
    /// </summary>
    /// <param name="array"></param>
    /// <param name="arrayIndex"></param>
    /// <exception cref="NotImplementedException"></exception>
    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        foreach(KeyValuePair<TKey, TValue> kv in this)
        {
            array[arrayIndex] = kv;
            arrayIndex++;
        }
    }

    /// <summary>
    /// Remove a key value pair.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        if (this.TryGetValue(item.Key, out var v))
        {
            if (v.Equals(item.Value))
            {
                this.Remove(item.Key);
                return true;
            }
        }
        return false;
    }
}