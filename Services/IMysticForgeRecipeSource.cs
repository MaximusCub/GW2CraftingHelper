using System.IO;

namespace GW2CraftingHelper.Services
{
    internal interface IMysticForgeRecipeSource
    {
        Stream Open();
    }
}
