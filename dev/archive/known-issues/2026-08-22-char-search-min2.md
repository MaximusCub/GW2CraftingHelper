> **Frozen record - 2026-08-22, branch `char-search-min2`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Character-name search minimum query length (char-search-min2)

Decision closing the "Character-search minimum query length"
item the nth-cleanup batch left open by choice. The reason is the result
list, not the per-keystroke cost: with one-letter matching, typing "ar"
on the way to an item name first passes through "a", which surfaces
everything held by every character whose name contains an "a" - so the
opening keystrokes of an item search widen the list instead of narrowing
it.

- **Rule:** a character label matches only from 2 characters on. Item
  names and wallet currency names are unchanged - a single letter still
  matches them, so the common search is untouched.
- **Where:** SnapshotSearchResultBuilder.CharacterNameMatches, behind the
  named MinCharacterSearchLength constant, so the one seam that decides
  "does this source's character name match" carries the floor and the
  BuildItemRows call site keeps its existing shape. The length compared
  is the trimmed query the builder already computes, so padding a single
  letter with spaces does not buy character matching.
- **Not a perf change:** the source walk itself is unchanged (BuildItemRows
  walks every checked source of every item regardless, to total it), so
  the one-character query costs what it always did, minus the substring
  scans it no longer performs. The bound recorded in char-source-search
  still holds.
- **Tests (+3, 1886 -> 1889):** a 1-character query returns the item whose
  own name matches and *not* the item held by a character whose name
  matches; the same pair at exactly 2 characters returns the
  character-held item (the boundary is exact - 2 matches, 1 does not); a
  whitespace-padded single letter stays below the floor.

Validation: module build 0 errors; suite 1889/1889.

Sandbox check should look at: type a single letter that begins a character
name into the Snapshot search and confirm only item/currency name matches
appear (no character holdings), then add the second letter and confirm
that character's items appear. The per-character checkboxes and the
AND-composition from char-source-search are unaffected and need no
re-gating.
Gate: PASS (2026-08-22, Paint-dummy sandbox session, branch build
651375c, captures preflight/m2a-one-char.png / m2b-two-char.png).
The preflight roster's holder names all contain "t" (Maximus Test,
Alt Number Two, Third Wheel, Ranger Of The North...), giving a clean
discriminator: typing "t" returned only item/currency name matches
with the 6-holder Green Wood Log ABSENT (the floor holding at one
character); adding "h" ("th", matched by no item name) returned
exactly Green Wood Log via Third Wheel / Ranger Of The North (the
floor lifting at two). The reviewer's noted empty-state wording gap
(a one-letter query's message does not mention the character-label
floor) was observed as accurate-but-unexplained live; left as
recorded.
