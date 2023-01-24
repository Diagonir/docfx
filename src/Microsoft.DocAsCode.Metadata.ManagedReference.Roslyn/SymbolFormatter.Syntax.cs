using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.DocAsCode.DataContracts.ManagedReference;

using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

#nullable enable

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    partial class SymbolFormatter
    {
        private SyntaxLanguage _language;
        private ImmutableArray<SymbolDisplayPart>.Builder _parts = ImmutableArray.CreateBuilder<SymbolDisplayPart>();

        private Func<ISymbol, bool> _includeSymbol = SymbolHelper.IncludeSymbol;
        private Func<ISymbol, bool> _includeAttribute = _ => true;

        private void AddSyntax(ISymbol symbol)
        {
            AddAttributes(symbol);
            AddAccessibility(symbol);
            AddClassModifiers(symbol);

            symbol = HidePropertyAccessorIfNeeded(symbol);

            var parts = _language is SyntaxLanguage.VB
                ? VB.SymbolDisplay.ToDisplayParts(symbol, s_syntaxFormat)
                : CS.SymbolDisplay.ToDisplayParts(symbol, s_syntaxFormat);

            foreach (var part in parts)
            {
                if (ExpandEnumClassName(symbol, part))
                    continue;

                if (StaticClassToVBModule(symbol, part))
                    continue;

                _parts.Add(part);
            }

            var namedTypeConstraints = RemoveNamedTypeConstraints(symbol);

            AddBaseTypeAndInterfaces(symbol);

            _parts.AddRange(namedTypeConstraints);
        }

        private void AddAccessibility(ISymbol symbol)
        {
            if (ShouldHideAccessibility(symbol))
                return;

            var syntaxKind = symbol.GetDisplayAccessibility() switch
            {
                Accessibility.Protected or Accessibility.ProtectedOrInternal => _language is SyntaxLanguage.VB ? "Protected" : "protected",
                Accessibility.Public => _language is SyntaxLanguage.VB ? "Public" : "public",
                _ => null,
            };

            if (syntaxKind != null)
            {
                AddKeyword(syntaxKind);
                AddSpace();
            }

            static bool ShouldHideAccessibility(ISymbol symbol)
            {
                if (symbol.Kind is SymbolKind.Method or SymbolKind.Property or SymbolKind.Event &&
                    symbol.ContainingType?.TypeKind is TypeKind.Interface)
                    return true;

                if (symbol.Kind is SymbolKind.Field && symbol.ContainingType?.TypeKind is TypeKind.Enum)
                    return true;

                if (symbol.Kind is SymbolKind.Method && symbol is IMethodSymbol method && method.ExplicitInterfaceImplementations.Length > 0)
                    return true;

                if (symbol.Kind is SymbolKind.Property && symbol is IPropertySymbol property && property.ExplicitInterfaceImplementations.Length > 0)
                    return true;

                if (symbol.Kind is SymbolKind.Event && symbol is IEventSymbol @event && @event.ExplicitInterfaceImplementations.Length > 0)
                    return true;

                return false;
            }
        }

        private void AddClassModifiers(ISymbol symbol)
        {
            if (symbol.Kind is SymbolKind.NamedType && symbol is INamedTypeSymbol type && type.TypeKind is TypeKind.Class)
            {
                if (symbol.IsStatic && _language is not SyntaxLanguage.VB)
                {
                    AddKeyword("static");
                    AddSpace();
                }

                if (symbol.IsAbstract)
                {
                    AddKeyword(_language is SyntaxLanguage.VB ? "MustInherit" : "abstract");
                    AddSpace();
                }

                if (symbol.IsSealed)
                {
                    AddKeyword(_language is SyntaxLanguage.VB ? "NotInheritable" : "sealed");
                    AddSpace();
                }
            }
        }

        private ImmutableArray<SymbolDisplayPart> RemoveNamedTypeConstraints(ISymbol symbol)
        {
            if (symbol.Kind is not SymbolKind.NamedType)
                return ImmutableArray<SymbolDisplayPart>.Empty;

            var result = ImmutableArray.CreateBuilder<SymbolDisplayPart>();

            for (var i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i];
                if (part.Kind == SymbolDisplayPartKind.Keyword && part.ToString() == "where")
                {
                    result.Add(new(SymbolDisplayPartKind.Space, null, " "));
                    while (i < _parts.Count)
                    {
                        result.Add(_parts[i]);
                        _parts.RemoveAt(i);
                    }
                    RemoveEnd();
                    break;
                }
            }

            return result.ToImmutable();
        }

        private void AddBaseTypeAndInterfaces(ISymbol symbol)
        {
            if (symbol.Kind is not SymbolKind.NamedType || symbol is not INamedTypeSymbol type)
                return;

            var baseTypes = new List<INamedTypeSymbol>();

            if (type.TypeKind is TypeKind.Enum)
            {
                if (type.EnumUnderlyingType is not null && type.EnumUnderlyingType.SpecialType is not SpecialType.System_Int32)
                {
                    if (_language is SyntaxLanguage.VB)
                    {
                        AddSpace();
                        AddKeyword("As");
                        AddSpace();
                        AddTypeName(type.EnumUnderlyingType);
                    }
                    else
                    {
                        baseTypes.Add(type.EnumUnderlyingType);
                    }
                }
            }
            else if (type.TypeKind is TypeKind.Class or TypeKind.Interface or TypeKind.Struct)
            {
                if (type.BaseType is not null &&
                    type.BaseType.SpecialType is not SpecialType.System_Object &&
                    type.BaseType.SpecialType is not SpecialType.System_ValueType)
                {
                    if (_language is SyntaxLanguage.VB)
                    {
                        AddSpace();
                        AddKeyword("Inherits");
                        AddSpace();
                        AddTypeName(type.BaseType);
                    }
                    else
                    {
                        baseTypes.Add(type.BaseType);
                    }
                }

                foreach (var @interface in type.AllInterfaces)
                {
                    if (_includeSymbol(@interface))
                        baseTypes.Add(@interface);
                }
            }

            if (baseTypes.Count <= 0)
                return;

            AddSpace();
            if (_language is SyntaxLanguage.VB)
                AddKeyword(type.TypeKind is TypeKind.Interface ? "Inherits" : "Implements");
            else
                AddPunctuation(":");
            AddSpace();

            foreach (var baseType in baseTypes)
            {
                AddTypeName(baseType);
                AddPunctuation(",");
                AddSpace();
            }

            RemoveEnd();
            RemoveEnd();
        }

        private void AddTypeName(INamedTypeSymbol symbol)
        {
            _parts.AddRange(_language is SyntaxLanguage.VB
                ? VB.SymbolDisplay.ToDisplayParts(symbol, s_syntaxTypeNameFormat)
                : CS.SymbolDisplay.ToDisplayParts(symbol, s_syntaxTypeNameFormat));
        }

        private void AddAttributes(ISymbol symbol)
        {
            if (symbol.Kind != SymbolKind.NamedType)
                return;

            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass.Kind == SymbolKind.ErrorType ||
                    attribute.AttributeConstructor is null ||
                    !_includeAttribute(attribute.AttributeConstructor))
                {
                    continue;
                }

                AddAttribute(attribute);
            }
        }

        private void AddAttribute(AttributeData attribute)
        {
            AddKeyword(_language is SyntaxLanguage.VB ? "<" : "[");

            var parts = _language is SyntaxLanguage.VB
                ? VB.SymbolDisplay.ToDisplayParts(attribute.AttributeClass, s_syntaxTypeNameFormat)
                : CS.SymbolDisplay.ToDisplayParts(attribute.AttributeClass, s_syntaxTypeNameFormat);
            foreach (var part in parts)
            {
                _parts.Add(
                    part.Kind == SymbolDisplayPartKind.ClassName && part.ToString().EndsWith("Attribute")
                        ? new(part.Kind, part.Symbol, part.ToString()[..^9])
                        : part);
            }

            AddAttributeArguments(attribute);
            AddKeyword(_language is SyntaxLanguage.VB ? ">" : "]");
            AddLineBreak();
        }

        private void AddAttributeArguments(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length == 0 && attribute.NamedArguments.Length == 0)
                return;

            AddKeyword("(");

            foreach (var argument in attribute.ConstructorArguments)
            {
                AddTypedConstant(argument);
                AddPunctuation(",");
                AddSpace();
            }

            foreach (var (key, argument) in attribute.NamedArguments)
            {
                _parts.Add(new(SymbolDisplayPartKind.ParameterName, null, key));

                if (_language is SyntaxLanguage.VB)
                {
                    AddPunctuation(":=");
                }
                else
                {
                    AddSpace();
                    AddPunctuation(_language is SyntaxLanguage.VB ? ":=" : "=");
                    AddSpace();
                }

                AddTypedConstant(argument);
                AddPunctuation(",");
                AddSpace();
            }

            RemoveEnd();
            RemoveEnd();
            AddKeyword(")");
        }

        private void AddTypedConstant(TypedConstant typedConstant)
        {
            switch (typedConstant.Kind)
            {
                case TypedConstantKind.Primitive when typedConstant.Value is not null:
                    var value = _language is SyntaxLanguage.VB
                        ? VB.SymbolDisplay.FormatPrimitive(typedConstant.Value, quoteStrings: true, useHexadecimalNumbers: false)
                        : CS.SymbolDisplay.FormatPrimitive(typedConstant.Value, quoteStrings: true, useHexadecimalNumbers: false);
                    _parts.Add(new(typedConstant.Value is string ? SymbolDisplayPartKind.StringLiteral : SymbolDisplayPartKind.NumericLiteral, null, value));
                    break;

                case TypedConstantKind.Enum:
                    var parameterSymbol = new ParameterSymbol
                    {
                        Type = typedConstant.Type,
                        HasExplicitDefaultValue = true,
                        ExplicitDefaultValue = typedConstant.Value,
                    };
                    var parts = _language is SyntaxLanguage.VB
                        ? VB.SymbolDisplay.ToDisplayParts(parameterSymbol, s_syntaxEnumConstantFormat)
                        : CS.SymbolDisplay.ToDisplayParts(parameterSymbol, s_syntaxEnumConstantFormat);
                    AddPartsAfter(parts, part => part.Kind == SymbolDisplayPartKind.Punctuation && part.ToString() == "=");
                    break;

                case TypedConstantKind.Type when typedConstant.Value is ITypeSymbol typeSymbol:
                    switch (_language)
                    {
                        case SyntaxLanguage.VB:
                            AddKeyword("GetType");
                            AddPunctuation("(");
                            _parts.AddRange(VB.SymbolDisplay.ToDisplayParts(typeSymbol, s_syntaxTypeNameFormat));
                            AddPunctuation(")");
                            break;

                        default:
                            AddKeyword("typeof");
                            AddPunctuation("(");
                            _parts.AddRange(CS.SymbolDisplay.ToDisplayParts(typeSymbol, s_syntaxTypeNameFormat));
                            AddPunctuation(")");
                            break;
                    }
                    break;

                case TypedConstantKind.Array when typedConstant.Type is not null:
                    AddKeyword(_language is SyntaxLanguage.VB ? "New" : "new");
                    AddSpace();
                    _parts.AddRange(_language is SyntaxLanguage.VB
                        ? VB.SymbolDisplay.ToDisplayParts(typedConstant.Type, s_syntaxTypeNameFormat)
                        : CS.SymbolDisplay.ToDisplayParts(typedConstant.Type, s_syntaxTypeNameFormat));
                    AddSpace();
                    AddPunctuation("{");
                    AddSpace();

                    if (typedConstant.Values.Length > 0)
                    {
                        foreach (var item in typedConstant.Values)
                        {
                            AddTypedConstant(item);
                            AddPunctuation(",");
                            AddSpace();
                        }

                        RemoveEnd();
                        RemoveEnd();

                        AddSpace();
                    }

                    AddPunctuation("}");
                    break;

                default:
                    AddKeyword(_language is SyntaxLanguage.VB ? "Nothing" : "null");
                    break;
            }
        }

        private void RemoveEnd()
        {
            _parts.RemoveAt(_parts.Count - 1);
        }

        private void AddKeyword(string text)
        {
            _parts.Add(new(SymbolDisplayPartKind.Keyword, null, text));
        }

        private void AddSpace()
        {
            _parts.Add(new(SymbolDisplayPartKind.Space, null, " "));
        }

        private void AddLineBreak()
        {
            _parts.Add(new(SymbolDisplayPartKind.LineBreak, null, "\r\n"));
        }

        private void AddPunctuation(string text)
        {
            _parts.Add(new(SymbolDisplayPartKind.Punctuation, null, text));
        }

        private void AddPartsAfter(ImmutableArray<SymbolDisplayPart> parts, Func<SymbolDisplayPart, bool> after)
        {
            var add = false;
            foreach (var part in parts)
            {
                if (add)
                {
                    if (part.Kind != SymbolDisplayPartKind.Space)
                    {
                        _parts.Add(part);
                    }
                }
                else if (after(part))
                {
                    add = true;
                    continue;
                }
            }
        }

        private bool ExpandEnumClassName(ISymbol symbol, SymbolDisplayPart part)
        {
            if (symbol.Kind != SymbolKind.Field && part.Kind == SymbolDisplayPartKind.EnumMemberName)
            {
                _parts.Add(new(SymbolDisplayPartKind.EnumName, part.Symbol.ContainingSymbol, part.Symbol.ContainingSymbol.Name));
                _parts.Add(new(SymbolDisplayPartKind.Punctuation, null, "."));
                _parts.Add(part);
                return true;
            }
            return false;
        }

        private bool StaticClassToVBModule(ISymbol symbol, SymbolDisplayPart part)
        {
            if (_language is SyntaxLanguage.VB && symbol.IsStatic && symbol.Kind is SymbolKind.NamedType &&
                part.Kind == SymbolDisplayPartKind.Keyword && part.ToString() == "Class")
            {
                _parts.Add(new(SymbolDisplayPartKind.Keyword, null, "Module"));
                return true;
            }
            return false;
        }

        private ISymbol HidePropertyAccessorIfNeeded(ISymbol symbol)
        {
            if (symbol is not IPropertySymbol property)
                return symbol;

            var accessibility = property.GetDisplayAccessibility();
            if (accessibility is null)
                return symbol;

            return new PropertySymbol
            {
                Inner = property,
                DeclaredAccessibility = accessibility.Value,
                GetMethod = GetAccessor(property.GetMethod),
                SetMethod = GetAccessor(property.SetMethod),
            };

            IMethodSymbol? GetAccessor(IMethodSymbol? method)
            {
                if (method is null)
                    return null;

                var accessibility = method.GetDisplayAccessibility();
                if (accessibility is null)
                    return null;

                if (accessibility == method.DeclaredAccessibility)
                    return method;

                return new MethodSymbol { Inner = method, DeclaredAccessibility = accessibility.Value };
            }
        }
    }
}
