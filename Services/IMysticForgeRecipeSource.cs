using System.IO;

namespace TaimisToolbench.Services
{
    internal interface IMysticForgeRecipeSource
    {
        Stream Open();
    }
}
