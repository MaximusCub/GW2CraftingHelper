# Minimum module width for the deepest recipe trees (GW2CraftingHelper master v0.2.0)

> **Status - acted on, twice.** Written 2026-08-23 against master
> `aa80382`, then implemented by branch `min-width-1436`, and the +2pt
> variant taken by branch `font-and-polish` (see the paragraph at the end
> of this block). The module minimum is now
> **1472 x 710** (`Services/WindowSizing.cs`) and
> `PlanRelayoutMath.TreePillColumnWidth` is **256**
> (section 6's suggestion, taken at the current fonts rather than only
> after a font bump).
>
> The window figures in the tables below are computed with the **240px**
> pill column this report measured. The shipped 256px column spends 16 of
> those pixels, so at the minimum: the depth-23 row carries the full 24px
> designed gutter past its complete name, and the depth-24 row - the one
> vendor-leaf indent level of headroom section 7 recommends - fits
> **exactly**, with no gutter. (With the report's 240px column, the minimum
> would have left that depth-24 row 16px of slack instead.) Those edges are
> pinned by `PlanRelayoutMathTests.ComputeTreeColumnEdges_DeepestRow*`,
> which read the shipped constants rather than copies of them.
>
> The minimum is **fitted to the game client**: on a client narrower than
> 1472 (an ordinary windowed 1280x720 or 1366x768) the enforced minimum
> falls back to the client's own width, floor 930, so the resize grip and
> the right-hand columns stay on-screen. Deep rows ellipsize there as they
> did before.
>
> The **+2pt variant landed** on `font-and-polish`: the maintainer took the
> bump after a field test, so row/body text is now Menomonia 16 and
> small/caption/pill text Menomonia 14 (`Views/Rendering/UiFonts`), and
> `WindowSizing.MinWindowWidth` moved 1436 -> **1472** exactly as this
> report's section 7 predicted (1448 + one `TreeIndentPer`). The pill
> column stayed at 256: section 6's f14 four-pill run measures 242px
> against the 252px budget a 256px column leaves. Section 7's "repo
> assumption looks wrong" finding was confirmed and fixed. See the
> "Minimum width raise (min-width-1436)" and "Font bump and decision-round
> polish (font-and-polish)" sections of
> [`docs/KNOWN-ISSUES.md`](../KNOWN-ISSUES.md).

## Answer

| | tree panelWidth | **window width** | free @1920 |
|---|---|---|---|
| **Current fonts** (Menomonia 14 rows / 12 pills) | 1286 px | **1412 px** (round to 1420) | 508 px |
| **+2pt row text** (Menomonia 16 rows / 14 pills) | 1322 px | **1448 px** (round to 1456) | 472 px |

Hard no-truncation floors (zero design gutter): panel 1270 / window 1396 (current), panel 1306 / window 1432 (+2pt). Today's minimum is 930 px (`Module.cs`), which ellipsizes tree rows from roughly depth 6 down.

**Defining item: `+24 Agony Infusion` (49447)** - a *forced* 23-level chain (one recipe per level: `2x +{N-1} Agony Infusion` + `1x Thermocatalytic Reagent`, Artificer). Deepest row renders as `4194304x Thermocatalytic Reagent` at depth 23. Legendaries do **not** define it - they are wide, not deep (deepest: Transcendence/Conflux at depth 14 -> 1170 px).

## 1. Data coverage (measured)

All from the module's own seeds; the official API was used only for names + prices (never gw2efficiency).

- `ref/recipes_seed.json`: **14,966 recipes**, 1,595 negative-id (1,591 Mystic Forge + 4 synthetic merchant/achievement).
- `ref/recipe_search_seed.json`: **16,022** search entries, 14,805 non-empty.
- `ref/mystic_forge_recipes.json` (1,591) is **already merged** into both seeds - all 1,591 ids appear in `recipes_seed` and are referenced by the search seed, so a from-seed traversal already covers MF shapes.
- Graph: **16,024 item nodes, 47,749 edges**. **0** referenced recipe ids missing detail.
- Only **2** ingredient ids lack a search entry (87557 Grandmaster Mark Shard, 93075 Emblem of the Avenger); both confirmed uncraftable live via `/v2/recipes/search?output=` -> `[]`. **No missing edges.**
- 1,262 graph items had no seed name -> 1,224 fetched from `/v2/items` (cached in `extra_names.json`); the other 38 return nothing from the API (module shows `Unknown Item`, narrow - no risk).

## 2. Traversal semantics mirrored (from `RecipeService.BuildNodeAsync`, `CraftingTreeBuilder`, cache stores)

1. Recipe set per item = seeded search store first; API/MF composite only for ids absent from the seed.
2. **Every** recipe of a node is expanded (`RecipeNode.Recipes` holds them all) -> depth = max over recipes.
3. Only `type=="Item"` ingredients recurse; `Currency`/`GuildUpgrade`/unrecognised are leaves.
4. Cycle guard is a **path** set (`visiting.Add`/`finally Remove`) - a repeat on the path returns a childless node that **is still rendered as a row**, so it costs one indent level (modelled as +1).
5. Reference branches capped at one per root-to-leaf path and descend `Recipes[0]` - inside the same graph, so no extra depth.
6. Multi-item wrapper never renders (`MultiItemRoots`) - roots stay at depth 0.
7. **Only depth constant in the codebase**: `PlanStructuralValidator.MaxTreeDepth = 200` (validation guard, not a render cap). Nothing truncates earlier.

Exactness: the graph has **9 non-trivial SCCs** (largest = the 12 Lunar New Year `Lucky * Lantern` items) and **98 self-loops** (e.g. `Gold Ingot`, `Elder Wood Plank` promotion/refinement). Longest simple path was therefore computed via SCC condensation with exact in-component path enumeration (`exact.py`). A naive memoised DP over-reports (claims 26 for `Monkey Statue`; exhaustive DFS and the SCC method both give 15).

## 3. Top-10 deepest craftable items (exact)

| # | depth | item | deepest row (real quantity propagation) |
|---|---|---|---|
| 1 | **23** | **+24 Agony Infusion** | `4194304x Thermocatalytic Reagent` |
| 2 | 22 | +23 Agony Infusion | `2097152x Thermocatalytic Reagent` |
| 3 | 21 | +22 Agony Infusion | `1048576x Thermocatalytic Reagent` |
| 4 | 20 | +21 Agony Infusion | `524288x Thermocatalytic Reagent` |
| 5 | 19 | +20 Agony Infusion | `262144x Thermocatalytic Reagent` |
| 6 | 18 | Jade Bot Core: Tier 10 | `429750x Pile of Glittering Dust` |
| 7 | 18 | +19 Agony Infusion | `131072x Thermocatalytic Reagent` |
| 8 | 17 | Jade Bot Core: Tier 9 | `429750x Pile of Glittering Dust` |
| 9 | 17 | +18 Agony Infusion | `65536x Thermocatalytic Reagent` |
| 10 | 16 | Jade Bot Core: Tier 8 / +17 Agony Infusion | `429750x Pile of Glittering Dust` |

Chain terminates at depth 23 because `Thermocatalytic Reagent` is vendor-only (no recipe). Lantern cycle reaches only 15; legendaries 10-14.

## 4. Width derivation (named constants only)

`TreeSectionController`: `TreeIndentPer=24`, `TreeCaretColWidth=18`, `TreeIconSize=32`+`TreeIconBorder=1`(x2), `TreeNameGap=6`, `TreePillColumnWidth=240`, `TreeCostColumnWidth=150` (floor), `TreeRightMargin=8`, `PillPadding=12`, `TightPillPadding=6`, `PillGap=6` ->
`nameX = depth*24 + 58`.

`PlanRelayoutMath`: `RightBlockX(pinned,wne)=min(pinned, max(wne+24,240))`; `pillColX = RightBlockX(panelWidth-(8+costColW)-240, wne)`; `nameMaxWidth = max(pillColX-nameX-8,20)`; `TableGutterBreathingRoom=24`.

`CoinSegmentMath`/`TreeCostColumnMath`: `segment = text+2+20`, `CoinSegmentGap=6`, `costColW = max(150, scanned)`.

**Fonts measured, not estimated**: `DefaultFont14`/`DefaultFont12` = Menomonia 14/12 regular; parsed the installed `C:\Blish.HUD\Content\fonts\menomonia\menomonia-{14,16,12}-regular.xnb` (MonoGame.Extended BitmapFontReader XNB, uncompressed) and measured strings with MG.Extended's own advance/`XOffset+Width` rule (`font.py`). Advances: 14 -> `M`13 `w`13 `0`9 ` `6; 16 -> `M`15 `w`14 `0`10.

Defining row:
```
nameX          = 23*24 + 58                          = 610
qty prefix     = measure("4194304x ")      f14       =  65
name           = measure("Thermocatalytic Reagent")  = 174
widestNameEnd  = 849            (f16: 875)
```
Cost column (live prices 2026-08-23: reagent 177c sell, +1 infusion 31c sell -> plan ~ **174,000 gold**, six-digit gold):
```
gold 54+2+20=76, silver 18+2+20=40, copper 40  ->  76+6+40+6+40 = 165   (>150 floor)   [f16: 175]
```
Formula:
```
panelWidth_min = widestNameEnd + gutter + 240 + costColW + 8
               = 849 + 24 + 240 + 165 + 8 = 1286   (designed 24px gutter)
               = 849 +  8 + 240 + 165 + 8 = 1270   (hard no-truncation)
```
Verified by replaying `ComputeTreeColumnEdges`: panel 804 (today) -> name budget 10 vs 174 needed (**truncated**); 1269 -> 173 (truncated); **1270 -> 174 (exactly fits)**; 1286 -> 190.

Chrome (panel -> window) = 46 (`windowRegion` 930 - `contentRegion` 884) + 32 (`ViewAdapter` OUTERx2) + 8 (Blish `Panel` border ~4/side) + 20 (INNERx2) + 20 (`RightEdgePadding`) = **126** -> window 1412 (designed) / 1396 (hard); +2pt: 1322+126 = **1448** / 1432.

## 5. Per-item minimum window width

| item | depth | extent f14 | window f14 | window +2pt |
|---|---|---|---|---|
| +24 Agony Infusion | 23 | 849 | **1412** | **1448** |
| +23 / +22 / +21 / +20 Agony | 22-19 | 828/802/773/746 | 1391/1365/1336/1309 | 1427/1401/1371/1344 |
| Jade Bot Core: Tier 10 | 18 | 703 | 1266 | 1298 |
| Transcendence / Conflux | 14 | 607 | 1170 | 1202 |
| Chuka and Champawat | 13 | 585 | 1148 | 1181 |
| Eternity | 11 | 584 | 1147 | 1193 |
| Twilight / Sunrise | 10 | 560 | 1123 | 1169 |
| Aurora | 11 | 535 | 1098 | 1130 |
| Mystic Tribute | 9 | 502 | 1065 | 1099 |
| Deldrimor Steel Ingot | 8 | 455 | 1018 | 1049 |

"Every legendary" would only need ~1200 px; the recommendation covers everything in the game.

## 6. Pills - window width cannot fix them

`RenderDecisionPills` sets `maxRightEdge = pillColX + TreePillColumnWidth - 4`; `pillColX` and `maxRightEdge` move together, so **the pill budget is a fixed 236 px at every window width** (matches `docs/KNOWN-ISSUES.md` L6). Measured runs:

| run | f12 (today) | tightened | f14 (+2pt) | tightened |
|---|---|---|---|---|
| CRAFT/TP/VENDOR/IGNORE | 222 | 198 | 242 | 218 |
| + `HAVE 4194304/8388608 NEEDED` | 436 | 406 | 482 | 452 |
| CURRENCY + HAVE n/m TOTAL | 246 | 234 | 277 | 265 |

So the 4-pill run fits today at normal padding, would need the tightened pass after a +2pt bump, and the `HAVE n/m NEEDED` annotation always overflows to `+N`. Fitting it all is a `TreePillColumnWidth` change (~440 f12 / ~490 f14, pushing the window to ~1612 / ~1700), not a width change. Suggested: bump `TreePillColumnWidth` to ~256 if the font bump lands.

## 7. Other findings

- **Repo assumption looks wrong**: `SettingsCurrencyGridLayoutTests.SettingsPanelWidthAtWindowMinimum = 864` ("884 content region - 20") skips the `ViewAdapter` chain (-32 outer, -8 border, -20 inner); the real panel at the 930 minimum is ~804. `docs/KNOWN-ISSUES.md` (~line 9190, "falls back to one column at the 930px minimum") agrees with 804 and contradicts 864. *(Confirmed and fixed on `min-width-1436`: the constant is now derived through the full chain, and the one-column result at the old minimum is asserted.)*
- **One extra level of headroom**: `CraftingTreeBuilder.BuildVendorCostComponentLeaves` can put synthesised leaves one level below the recipe graph. Add one `TreeIndentPer` (24 px) to cover it unconditionally -> 1436 / 1472.
- **Today**: at panel 804 the block is pinned at `pillColX = 391`, so any row exceeding 383 px of `nameX+qty+name` ellipsizes (~depth 6 for a 150 px name); the deepest rows clamp to the 10 px name floor (bare ellipsis).

## 8. Confidence

**Measured**: recipe graph/depths/chains/quantities (module traversal re-implemented, cross-checked exhaustive DFS vs SCC-DP - agree at 23 and 15); all layout constants (read from source); all text widths (parsed Menomonia XNB bitmap fonts); the truncation threshold 1270 (replayed `ComputeTreeColumnEdges`); TP prices behind the six-digit gold column (live API 2026-08-23). The +2pt case is **measured** at Menomonia 16/14, not scaled - the implied real-string factor is 1.09-1.15x vs the naive 16/14 = 1.143, so pure proportional scaling would have over-stated the window by ~10 px.

**Modelled / inferred**: (a) window chrome 126 px - 46 solid from `Module.cs`, 60 from the `ViewAdapter` chain in source, ~8 from Blish `Panel` border taken from the repo's own comment rather than a decompile (+/-2 px); one runtime log of `_contentPanel.Width - RightEdgePadding` at the 930 minimum would settle it, and it is the only measurement I could not make offline. (b) six-digit gold cost column (eight digits would add ~21 px). (c) widest rows for multi-recipe items (the legendary rows) are upper bounds because the simulation expands every alternative recipe while the rendered tree follows the chosen one - the defining item is unaffected (unique recipe per level). (d) the +2pt case assumes rows 14->16 and pills 12->14.

**Analysis scripts**: the derivation ran from a session-scoped scratch directory outside this repo (see the provenance note in [`README.md`](README.md)) - `exact.py`+`exact.json` (SCC-exact depths/levels), `depth2.py`, `scc.py`, `chain.py`, `levels.py`, `widths.py`+`widths.json`, `table.py`+`table.json`, `final_math.py`+`final_math.json`, `font.py` (Menomonia XNB parser), `fetch_names.py`+`extra_names.json`, `legendary.py`+`legendary.json`. Every input they read is in this repo (`ref/recipes_seed.json`, `ref/recipe_search_seed.json`, `ref/mystic_forge_recipes.json`), on the machine (`C:\Blish.HUD\Content\fonts\menomonia\`), or from the official API, so the numbers are reproducible without them.