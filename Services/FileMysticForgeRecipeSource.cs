using System;
using System.IO;

namespace GW2CraftingHelper.Services
{
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
