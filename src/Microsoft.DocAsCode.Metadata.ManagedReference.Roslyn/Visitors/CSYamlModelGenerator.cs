// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    using System.Collections.Generic;

    using Microsoft.CodeAnalysis;
    using Microsoft.DocAsCode.DataContracts.ManagedReference;

    public class CSYamlModelGenerator : SimpleYamlModelGenerator
    {
        public CSYamlModelGenerator() : base(SyntaxLanguage.CSharp)
        {
        }

        #region Overrides

        public override void DefaultVisit(ISymbol symbol, MetadataItem item, SymbolVisitorAdapter adapter)
        {
            item.DisplayNames[SyntaxLanguage.CSharp] = SymbolFormatter.GetName(symbol, SyntaxLanguage.CSharp);
            item.DisplayNamesWithType[SyntaxLanguage.CSharp] = SymbolFormatter.GetNameWithType(symbol, SyntaxLanguage.CSharp);
            item.DisplayQualifiedNames[SyntaxLanguage.CSharp] = SymbolFormatter.GetQualifiedName(symbol, SyntaxLanguage.CSharp);
        }

        protected override string GetSyntaxContent(MemberType typeKind, ISymbol symbol, SymbolVisitorAdapter adapter)
        {
            return SymbolFormatter.GetSyntax(symbol, SyntaxLanguage.CSharp);
        }

        protected override void GenerateReference(ISymbol symbol, ReferenceItem reference, SymbolVisitorAdapter adapter, bool asOverload)
        {
            symbol.Accept(new CSReferenceItemVisitor(reference, asOverload));
        }

        #endregion

        #region Private methods

        private static bool IsSymbolAccessible(ISymbol symbol)
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public && symbol.DeclaredAccessibility != Accessibility.Protected && symbol.DeclaredAccessibility != Accessibility.ProtectedOrInternal)
                return false;
            if (symbol.ContainingSymbol != null && symbol.Kind == SymbolKind.NamedType)
                return IsSymbolAccessible(symbol.ContainingSymbol);
            return true;
        }

        private static bool IsPropertyReadonly(IPropertySymbol property)
        {
            if (property.ContainingType.TypeKind != TypeKind.Struct)
            {
                return false;
            }

            if (property.IsReadOnly)
            {
                return true;
            }

            if (property.GetMethod is null)
            {
                return property.SetMethod.IsReadOnly;
            }
            
            if (property.SetMethod is null)
            {
                return property.GetMethod.IsReadOnly;
            }
            
            return property.GetMethod.IsReadOnly && property.SetMethod.IsReadOnly;
        }

        private static string GetVisiblity(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    return "protected";
                case Accessibility.Public:
                    return "public";
                default:
                    return null;
            }
        }

        #endregion
    }
}
