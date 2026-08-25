## Desktop gate batch: value-detail closure + partial currency coverage (2026-08-17, orchestrator live session)

Sandbox at master 51bdf88 (m38-final copy, preflight settings, Paint
dummy, PID-scoped teardown).

**Value-detail hover: CLOSED, both directions verified live.**
- Deldrimor Steel Ingot plan: the chosen path TP-buys both ingot
  children, so the shard-bearing branch (Philosopher's Stone under the
  dust promotion) is subdued/unchosen and the root's decision carries
  no currency. Root CRAFT hover shows base tooltip only - CORRECT
  suppression, matching the pipeline-level correct-by-design verdict.
  The unchosen vendor leaf itself renders the full value-detail block
  when hovered (gold 0, Currencies 36s = 1 Spirit Shard at the curated
  3600c, Optimization 36s), proving the builder fires live.
- Mystic Clover x77 plan (chosen path rich in currencies): root CRAFT
  hover renders the full value-detail block live - "Crafting gold
  price: 41g 26s 80c / Currencies: 143g 64s 0c / Optimization price:
  184g 90s 80c" - arithmetic exact. POSITIVE LIVE RENDER. The
  2026-08-16 "live miss" is thereby explained: that plan's chosen
  subtree was gold-only, and the hover correctly showed only the base
  tooltip. No code defect; no log-line instrumentation needed.

**Partial currency coverage verified live** (previously only the
full-coverage collapsed HAVE form was seen): the currency table
rendered Spirit Shard Required 244 / Have 50 / Needed 194 with
Blue Prophet Shard and Fractal Relic rows at 0 holdings. The tree-leaf
HAVE pill partial variant had no reachable surface in either tested
tree (currencies render in price columns and the table; no currency
leaf rows exist in these shapes) - same reachability status as before,
not a failure.

**Incidental re-verifications:** VOM consumed the 30 owned Mystic
Clovers (Used Materials section; qty 77 -> root 47x); merged-ceil
vendor math live (912 Philosopher's Stones priced 92 shards = ceil of
91.2); vendor unit-price tooltip ("Unit price: 1275 for 152" with
currency icon) and the "Right-click: Open wiki page" affordance line;
W3B phase text + spinner ("Fetching item details (27 items)");
dated plan timestamps and dated Log-tab rows (a store WARN for the
stale sandbox plan.json rendered with full date, correctly routed);
coin and currency icons right of numbers throughout; shopping list
four-row format with per-currency Each/Total columns.

**Still unreachable:** GuildUpgrade pill/label visuals (no
guild-decoration output is plannable from the search list - unchanged
since the 2026-08-16 partial pass; rides the next natural
opportunity).
