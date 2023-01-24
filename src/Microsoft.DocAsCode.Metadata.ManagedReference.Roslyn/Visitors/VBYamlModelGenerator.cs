// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    using System.Collections.Generic;

    using Microsoft.CodeAnalysis;
    using Microsoft.DocAsCode.DataContracts.ManagedReference;

    public class VBYamlModelGenerator : SimpleYamlModelGenerator
    {
        public VBYamlModelGenerator() : base(SyntaxLanguage.VB)
        {
        }

        #region Overrides

        public override void DefaultVisit(ISymbol symbol, MetadataItem item, SymbolVisitorAdapter adapter)
        {
            item.DisplayNames[SyntaxLanguage.VB] = SymbolFormatter.GetName(symbol, SyntaxLanguage.VB);
            item.DisplayNamesWithType[SyntaxLanguage.VB] = SymbolFormatter.GetNameWithType(symbol, SyntaxLanguage.VB);
            item.DisplayQualifiedNames[SyntaxLanguage.VB] = SymbolFormatter.GetQualifiedName(symbol, SyntaxLanguage.VB);
        }

        protected override string GetSyntaxContent(MemberType typeKind, ISymbol symbol, SymbolVisitorAdapter adapter)
        {
            return SymbolFormatter.GetSyntax(symbol, SyntaxLanguage.VB);
        }

        protected override void GenerateReference(ISymbol symbol, ReferenceItem reference, SymbolVisitorAdapter adapter, bool asOverload)
        {
            symbol.Accept(new VBReferenceItemVisitor(reference, asOverload));
        }

        #endregion

        #region Private Methods

        private static bool IsSymbolAccessible(ISymbol symbol)
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public && symbol.DeclaredAccessibility != Accessibility.Protected && symbol.DeclaredAccessibility != Accessibility.ProtectedOrInternal)
                return false;
            if (symbol.ContainingSymbol != null && symbol.Kind == SymbolKind.NamedType)
                return IsSymbolAccessible(symbol.ContainingSymbol);
            return true;
        }

        private static string GetVisiblity(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    return "Protected";
                case Accessibility.Public:
                    return "Public";
                default:
                    return null;
            }
        }

        #endregion
    }
}
