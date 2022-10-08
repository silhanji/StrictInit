﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StrictInit.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StrictInitAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "SI001";
        
        private static readonly string Title = "Set all properties";
        private static readonly string MessageFormat = "Public property {0} not set";
        private static readonly string Description = "Consider setting other public properties in object initializer.";
        private const string Category = "Usage";
        
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(Id, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);
        
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ObjectInitializerExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var objectInitializer = (InitializerExpressionSyntax)context.Node;
            var constructedSymbol = GetConstructedSymbol(objectInitializer, context.SemanticModel);
            if (constructedSymbol is null)
                return;

            var publicProperties = GetPublicPropertiesNames(constructedSymbol);
            var assignedProperties = new HashSet<string>();
            foreach (var expression in objectInitializer.Expressions)
            {
                if (expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier })
                {
                    assignedProperties.Add(identifier.ToString());
                }
            }

            var unassignedProperties = new List<string>();
            foreach (var publicProperty in publicProperties)
            {
                if (! assignedProperties.Contains(publicProperty))
                {
                    unassignedProperties.Add(publicProperty);
                }
            }

            if (unassignedProperties.Count > 0)
            {
                var diagnostic = Diagnostic.Create(Rule, objectInitializer.GetLocation(), string.Join(", ", unassignedProperties));
                context.ReportDiagnostic(diagnostic);
            }
        }

        private ImmutableArray<string> GetPublicPropertiesNames(INamedTypeSymbol constructedSymbol)
        {
            return constructedSymbol.GetMembers().OfType<IPropertySymbol>()
                .Where(property => property.SetMethod is not null &&
                                   property.SetMethod.DeclaredAccessibility == Accessibility.Public)
                .Select(property => property.Name)
                .ToImmutableArray();
        }

        private INamedTypeSymbol? GetConstructedSymbol(InitializerExpressionSyntax objectInitializer, SemanticModel semanticModel)
        {
            var constructedType = (objectInitializer.Parent as ObjectCreationExpressionSyntax)?.Type;
            if (constructedType is null)
                return null;

            return semanticModel.GetSymbolInfo(constructedType).Symbol as INamedTypeSymbol;
        }
    }
}