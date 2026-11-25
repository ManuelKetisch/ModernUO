using System;
using Server.Gumps;

namespace Server.Engines.BioEngineering;

internal readonly record struct BioProjection(
    Body Body, BioTrait Trait, int Strength, int Dexterity, int Intelligence,
    int Hits, int Stamina, int Mana, int MinimumDamage, int MaximumDamage, int Armor,
    int PhysicalResistance, int FireResistance, int ColdResistance, int EnergyResistance, int PoisonResistance
)
{
    internal void AppendSummary(ref DynamicGumpBuilder builder, int x, int y)
    {
        var form = Body.IsHuman ? "Human" : "Non-human";
        builder.AddLabel(x, y, BioPreview.LabelHue, $"{form} form · {BioConstruct.TraitName(Trait)}");
        builder.AddLabel(x, y + 20, BioPreview.LabelHue, $"Strength {Strength}   Dexterity {Dexterity}   Intelligence {Intelligence}");
        builder.AddLabel(x, y + 40, BioPreview.LabelHue, $"Hit points {Hits}   Stamina {Stamina}   Mana {Mana}");
        builder.AddLabel(x, y + 60, BioPreview.LabelHue, $"Damage {MinimumDamage}–{MaximumDamage}   Armor {Armor}");
        builder.AddLabel(x, y + 80, BioPreview.LabelHue, $"Resistances: Physical {PhysicalResistance}   Fire {FireResistance}   Cold {ColdResistance}");
        builder.AddLabel(x, y + 100, BioPreview.LabelHue, $"Energy {EnergyResistance}   Poison {PoisonResistance}");
    }
}

internal static class BioPreview
{
    internal const int LabelHue = 0x480;

    internal static bool TryProject(
        int[] prowess, int[] environment, int[] mental, int[] mimic, out BioProjection projection
    )
    {
        projection = default;
        if (prowess is not [var str, var hits, var minimum, var maximum, var physical, var armor, ..] ||
            prowess.Length is not (9 or 15) ||
            environment is not [var dex, var stamina, var fire, var cold, _] ||
            mental is not [var intel, var mana, var energy, var poison, _, _, _, _] ||
            mimic is not [var bodyId, _, _, var trait, _, _, _, _, _])
        {
            return false;
        }

        var body = new Body(bodyId);
        var typedTrait = (BioTrait)trait;
        if (!(body.IsHuman || body.IsAnimal || body.IsMonster) || !Enum.IsDefined(typedTrait))
        {
            return false;
        }

        var resistanceCap = body.IsHuman ? 70 : 75;
        var (damageMin, damageMax) = CapDamage(minimum, maximum);
        projection = new BioProjection(
            body, typedTrait, CapStrength(str), CapDexterity(dex), CapIntelligence(intel),
            CapHits(hits), CapStamina(stamina), CapMana(mana), damageMin, damageMax, CapArmor(armor),
            Math.Clamp(physical, 0, resistanceCap), Math.Clamp(fire, 0, resistanceCap),
            Math.Clamp(cold, 0, resistanceCap), Math.Clamp(energy, 0, resistanceCap), Math.Clamp(poison, 0, resistanceCap)
        );
        return true;
    }

    private static int CapStrength(int value) => Math.Clamp(value, 1, 1200);
    private static int CapDexterity(int value) => Math.Clamp(value, 1, 300);
    private static int CapIntelligence(int value) => Math.Clamp(value, 1, 1200);
    private static int CapHits(int value) => Math.Clamp(value, 1, 3000);
    private static int CapStamina(int value) => Math.Clamp(value, 1, 1000);
    private static int CapMana(int value) => Math.Clamp(value, 1, 2000);
    private static int CapArmor(int value) => Math.Clamp(value, 0, 75);

    internal static int CapSkill(int fixedPoint) => Math.Clamp(fixedPoint, 0, 1000);

    private static (int Minimum, int Maximum) CapDamage(int minimum, int maximum)
    {
        var damageMin = Math.Clamp(minimum, 1, 18);
        return (damageMin, Math.Clamp(maximum, damageMin, 26));
    }

    private static string Resistance(int value)
    {
        var other = Math.Clamp(value, 0, 75);
        return other > 70 ? $"70 human / {other} non-human" : $"{other}";
    }

    internal static void AppendSample(
        ref DynamicGumpBuilder builder, BioSampleKind kind, int[] sample, int x, int y
    )
    {
        switch (kind)
        {
            case BioSampleKind.Prowess when sample is [var str, var hits, var min, var max, var physical, var armor,
                var wrestling, var tactics, var anatomy, ..]:
                var damage = CapDamage(min, max);
                builder.AddLabel(x, y, LabelHue, $"Strength {CapStrength(str)}   Hit points {CapHits(hits)}");
                builder.AddLabel(x, y + 21, LabelHue, $"Damage {damage.Minimum}–{damage.Maximum}   Armor {CapArmor(armor)}");
                builder.AddLabel(x, y + 42, LabelHue, $"Physical resistance {Resistance(physical)}");
                builder.AddLabel(x, y + 63, LabelHue, $"Wrestling {CapSkill(wrestling) / 10.0:F1}   Tactics {CapSkill(tactics) / 10.0:F1}   Anatomy {CapSkill(anatomy) / 10.0:F1}");
                break;
            case BioSampleKind.Environment when sample is [var dex, var stamina, var fire, var cold, var skill]:
                builder.AddLabel(x, y, LabelHue, $"Dexterity {CapDexterity(dex)}   Stamina {CapStamina(stamina)}");
                builder.AddLabel(x, y + 21, LabelHue, $"Fire resistance {Resistance(fire)}");
                builder.AddLabel(x, y + 42, LabelHue, $"Cold resistance {Resistance(cold)}");
                builder.AddLabel(x, y + 63, LabelHue, $"Poisoning {CapSkill(skill) / 10.0:F1}");
                break;
            case BioSampleKind.Mental when sample is [var intel, var mana, var energy, var poison, var magic,
                var evaluating, var meditation, var resisting]:
                builder.AddLabel(x, y, LabelHue, $"Intelligence {CapIntelligence(intel)}   Mana {CapMana(mana)}");
                builder.AddLabel(x, y + 21, LabelHue, $"Energy resistance {Resistance(energy)}   Poison resistance {Resistance(poison)}");
                builder.AddLabel(x, y + 42, LabelHue, $"Magery {CapSkill(magic) / 10.0:F1}   Evaluating Intelligence {CapSkill(evaluating) / 10.0:F1}");
                builder.AddLabel(x, y + 63, LabelHue, $"Meditation {CapSkill(meditation) / 10.0:F1}   Resisting Spells {CapSkill(resisting) / 10.0:F1}");
                break;
            case BioSampleKind.Mimic when sample is [var bodyId, _, var female, var trait, ..]:
                var form = new Body(bodyId).IsHuman ? "Human" : "Non-human";
                builder.AddLabel(x, y, LabelHue, $"{form} form ({(female == 0 ? "male" : "female")})");
                builder.AddLabel(x, y + 21, LabelHue, $"Special move: {BioConstruct.TraitName((BioTrait)trait)}");
                builder.AddLabel(x, y + 42, LabelHue, "This donor also supplies appearance and sounds.");
                break;
        }
    }
}
