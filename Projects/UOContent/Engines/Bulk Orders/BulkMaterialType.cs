using System;
using Server.Items;

namespace Server.Engines.BulkOrders
{
    public enum BulkMaterialType
    {
        None,
        DullCopper,
        ShadowIron,
        Copper,
        Bronze,
        Gold,
        Agapite,
        Verite,
        Valorite,
        Spined,
        Horned,
        Barbed,
        NaturalWood,
        Oak,
        Ash,
        Yew,
        Heartwood,
        Bloodwood,
        Frostwood,
        Blaze,
        Ice,
        Toxic,
        Electrum,
        Platinum,
        PolarLeather,
        SyntheticLeather,
        BlazeLeather,
        DaemonicLeather,
        ShadowLeather,
        FrostLeather,
        EtherealLeather,
        Pine,
        Mahogany,
        Zircote,
        Ebony,
        Bamboo,
        PurpleHeart,
        Redwood,
        Petrified
    }

    public enum BulkGenericType
    {
        Iron,
        Cloth,
        Leather,
        Wood
    }

    public static class BGTClassifier
    {
        public static BulkGenericType Classify(BODType deedType, Type itemType)
        {
            deedType = ExcelsiorOrderRules.Profession(deedType);
            if (WoodworkBulkOrders.IsWoodwork(deedType))
            {
                return BulkGenericType.Wood;
            }

            if (deedType != BODType.Tailor)
            {
                return BulkGenericType.Iron;
            }

            return itemType?.IsSubclassOf(typeof(BaseArmor)) != false || itemType.IsSubclassOf(typeof(BaseShoes))
                ? BulkGenericType.Leather
                : BulkGenericType.Cloth;
        }
    }
}
