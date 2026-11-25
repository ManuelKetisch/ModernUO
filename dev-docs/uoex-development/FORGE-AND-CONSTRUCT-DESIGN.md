# Boss rewards, hybrid crafting, and Bio Construct modification

**First slice and expansion design · 24 November 2026**

Britannia Solo already has classic resource crafting, runic tools, Weapon
Leveling, Pet Breeding, and Bio Engineering. This design links high-level
hunting and crafting while giving metals, components, and saved equipment
distinct uses. The first slice contains one boss and two hybrid recipes.
The larger design records how later recipes, metals, and construct modules
will join the same systems.

## What other shards demonstrate

| Shard | Mechanic | Application here |
| --- | --- | --- |
| UO Evolution | The published dagger chart gives Blaze, Ice, Toxic, and Electrum distinct elemental damage profiles. Their armor and weapon bonuses differ. Platinum favors physical damage and durability. | Give a material a recognizable purpose. Offer bounded elemental conversion so elemental resistance still matters. |
| UO Outlands | Avarite extends the ore ladder. One set of material rules covers durable gear, armor, and weapon accuracy, with analogous grades for leather and wood. Resource maps give skilled gatherers a destination and predictable material. | Share material rules across professions where suitable. Use routes for scarce materials. Keep several materials relevant at the same tier. |
| Deusovis IV | The shard’s alpha notes describe refining two existing ingot types into Bronze Alloy at a forge and anvil. | Let valuable alloys consume familiar metals. Add mining and refining together so lower ore grades retain a purpose. |

Sources. [UO Evolution materials](https://uoevo.com/wiki/Resources) ·
[Outlands material bonuses](https://wiki.uooutlands.com/Modifiers) ·
[Outlands resource maps](https://wiki.uooutlands.com/Resource_Maps) ·
[Deusovis IV ore and alloy notes](https://deusovis4.com/)

## Boss rewards

1. Defeat a supported high-level foe and qualify through the existing loot
   rights rules. The Solo pilot awards one recipient based on credited
   damage, with a stable tie break. A single saved receipt records the
   signature component and one boss credit. When the backpack is full,
   keep the component available to claim. Use a defeat identifier so
   multiple callbacks or retries cannot create another receipt.
2. The boss can also drop a recipe blueprint or cosmetic trophy. The
   exchange consumes unspent credits for each guaranteed blueprint.
   The first slice starts at six credits per choice. A trophy gives the
   player a rare target after both recipes are learned.
3. Learning a blueprint unlocks the recipe on that character. A duplicate
   blueprint can be exchanged for boss components. An attempted exchange
   checks inventory room and entitlement before consuming a reward.
4. Later hunting contracts can ask for several different bosses or regions.
   Completion offers a choice of component. A given target can contribute
   once to that contract. A player can ignore the contract and continue
   earning the standard rewards.

Boss components join the existing loot path. Reuse ordinary encounter
ownership, display the recipe their component enables, and carry posted
reward progress across saves. Test consecutive deaths, shared pet credit,
capacity failure, and a clean restart. Do not award components for summons,
staff-created fixtures or released pets.

## Hybrid weapons and armor

A recipe declares a **base item**, a **donor**, and a **binding component**.
The base determines the output's slot, handling, core requirements, repair
route, and Weapon Leveling policy. The donor contributes exactly one
compatible move or a bounded redistribution of an existing stat. The
blueprint defines the allowed inputs and the resulting item type. Future
recipes use the same recipe definitions and compatibility checks.

- Open the forge, choose the three inputs, and show the exact output before
  confirmation. Include the ability gained and replaced, resistances,
  durability, skill, weight, consumed items, and the outcome for XP, points,
  and any existing tags.
- Apply the result from the same calculation that produced the preview.
  Recheck input ownership, backpack location, eligibility, properties and
  capacity at commit. A changed input refreshes the preview. A failed craft
  leaves every input available.
- A compatible weapon keeps the **base weapon's** leveling state and
  permitted upgrades. The donor's XP and spending points stay out of the
  result. Preserve crafted/loot classification and artifact pricing unless
  a displayed recipe explicitly changes them. The first recipes require
  ordinary, unbound base and donor items. The weapon copy uses the path
  that preserves artifact state. The type-change waiver belongs to its
  own deed. The preview leaves existing overcap values intact and rejects
  a recipe when it cannot honor that policy.
- Preserve existing slot, skill, and item-boundary checks. A new ability
  combination needs its own allowed pair; it does not emerge from arbitrary
  runtime type combinations. Make the result inspectable in a standard
  tooltip and repairable, tradable, and serializable through normal paths.
- Limit each recipe's inherited properties and numerical budget. Material,
  runic bonuses, Weapon Leveling, and the hybrid modifier share the
  effective caps. Prevent repeated conversion from generating extra points
  or retaining a removed ability.
- Later recipes can cover light weapons, ranged frames, shields, and
  layered armor. Record a rule from each new family before expanding the
  catalog. Add original visuals as separate assets when needed.

### Scale-lined plate artwork

The scale-lined chest has original inventory and ground sprites in two
orientations and fitted paperdoll art for both human wearer shapes. The
profile keeps stock plate animation for walking and combat. Its partial
hue preserves red scales and brass details when a material colors the steel.

Build a matched profile with
[build_plate_art_profile.py](build_plate_art_profile.py) using the installed
Classic UO data and a new destination. The builder checks that the reserved
art IDs are free and puts the PNGs, tile metadata, and equipment aliases
in that destination. Point CrossUO's `CustomPath` there with streaming
disabled. Include the same profile before the original game data in
ModernUO's data directories while continuing to use the converted land maps.

The forge preserves source armor orientation and stats. The server reads
the custom graphic IDs only when both matching tile records are present.
Saved scale-lined chests select the corresponding stock art when loaded
without the profile, then return to custom art if the matching data is
restored. To roll back to older server code, save once with the current
build and without the art profile before installing the older build.

## Metallurgy

### Prospecting and refining pilot

Buy a Minoc prospecting map from a miner or blacksmith for 200 gold. Bring a pickaxe or
shovel to the marked rock face at Minoc Mine in Trammel. At 65 base Mining,
target the rock with the map to collect one meteorite fragment. The claim
uses the map and one tool charge.

At an anvil and forge, use `[Metallurgy` with 80 base Blacksmithy. Refine one
fragment, eight Iron ingots, and six Bronze ingots into one Meteoric Bronze
ingot. Shape that ingot into a 240-use pickaxe that weighs five stones and
uses the stock mining rules.

### Deep ore seams and specialty equipment

Two fixed deposits use ordinary pickaxe or shovel targeting. Each has four
portions and refills 25 minutes after depletion. The seam preserves its
remaining portions and refill time through saves. The ambient ore ladder
continues from Iron to Valorite.

| Material | Source | First equipment use |
| --- | --- | --- |
| Volcanic glass | Fire dungeon in Trammel, near 5695, 1390. Requires 75 Mining | A brittle dagger that deals 25% fire damage in place of physical damage |
| Meteoric iron | Shame dungeon in Trammel, near 5396, 126. Requires 90 Mining | A heavier, more durable heater shield with a physical defense focus |
| Conductive silver | Refine a plain silver ring, meteoric ore, Copper and Dull Copper ingots | Silver conduit sleeves with 5% lower mana cost and full meditation. They give up some durability and physical defense |

Use `[Metallurgy` within two tiles of an anvil and forge. The volcanic
dagger requires 85 Blacksmithy. The shield, silver refining, and sleeves
require 95. The gump lists the exact ingredients and consumes them only
when the result fits in the backpack. The specialty pieces use ordinary
equipment and repair, with dedicated recipes.

Further deposits and materials should have an accessible source, a
recipe family, and a tradeoff that keeps older grades useful. Prospecting,
elemental identities, and refining can all expand independently through
the same loop.

## Bio Construct modification

### Assembly and equipment

The four samples already determine assembly. Prowess provides physical
stats and combat skills. Environment supplies dexterity, stamina, fire and
cold resistance, and poison skill. Mental provides intellect, mana, the
remaining nonphysical resistances, and casting skills. Mimic supplies
body, appearance, sounds, and one special. Organic matter and iron ingots
are currently recipe costs. Their type or amount does not alter assembly.

Sources. [Bio projection](../../Projects/UOContent/Engines/BioEngineering/BioPreview.cs) ·
[Bio recipes](../../Projects/UOContent/Engines/BioEngineering/BioEngineeringTool.cs) ·
[Human equipment and lifecycle](../../Projects/UOContent/Engines/BioEngineering/BioConstruct.Human.cs)

Human-form constructs can wear compatible jewelry through their owner-managed
gear path. An activated weapon they wield earns standard player Weapon
Leveling XP when it directly kills an eligible creature. The owner must be
connected and within 18 tiles on the same map. The same kill cannot also
credit the owner's weapon. Other eligible pet kills continue to use the
existing reduced owner-weapon award. Retrieve the weapon to spend its points.
The samples still determine the construct's form, stats, skills and donor move.

### Frame and module refits

Use `[BioRefit` within two steps of an anvil and forge. The owner must have
studied the Bio Engineering handbook and have 100 base Animal Taming and
Tinkering. Bring the living, assembled construct and leave combat before
refitting. The menu previews the cost and effect of each change.

| Slot | Choice | Materials | Effect |
| --- | --- | --- | --- |
| Frame | Iron | Free | Default frame |
| Frame | Bronze | 20 bronze ingots | Recover up to 8 health when a wild foe lands an eligible melee hit, once per 12 seconds |
| Frame | Agapite | 20 agapite ingots | Recover up to 4 mana when landing or taking an eligible melee hit, once per 12 seconds |
| Frame | Shadow Iron | 20 shadow iron ingots | Drain up to 8 stamina from an eligible wild foe on a landed melee hit, once per 12 seconds |
| Combat | Focused Circuit | 15 shadow iron ingots | Change the existing donor move from 12 stamina and a 12-second cooldown to 9 stamina and a 14-second cooldown |
| Utility | Stamina Regulator | 15 bronze ingots | Recover up to 5 stamina when a wild foe lands an eligible melee hit, once per 18 seconds |

Each construct has one frame, one combat slot, and one utility slot. Swapping
or removing parts does not yield ingots. Refits preserve equipped items,
ownership, bonding, appearance, donor stats and skills, and current resource
pools. Existing constructs begin with all optional slots empty and an Iron
frame. Installed parts and cooldowns carry through saves and bonded ghosts.

Use `[BioSheet` and target your construct, say its name followed by `sheet`,
or open the sheet from its skills window. The sheet shows the four donors,
worn equipment, the wielded weapon's progression, and the effects of the
frame and installed modules. Older constructs show unrecorded donors.

Future refits can offer alternate modules that trade one donor move effect
for another within a fixed budget. Repeatable
Tinker contracts could send players after ore elementals for a choice of refit
materials. Treat a body conversion as a separate service with equipment and
mount checks.

## First slice

| Decision | First playable value |
| --- | --- |
| Boss | The existing **Ancient Wyrm** spawns in Felucca and Trammel Destard |
| Boss reward | One Ancient Wyrm scale and one credit per qualifying defeat. An additional roll can award either blueprint (4% each) or a trophy (2%). The guaranteed blueprint exchange costs six unspent credits. |
| Weapon hybrid | **Polehammer** from an ordinary halberd, war hammer, and scale. Keep Whirlwind Attack and replace Concussion Blow with Crushing Blow. Preserve the eligible base weapon's XP. Use stock hammer art so CrossUO exposes both moves. |
| Armor hybrid | **Scale-lined plate chest** from ordinary plate chest armor, ten red dragon scales, and the signature scale. Move three points of physical resistance to fire. An ordinary chest starts at 5 physical and 3 fire, so the output starts at 2 and 6 before other bonuses. The total resistance budget stays the same. The base needs at least three physical resistance, and normal wearable resistance limits still apply. |
| Entry | `[WyrmRewards` shows credits, pending rewards, and the blueprint exchange. Double-click a blueprint, then use `[HybridForge` at an anvil and forge with 100 base Blacksmithy. |

The first slice exercises real loot, both recipes, and existing equipment
rather than creating a wider ore catalog. Use a disposable shard to confirm
repeat rewards, guaranteed redemption, stale forge quotes, full-pack safety,
save/reload, equipping and actual combat. Fix the rates after normal play.

After playtesting the first forge and refit options, expand compatible recipe
families and add multi-boss contracts. Reassess how many fights, components,
and recipe choices are fun before expanding progression tiers. Later changes
to reward costs, inherited budgets, or module caps update this document and
the corresponding behavioral tests together.
