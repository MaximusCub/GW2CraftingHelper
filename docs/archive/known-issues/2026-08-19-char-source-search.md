## Per-character source checkboxes + character-name search (char-source-search)

Maintainer directive, verbatim: *"i want per character source checkboxes
and search matching character labels"* - which resolves d1-snapshot-about-
settings.md Feature 1's Open Questions 1 and 2 (both recorded as RESOLVED
in that proposal, against their original opposite choices).

- **Per-character checkboxes (commit 1):** the Snapshot tab's single
  "Characters" checkbox is replaced by one checkbox per character in the
  current snapshot, labeled with the character's name. Bank / Material
  Storage / Shared Inventory are unchanged. The filter decision moved
  fully into the Blish-free service layer: SnapshotSourceFilter dropped
  its Characters bool for an UncheckedCharacters exclusion set keyed by
  bare character name, and SnapshotSearchResultBuilder.IsSourceEnabled
  resolves a "Character:<name>" source against it. Exclusion (rather than
  inclusion) is what makes a character new in a fresh snapshot default to
  checked without the filter knowing the roster; the view holds the same
  kind of set as its session-sticky state, so unchecked characters survive
  a tab bounce exactly like the search text and the content-type dropdown.
  The sticky set is copied into the filter per rebuild rather than shared
  by reference - SnapshotSourceFilter is a mutable public carrier, and a
  later normalizing or pruning pass on the service side would otherwise
  reach straight into the user's UI state and re-check their boxes.
  Stale names (a deleted character) are deliberately not pruned - they
  match nothing, and pruning would forget the user's choice whenever a
  degraded snapshot happened to omit a character.
- **Layout mechanism:** the row is account-sized (1 to 15+ characters), so
  it can no longer use fixed X positions. MainView measures each label
  with DefaultFont14 (plus a 40px box-and-gap constant chosen to land
  close to the four widths the row previously hardcoded - an
  approximation, not a reproduction: no single constant can make two
  different 16-character labels both measure 170) and hands the widths to a new
  Blish-free SourceFilterFlowLayout, which wraps them left-to-right and
  returns per-cell offsets plus the height the row needs. CoinRowY /
  ContentY / TopRegionHeight became computed properties over that measured
  height, so the coin and content rows shift down by however many rows the
  filter wrapped onto - on build, on every snapshot, and on every resize
  (a narrower window re-wraps). Single-row height is floored at the exact
  30px the row had before, so the common case is unchanged vertically -
  the exact part of the reproduction; the cells' own X positions are the
  measured approximation above.
- **The row is bounded, and scrolls past the bound:** an account-sized row
  cannot be allowed to grow without limit - a large roster in a short
  window would otherwise push the result list to zero height, with no way
  for the user to shrink the row back (it cannot be collapsed, and the only
  recourse would be enlarging a window that may already be at the display's
  limit). MainView caps the row at whichever is smaller: four flowed rows,
  or whatever leaves the result list 120px. Past the cap the panel gets
  CanScroll and the cells are re-flowed clear of the scrollbar strip, so
  every checkbox stays reachable rather than being clipped away. The
  content panel's own clamp-at-zero stays as a floor for the case of a
  window shorter than the fixed rows above the filter row.
- **New roster seam:** SnapshotSearchResultBuilder.CollectCharacterNames
  merges the "Character:<name>" item sources with CharacterDisciplines, so
  a character holding no items still gets a checkbox, and keeps
  zero-count entries (which AccountItemIndex drops) for the same reason.
- **Checkbox row construction moved to the main thread:** the checkboxes
  are now created only by RebuildSourceFilterRow, called from Build's
  marshaled tail and from SetSnapshot, instead of inline in Build's
  ThreadPool-thread body. Two reasons: a roster change has to rebuild the
  row, not just the result list, and one creation path cannot drift from
  the other. SetSnapshot rebuilds the row **only when the roster actually
  changed** (ordinal element-wise compare against the previous names, which
  CollectCharacterNames sorts, so it is stable): SetSnapshot is driven by
  the periodic background refresh, and an unconditional rebuild disposes
  the very checkbox a click may be mid-press on, silently losing the click,
  besides reallocating the whole row for a byte-identical roster. An
  unchanged roster still re-runs the layout pass. An "All Characters"
  master toggle (present only when there
  is more than one character) cascades check/uncheck-all behind a
  re-entrancy guard, so one user click stays one content rebuild.
  **Known two-state quirk:** after unchecking the master, a character
  first appearing in a later snapshot renders checked while the master
  still reads unchecked - a deliberate consequence of the exclusion model
  (only named characters are excluded, so an unknown name defaults to
  visible); a tri-state master visual was considered and deferred.
  RebuildContent now reads the sticky fields rather than the controls,
  which is also what makes it safe while the row does not yet exist.
- **Character-name search (commit 2):** typing a character's name
  surfaces every item that character holds. Implemented in
  BuildItemRows, not the view: an item is kept when its own name matches
  OR a character holding it matches (case-insensitive substring either
  way). The character check only consults sources that already survive
  the source filter, so **an unchecked character's rows stay hidden even
  when its own name is typed** - deliberate AND-composition, chosen over
  letting a typed name re-enable a box the user unchecked. Scope limits,
  also deliberate: the scan starts past the "Character:" encoding token
  (so that internal token is not itself searchable), storage-location
  labels do not match, and the wallet list is untouched - currencies have
  no per-character holding, so a character search never lists them. A
  character-matched row still reports the account-wide total and full
  breakdown across the checked sources, not just the matched character's
  share, so a total means the same thing on every row in the list.
  Character labels later gained a 2-character minimum query length
  (char-search-min2 below); item and currency names still match from the
  first letter.
- **Perf note (keystroke path):** character matching costs a full source
  walk for every item whose name does not match, where the old name-only
  search skipped straight past it. That is bounded above by the
  empty-search rebuild, which already walked every source of every item,
  so the worst case is unchanged. Per-resize cost gains one small list
  plus one placement object per checkbox; the row is otherwise rebuilt
  once per snapshot, not per keystroke.

Validation per commit: module build 0 errors throughout; suite 1854
baseline -> 1876 (commit 1: per-character filter semantics, roster
collection, flow-layout wrapping) -> 1884 (commit 2: character-label
matching, AND-composition, wallet-unaffected). Commit 3 is docs plus one
redundant loop-variable copy dropped from the row builder; suite 1884.
Commit 4 applies the review's three Must Fix findings (row height bound +
scroll, roster-change guard on the rebuild, defensive copy of the sticky
set); all three land in MainView, which is Blish-coupled and therefore
outside the test suite's reach - build 0 errors, suite still 1884.

Desktop gate should look at: (1) the per-character checkbox row rendering
with a multi-character snapshot - labels not clipped, no overflow past
the window's right edge, wrapping onto a second row when needed, and the
coin row and result list shifting down by exactly that much (then drag
the window narrower and confirm it re-wraps and re-anchors); (2)
stickiness across a tab bounce - uncheck a character (and the All
Characters master), switch to another tab and back, confirm the boxes
come back as left, and that a Refresh Now does not silently re-check
them; (3) character-name search - type a character's name and confirm
its items appear while currencies do not, then uncheck that same
character and confirm the list empties rather than the search overriding
the box; (4) the bounded row - shrink the window until the filter row hits
its cap and confirm the result list keeps its minimum height, the row
gains a working scrollbar, and the checkboxes do not sit under it.
Gate: PASS on items (1)-(3), (4) not exercisable live (2026-08-19,
Paint-dummy desktop session, branch build b59df59, captures
preflight/cs1-cs5). The canned preflight snapshot was enriched with
three character-sourced items (Mystic Coin 25 + Mystic Clover 5 on
"Maximus Test", Orichalcum Ore 50 on "Alt Number Two"; original
backed up as snapshot.json.pre-charsrc-bak) so both rosters were
live. (1) The row rendered Bank / Material Storage / Shared
Inventory / All Characters / Alt Number Two / Maximus Test on one
measured row, no clipping; the merged Mystic Clover row showed
"x35 - Material Storage 30, Character: Maximus Test 5". (2)
Unchecking Alt Number Two hid Orichalcum Ore and dropped the All
Characters master; a Log-tab bounce restored the boxes exactly as
left with the row still filtered. (3) Typing lowercase "maximus"
surfaced exactly the two Maximus Test items (account-wide
breakdowns intact) and no wallet rows; unchecking Maximus Test with
the search still active emptied the list to the message 'No items
match "maximus" in the selected sources' - AND-composition
confirmed live. (4) The 4-row cap cannot be reached with a
2-character roster and synthetic resize-grip drags are documented
unreliable; the cap/scroll math was review-verified and the
CanScroll-after-construction caveat stands as the one untested
behavior - re-check visually if a large-roster account ever shows
a scrolling filter row.
