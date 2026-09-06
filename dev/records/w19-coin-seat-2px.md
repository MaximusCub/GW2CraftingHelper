> **Milestone record - 2026-09-05, branch `w19-coin-seat-2px`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## The coin seat hangs the art's disc on the digits, not its shadow

Branch `w17-coin-seat` gave gold, silver and copper their own vertical seat:
instead of centring the icon box on the digits, it hangs the coin art's last
inked row one row below the digits' ink bottom, which is what the in-game
wallet bar shows. After it shipped, the three coins still drew two rows above
where the game draws them. The seat rule was right. What was wrong was the
number it was fed - where the art's ink was said to end. This branch, merged
as pull request 249, corrects that number. Four files.

### Rows 24, 25 and 26 are the art's black rim, not coin

`Services/CoinSegmentMath.cs` held `CoinArtLastInkRow` at 26, the last row of
the three shipped 32x32 textures with a non-zero alpha. Re-measured by
compositing each source row over a dark row ground, rows 24, 25 and 26 come
out DARKER than the ground on gold 156904, silver 156907 and copper 156902
alike: they are the art's black bottom rim. Nothing there reads as coin.

Hanging that edge one row under the digits therefore hung three rows of shadow
under them and left the visible disc two rows high, which is the gap measured
against a wallet bar capture. The last row that actually draws coin is 23 on
all three textures, so `CoinArtLastInkRow` is now 23 and the seat lands where
it always claimed to. The first drawn row is not shared - gold and silver draw
5..23, copper 7..23 - and a bottom seat does not read it.

Only gold, silver and copper move. Every other inline currency icon keeps its
centred box, which does not go through `CoinArtInkBottom`.

### The hang below the baseline is restated ink against ink

`CoinInkBelowBaseline` stays 1, but its derivation changed. It used to be
derived from the icon box's matched position in a bar tier capture - box
y114..129 against digit ink y115..126. That is a template fit against art the
game rescales itself, and it does not resolve to the row. The constant is now
measured ink against ink within one capture: the coin's lowest drawn row is
one below the digits' lowest.

A follow-up comment records why that reading survives the capture's UI size.
Blish paints module coordinates through the game's UI size, so a row count
lifted off a screen capture is a physical number while a layout constant is a
logical one. Because both edges are read inside a single capture they scale
together, the UI size cancels, and the count carries straight into a logical
constant with no conversion and no separate claim that the capture was native.
`docs/ARCHITECTURE.md` carries the same correction in its coin-tier section.

### Regression coverage

`tests/TaimisToolbench.Tests/Services/CoinSegmentMathTests.cs` carries the
per-denomination drawn rows as theory data, now 5..23, 5..23 and 7..23, and
asserts the relationship in rows rather than in edges: the coin's lowest drawn
row is one below the digits' lowest. Flush, or above it, fails.

A new case, `TheCoinSeat_IsPinnedAtTheShippedFaceAndTier`, pins the whole seat
end to end as one number - 5, at Menomonia 16's `0` against the 16px bar-tier
box every plan table draws its coin runs in. It exists because the glyph pad,
the art's last drawn row and the hang below the baseline can each move that
seat without failing anything else.

The scaling cases move with the constant: a 16px box's ink bottom goes 14 ->
12, the 32px wallet-list box 27 -> 24, a 12px box 10 -> 9, and the wallet
tier's own seat 4 -> 7.

`docs/file-budgets.txt` raises `Services/CoinSegmentMath.cs` from 302 to 303
lines for the UI-size note, and the test file from 498 to 511 for the new
case.

Gate: NOT RUN - no live game session is recorded in the branch's commits. The
failing state is the two-row gap measured off a wallet bar capture. In game,
confirm that a gold, silver or copper icon beside a plan figure sits with its
disc level on the digits rather than riding above them, and that other
currency icons in the same rows are unchanged.
