using Microsoft.CodeAnalysis;

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    internal static class SymbolHelper
    {
        public static bool IncludeSymbol(this ISymbol symbol)
        {
            if (symbol.GetDisplayAccessibility() is null)
                return false;

            return symbol.ContainingSymbol is null || IncludeSymbol(symbol.ContainingSymbol);
        }

        public static Accessibility? GetDisplayAccessibility(this ISymbol symbol)
        {
            // Hide internal or private APIs by default
            return symbol.DeclaredAccessibility switch
            {
                Accessibility.NotApplicable => Accessibility.NotApplicable,
                Accessibility.Public => Accessibility.Public,
                Accessibility.Protected => Accessibility.Protected,
                Accessibility.ProtectedOrInternal => Accessibility.Protected,
                _ => null,
            };
        }
    }
}
