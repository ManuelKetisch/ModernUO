using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;

namespace Server.Engines.PetBreeding;

internal readonly record struct PetPedigree(
    string DamName, int DamLevel, int DamGeneration, string SireName, int SireLevel, int SireGeneration
);

public sealed class PetBreedingRecords : GenericPersistence
{
    private static readonly Dictionary<BaseCreature, PetPedigree> _pedigrees = new();

    internal PetBreedingRecords() : base("PetBreedingPedigrees", 11) { }

    public static void Configure() => _ = new PetBreedingRecords();

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnPetDeleted(BaseCreature pet) => _pedigrees.Remove(pet);

    internal static PetPedigree? GetPedigree(BaseCreature pet) =>
        pet != null && _pedigrees.TryGetValue(pet, out var pedigree) ? pedigree : null;

    internal static void RecordPedigree(BaseCreature pet, PetPedigree pedigree) => _pedigrees[pet] = pedigree;

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_pedigrees.Count);
        foreach (var (pet, pedigree) in _pedigrees)
        {
            writer.Write(pet);
            writer.Write(pedigree.DamName);
            writer.WriteEncodedInt(pedigree.DamLevel);
            writer.WriteEncodedInt(pedigree.DamGeneration);
            writer.Write(pedigree.SireName);
            writer.WriteEncodedInt(pedigree.SireLevel);
            writer.WriteEncodedInt(pedigree.SireGeneration);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt(); // version
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var pet = reader.ReadEntity<BaseCreature>();
            var pedigree = new PetPedigree(reader.ReadString(), reader.ReadEncodedInt(), reader.ReadEncodedInt(),
                reader.ReadString(), reader.ReadEncodedInt(), reader.ReadEncodedInt());
            if (pet != null)
            {
                RecordPedigree(pet, pedigree);
            }
        }
    }
}
