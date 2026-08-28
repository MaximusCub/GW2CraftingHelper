using System;
using System.IO;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Reads the Mystic Forge recipe seed from a file on disk, for the
    /// console tools. The module itself never uses this: in-game the seed
    /// arrives through Blish's ContentsManager, not the filesystem, which
    /// is what Module.cs's own ContentsManagerRecipeSource does.
    /// <para>
    /// Compiled into TaimisToolbench.Harness and
    /// TaimisToolbench.RecipeSeeder by source link rather than living in
    /// the module's csproj, so the shipped module does not carry a class
    /// nothing in it calls. Both tools already ProjectReference the module
    /// for <see cref="IMysticForgeRecipeSource"/> itself; only this
    /// implementation is tool-side.
    /// </para>
    /// </summary>
    public class FileMysticForgeRecipeSource : IMysticForgeRecipeSource
    {
        private readonly string _filePath;

        public FileMysticForgeRecipeSource()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ref", "mystic_forge_recipes.json"))
        {
        }

        public FileMysticForgeRecipeSource(string filePath)
        {
            _filePath = filePath;
        }

        public Stream Open()
        {
            return File.OpenRead(_filePath);
        }
    }
}
