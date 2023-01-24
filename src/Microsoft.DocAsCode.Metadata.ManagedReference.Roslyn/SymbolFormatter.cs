using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.DocAsCode.DataContracts.ManagedReference;

using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    internal partial class SymbolFormatter
    {
        private static readonly SymbolDisplayFormat s_nameFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral);

        private static readonly SymbolDisplayFormat s_nameWithTypeFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeContainingType,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral);

        private static readonly SymbolDisplayFormat s_qualifiedNameFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeContainingType,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType);

        private static readonly SymbolDisplayFormat s_namespaceFormat = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private static readonly SymbolDisplayFormat s_methodNameFormat = s_nameFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut);

        private static readonly SymbolDisplayFormat s_methodNameWithTypeFormat = s_nameWithTypeFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut);

        private static readonly SymbolDisplayFormat s_methodQualifiedNameFormat = s_qualifiedNameFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut);

        private static readonly SymbolDisplayFormat s_syntaxFormat = new(
            kindOptions:
                SymbolDisplayKindOptions.IncludeNamespaceKeyword |
                SymbolDisplayKindOptions.IncludeMemberKeyword |
                SymbolDisplayKindOptions.IncludeTypeKeyword,
            memberOptions:
                SymbolDisplayMemberOptions.IncludeParameters |
                SymbolDisplayMemberOptions.IncludeConstantValue |
                SymbolDisplayMemberOptions.IncludeModifiers |
                SymbolDisplayMemberOptions.IncludeRef |
                SymbolDisplayMemberOptions.IncludeType |
                SymbolDisplayMemberOptions.IncludeExplicitInterface,
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters |
                SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                SymbolDisplayGenericsOptions.IncludeVariance,
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeParamsRefOut |
                SymbolDisplayParameterOptions.IncludeDefaultValue |
                SymbolDisplayParameterOptions.IncludeName |
                SymbolDisplayParameterOptions.IncludeExtensionThis,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix,
            localOptions: SymbolDisplayLocalOptions.IncludeType,
            propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes);

        private static readonly SymbolDisplayFormat s_syntaxTypeNameFormat = new(
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes);

        private static readonly SymbolDisplayFormat s_syntaxEnumConstantFormat = s_syntaxFormat
            .WithMemberOptions(s_syntaxFormat.MemberOptions | SymbolDisplayMemberOptions.IncludeContainingType);

        public static string GetName(ISymbol symbol, SyntaxLanguage language)
        {
            return GetNameParts(symbol, language).ToDisplayString();
        }

        public static ImmutableArray<SymbolDisplayPart> GetNameParts(ISymbol symbol, SyntaxLanguage language)
        {
            var format = symbol.Kind switch
            {
                SymbolKind.NamedType => s_nameWithTypeFormat,
                SymbolKind.Namespace => s_namespaceFormat,
                SymbolKind.Method => s_methodNameFormat,
                _ => s_nameFormat,
            };

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

        public static string GetSyntax(ISymbol symbol, SyntaxLanguage language)
        {
            return GetSyntaxParts(symbol, language).ToDisplayString();
        }

        public static ImmutableArray<SymbolDisplayPart> GetSyntaxParts(ISymbol symbol, SyntaxLanguage language)
        {
            try
            {
                var formatter = new SymbolFormatter { _language = language };
                formatter.AddSyntax(symbol);
                return formatter._parts.ToImmutable();
            }
            catch (InvalidOperationException)
            {
                return ImmutableArray<SymbolDisplayPart>.Empty;
            }
        }
    }
}
