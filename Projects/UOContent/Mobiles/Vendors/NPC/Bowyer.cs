using System;
using ModernUO.Serialization;
using Server.Engines.BulkOrders;
using System.Collections.Generic;
using Server.Items;

namespace Server.Mobiles
{
    [TypeAlias("Server.Mobiles.Bower")]
    [SerializationGenerator(0, false)]
    public partial class Bowyer : BaseVendor
    {
        private readonly List<SBInfo> m_SBInfos = new();

        [Constructible]
        public Bowyer() : base("the bowyer")
        {
            SetSkill(SkillName.Fletching, 80.0, 100.0);
            SetSkill(SkillName.Archery, 80.0, 100.0);
        }

        protected override List<SBInfo> SBInfos => m_SBInfos;

        public override VendorShoeType ShoeType => Female ? VendorShoeType.ThighBoots : VendorShoeType.Boots;

        public override int GetShoeHue() => 0;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Bow());
            AddItem(new LeatherGorget());
        }

        public override void InitSBInfo()
        {
            m_SBInfos.Add(new SBBowyer());
            m_SBInfos.Add(new SBRangedWeapon());

            if (IsTokunoVendor)
            {
                m_SBInfos.Add(new SBSEBowyer());
            }
        }

        public override bool SupportsBulkOrders(Mobile from) => WoodworkBulkOrders.Supports(from, BODType.Fletching);

        public override Item CreateBulkOrder(Mobile from, bool fromContextMenu) =>
            WoodworkBulkOrders.CreateOffer(from, BODType.Fletching, fromContextMenu);

        public override bool IsValidBulkOrder(Item item) => WoodworkBulkOrders.Accepts(item, BODType.Fletching);

        public override TimeSpan GetNextBulkOrder(Mobile from) => WoodworkBulkOrders.Remaining(from, BODType.Fletching);

        public override void OnSuccessfulBulkOrderReceive(Mobile from) => WoodworkBulkOrders.Clear(from, BODType.Fletching);
    }
}
