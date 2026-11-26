using System;
using System.Collections.Generic;
using Server.Collections;

namespace Server.Engines.Imbuing;

/// <summary>
/// Tracks imbued item usage counts in an isolated persistence store so existing equipment types
/// require zero serialization format changes.
/// </summary>
public sealed class ImbuingPersistence : GenericPersistence
{
    private const int SaveVersion = 0;
    public const int ImbuedCliloc = 1080418; // (Imbued)

    private static ImbuingPersistence _instance;
    private static readonly Dictionary<Item, int> _imbuedCounts = new();

    public static void Configure()
    {
        _instance = new ImbuingPersistence();
    }

    public ImbuingPersistence() : base("Imbuing", 10)
    {
    }

    public static bool IsImbued(Item item) =>
        item is { Deleted: false } && _imbuedCounts.TryGetValue(item, out var count) && count > 0;

    public static int GetTimesImbued(Item item) =>
        item is { Deleted: false } && _imbuedCounts.TryGetValue(item, out var count) ? count : 0;

    public static void MarkImbued(Item item)
    {
        if (item == null || item.Deleted)
        {
            return;
        }

        _imbuedCounts.TryGetValue(item, out var count);
        _imbuedCounts[item] = count + 1;
        item.InvalidateProperties();
    }

    public static void CopyImbuedState(Item source, Item target)
    {
        if (source == null || target == null || target.Deleted)
        {
            return;
        }

        if (_imbuedCounts.TryGetValue(source, out var count) && count > 0)
        {
            _imbuedCounts[target] = count;
            target.InvalidateProperties();
        }
        else if (_imbuedCounts.Remove(target))
        {
            target.InvalidateProperties();
        }
    }

    public static void AddImbuedProperty(Item item, IPropertyList list)
    {
        if (list != null && IsImbued(item))
        {
            list.Add(ImbuedCliloc);
        }
    }

    public static void Clear(Item item)
    {
        if (item != null && _imbuedCounts.Remove(item) && !item.Deleted)
        {
            item.InvalidateProperties();
        }
    }

    public static void ResetForTesting()
    {
        _imbuedCounts.Clear();
    }

    public override void Serialize(IGenericWriter writer) => SerializeState(writer);

    public override void Deserialize(IGenericReader reader) => DeserializeState(reader);

    public static void SerializeState(IGenericWriter writer)
    {
        writer.WriteEncodedInt(SaveVersion);

        using var stale = PooledRefList<Item>.Create();
        using var valid = PooledRefList<(Item Item, int Count)>.Create();

        foreach (var (item, count) in _imbuedCounts)
        {
            if (item == null || item.Deleted || count <= 0)
            {
                if (item != null)
                {
                    stale.Add(item);
                }

                continue;
            }

            valid.Add((item, count));
        }

        for (var i = 0; i < stale.Count; i++)
        {
            _imbuedCounts.Remove(stale[i]);
        }

        writer.WriteEncodedInt(valid.Count);

        for (var i = 0; i < valid.Count; i++)
        {
            var entry = valid[i];
            writer.Write(entry.Item);
            writer.WriteEncodedInt(entry.Count);
        }
    }

    public static void DeserializeState(IGenericReader reader)
    {
        _imbuedCounts.Clear();

        var version = reader.ReadEncodedInt();
        if (version != SaveVersion)
        {
            throw new Exception($"Unsupported ImbuingPersistence version {version}.");
        }

        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var item = reader.ReadEntity<Item>();
            var timesImbued = reader.ReadEncodedInt();

            if (item != null && !item.Deleted && timesImbued > 0)
            {
                _imbuedCounts[item] = timesImbued;
            }
        }
    }
}
