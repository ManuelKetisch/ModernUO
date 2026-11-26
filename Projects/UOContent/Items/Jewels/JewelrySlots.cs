using System;

namespace Server.Items;

public static class JewelrySlots
{
    public const Layer Ring1 = Layer.Ring;
    public const Layer Ring2 = (Layer)0x1E;
    public const Layer Ring3 = (Layer)0x1F;
    public const Layer Ring4 = (Layer)0x20;
    public const Layer Ring5 = (Layer)0x21;
    public const Layer Ring6 = (Layer)0x22;

    public const Layer Earrings1 = Layer.Earrings;
    public const Layer Earrings2 = (Layer)0x23;

    public const Layer Bracelet1 = Layer.Bracelet;
    public const Layer Bracelet2 = (Layer)0x24;

    private static readonly Layer[] _ringLayers =
    {
        Ring1, Ring2, Ring3, Ring4, Ring5, Ring6
    };

    private static readonly Layer[] _earringsLayers =
    {
        Earrings1, Earrings2
    };

    private static readonly Layer[] _braceletLayers =
    {
        Bracelet1, Bracelet2
    };

    public static ReadOnlySpan<Layer> RingLayers => _ringLayers;

    public static ReadOnlySpan<Layer> EarringsLayers => _earringsLayers;

    public static ReadOnlySpan<Layer> BraceletLayers => _braceletLayers;

    public static bool IsRingLayer(Layer layer) =>
        layer is Ring1 or Ring2 or Ring3 or Ring4 or Ring5 or Ring6;

    public static bool IsEarringsLayer(Layer layer) =>
        layer is Earrings1 or Earrings2;

    public static bool IsBraceletLayer(Layer layer) =>
        layer is Bracelet1 or Bracelet2;

    public static bool IsExtendedJewelryLayer(Layer layer) =>
        layer is Ring2 or Ring3 or Ring4 or Ring5 or Ring6 or Earrings2 or Bracelet2;

    public static bool IsMultiSlotJewelryLayer(Layer layer) =>
        IsRingLayer(layer) || IsEarringsLayer(layer) || IsBraceletLayer(layer);

    public static bool IsRingItem(Item item) =>
        item != null && (item is BaseRing || IsRingLayer(item.Layer));

    public static bool IsEarringsItem(Item item) =>
        item != null && (item is BaseEarrings || IsEarringsLayer(item.Layer));

    public static bool IsBraceletItem(Item item) =>
        item != null && (item is BaseBracelet || IsBraceletLayer(item.Layer));

    public static bool IsMultiSlotJewelry(Item item) =>
        IsRingItem(item) || IsEarringsItem(item) || IsBraceletItem(item);

    public static Layer GetCanonicalLayer(Layer layer)
    {
        if (IsRingLayer(layer))
        {
            return Layer.Ring;
        }

        if (IsEarringsLayer(layer))
        {
            return Layer.Earrings;
        }

        if (IsBraceletLayer(layer))
        {
            return Layer.Bracelet;
        }

        return layer;
    }

    public static Layer GetCanonicalLayer(Item item)
    {
        if (item == null)
        {
            return Layer.Invalid;
        }

        if (item is BaseRing)
        {
            return Layer.Ring;
        }

        if (item is BaseEarrings)
        {
            return Layer.Earrings;
        }

        if (item is BaseBracelet)
        {
            return Layer.Bracelet;
        }

        return GetCanonicalLayer(item.Layer);
    }

    public static ReadOnlySpan<Layer> GetSlotsFor(Item item)
    {
        if (IsRingItem(item))
        {
            return _ringLayers;
        }

        if (IsEarringsItem(item))
        {
            return _earringsLayers;
        }

        if (IsBraceletItem(item))
        {
            return _braceletLayers;
        }

        return ReadOnlySpan<Layer>.Empty;
    }

    public static bool HasFreeSlot(Mobile mobile, Item item) =>
        TryResolveEquipLayer(mobile, item, Layer.Invalid, out _);

    public static bool TryResolveEquipLayer(Mobile mobile, Item item, Layer requestedLayer, out Layer resolvedLayer)
    {
        resolvedLayer = Layer.Invalid;
        if (mobile == null || item == null)
        {
            return false;
        }

        var slots = GetSlotsFor(item);
        if (slots.IsEmpty)
        {
            return false;
        }

        if (requestedLayer != Layer.Invalid && IsSameFamily(item, requestedLayer) &&
            mobile.FindItemOnLayer(requestedLayer) == null)
        {
            resolvedLayer = requestedLayer;
            return true;
        }

        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (mobile.FindItemOnLayer(slot) == null)
            {
                resolvedLayer = slot;
                return true;
            }
        }

        return false;
    }

    public static void ResetToCanonicalLayer(Item item)
    {
        if (item != null && IsExtendedJewelryLayer(item.Layer))
        {
            item.Layer = GetCanonicalLayer(item);
        }
    }

    private static bool IsSameFamily(Item item, Layer layer)
    {
        if (IsRingItem(item))
        {
            return IsRingLayer(layer);
        }

        if (IsEarringsItem(item))
        {
            return IsEarringsLayer(layer);
        }

        return IsBraceletItem(item) && IsBraceletLayer(layer);
    }
}
