using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class HealerGuildmaster : BaseGuildmaster
    {
        private static readonly TimeSpan ResurrectDelay = TimeSpan.FromSeconds(2.0);
        private DateTime _nextResurrect;

        [Constructible]
        public HealerGuildmaster() : base("healer")
        {
            SetSkill(SkillName.Anatomy, 85.0, 100.0);
            SetSkill(SkillName.Healing, 90.0, 100.0);
            SetSkill(SkillName.Forensics, 75.0, 98.0);
            SetSkill(SkillName.MagicResist, 75.0, 98.0);
            SetSkill(SkillName.SpiritSpeak, 65.0, 88.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.HealersGuild;

        public override VendorShoeType ShoeType => VendorShoeType.Sandals;

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Robe(Utility.RandomYellowHue()));
        }

        public virtual bool CheckResurrect(Mobile m)
        {
            if (m.Criminal)
            {
                Say(501222); // Thou art a criminal.  I shall not resurrect thee.
                return false;
            }

            if (m.Murderer)
            {
                Say(501223); // Thou'rt not a decent and good person. I shall not resurrect thee.
                return false;
            }

            if (m.Karma < 0)
            {
                Say(501224); // Thou hast strayed from the path of virtue, but thou still deservest a second chance.
            }

            return true;
        }

        public virtual void OfferResurrection(Mobile m)
        {
            Direction = GetDirectionTo(m);

            m.PlaySound(0x1F2);
            m.FixedEffect(0x376A, 10, 16);
            m.SendGump(new ResurrectGump(m, ResurrectMessage.Healer), true);
        }

        public virtual bool TryOfferResurrection(Mobile m)
        {
            if (m == null || m.Deleted || m.Alive || m.Frozen)
            {
                return false;
            }

            if (!InRange(m, 4))
            {
                m.SendLocalizedMessage(500446); // That is too far away.
                return false;
            }

            if (!InLOS(m))
            {
                m.SendLocalizedMessage(500237); // Target can not be seen.
                return false;
            }

            if (m.Map?.CanFit(m.Location, 16, false, false) != true)
            {
                m.SendLocalizedMessage(502391); // Thou can not be resurrected there!
                return false;
            }

            if (!CheckResurrect(m))
            {
                return false;
            }

            _nextResurrect = Core.Now + ResurrectDelay;
            OfferResurrection(m);
            return true;
        }

        public override void OnDoubleClickDead(Mobile from)
        {
            TryOfferResurrection(from);
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);

            if (!from.Alive)
            {
                list.Add(new ResurrectEntry());
            }
        }

        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            base.OnMovement(m, oldLocation);

            if (m.Frozen || m.Alive || Core.Now < _nextResurrect || !InRange(m, 4) || !InLOS(m))
            {
                return;
            }

            if (m.NetState?.GetGumps().Has<ResurrectGump>() == true)
            {
                return;
            }

            var enteredRange = !InRange(oldLocation, 4);
            var enteredSight = !InLOS(new Point3D(oldLocation.X, oldLocation.Y, oldLocation.Z + 14));
            var leftBlockedTile = m.Map?.CanFit(oldLocation, 16, false, false) != true;

            if (!enteredRange && !enteredSight && !leftBlockedTile)
            {
                return;
            }

            if (m.Map?.CanFit(m.Location, 16, false, false) != true)
            {
                m.SendLocalizedMessage(502391); // Thou can not be resurrected there!
            }
            else if (CheckResurrect(m))
            {
                _nextResurrect = Core.Now + ResurrectDelay;
                OfferResurrection(m);
            }
        }

        private sealed class ResurrectEntry : ContextMenuEntry
        {
            public ResurrectEntry() : base(6195, 4)
            {
            }

            public override void OnClick(Mobile from, IEntity target)
            {
                if (target is HealerGuildmaster healer)
                {
                    healer.TryOfferResurrection(from);
                }
            }
        }
    }
}
