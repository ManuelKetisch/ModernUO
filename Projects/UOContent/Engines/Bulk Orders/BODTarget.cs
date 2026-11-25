using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.BulkOrders
{
    public class BODTarget : Target
    {
        private readonly BaseBOD m_Deed;

        public BODTarget(BaseBOD deed) : base(18, false, TargetFlags.None) => m_Deed = deed;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (m_Deed.Deleted || !m_Deed.IsChildOf(from.Backpack))
            {
                return;
            }

            if (m_Deed is TamingSmallBOD taming)
            {
                if (from is PlayerMobile player && targeted is BaseCreature pet)
                {
                    taming.ConfirmSurrender(player, pet);
                }
                else
                {
                    from.SendMessage("Choose a nearby animal you tamed.");
                }

                return;
            }

            if (!(targeted is Item item && item.IsChildOf(from.Backpack)))
            {
                from.SendLocalizedMessage(1045158); // You must have the item in your backpack to target it.
                return;
            }

            m_Deed.EndCombine(from, item);
        }
    }
}
