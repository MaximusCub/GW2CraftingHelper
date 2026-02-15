using System.IO;

namespace GW2CraftingHelper.Services
{
    public interface IMysticForgeRecipeSource
    {
        Stream Open();
    }
}
