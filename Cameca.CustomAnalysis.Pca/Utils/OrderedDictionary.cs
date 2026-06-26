#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

internal class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull
{
    // Backing store holds the value and a monotonically increasing entry index
    private readonly Dictionary<TKey, (TValue Value, long Order)> _dictionary = new();
    private long _counter = 0;

    public OrderedDictionary()
    {
        _dictionary = new();
    }

    public OrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection) : base()
    {
        if (collection is null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        foreach (var kvp in collection)
        {
            this.Add(kvp.Key, kvp.Value);
        }
    }

    public TValue this[TKey key]
    {
        get => _dictionary[key].Value;
        set
        {
            if (_dictionary.TryGetValue(key, out var existing))
            {
                // Update value but strictly preserve the original insertion order number
                _dictionary[key] = (value, existing.Order);
            }
            else
            {
                _dictionary[key] = (value, _counter++);
            }
        }
    }

    // O(1) removals with absolutely no memory shifting
    public bool Remove(TKey key) => _dictionary.Remove(key);

    public void Add(TKey key, TValue value) => _dictionary.Add(key, (value, _counter++));

    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_dictionary.TryGetValue(key, out var pair))
        {
            value = pair.Value;
            return true;
        }
        value = default;
        return false;
    }

    public int Count => _dictionary.Count;

    public bool IsReadOnly => false;

    public void Clear()
    {
        _dictionary.Clear();
        _counter = 0;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return ordered.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ICollection<TKey> Keys => ordered.Select(kvp => kvp.Key).ToList();
    public ICollection<TValue> Values => ordered.Select(kvp => kvp.Value).ToList();
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public bool Contains(KeyValuePair<TKey, TValue> item) => _dictionary.TryGetValue(item.Key, out var p) && EqualityComparer<TValue>.Default.Equals(p.Value, item.Value);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => this.ToList().CopyTo(array, arrayIndex);
    public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);

    private IEnumerable<KeyValuePair<TKey, TValue>> ordered => _dictionary
        .OrderBy(kvp => kvp.Value.Order)
        .Select(kvp => new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value));
}
#nullable restore