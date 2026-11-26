using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Json;
using Server.Logging;
using Server.Maps;

namespace Server.Engines.Spawners;

/// <summary>
/// Reconciles canonical town vendor and NPC spawners at startup so existing saves recover any NPCs
/// lost to prior same-tile import clobbering, missing facet flags, or newly populated shop definitions.
/// </summary>
public static class TownNpcSpawns
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TownNpcSpawns));

    // Malas South wilderness spawner carrying a WanderingHealer that shared coordinates with a wildlife spawner.
    private static readonly Guid MalasSouthWanderingHealerGuid = new("27bdb175-d7c0-49c9-b389-b26fe028e58a");

    public static void Initialize()
    {
        if (Core.IsRunningFromXUnit)
        {
            return;
        }

        var availableMaps = ExpansionInfo.CoreExpansion.MapSelectionFlags;
        var eraFolder = Core.SA ? "post-uoml" : "uoml";

        var added = 0;
        var updated = 0;

        if (Core.SA && availableMaps.Includes(MapSelectionFlags.TerMur))
        {
            ReconcileRelativeFile("Data/Spawns/post-uoml/termur/Vendors.json", ref added, ref updated);
        }

        if (availableMaps.Includes(MapSelectionFlags.Malas))
        {
            ReconcileRelativeFile($"Data/Spawns/{eraFolder}/malas/Vendors.json", ref added, ref updated);
            ReconcileRelativeFile("Data/Spawns/shared/malas/Vendors.json", ref added, ref updated);

            if (Core.SA)
            {
                ReconcileRelativeFile(
                    "Data/Spawns/post-uoml/malas/South.json",
                    ref added,
                    ref updated,
                    MalasSouthWanderingHealerGuid
                );
            }
        }

        if (availableMaps.Includes(MapSelectionFlags.Tokuno))
        {
            ReconcileRelativeFile("Data/Spawns/shared/tokuno/Vendors.json", ref added, ref updated);
        }

        if (availableMaps.Includes(MapSelectionFlags.Ilshenar))
        {
            ReconcileRelativeFile("Data/Spawns/shared/ilshenar/Vendors.json", ref added, ref updated);
        }

        if (availableMaps.Includes(MapSelectionFlags.Trammel))
        {
            ReconcileRelativeFile($"Data/Spawns/{eraFolder}/trammel/Vendors.json", ref added, ref updated);
            ReconcileRelativeFile("Data/Spawns/shared/trammel/Vendors.json", ref added, ref updated);
            ReconcileRelativeFile("Data/Spawns/shared/trammel/TownsPeople.json", ref added, ref updated);
        }

        if (availableMaps.Includes(MapSelectionFlags.Felucca))
        {
            if (Core.SA)
            {
                ReconcileRelativeFile("Data/Spawns/post-uoml/felucca/Vendors.json", ref added, ref updated);
            }

            ReconcileRelativeFile("Data/Spawns/shared/felucca/Vendors.json", ref added, ref updated);
            ReconcileRelativeFile("Data/Spawns/shared/felucca/TownsPeople.json", ref added, ref updated);
        }

        if (added > 0 || updated > 0)
        {
            _logger.Information(
                "Reconciled town NPC spawners: added {Added}, updated {Updated}.",
                added,
                updated
            );
        }
    }

    private static void ReconcileRelativeFile(
        string relativePath,
        ref int added,
        ref int updated,
        Guid? onlyGuid = null
    )
    {
        var path = Path.Combine(Core.BaseDirectory, relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        ReconcileFile(path, ref added, ref updated, onlyGuid);
    }

    internal static void ReconcileFile(
        string path,
        ref int added,
        ref int updated,
        Guid? onlyGuid = null
    )
    {
        List<SpawnerDto> definitions;
        try
        {
            definitions = JsonConfig.Deserialize<List<SpawnerDto>>(path, SpawnerJsonSerializer.Options);
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            _logger.Error(error, "Could not load town NPC spawn file {Path}.", path);
            return;
        }

        if (definitions == null)
        {
            return;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (onlyGuid.HasValue && definition.Guid != onlyGuid.Value)
            {
                continue;
            }

            ReconcileDefinition(definition, ref added, ref updated);
        }
    }

    internal static void ReconcileDefinition(SpawnerDto definition, ref int added, ref int updated)
    {
        var map = definition.Map;
        if (map == null || map == Map.Internal)
        {
            return;
        }

        BaseSpawner existingByGuid = null;
        var coveredByOtherSpawner = false;

        foreach (var candidate in map.GetItemsAt<BaseSpawner>(definition.Location))
        {
            if (candidate.Guid == definition.Guid)
            {
                existingByGuid = candidate;
                break;
            }

            if (candidate.Z == definition.Location.Z && CoversAllEntries(candidate, definition))
            {
                coveredByOtherSpawner = true;
            }
        }

        if (existingByGuid != null)
        {
            if (UpdateExistingSpawner(existingByGuid, definition))
            {
                updated++;
            }

            return;
        }

        if (coveredByOtherSpawner)
        {
            return;
        }

        BaseSpawner spawner;
        try
        {
            spawner = definition.ToSpawner();
        }
        catch (Exception error)
        {
            _logger.Error(error, "Could not build town NPC spawner {Guid}.", definition.Guid);
            return;
        }

        spawner.MoveToWorld(definition.Location, map);
        spawner.Respawn();
        added++;
    }

    private static bool UpdateExistingSpawner(BaseSpawner spawner, SpawnerDto definition)
    {
        var changed = false;

        if (spawner.Z != definition.Location.Z)
        {
            spawner.MoveToWorld(definition.Location, definition.Map);
            changed = true;
        }

        var desiredEntries = definition.EntryView;
        if (desiredEntries != null && desiredEntries.Count > 0)
        {
            var existingCounts = BuildEntryCountMap(spawner.Entries);
            var desiredCounts = BuildEntryCountMap(desiredEntries);

            foreach (var (name, desiredMax) in desiredCounts)
            {
                existingCounts.TryGetValue(name, out var currentMax);
                if (currentMax >= desiredMax)
                {
                    continue;
                }

                var probability = GetEntryProbability(desiredEntries, name);
                spawner.AddEntry(name, probability, desiredMax - currentMax, dotimer: false);
                changed = true;
            }
        }

        if (spawner.Count < definition.Count)
        {
            spawner.Count = definition.Count;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        // Fill only the newly added slots so already-spawned shopkeepers keep their live state.
        for (var i = 0; i < definition.Count && !spawner.IsFull; i++)
        {
            var before = spawner.Spawned.Count;
            spawner.Spawn();
            if (spawner.Spawned.Count == before)
            {
                break;
            }
        }

        return true;
    }

    private static bool CoversAllEntries(BaseSpawner candidate, SpawnerDto definition)
    {
        var desiredEntries = definition.EntryView;
        if (desiredEntries == null || desiredEntries.Count == 0)
        {
            return candidate.Entries.Count == 0;
        }

        var candidateCounts = BuildEntryCountMap(candidate.Entries);
        var desiredCounts = BuildEntryCountMap(desiredEntries);

        foreach (var (name, desiredMax) in desiredCounts)
        {
            if (!candidateCounts.TryGetValue(name, out var currentMax) || currentMax < desiredMax)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, int> BuildEntryCountMap(IReadOnlyList<SpawnerEntry> entries)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (entries == null)
        {
            return counts;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (string.IsNullOrEmpty(entry.SpawnedName))
            {
                continue;
            }

            counts.TryGetValue(entry.SpawnedName, out var existing);
            counts[entry.SpawnedName] = existing + entry.SpawnedMaxCount;
        }

        return counts;
    }

    private static int GetEntryProbability(IReadOnlyList<SpawnerEntry> entries, string name)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].SpawnedName.InsensitiveEquals(name))
            {
                return entries[i].SpawnedProbability;
            }
        }

        return 100;
    }
}
