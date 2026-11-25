# Excelsior crafting and resource integration

Reviewed November 24, 2026 against the five supplied Excelsior snapshots.
The following systems run alongside the stock resources, the Minoc survey,
and the Fire and Shame deep seams. [PDF capture and revision index](../references/uoex/snapshot/PDF-REVIEW.md#crafting-and-resources-captures)

## Bulk orders

- Blacksmiths, Armorers, Tailors and Weavers retain their established
  merchant flows. `[BODRules` selects Classic or Excelsior per character.
  Excelsior smith and tailor offers use a two-hour interval and their
  own reward tables. Switching rules preserves the active offer time.
  Each deed keeps its rules when transferred, stored in a book, or reloaded.
- Carpenters and Bowyers issue and accept small and large orders with
  separate two-hour timers. Use the crafting order book to store them and
  filter by craft, wood grade, quantity, or quality.
- Animal Trainers issue small and large Taming orders every three hours.
  Confirm surrender of the matching animal tamed within the last day.
  The system checks ownership, training, bonding, cargo, and prior owners.
  Taming orders have their own book. Their rewards include pet power
  scrolls, a leash, and Bio Engineering supplies.
- Drag a completed deed onto the correct vendor to reset that profession's
  offer timer. The claim button in a completed deed's window works near
  the correct vendor and retains the current timer.
- Excelsior reward tables provide extended-resource runics, +1/+3/+5 craft
  or gathering gloves, 600-use Ancient Carpentry and Fletching Hammers,
  and the specialty workshop items, ammunition dip tubs, and durable tools
  described in the linked reward catalogs. Crafting hammers can be equipped
  while using the matching runic tool from the backpack.

## Tools of the trades

Open the **Trade Tools** category with Tinker Tools. Craft Spell Caster's,
Wood Worker's, Metal Worker's, Tailor's, and Stone Worker's Keys,
Gem Pouch, Tool House, Runic House, and Mobile Forge. The existing
portable anvil remains available through Blacksmith orders. A supply
chest at the Minoc Public Moongate gives one set of starter caster keys
per account. The Trinsic token exchange also stocks Gem Pouches and
Ethereal Powder. The powder can be used for the Mana pet ointment recipe.

Each key or pouch holds up to 60,000 units of each allowed resource.
Tool houses keep up to 60,000 uses per exact tool type and runic grade.
Combining requires compatible tool properties. To withdraw part of a
tool's charges, add them to another existing matching tool. Original
objects with special properties can be retrieved whole. For the Mobile
Forge, keep it and the ore in the backpack, double-click the ore, and
target the forge. The regular skill checks and ore yields apply.

## Gathering and resource elementals

- Ilshenar adds **Blaze, Ice, Toxic, Electrum, and Platinum** ore.
  The required Mining values are 100, 105, 110, 115, and 119. They have
  matching ingots and granite and are selectable in the usual craft menus.
  The ordinary stock vein distribution remains available on other facets.
- Ilshenar forests contain **Pine, Mahogany, Zircote, Ebony, Bamboo,
  Purple Heart, Redwood, and Petrified**. They enter the normal
  lumberjacking and board conversion paths at their respective skill
  thresholds. Prospector's Axe improves a grove until refill. A Gargoyle
  Axe can improve it again for the current chop. Enslaved Gargoyles may
  drop the axe. A wood elemental drops 50 matching logs and a five-use
  matching runic fletcher tool.
- **Polar, Synthetic, Blaze, Daemonic, Shadow, Frost, and Ethereal** hides
  add distinct leather grades. Named wildlife and Gargoyle Knife
  harvesting supply them. A spawned leather elemental drops a five-use
  runic sewing kit and can be carved for 50 matching hides. A follow-up
  encounter has a bounded lineage.
- Gargoyle Pickaxes retain their normal encounters. A delivered primary
  ore result can yield a matching five-use runic hammer or tinker tool.
  The rarer ore elementals carry 25 matching ore. Tinker runics add magical
  properties to crafted jewelry.
- A Sturdy Axe yields Grade A logs. Use them in Carpentry's boatbuilding
  category to make a small boat deed. The ordinary boat placement rules
  apply.

The wood elemental damage figures in the PDF add to more than 100.
The live encounter uses those proportions normalized to a 100-point
damage distribution.

## Material properties

Ordinary materials keep their existing resource IDs and properties.
The twenty additional grades have their own crafting behavior. Use
`[RefineCraftGrade` to convert ordinary ingots, leather, or supported
boards into a selectable **Excelsior** grade. The confirmation displays
the exact stack and the conversion is one for one. Metal refinements
require an anvil and forge. The stat changes apply to gear made from the
selected grade.

The separate grades use the reference's elemental splits, requirements,
and exceptional durability totals. The resistance table supplies totals
for sample armor without a per-element breakdown. This implementation
assigns the additional resistance to the grade's element and adds it to
the chosen item's existing base resistances. Higher runic grades have
their own property-count and intensity ranges.

## Verification

A disposable shard is used for gameplay and world persistence checks.
See the reported test and capture results for the routes exercised.
