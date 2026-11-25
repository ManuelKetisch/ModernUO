using System;
using System.Linq;
using Server;
using Server.Engines.BioEngineering;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class BioEngineeringVendorTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void APlayerCanBuyAFullSetOfBioEngineeringSuppliesFromATinker()
    {
        var tinker = _harness.Track(new Tinker());
        var player = _harness.NewPlayer();
        var backpack = new Backpack();
        player.AddItem(backpack);
        backpack.DropItem(new Gold(40000));
        backpack.DropItem(new Gold(40000));
        var stock = tinker.GetBuyInfo().OfType<GenericBuyInfo>().ToArray();
        var book = Assert.Single(stock, entry => entry.Type == typeof(BioEngineeringBook));
        var tool = Assert.Single(stock, entry => entry.Type == typeof(BioEngineeringTool));
        var organicMatter = Assert.Single(stock, entry => entry.Type == typeof(BioOrganicMatterSack));

        Assert.True(tinker.OnBuyItems(player, [
            new BuyItemResponse(book.GetDisplayEntity().Serial, 1),
            new BuyItemResponse(tool.GetDisplayEntity().Serial, 1),
            new BuyItemResponse(organicMatter.GetDisplayEntity().Serial, 5)
        ]));

        Assert.NotNull(backpack.FindItemByType<BioEngineeringBook>());
        Assert.NotNull(backpack.FindItemByType<BioEngineeringTool>());
        Assert.Equal(5000, backpack.GetAmount(typeof(BioOrganicMatter)));
        Assert.Equal(13000, backpack.GetAmount(typeof(Gold)));
    }
}
