namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// One learned crafting discipline on one character, captured at
    /// snapshot time (per-character discipline display, gw2efficiency
    /// parity - see AccountSnapshot.CharacterDisciplines' own doc comment).
    /// Rating persists even when the discipline is not currently active - a
    /// character can only have 2 disciplines active at once, but a
    /// previously-levelled discipline keeps its Rating when swapped out (a
    /// Master NPC re-activates it for a small fee) - so this is captured
    /// for every LEARNED discipline regardless of Active, mirroring exactly
    /// what GET /v2/characters/:id/crafting returns.
    /// </summary>
    public class SnapshotCharacterDiscipline
    {
        public string CharacterName { get; set; } = "";
        public string Discipline    { get; set; } = "";
        public int    Rating        { get; set; }
        public bool   Active        { get; set; }
    }
}
