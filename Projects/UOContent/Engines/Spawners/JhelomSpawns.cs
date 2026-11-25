using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Json;
using Server.Logging;

namespace Server.Engines.Spawners;

public static class JhelomSpawns
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(JhelomSpawns));

    public static void Initialize()
    {
        if (!SoloProgression.Enabled)
        {
            return;
        }

        AddMissingSpawnPoints("jhelom-taming-pit.json", "taming pit");
        AddMissingSpawnPoints("jhelom-cemetery.json", "cemetery");
    }

    private static void AddMissingSpawnPoints(string fileName, string area)
    {
        var path = Path.Combine(Core.BaseDirectory, "Data/Spawns/shared", fileName);
        List<SpawnerDto> definitions;
        try
        {
            definitions = JsonConfig.Deserialize<List<SpawnerDto>>(path, SpawnerJsonSerializer.Options);
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            _logger.Error(error, "Could not load the Jhelom {Area} spawn points.", area);
            return;
        }

        if (definitions == null)
        {
            return;
        }

        var added = 0;
        foreach (var definition in definitions)
        {
            var map = definition.Map;
            if (HasSpawnPoint(map, definition))
            {
                continue;
            }

            var spawner = definition.ToSpawner();
            spawner.MoveToWorld(definition.Location, map);
            spawner.Respawn();
            added++;
        }

        if (added > 0)
        {
            _logger.Information("Added {Count} Jhelom {Area} spawn points.", added, area);
        }
    }

    private static bool HasSpawnPoint(Map map, SpawnerDto definition)
    {
        foreach (var spawner in map.GetItemsAt<Spawner>(definition.Location))
        {
            if (spawner.Guid == definition.Guid)
            {
                return true;
            }
        }

        return false;
    }
}
