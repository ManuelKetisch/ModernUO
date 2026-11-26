using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Spawners;
using Server.Json;
using Server.Maps;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines.Spawners;

[Collection("Sequential UOContent Tests")]
public class TownNpcSpawnsTests : IDisposable
{
    private readonly TempDirectory _dir = new("muo-town-npc-spawns");

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void AllActiveVendorSpawnFiles_HaveUniqueGuidsAndNoWithinFile2DCollisions()
    {
        var root = Path.Combine(Core.BaseDirectory, "Data", "Spawns");
        var files = new[]
        {
            "shared/trammel/Vendors.json",
            "shared/felucca/Vendors.json",
            "shared/ilshenar/Vendors.json",
            "shared/malas/Vendors.json",
            "shared/tokuno/Vendors.json",
            "post-uoml/trammel/Vendors.json",
            "post-uoml/felucca/Vendors.json",
            "post-uoml/malas/Vendors.json",
            "post-uoml/termur/Vendors.json"
        };

        var failures = new List<string>();
        var seenGuids = new HashSet<Guid>();

        foreach (var relative in files)
        {
            var fullPath = Path.Combine(root, relative);
            Assert.True(File.Exists(fullPath), $"Missing vendor spawn file {fullPath}");

            var dtos = JsonConfig.Deserialize<List<SpawnerDto>>(fullPath, SpawnerJsonSerializer.Options);
            Assert.NotNull(dtos);

            var seen2D = new Dictionary<(Map, int, int), Guid>();
            foreach (var dto in dtos)
            {
                if (!seenGuids.Add(dto.Guid))
                {
                    failures.Add($"{relative}: duplicate GUID {dto.Guid}");
                }

                var key2D = (dto.Map, dto.Location.X, dto.Location.Y);
                if (seen2D.TryGetValue(key2D, out var otherGuid))
                {
                    failures.Add($"{relative}: 2D collision at {dto.Map} ({dto.Location.X}, {dto.Location.Y}) between {otherGuid} and {dto.Guid}");
                }
                else
                {
                    seen2D[key2D] = dto.Guid;
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void RestoredShopDefinitions_ContainExpectedVendors()
    {
        var root = Path.Combine(Core.BaseDirectory, "Data", "Spawns");

        var trammel = JsonConfig.Deserialize<List<SpawnerDto>>(
            Path.Combine(root, "shared/trammel/Vendors.json"),
            SpawnerJsonSerializer.Options
        );
        var felucca = JsonConfig.Deserialize<List<SpawnerDto>>(
            Path.Combine(root, "shared/felucca/Vendors.json"),
            SpawnerJsonSerializer.Options
        );
        var newHaven = JsonConfig.Deserialize<List<SpawnerDto>>(
            Path.Combine(root, "post-uoml/trammel/Vendors.json"),
            SpawnerJsonSerializer.Options
        );
        var terMur = JsonConfig.Deserialize<List<SpawnerDto>>(
            Path.Combine(root, "post-uoml/termur/Vendors.json"),
            SpawnerJsonSerializer.Options
        );

        AssertShopHasEntries(trammel, new Point3D(1442, 3802, 0), "Provisioner", "Cobbler");
        AssertShopHasEntries(trammel, new Point3D(1850, 2796, -8), "Provisioner", "Cobbler");
        AssertShopHasEntries(trammel, new Point3D(620, 2218, 0), "Healer", "HealerGuildmaster");
        AssertShopHasEntries(trammel, new Point3D(2972, 3425, 15), "TavernKeeper", "Waiter", "Cook", "Barkeeper");
        AssertShopHasEntries(trammel, new Point3D(3007, 3408, 15), "Blacksmith", "BlacksmithGuildmaster");
        AssertShopHasEntries(trammel, new Point3D(3013, 3353, 15), "Herbalist", "Alchemist", "CustomHairstylist");
        AssertShopHasEntries(trammel, new Point3D(2227, 1198, 0), "Banker", "Minter");
        AssertShopHasEntries(trammel, new Point3D(1506, 1572, 20), "Veterinarian");
        AssertShopHasEntries(trammel, new Point3D(2898, 739, 0), "Bard", "BardGuildmaster");
        AssertShopHasEntries(trammel, new Point3D(551, 826, 0), "Miller");

        AssertShopHasEntries(felucca, new Point3D(1850, 2796, -8), "Provisioner", "Cobbler");
        AssertShopHasEntries(felucca, new Point3D(2450, 428, 15), "Provisioner", "Cobbler");
        AssertShopHasEntries(felucca, new Point3D(2513, 477, 15), "Carpenter", "Architect", "RealEstateBroker");
        AssertShopHasEntries(felucca, new Point3D(3545, 1178, 0), "Tanner", "Furtrader");
        AssertShopHasEntries(felucca, new Point3D(2227, 1198, 0), "Banker", "Minter");

        AssertShopHasEntries(newHaven, new Point3D(3446, 2606, 30), "Tanner", "Furtrader", "LeatherWorker");
        AssertShopHasEntries(newHaven, new Point3D(3461, 2566, 35), "HairStylist", "Herbalist", "Alchemist");
        AssertShopHasEntries(newHaven, new Point3D(3463, 2558, 35), "Healer", "HealerGuildmaster");
        AssertShopHasEntries(newHaven, new Point3D(3470, 2519, 48), "Herbalist", "HairStylist");
        AssertShopHasEntries(newHaven, new Point3D(3526, 2536, 20), "BlacksmithGuildmaster", "IronWorker", "Miner");
        AssertShopHasEntries(newHaven, new Point3D(3479, 2534, 38), "Provisioner", "Cobbler");
        AssertShopHasEntries(newHaven, new Point3D(3443, 2640, 28), "Carpenter", "Architect", "RealEstateBroker");
        AssertShopHasEntries(newHaven, new Point3D(3535, 2540, 20), "Bowyer");
        AssertShopHasEntries(newHaven, new Point3D(3526, 2554, 20), "WarriorGuildmaster");
        AssertShopHasEntries(newHaven, new Point3D(3414, 2602, 55), "Bard", "BardGuildmaster");
        AssertShopHasEntries(newHaven, new Point3D(3494, 2591, 13), "Mapmaker");

        AssertShopHasEntries(terMur, new Point3D(943, 3924, -41), "InnKeeper");
        AssertShopHasEntries(terMur, new Point3D(797, 3494, -20), "Provisioner", "Cobbler");
    }

    [Fact]
    public void ExpansionsJson_EnablesTerMurFlagForSAAndLater()
    {
        var path = Path.Combine(Core.BaseDirectory, "Data", "expansions.json");
        var raw = File.ReadAllText(path);
        Assert.DoesNotContain("\"TerMer\"", raw);

        var expansions = JsonConfig.Deserialize<ExpansionInfo[]>(path);
        Assert.NotNull(expansions);

        var ej = expansions.First(e => e.Id == (int)Expansion.EJ);
        Assert.True(ej.MapSelectionFlags.Includes(MapSelectionFlags.TerMur));
    }

    [Fact]
    public void ReconcileFile_UpdatesExistingSplitSpawnerAndIsIdempotent()
    {
        var guid = new Guid("66666666-6666-6666-6666-666666666661");
        var loc = new Point3D(340, 340, 0);
        var path = _dir.WriteFile("jhelom-needful.json", """
            [
              {
                "$type": "Spawner",
                "guid": "66666666-6666-6666-6666-666666666661",
                "location": [340, 340, 0],
                "map": "Felucca",
                "count": 2,
                "minDelay": "00:05:00",
                "maxDelay": "00:10:00",
                "homeRange": 0,
                "walkingRange": 5,
                "entries": [
                  { "name": "Provisioner", "maxCount": 1, "probability": 100 },
                  { "name": "Cobbler", "maxCount": 1, "probability": 100 }
                ]
              }
            ]
            """);

        // Simulate existing save that only had Cobbler after Provisioner was clobbered.
        var existingDto = JsonSerializer.Deserialize<SpawnerDto>(
            """
            {
              "$type": "Spawner",
              "guid": "66666666-6666-6666-6666-666666666661",
              "location": [340, 340, 0],
              "map": "Felucca",
              "count": 1,
              "minDelay": "00:05:00",
              "maxDelay": "00:10:00",
              "homeRange": 0,
              "walkingRange": 5,
              "entries": [
                { "name": "Cobbler", "maxCount": 1, "probability": 100 }
              ]
            }
            """,
            SpawnerJsonSerializer.Options
        );

        var existing = existingDto.ToSpawner();
        try
        {
            existing.MoveToWorld(loc, Map.Felucca);
            existing.Respawn();
            var originalCobbler = Assert.Single(existing.Spawned.Keys);
            Assert.IsType<Cobbler>(originalCobbler);

            var added = 0;
            var updated = 0;
            TownNpcSpawns.ReconcileFile(path, ref added, ref updated);

            Assert.Equal(0, added);
            Assert.Equal(1, updated);
            Assert.Equal(2, existing.Count);
            Assert.Contains(existing.Spawned.Keys, s => ReferenceEquals(s, originalCobbler));
            Assert.Contains(existing.Spawned.Keys, s => s is Provisioner);

            // Second reconciliation pass is a no-op.
            var added2 = 0;
            var updated2 = 0;
            TownNpcSpawns.ReconcileFile(path, ref added2, ref updated2);

            Assert.Equal(0, added2);
            Assert.Equal(0, updated2);
            Assert.Equal(2, existing.Spawned.Count);
        }
        finally
        {
            existing.Delete();
        }
    }

    [Fact]
    public void ReconcileFile_RestoresMissingVendorAtDifferentZWithoutTouchingOtherSpawner()
    {
        var vendorGuid = new Guid("77777777-7777-7777-7777-777777777771");
        var path = _dir.WriteFile("trinsic-provisions.json", """
            [
              {
                "$type": "Spawner",
                "guid": "77777777-7777-7777-7777-777777777771",
                "location": [350, 350, -8],
                "map": "Felucca",
                "count": 2,
                "minDelay": "00:05:00",
                "maxDelay": "00:10:00",
                "homeRange": 0,
                "walkingRange": 5,
                "entries": [
                  { "name": "Provisioner", "maxCount": 1, "probability": 100 },
                  { "name": "Cobbler", "maxCount": 1, "probability": 100 }
                ]
              }
            ]
            """);

        // Simulate Reagents spawner at same X/Y with Z = 0.
        var reagentSpawner = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, spawnedNames: ["BlackPearl"]);
        BaseSpawner restored = null;
        try
        {
            reagentSpawner.MoveToWorld(new Point3D(350, 350, 0), Map.Felucca);

            var added = 0;
            var updated = 0;
            TownNpcSpawns.ReconcileFile(path, ref added, ref updated);

            Assert.Equal(1, added);
            Assert.Equal(0, updated);
            Assert.False(reagentSpawner.Deleted);

            foreach (var sp in Map.Felucca.GetItemsAt<BaseSpawner>(new Point3D(350, 350, -8)))
            {
                if (sp.Guid == vendorGuid)
                {
                    restored = sp;
                    break;
                }
            }

            Assert.NotNull(restored);
            Assert.Equal(-8, restored.Z);
            Assert.Contains(restored.Spawned.Keys, s => s is Provisioner);
            Assert.Contains(restored.Spawned.Keys, s => s is Cobbler);

            // Second reconciliation pass is a no-op.
            var added2 = 0;
            var updated2 = 0;
            TownNpcSpawns.ReconcileFile(path, ref added2, ref updated2);
            Assert.Equal(0, added2);
            Assert.Equal(0, updated2);
        }
        finally
        {
            reagentSpawner.Delete();
            restored?.Delete();
        }
    }

    private static void AssertShopHasEntries(List<SpawnerDto> dtos, Point3D location, params string[] expectedNames)
    {
        var match = dtos.FirstOrDefault(d => d.Location == location);
        Assert.True(match != null, $"Expected spawner at {location} not found.");
        foreach (var name in expectedNames)
        {
            Assert.True(
                match.EntryView.Any(e => e.SpawnedName.InsensitiveEquals(name)),
                $"Expected entry '{name}' in spawner at {location}."
            );
        }
    }
}
