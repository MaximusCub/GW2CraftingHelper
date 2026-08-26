using System;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Turns a <c>nameof</c> enum-member string from an <c>[InlineData]</c> row
    /// back into the enum value.
    /// </summary>
    /// <remarks>
    /// xUnit 2.x only discovers public test classes (xUnit1000, an error here),
    /// and a public method's parameter types must be at least as accessible as
    /// the method - so a <c>[Theory]</c> taking a module enum directly would
    /// force that enum <c>public</c> in the shipped assembly, where only
    /// <c>Module</c> is meant to be (CONTRIBUTING.md, "Code Style"). Passing
    /// <c>nameof(CraftingDecision.BuyFromTp)</c> keeps the rename-safety of the
    /// symbol reference - <c>nameof</c> is resolved by the compiler, so a
    /// renamed member fails the build - while keeping the enum internal.
    /// </remarks>
    internal static class EnumArg
    {
        internal static T Parse<T>(string memberName)
            where T : struct
        {
            return (T)Enum.Parse(typeof(T), memberName);
        }
    }
}
