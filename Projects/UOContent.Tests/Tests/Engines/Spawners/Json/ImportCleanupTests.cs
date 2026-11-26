using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Spawners;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines.Spawners.Json;

[Collection("Sequential UOContent Tests")]
public class ImportCleanupTests : IDisposable
{
    private const string SpawnerGuid = "11111111-1111-1111-1111-111111111111";

    private readonly TempDirectory _dir = new("muo-spawner-import");

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Import_ValidFile_PlacesSpawner()
    {
        var path = _dir.WriteFile("test.json", """
            [
              {
                "$type": "Spawner",
                "guid": "11111111-1111-1111-1111-111111111111",
                "location": [305, 305, 0],
                "map": "Felucca",
                "count": 1,
                "spawnBounds": { "x1": 300, "y1": 300, "x2": 310, "y2": 310 },
                "entries": []
              }
            ]
            """);

        BaseSpawner placedSpawner = null;
        try
        {
            var all = new Dictionary<Guid, ISpawner>();
            ImportSpawnersCommand.ImportFile(new FileInfo(path), all);

            foreach (var s in Map.Felucca.GetItemsAt<BaseSpawner>(new Point3D(305, 305, 0)))
            {
                if (s.Guid == new Guid(SpawnerGuid))
                {
                    placedSpawner = s;
                    break;
                }
            }

            Assert.NotNull(placedSpawner);
            Assert.True(all.ContainsKey(new Guid(SpawnerGuid)));
        }
        finally
        {
            placedSpawner?.Delete();
        }
    }

    // A DTO with no map entry deserializes with map == null. The importer rejects it before building
    // a spawner, so nothing is registered and nothing lands on any live map.
    [Fact]
    public void Import_NoMap_SkipsTheSpawner()
    {
        var path = _dir.WriteFile("test-nomap.json", """
            [
              {
                "$type": "Spawner",
                "guid": "22222222-2222-2222-2222-222222222222",
                "location": [400, 400, 0],
                "count": 1,
                "spawnBounds": { "x1": 395, "y1": 395, "x2": 405, "y2": 405 },
                "entries": []
              }
            ]
            """);

        var guid = new Guid("22222222-2222-2222-2222-222222222222");
        var all = new Dictionary<Guid, ISpawner>();
        ImportSpawnersCommand.ImportFile(new FileInfo(path), all);

        Assert.DoesNotContain(guid, all.Keys);

        var maps = Map.AllMaps;
        for (var i = 0; i < maps.Count; i++)
        {
            foreach (var spawner in maps[i].GetItemsAt<BaseSpawner>(new Point3D(400, 400, 0)))
            {
                Assert.NotEqual(guid, spawner.Guid);
            }
        }
    }

    [Fact]
    public void Import_DuplicateLocation_ReplacesExistingSpawner()
    {
        // Verifies that an existing spawner at the same location and type is removed and replaced.
        var newGuid = new Guid("33333333-3333-3333-3333-333333333333");
        var path = _dir.WriteFile("test-dup.json", """
            [
              {
                "$type": "Spawner",
                "guid": "33333333-3333-3333-3333-333333333333",
                "location": [310, 310, 0],
                "map": "Felucca",
                "count": 1,
                "spawnBounds": { "x1": 305, "y1": 305, "x2": 315, "y2": 315 },
                "entries": []
              }
            ]
            """);

        var existing = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
        BaseSpawner placedSpawner = null;

        try
        {
            existing.MoveToWorld(new Point3D(310, 310, 0), Map.Felucca);

            var all = new Dictionary<Guid, ISpawner> { [existing.Guid] = existing };
            ImportSpawnersCommand.ImportFile(new FileInfo(path), all);

            // existing should have been deleted and replaced.
            Assert.True(existing.Deleted);
            Assert.DoesNotContain(existing.Guid, all.Keys);

            foreach (var s in Map.Felucca.GetItemsAt<BaseSpawner>(new Point3D(310, 310, 0)))
            {
                if (s.Guid == newGuid)
                {
                    placedSpawner = s;
                    break;
                }
            }

            Assert.NotNull(placedSpawner);
        }
        finally
        {
            existing.Delete();
            placedSpawner?.Delete();
        }
    }

    [Fact]
    public void Import_DistinctGuidsAtSameLocationInSameRun_PreservesBoth()
    {
        var guidA = new Guid("44444444-4444-4444-4444-444444444441");
        var guidB = new Guid("44444444-4444-4444-4444-444444444442");
        var path = _dir.WriteFile("test-same-loc-batch.json", """
            [
              {
                "$type": "Spawner",
                "guid": "44444444-4444-4444-4444-444444444441",
                "location": [320, 320, 0],
                "map": "Felucca",
                "count": 1,
                "homeRange": 0,
                "entries": []
              },
              {
                "$type": "Spawner",
                "guid": "44444444-4444-4444-4444-444444444442",
                "location": [320, 320, 0],
                "map": "Felucca",
                "count": 1,
                "homeRange": 0,
                "entries": []
              }
            ]
            """);

        var all = new Dictionary<Guid, ISpawner>();
        try
        {
            ImportSpawnersCommand.ImportFile(new FileInfo(path), all);

            Assert.True(all.TryGetValue(guidA, out var spawnerA));
            Assert.True(all.TryGetValue(guidB, out var spawnerB));
            Assert.False(spawnerA.Deleted);
            Assert.False(spawnerB.Deleted);
        }
        finally
        {
            foreach (var spawner in all.Values)
            {
                spawner.Delete();
            }
        }
    }

    [Fact]
    public void Import_DifferentZAtSameXY_PreservesExistingSpawner()
    {
        var guidLower = new Guid("55555555-5555-5555-5555-555555555551");
        var guidUpper = new Guid("55555555-5555-5555-5555-555555555552");
        var fileLower = _dir.WriteFile("test-lower-z.json", """
            [
              {
                "$type": "Spawner",
                "guid": "55555555-5555-5555-5555-555555555551",
                "location": [330, 330, -8],
                "map": "Felucca",
                "count": 1,
                "homeRange": 0,
                "entries": []
              }
            ]
            """);
        var fileUpper = _dir.WriteFile("test-upper-z.json", """
            [
              {
                "$type": "Spawner",
                "guid": "55555555-5555-5555-5555-555555555552",
                "location": [330, 330, 0],
                "map": "Felucca",
                "count": 1,
                "homeRange": 0,
                "entries": []
              }
            ]
            """);

        var all = new Dictionary<Guid, ISpawner>();
        try
        {
            // Import in two separate calls (mirrors separate Vendors.json and Reagents.json imports).
            ImportSpawnersCommand.ImportFile(new FileInfo(fileLower), all);
            ImportSpawnersCommand.ImportFile(new FileInfo(fileUpper), all);

            Assert.True(all.TryGetValue(guidLower, out var lower));
            Assert.True(all.TryGetValue(guidUpper, out var upper));
            Assert.False(lower.Deleted);
            Assert.False(upper.Deleted);
        }
        finally
        {
            foreach (var spawner in all.Values)
            {
                spawner.Delete();
            }
        }
    }
}
