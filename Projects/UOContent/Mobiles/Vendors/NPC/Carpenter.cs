using System;
using ModernUO.Serialization;
using Server.Engines.BulkOrders;
using System.Collections.Generic;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Carpenter : BaseVendor
    {
        private readonly List<SBInfo> m_SBInfos = new();

        [Constructible]
        public Carpenter() : base("the carpenter")
        {
            SetSkill(SkillName.Carpentry, 85.0, 100.0);
            SetSkill(SkillName.Lumberjacking, 60.0, 83.0);
        }

        protected override List<SBInfo> SBInfos => m_SBInfos;

        public override NpcGuild NpcGuild => NpcGuild.TinkersGuild;

        public override void InitSBInfo()
        {
            m_SBInfos.Add(new SBStavesWeapon());
            m_SBInfos.Add(new SBCarpenter());
            m_SBInfos.Add(new SBWoodenShields());

            if (IsTokunoVendor)
            {
                m_SBInfos.Add(new SBSECarpenter());
            }
        }

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new HalfApron());
        }

        public override bool SupportsBulkOrders(Mobile from) => WoodworkBulkOrders.Supports(from, BODType.Carpentry);

        public override Item CreateBulkOrder(Mobile from, bool fromContextMenu) =>
            WoodworkBulkOrders.CreateOffer(from, BODType.Carpentry, fromContextMenu);

        public override bool IsValidBulkOrder(Item item) => WoodworkBulkOrders.Accepts(item, BODType.Carpentry);

        public override TimeSpan GetNextBulkOrder(Mobile from) => WoodworkBulkOrders.Remaining(from, BODType.Carpentry);

        public override void OnSuccessfulBulkOrderReceive(Mobile from) => WoodworkBulkOrders.Clear(from, BODType.Carpentry);
    }
}
