using System;
using System.Collections.Generic;

namespace Server.Items;

internal static class ExcelsiorMaterialVariants
{
    private static readonly Dictionary<Type, CraftResource> _sources = new()
    {
        [typeof(IronIngot)] = CraftResource.ExcelsiorIron,
        [typeof(DullCopperIngot)] = CraftResource.ExcelsiorDullCopper,
        [typeof(ShadowIronIngot)] = CraftResource.ExcelsiorShadowIron,
        [typeof(CopperIngot)] = CraftResource.ExcelsiorCopper,
        [typeof(BronzeIngot)] = CraftResource.ExcelsiorBronze,
        [typeof(GoldIngot)] = CraftResource.ExcelsiorGold,
        [typeof(AgapiteIngot)] = CraftResource.ExcelsiorAgapite,
        [typeof(VeriteIngot)] = CraftResource.ExcelsiorVerite,
        [typeof(ValoriteIngot)] = CraftResource.ExcelsiorValorite,
        [typeof(Leather)] = CraftResource.ExcelsiorRegularLeather,
        [typeof(SpinedLeather)] = CraftResource.ExcelsiorSpinedLeather,
        [typeof(HornedLeather)] = CraftResource.ExcelsiorHornedLeather,
        [typeof(BarbedLeather)] = CraftResource.ExcelsiorBarbedLeather,
        [typeof(AshBoard)] = CraftResource.ExcelsiorAshWood,
        [typeof(YewBoard)] = CraftResource.ExcelsiorYewWood,
        [typeof(OakBoard)] = CraftResource.ExcelsiorOakWood
    };

    internal static CraftResource VariantOf(Type material) => _sources.GetValueOrDefault(material);

    internal static ExcelsiorMaterial[] CreateCatalog() =>
    [
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorIron, "Excelsior Iron", 0, CraftResources.GetHue(CraftResource.Iron), 20, 0,
            (0, 0, 0, 0, 0), (0, 0, 0, 0), (0, 0, 0, 0), typeof(ExcelsiorIronIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorDullCopper, "Excelsior Dull Copper", 65, CraftResources.GetHue(CraftResource.DullCopper), 30, 5,
            (3, 0, 0, 0, 0), (0, 0, 0, 0), (1, 2, 40, 100), typeof(ExcelsiorDullCopperIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorShadowIron, "Excelsior Shadow Iron", 70, CraftResources.GetHue(CraftResource.ShadowIron), 40, 10,
            (2, 0, 2, 0, 0), (0, 20, 0, 0), (2, 2, 45, 100), typeof(ExcelsiorShadowIronIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorCopper, "Excelsior Copper", 75, CraftResources.GetHue(CraftResource.Copper), 50, 15,
            (2, 0, 0, 1, 3), (0, 0, 10, 20), (2, 3, 50, 100), typeof(ExcelsiorCopperIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorBronze, "Excelsior Bronze", 80, CraftResources.GetHue(CraftResource.Bronze), 60, 20,
            (1, 5, 0, 0, 0), (40, 0, 0, 0), (3, 3, 55, 100), typeof(ExcelsiorBronzeIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorGold, "Excelsior Gold", 85, CraftResources.GetHue(CraftResource.Gold), 75, 25,
            (2, 2, 1, 2, 2), (0, 0, 0, 0), (3, 4, 60, 100), typeof(ExcelsiorGoldIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorAgapite, "Excelsior Agapite", 90, CraftResources.GetHue(CraftResource.Agapite), 90, 30,
            (0, 0, 3, 0, 3), (0, 30, 0, 20), (4, 4, 65, 100), typeof(ExcelsiorAgapiteIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorVerite, "Excelsior Verite", 95, CraftResources.GetHue(CraftResource.Verite), 105, 40,
            (2, 0, 0, 4, 2), (0, 0, 40, 20), (4, 5, 70, 100), typeof(ExcelsiorVeriteIngot)),
        ExcelsiorMaterials.Metal(CraftResource.ExcelsiorValorite, "Excelsior Valorite", 99, CraftResources.GetHue(CraftResource.Valorite), 120, 50,
            (2, 1, 2, 0, 2), (10, 20, 10, 20), (5, 5, 85, 100), typeof(ExcelsiorValoriteIngot)),
        ExcelsiorMaterials.Hide(CraftResource.ExcelsiorRegularLeather, "Excelsior Regular", 0, CraftResources.GetHue(CraftResource.RegularLeather), 20, 20,
            (0, 0, 0, 0, 0), (0, 0, 0, 0), typeof(ExcelsiorRegularLeather)),
        ExcelsiorMaterials.Hide(CraftResource.ExcelsiorSpinedLeather, "Excelsior Spined", 65, CraftResources.GetHue(CraftResource.SpinedLeather), 45, 20,
            (3, 0, 0, 0, 0), (1, 3, 40, 100), typeof(ExcelsiorSpinedLeather)),
        ExcelsiorMaterials.Hide(CraftResource.ExcelsiorHornedLeather, "Excelsior Horned", 80, CraftResources.GetHue(CraftResource.HornedLeather), 70, 30,
            (1, 1, 1, 1, 0), (3, 4, 45, 100), typeof(ExcelsiorHornedLeather)),
        ExcelsiorMaterials.Hide(CraftResource.ExcelsiorBarbedLeather, "Excelsior Barbed", 99, CraftResources.GetHue(CraftResource.BarbedLeather), 95, 40,
            (2, 1, 1, 1, 1), (4, 5, 50, 100), typeof(ExcelsiorBarbedLeather)),
        ExcelsiorMaterials.Wood(CraftResource.ExcelsiorAshWood, "Excelsior Ash", 30, CraftResources.GetHue(CraftResource.AshWood), 10,
            (40, 0, 0, 0), (2, 2, 35, 65), typeof(ExcelsiorAshBoard)),
        ExcelsiorMaterials.Wood(CraftResource.ExcelsiorYewWood, "Excelsior Yew", 50, CraftResources.GetHue(CraftResource.YewWood), 30,
            (0, 30, 0, 20), (2, 3, 40, 70), typeof(ExcelsiorYewBoard)),
        ExcelsiorMaterials.Wood(CraftResource.ExcelsiorOakWood, "Excelsior Oak", 60, CraftResources.GetHue(CraftResource.OakWood), 40,
            (0, 0, 40, 20), (1, 2, 30, 60), typeof(ExcelsiorOakBoard))
    ];
}
