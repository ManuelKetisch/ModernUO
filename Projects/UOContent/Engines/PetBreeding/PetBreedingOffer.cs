using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.PetBreeding;

internal sealed class PetBreedingOffer(
    PlayerMobile firstOwner, BaseCreature first, PlayerMobile partner, BaseCreature second, PetBreeder breeder, DateTime now
)
{
    private static readonly TimeSpan _duration = TimeSpan.FromMinutes(1);
    private readonly DateTime _expiresAt = now + _duration;
    private readonly DateTime _projectedClaim = PetBreedingSystem.ClaimReadyAt(now);
    private readonly PetBreedingPreview _preview = PetBreedingPreview.Create(first, second);
    private readonly NetState _firstConnection = firstOwner.NetState;
    private readonly NetState _partnerConnection = partner.NetState;
    private readonly bool _firstWasStabled = first.IsStabled;
    private readonly bool _secondWasStabled = second.IsStabled;
    private TimerExecutionToken _timeout;
    private bool _closed;

    internal static void CancelActiveFor(PlayerMobile owner) => owner.CloseGump<PartnerOfferGump>();

    internal static void Begin(PlayerMobile firstOwner, BaseCreature first, PlayerMobile partner, BaseCreature second,
        PetBreeder breeder)
    {
        var now = Core.Now;
        if (PetBreedingSystem.CheckPartnerPair(firstOwner, first, partner, second, breeder, now) is { } failure)
        {
            firstOwner.SendMessage(failure);
            return;
        }

        if (partner.FindGump<PartnerOfferGump>() != null)
        {
            firstOwner.SendMessage("That player already has a breeding offer open.");
            return;
        }

        CancelActiveFor(firstOwner);
        var offer = new PetBreedingOffer(firstOwner, first, partner, second, breeder, now);
        firstOwner.SendGump(new PartnerOfferGump(offer, firstOwner, partner, false));
        partner.SendGump(new PartnerOfferGump(offer, partner, firstOwner, true));
        Timer.StartTimer(_duration, offer.Expire, out offer._timeout);
    }

    private void Expire()
    {
        if (Close())
        {
            firstOwner.SendMessage("The breeding offer expired.");
            partner.SendMessage("The breeding offer expired.");
        }
    }

    private bool Close(PlayerMobile closingOwner = null)
    {
        if (_closed)
        {
            return false;
        }

        _closed = true;
        _timeout.Cancel();
        if (closingOwner != firstOwner)
        {
            CloseOwnGump(firstOwner);
        }

        if (closingOwner != partner)
        {
            CloseOwnGump(partner);
        }

        return true;
    }

    private void CloseOwnGump(PlayerMobile owner)
    {
        if (owner.FindGump<PartnerOfferGump>()?.Offer == this)
        {
            CancelActiveFor(owner);
        }
    }

    private void Cancel(PlayerMobile owner, bool isServerClosing)
    {
        if (Close(isServerClosing ? owner : null))
        {
            (owner == firstOwner ? partner : firstOwner).SendMessage("The other owner closed the breeding offer.");
        }
    }

    private void Accept(NetState sender)
    {
        if (_closed || sender != _partnerConnection || sender.Mobile != partner)
        {
            return;
        }

        var now = Core.Now;
        if (now >= _expiresAt)
        {
            Expire();
            return;
        }

        Close();
        if (firstOwner.NetState != _firstConnection || partner.NetState != _partnerConnection ||
            first.IsStabled != _firstWasStabled || second.IsStabled != _secondWasStabled ||
            _preview != PetBreedingPreview.Create(first, second))
        {
            firstOwner.SendMessage("The breeding offer changed. Choose the parents again.");
            partner.SendMessage("The breeding offer changed. Choose the parents again.");
            return;
        }

        PetBreedingSystem.TryPartnerBreed(firstOwner, first, partner, second, breeder, now);
    }

    private sealed class PartnerOfferGump(
        PetBreedingOffer offer, PlayerMobile owner, PlayerMobile other, bool canAccept
    ) : DynamicGump(50, 50)
    {
        private const int LabelHue = 0x481;

        public override bool Singleton => true;

        internal PetBreedingOffer Offer => offer;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 440, 284, 0x13BE);
            builder.AddLabelCropped(24, 16, 390, 24, LabelHue, $"Breeding offer with {other.Name}");
            var preview = offer._preview;
            var yourPet = canAccept ? preview.SecondName : preview.FirstName;
            var theirPet = canAccept ? preview.FirstName : preview.SecondName;
            var forecast = preview.Forecast;
            var yourGeneration = canAccept ? forecast.SecondGeneration : forecast.FirstGeneration;
            var theirGeneration = canAccept ? forecast.FirstGeneration : forecast.SecondGeneration;
            builder.AddLabelCropped(24, 43, 390, 24, LabelHue, $"Your pet: {yourPet}");
            builder.AddLabelCropped(24, 69, 390, 24, LabelHue, $"Their pet: {theirPet}");
            builder.AddLabel(24, 96, LabelHue, $"Projected claim: {offer._projectedClaim:yyyy-MM-dd HH:mm:ss} UTC");
            builder.AddLabel(24, 121, LabelHue, $"Offspring generation: yours {yourGeneration}, theirs {theirGeneration}");
            builder.AddLabel(24, 146, LabelHue,
                $"Maximum level {forecast.MinimumLevel}–{forecast.MaximumLevel}  •  Follower slots {forecast.FollowerSlots}");
            builder.AddLabel(24, 172, LabelHue,
                $"Hit points {preview.Hits.Min}–{preview.Hits.Max}  •  Strength {preview.Strength.Min}–{preview.Strength.Max}");
            builder.AddLabel(24, 197, LabelHue, "One ticket each. Each parent rests for six days.");
            if (canAccept)
            {
                builder.AddButton(24, 231, 0xFA5, 0xFA7, 1);
                builder.AddLabel(60, 231, LabelHue, "Accept");
            }

            builder.AddButton(205, 231, 0xFA5, 0xFA7, 2);
            builder.AddLabel(241, 231, LabelHue, canAccept ? "Decline" : "Cancel offer");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner)
            {
                return;
            }

            if (canAccept && info.ButtonID == 1)
            {
                offer.Accept(sender);
            }
            else if (info.ButtonID is 0 or 2)
            {
                offer.Cancel(owner, false);
            }
        }

        public override void OnServerClose(NetState sender) => offer.Cancel(owner, true);
    }
}
