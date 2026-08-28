using System.Runtime.CompilerServices;

// Blish HUD reflects exactly one type out of this assembly - the Module
// subclass, via MEF - so Module is the only public type here and everything
// else is internal (see CONTRIBUTING.md, "Code Style"). The assemblies that
// legitimately consume module internals are named below; none of them ship.
// TaimisToolbench.RecipeSeeder.Tests is on the list transitively: it calls
// RecipeSeeder's own internals, and those signatures mention module types.
[assembly: InternalsVisibleTo("TaimisToolbench.Tests")]
[assembly: InternalsVisibleTo("TaimisToolbench.Harness")]
[assembly: InternalsVisibleTo("TaimisToolbench.RecipeSeeder")]
[assembly: InternalsVisibleTo("TaimisToolbench.RecipeSeeder.Tests")]
