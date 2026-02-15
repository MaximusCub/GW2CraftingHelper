namespace GW2CraftingHelper.Services
{
    public static class RecipeClientFactory
    {
        public static IRecipeApiClient Create(
            IRecipeApiClient primary,
            IMysticForgeRecipeSource mfSource)
        {
            MysticForgeRecipeData mfData;
            try
            {
                using (var stream = mfSource.Open())
                {
                    mfData = MysticForgeRecipeData.Load(stream);
                }
            }
            catch
            {
                mfData = MysticForgeRecipeData.Empty;
            }

            return new CompositeRecipeApiClient(primary, mfData);
        }
    }
}
