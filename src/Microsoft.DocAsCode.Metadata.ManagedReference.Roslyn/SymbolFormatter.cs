using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DocAsCode.DataContracts.ManagedReference;

using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    internal static class SymbolFormatter
    {
        private static readonly SymbolDisplayFormat s_nameFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod);

        private static readonly SymbolDisplayFormat s_nameWithTypeFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeContainingType,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod);

        private static readonly SymbolDisplayFormat s_qualifiedNameFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeContainingType,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod);

        private static readonly SymbolDisplayFormat s_namespaceFormat = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private static readonly SymbolDisplayFormat s_methodNameFormat = s_nameFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut);

        private static readonly SymbolDisplayFormat s_methodNameWithTypeFormat = s_nameWithTypeFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut);

        private static readonly SymbolDisplayFormat s_methodQualifiedNameFormat = s_qualifiedNameFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut);

        private static readonly SymbolDisplayFormat s_linkItemNameWithTypeFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes);

        private static readonly SymbolDisplayFormat s_linkItemQualifiedNameFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        public static string GetName(ISymbol symbol, SyntaxLanguage language)
        {
            return GetNameParts(symbol, language).ToDisplayString();
        }

        public static ImmutableArray<SymbolDisplayPart> GetNameParts(ISymbol symbol, SyntaxLanguage language, bool overload = false)
        {
            var format = symbol.Kind switch
            {
                SymbolKind.NamedType => s_nameWithTypeFormat,
                SymbolKind.Namespace => s_namespaceFormat,
                SymbolKind.Method => s_methodNameFormat,
                _ => s_nameFormat,
            };

            if (overload)
            {
                format = format
                    .WithMemberOptions(format.MemberOptions ^ SymbolDisplayMemberOptions.IncludeParameters)
                    .WithGenericsOptions(format.GenericsOptions ^ SymbolDisplayGenericsOptions.IncludeTypeParameters);
            }

            return GetParts(symbol, language, format);
        }

        public static string GetNameWithType(ISymbol symbol, SyntaxLanguage language)
        {
            return GetNameWithTypeParts(symbol, language).ToDisplayString();
        }

        public static ImmutableArray<SymbolDisplayPart> GetNameWithTypeParts(ISymbol symbol, SyntaxLanguage language)
        {
            var format = symbol.Kind switch
            {
                SymbolKind.Namespace => s_namespaceFormat,
                SymbolKind.Method => s_methodNameWithTypeFormat,
                _ => s_nameWithTypeFormat,
            };

            return GetParts(symbol, language, format);
        }

        public static string GetQualifiedName(ISymbol symbol, SyntaxLanguage language)
        {
            return GetQualifiedNameParts(symbol, language).ToDisplayString();
        }

        public static ImmutableArray<SymbolDisplayPart> GetQualifiedNameParts(ISymbol symbol, SyntaxLanguage language)
        {
            var format = symbol.Kind switch
            {
                SymbolKind.Namespace => s_namespaceFormat,
                SymbolKind.Method => s_methodQualifiedNameFormat,
                _ => s_qualifiedNameFormat,
            };

            return GetParts(symbol, language, format);
        }

        public static List<LinkItem> GetLinkItems(ISymbol symbol, SyntaxLanguage language, bool overload)
        {
            return GetNameParts(symbol, language, overload).Select(ToLinkItem).ToList();

            LinkItem ToLinkItem(SymbolDisplayPart part)
            {
                var symbol = part.Symbol;
                if (symbol is null || part.Kind is SymbolDisplayPartKind.TypeParameterName)
                {
                    return new()
                    {
                        DisplayName = part.ToString(),
                        DisplayNamesWithType = part.ToString(),
                        DisplayQualifiedNames = part.ToString(),
                    };
                }

                if (symbol is INamedTypeSymbol type && type.IsGenericType)
                {
                    symbol = type.ConstructedFrom;
                }

                var name = overload ? VisitorHelper.GetOverloadId(symbol) : VisitorHelper.GetId(symbol);
                
                return new()
                {
                    Name = overload ? VisitorHelper.GetOverloadId(symbol) : VisitorHelper.GetId(symbol),
                    DisplayName = part.ToString(),
                    DisplayNamesWithType = GetParts(symbol, language, s_linkItemNameWithTypeFormat).ToDisplayString(),
                    DisplayQualifiedNames = GetParts(symbol, language, s_linkItemQualifiedNameFormat).ToDisplayString(),
                    IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
                };
            }
        }

        private static ImmutableArray<SymbolDisplayPart> GetParts(ISymbol symbol, SyntaxLanguage language, SymbolDisplayFormat format)
        {
            try
            {
                return language switch
                {
                    SyntaxLanguage.VB => VB.SymbolDisplay.ToDisplayParts(symbol, format),
                    _ => CS.SymbolDisplay.ToDisplayParts(symbol, format),
                };
            }
            catch (InvalidOperationException)
            {
                return ImmutableArray<SymbolDisplayPart>.Empty;
            }
        }
    }
}
