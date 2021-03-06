﻿// © 2019 Koninklijke Philips N.V. See License.md in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Philips.CodeAnalysis.Common;

namespace Philips.CodeAnalysis.MsTestAnalyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class AssertAreEqualTypesMatchAnalyzer : DiagnosticAnalyzer
	{
		private const string Title = @"Assert.AreEqual parameter types must match";
		private const string MessageFormat = @"The type of <expected> ({0}) does not match the type of <actual> ({1})";
		private const string Description = @"The types of the parameters of Are[Not]Equal must match.";
		private const string Category = Categories.Maintainability;

		private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(Helper.ToDiagnosticId(DiagnosticIds.AssertAreEqualTypesMatch), Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
			context.EnableConcurrentExecution();
			context.RegisterCompilationStartAction(startContext =>
			{
				if (startContext.Compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.Assert") == null)
				{
					return;
				}

				startContext.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
			});
		}

		private static void Analyze(SyntaxNodeAnalysisContext context)
		{
			var mds = context.Node as InvocationExpressionSyntax;
			if (mds == null)
			{
				return;
			}

			MemberAccessExpressionSyntax maes = mds.Expression as MemberAccessExpressionSyntax;
			if (maes == null)
			{
				return;
			}

			string memberName = maes.Name.ToString();
			if ((memberName != @"AreEqual") && (memberName != @"AreNotEqual"))
			{
				return;
			}

			IMethodSymbol memberSymbol = context.SemanticModel.GetSymbolInfo(maes).Symbol as IMethodSymbol;
			if ((memberSymbol == null) || !memberSymbol.ToString().StartsWith("Microsoft.VisualStudio.TestTools.UnitTesting.Assert"))
			{
				return;
			}

			// If it resolved to Are[Not]Equal<T>, then we know the types are the same.
			if (memberSymbol.IsGenericMethod)
			{
				return;
			}

			ArgumentListSyntax argumentList = mds.ArgumentList as ArgumentListSyntax;
			TypeInfo ti1 = context.SemanticModel.GetTypeInfo(argumentList.Arguments[0].Expression);
			TypeInfo ti2 = context.SemanticModel.GetTypeInfo(argumentList.Arguments[1].Expression);

			// Our <actual> is of type object.  If it matches the type of <expected>, then AreEqual will pass, so we wouldn't want to fail here.
			// However, if the types differ, it will be a runtime Assert fail, so the early notice is advantageous.  The code is also clearer.
			// Moreover, if it were "AreNotEqual", that is particularly insidious, because the Assert would pass due to the type difference
			// rather than the value difference.  Let's play it safe, and require the author to be clear.
			//if (ti2.Type.ToString() == @"object")
			//{
			//	return;
			//}

			if (!context.SemanticModel.Compilation.ClassifyConversion(ti2.Type, ti1.Type).IsImplicit)
			{
				Diagnostic diagnostic = Diagnostic.Create(Rule, mds.GetLocation(), ti1.Type.ToString(), ti2.Type.ToString());
				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
