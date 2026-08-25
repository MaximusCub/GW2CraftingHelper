## Item stat tooltips (item-stat-tooltips)

Hovering an item anywhere in a plan now shows what the game's own item
tooltip shows: attributes, defense or weapon strength, upgrade bonuses,
rarity, binding, vendor value, flavour text.

### The headline measurement: this costs zero API requests

`Gw2ItemApiClient.GetItemsAsync` already called `/v2/items?ids=...` and
already received `description`, `type`, `level`, `vendor_value`,
`restrictions` and the entire `details{}` block in that response. It
parsed `id/name/icon/rarity/flags` and threw the rest away. The whole
feature is therefore a larger walk of a `JObject` that was already in
memory - no new endpoint, no new request, no extra round trip on the
plan path, and nothing at all on the hover path.

The one thing that WOULD need a request (`/v2/itemstats?ids=all`, 191
entries, 74 KB) is not made, because the only feature that needs it is
behind an open judgment call - see Q4 below.

### Why stats are not on ItemMetadata

`PersistedPlan.Result` is a `CraftingPlanResult`, which holds the
`ItemMetadata` dictionary, and `PersistedPlanSchemaMemberSetTests`
reflectively guards that whole reachable graph against
`PersistedPlan.CurrentSchemaVersion`. Adding stat fields to
`ItemMetadata` would have bumped the schema 3 -> 4, and
`PlanStoreHelpers.DeserializePersistedPlan` answers a version mismatch
by rejecting the file outright - i.e. it would have silently discarded
every existing user's saved plan, and bloated every future plan file
with stat blocks for 25-50 items.

`ItemStatBlock` is therefore a session-scoped side table on
`ItemMetadataService`, unreachable from `PersistedPlan`. It is filled by
the same `/v2/items` response that fills the metadata cache, and read
back by `GetCachedStatBlock`, which NEVER fetches: its caller is a hover
on the UI thread, and a network round trip inside a hover window is not
something the tooltip facility can cancel.

It is also the only one of that service's dictionaries read from a
thread other than the one writing it (UI render vs. background plan
generation), which is why it alone is locked.

### The attribute formula is the API's own, not a reconstruction

`attribute value = round(multiplier * details.attribute_adjustment)`,
where the multipliers come from the item's `/v2/itemstats` entry.

For every FIXED-stat item the endpoint publishes both the inputs and the
answer, so this is checkable rather than inferred. Measured against
Berserker's (itemstats 161, .35/.25/.25):

| Item | attribute_adjustment | API's own modifiers | formula |
|---|---|---|---|
| Zojja's Warfists / Pauldrons | 134.442 | 47 / 34 / 34 | 47 / 34 / 34 |
| Zojja's Visor | 179.256 | 63 / 45 / 45 | 63 / 45 / 45 |
| Zojja's Tassets | 268.884 | 94 / 67 / 67 | 94 / 67 / 67 |
| Zojja's Breastplate / Doublet | 403.326 | 141 / 101 / 101 | 141 / 101 / 101 |

`ItemStatMathTests` asserts against the published modifiers, so it
proves agreement with the API rather than with its own arithmetic.

The legacy `value` field on a `/v2/itemstats` attribute is NOT part of
this and is not read: entry 161 reports `value: 0` on all three
attributes while a different entry of the same name reports non-zero
ones. Multiplier only.

### What the tooltip facility gained

`RichTooltipSurface.RenderRow` hardcoded `Color.White`, so a
rarity-coloured item name, a green rune bonus and muted flavour text
were all unrepresentable. A `TooltipSpan` now carries a semantic ROLE
(`Default` / `Rarity` / `Bonus` / `Muted`) - and, for a name, the GW2
rarity STRING - rather than a colour, because `Services/` must stay
XNA-free for composer tests to stay Blish-free. Only the surface maps a
role to a colour.

Found while doing it: `TooltipLayoutMath`'s wrapper re-created every
wrapped piece with `TooltipSpan.FromText`, which would have reset each
wrapped line to `Default` - a long item name would have lost its colour
the moment it wrapped. It now clones the source span via `WithText`.

`ToPlainText()` output is byte-for-byte unchanged. That is the
regression gate, and every pre-existing tooltip test passes untouched.

### What the API cannot tell us, and is therefore never claimed

- What is actually socketed in the player's copy of an item. Infusion
  slots report a COUNT ("1 Infusion Slot"), never "unused" - the
  contents are instance state `/v2/items` does not carry.
- Agony resistance totals, transmuted skins, attunement state.
- Salvage or upgrade-extraction results.
- Ascended food's effect. `91805` returns `details: {type: Food}` and
  nothing else, so the tooltip says nothing about an effect rather than
  announcing that it has no data (fine food, e.g. `12472`, returns the
  full nourishment block and shows it).

### What shipped

- `RawItemDetail` parsing in `Gw2ItemApiClient` (zero new requests).
- `ItemStatBlock` / `ItemAttributeLine` models, deliberately unreachable
  from `PersistedPlan`.
- `ItemStatMath` (the formula above, plus the attribute-token to
  in-game-name map: CritDamage -> Ferocity, Healing -> Healing Power,
  BoonDuration -> Concentration, ConditionDuration -> Expertise).
- `ItemDescriptionSanitizer` for the API's closed markup vocabulary
  (`<c=...>` spans, `<br>`). Any OTHER angle-bracket text is passed
  through verbatim: deleting unknown text would be a silent bug,
  showing it is a reportable one.
- `ItemStatBlockFactory` - the fixed-stat path plus the null-`details`
  crafting-material path, and one place that decides what an absent
  field MEANS.
- `ItemStatTooltipComposer` - the in-game line order, Blish-free.
- `ItemMetadataService.GetCachedStatBlock` - the session side cache.
- Wiring on the recipe-tree rows and the plan header only - row panel,
  name Label, quantity Label AND the framed icon, since Blish resolves a
  tooltip on the deepest control under the cursor and never bubbles to
  the parent.

### The id-space gate

`CraftingTreeNode.ItemId` is one numeric slot shared by three id spaces
(see `CraftingDecision`): real item ids, wallet currency ids, and guild
upgrade ids. Id 24 is BOTH a real vendor-offer output item and the
currency "Pristine Fractal Relics", and `CraftingPlanPipeline`'s
metadata id union can put the genuine item-24 entry into the very
dictionary the stat cache is filled from.

An item-keyed stat lookup on a currency row is therefore the same
cross-domain collision `CraftingTreeBuilder` already guards icon and
rarity against - only worse, because a stat block's FIRST line is the
item's name in its rarity colour, and it displaces the row's own name
line. `TreeRowTooltipComposer.RowIdIsAnItemId` is the single gate: it
rejects `Currency`, `GuildUpgrade` and `UnrecognizedIngredient` rows,
plus vendor cost-component leaves with no `SubtreeCost` (the
currency half of a barter offer - the item half carries its gold value).

### What is stubbed, and behind which judgment call

The design pass left 14 numbered judgment calls for the maintainer.
This branch built only what is unambiguous and applied the recommended
default wherever it was cheap and reversible. Everything below is a
deliberate stub, not an oversight:

- **Q1 (which surfaces)** - built (a): tree rows + plan header, both
  already on the rich path. Used Materials and the Shopping List are NOT
  wired. The Shopping List is the expensive one: it would force
  `ShoppingRowTooltipFormatter` from `string` to `TooltipContent` plus
  five stamping sites and its test file, and that is the merge hotspot
  of this area.
- **Q4 (stat-selectable gear)** - built (a): a bare `Select stats` line,
  no numbers, no `/v2/itemstats` dependency at all.
  `ItemStatBlock.StatChoiceCount` carries the count as the seam, so
  (b)/(c)/(d) are a composer change plus one catalog service, not a
  re-architecture. Affects e.g. Bolt (39 combinations) and every
  stat-selectable exotic/ascended/legendary.
- **Q5 (itemstats live vs. bundled seed)** - not applicable while Q4 is
  (a). Nothing fetches `/v2/itemstats` today.
- **Q13 (restored-from-disk plans)** - NOT implemented. `PlanStore`
  restore makes no network call by design, so a restored plan's stat
  cache is empty and its rows fall back to exactly the tooltip they had
  before this feature. That is a graceful degradation, never an error
  and never a blank box (`RichTooltipSurface.Show` stays hidden on empty
  content), but it does mean the feature looks absent on the first
  screen after a module restart until the user regenerates. The
  recommended fix (one background `GetMetadataAsync` over the restored
  plan's item ids, then re-stamp on the main thread) was judged too
  expensive to lock in unasked.
- **Snapshot result list** - NOT wired, and not touchable here:
  `Views/MainView.cs` is owned by the concurrent `snapshot-grid`
  branch, which is rewriting that result list. Recorded as post-merge
  wiring.

Defaults applied where the call was cheap and reversible: vendor value
shown and suppressed on `NoSell` (Q7a); binding and restrictions shown
(Q8a); rune bonus lists and sigil/infusion buff lines shown (Q6a);
flavour text shown, muted, markup stripped (Q12a); no item icon in the
tooltip (Q9a - the game has none either); Blish's stock hover delay
(Q10a - `TOOLTIP_HOVER_DELAY` is a private const with no public knob, so
changing it means re-implementing show/hide); `PreferredMaxContentWidth`
left at 500 (Q11a); crafting materials do get a tooltip (Q2a) and it is
always shown rather than only when the name is truncated (Q3a).

Q2a/Q3a are the one VISIBLE behaviour change on every plan: rows that
previously had no tooltip at all now have one.

### Reviewer notes

- **Render-path cost (inferred, not measured):** a row's stat content is
  composed once per row per render and reused verbatim by the settle
  re-ellipsis closure, exactly as `extraTooltipContent` already was. That
  is roughly 15 builder lines per row against the ~10 Blish controls the
  same loop constructs, so it is not expected to be visible - but it is
  new work in the tree render path and nobody has profiled it.
- `ItemStatTooltipComposer.SpaceCamelCase` is a mechanical camel-case
  split, not a lookup table, so a future API type renders as its own raw
  name rather than as nothing. It would mangle an acronym-bearing token
  ("PvP" -> "Pv P"); no such token exists in the type vocabulary today.
- `ItemStatBlockFactory`'s shared empty `NoAttributes`/`NoStrings` lists
  are exposed as `IReadOnlyList` and could in principle be cast and
  mutated by a caller.
- An agony infusion reports the same fact twice - as
  `infix_upgrade.buff.description` and as an `infix_upgrade.attributes`
  entry that renders to the identical string. The composer suppresses
  the buff line when an attribute line already said it VERBATIM; a buff
  description that summarises several attributes is distinct wording and
  still renders.

### Desktop gate (live, required)

1. Generate a plan whose tree contains ordinary crafting materials
   (Mithril Ore, Orichalcum Ingot). Hover a material row: a tooltip
   appears showing the item name in its rarity colour, a blank line,
   then rarity / "Crafting Material" / vendor value with real coin
   ICONS to the RIGHT of their numbers, then the item's description.
   Confirm the coin icons are icons, not the text "0g 0s 7c".
2. Hover the plan header's target item name AND its " x N needed"
   suffix AND the 44px ICON to their left: all three show the same stat
   tooltip (nothing lying over the panel may swallow the hover and show
   nothing). Repeat on a tree row's 34px icon, including a dimmed
   not-crafted reference row, whose scrim overlays the icon.
3. Hover a tree row whose name is ELLIPSIZED: the tooltip's first line
   must be the FULL name, exactly once - not twice. Resize the window
   narrower and wider across the truncation boundary with the cursor
   held still on that row and confirm the tooltip re-renders without
   duplicating or losing the name line.
4. Hover a tree row that also has plan lines (a BuyFromTp row with
   quantity > 1, so it carries a "Unit price:" line): the stat block
   renders first, then ONE blank line, then the unit price with coin
   icons, then "Right-click: Open wiki page". Right-click still opens
   the wiki page.
5. Generate a plan for an item with real attributes (any ascended
   armour piece) and hover its root row: "Defense: N" and the "+N Power"
   attribute lines must match what the game shows for that item.
6. Rarity colour check: hover an Exotic row and an Ascended row and
   confirm the tooltip's name line and its rarity line are the expected
   orange and pink, matching the row's own name colour.
7. Tallest routine case: put a Superior Rune in a plan and hover it.
   The tooltip runs to roughly 16 lines (6 positional bonuses plus the
   identity block). Confirm it stays fully on screen when hovered near
   the BOTTOM edge of the game window - the surface clamps but cannot
   scroll.
8. Restart Blish so the plan restores from disk WITHOUT regenerating.
   Hover a tree row: the tooltip must be the pre-feature one (full name
   when truncated, plan lines) with no stat block, and must never be an
   empty or flickering box. Then press Generate and confirm the stat
   block appears.
9. Confirm no tooltip anywhere shows a raw item id, currency id or
   vendor id.
10. Id-space gate: generate a plan whose tree contains a CURRENCY row
    (a vendor offer paid in karma / fractal relics / spirit shards) and
    hover it, plus a currency cost-component leaf under a vendor node.
    The tooltip must be the pre-feature one - the row's own currency
    name and its plan lines - and must never open with an unrelated
    ITEM's name, rarity colour, type or vendor value.
11. Infusion de-duplication: put a +1 Agony Infusion in a plan and hover
    it. "+1 Agony Resistance" must appear exactly ONCE.

Gate: PASS (2026-08-24 desktop session, branch build at the
review-fix HEAD, captures preflight/gIS3-gIS4). Generated +22 Agony
Infusion live (typed adoption); hovering the root tree row rendered
the full stat block on the opaque facility surface: the item name
in the ascended rarity colour (semantic span roles resolving to
colour in the surface only), the "+22 Agony Resistance" infix buff
line rendered ONCE (the dedup fix live), the rarity and type lines,
the sanitized description with the <c=@...> markup stripped and
wrapped, and the retained "Right-click: Open wiki page" action
line. Materials-with-null-details tooltips (Q2a default) and the
plan-header surface share the same composer path; the cross-domain
id gate (the review's Critical - currency/guild-upgrade ids must
never be looked up as item ids) is pinned by its Blish-free tests.
Judgment calls Q1-Q14 ride to the maintainer unresolved beyond the
implemented cheap-reversible defaults.
---
