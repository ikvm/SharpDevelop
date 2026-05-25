// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;

using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;
using NRSymbolKind = ICSharpCode.NRefactory.TypeSystem.SymbolKind;
using NRTypeKind = ICSharpCode.NRefactory.TypeSystem.TypeKind;

namespace CSharpBinding.Parser.Roslyn
{
	/// <summary>
	/// 从 Roslyn SyntaxTree 提取类型信息的 IUnresolvedFile 适配器。
	/// 将 Roslyn AST 中的类型声明、成员声明等转换为 NRefactory 类型系统接口。
	/// </summary>
	public class RoslynUnresolvedFileAdapter : IUnresolvedFile
	{
		readonly string fileName;
		readonly List<IUnresolvedTypeDefinition> topLevelTypeDefinitions = new List<IUnresolvedTypeDefinition>();
		readonly List<Error> errors = new List<Error>();

		public RoslynUnresolvedFileAdapter(SyntaxTree syntaxTree, string fileName)
		{
			this.fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));

			if (syntaxTree == null)
				throw new ArgumentNullException(nameof(syntaxTree));

			// 从 Roslyn 诊断信息中提取错误
			var diagnostics = syntaxTree.GetDiagnostics();
			foreach (var diag in diagnostics) {
				var errorType = diag.Severity == DiagnosticSeverity.Error ? ErrorType.Error : ErrorType.Warning;
				var lineSpan = diag.Location.GetLineSpan();
				var startLinePos = lineSpan.StartLinePosition;
				var nrLocation = new TextLocation(startLinePos.Line + 1, startLinePos.Character + 1);
				errors.Add(new Error(errorType, diag.GetMessage(), nrLocation));
			}

			// 提取顶层类型定义
			var root = syntaxTree.GetRoot() as CompilationUnitSyntax;
			if (root != null) {
				foreach (var member in root.Members) {
					ExtractTypeDefinitions(member, this, null, topLevelTypeDefinitions);
				}
			}
		}

		/// <summary>
		/// 从 Roslyn 语法节点中递归提取类型定义。
		/// </summary>
		static void ExtractTypeDefinitions(MemberDeclarationSyntax member, IUnresolvedFile file, IUnresolvedTypeDefinition parent, List<IUnresolvedTypeDefinition> targetList)
		{
			var typeDecl = member as TypeDeclarationSyntax;
			if (typeDecl != null) {
				var typeDef = CreateTypeDefinition(typeDecl, file, parent);
				targetList.Add(typeDef);

				// 提取嵌套类型
				foreach (var nestedMember in typeDecl.Members) {
					ExtractTypeDefinitions(nestedMember, file, typeDef, (List<IUnresolvedTypeDefinition>)typeDef.NestedTypes);
				}

				// 提取成员
				ExtractMembers(typeDecl, typeDef);
			} else if (member is EnumDeclarationSyntax enumDecl) {
				var typeDef = CreateEnumDefinition(enumDecl, file, parent);
				targetList.Add(typeDef);
			} else if (member is DelegateDeclarationSyntax) {
				// 委托类型暂不处理，后续可扩展
			} else if (member is NamespaceDeclarationSyntax nsDecl) {
				foreach (var nsMember in nsDecl.Members) {
					ExtractTypeDefinitions(nsMember, file, parent, targetList);
				}
			}
		}

		/// <summary>
		/// 从 Roslyn 类型声明创建 IUnresolvedTypeDefinition。
		/// </summary>
		static RoslynUnresolvedTypeDefinitionAdapter CreateTypeDefinition(TypeDeclarationSyntax typeDecl, IUnresolvedFile file, IUnresolvedTypeDefinition parent)
		{
			NRTypeKind kind;
			switch (typeDecl.Keyword.Kind()) {
				case SyntaxKind.ClassKeyword:
					kind = NRTypeKind.Class;
					break;
				case SyntaxKind.StructKeyword:
					kind = NRTypeKind.Struct;
					break;
				case SyntaxKind.InterfaceKeyword:
					kind = NRTypeKind.Interface;
					break;
				default:
					kind = NRTypeKind.Class;
					break;
			}

			// 确定命名空间
			string namespaceName = GetNamespace(typeDecl);

			var typeDef = new RoslynUnresolvedTypeDefinitionAdapter(typeDecl.Identifier.ValueText, namespaceName, kind, file, parent);

			// 设置区域信息
			var startLinePos = typeDecl.GetLocation().GetLineSpan().StartLinePosition;
			var endLinePos = typeDecl.GetLocation().GetLineSpan().EndLinePosition;
			typeDef.Region = new DomRegion(
				new TextLocation(startLinePos.Line + 1, startLinePos.Character + 1),
				new TextLocation(endLinePos.Line + 1, endLinePos.Character + 1)
			);

			// 设置 BodyRegion（花括号范围）
			if (typeDecl.OpenBraceToken != null && !typeDecl.OpenBraceToken.IsKind(SyntaxKind.None) &&
			    typeDecl.CloseBraceToken != null && !typeDecl.CloseBraceToken.IsKind(SyntaxKind.None)) {
				var openBracePos = typeDecl.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition;
				var closeBracePos = typeDecl.CloseBraceToken.GetLocation().GetLineSpan().EndLinePosition;
				typeDef.BodyRegion = new DomRegion(
					new TextLocation(openBracePos.Line + 1, openBracePos.Character + 1),
					new TextLocation(closeBracePos.Line + 1, closeBracePos.Character + 1)
				);
			}

			// 设置可访问性
			typeDef.Accessibility = GetAccessibility(typeDecl.Modifiers);

			// 设置修饰符
			typeDef.IsStatic = typeDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
			typeDef.IsAbstract = typeDecl.Modifiers.Any(SyntaxKind.AbstractKeyword);
			typeDef.IsSealed = typeDecl.Modifiers.Any(SyntaxKind.SealedKeyword);
			typeDef.IsPartial = typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);

			return typeDef;
		}

		/// <summary>
		/// 从 Roslyn 枚举声明创建 IUnresolvedTypeDefinition。
		/// </summary>
		static RoslynUnresolvedTypeDefinitionAdapter CreateEnumDefinition(EnumDeclarationSyntax enumDecl, IUnresolvedFile file, IUnresolvedTypeDefinition parent)
		{
			string namespaceName = GetNamespace(enumDecl);
			var typeDef = new RoslynUnresolvedTypeDefinitionAdapter(enumDecl.Identifier.ValueText, namespaceName, NRTypeKind.Enum, file, parent);

			var startLinePos = enumDecl.GetLocation().GetLineSpan().StartLinePosition;
			var endLinePos = enumDecl.GetLocation().GetLineSpan().EndLinePosition;
			typeDef.Region = new DomRegion(
				new TextLocation(startLinePos.Line + 1, startLinePos.Character + 1),
				new TextLocation(endLinePos.Line + 1, endLinePos.Character + 1)
			);

			if (enumDecl.OpenBraceToken != null && !enumDecl.OpenBraceToken.IsKind(SyntaxKind.None) &&
			    enumDecl.CloseBraceToken != null && !enumDecl.CloseBraceToken.IsKind(SyntaxKind.None)) {
				var openBracePos = enumDecl.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition;
				var closeBracePos = enumDecl.CloseBraceToken.GetLocation().GetLineSpan().EndLinePosition;
				typeDef.BodyRegion = new DomRegion(
					new TextLocation(openBracePos.Line + 1, openBracePos.Character + 1),
					new TextLocation(closeBracePos.Line + 1, closeBracePos.Character + 1)
				);
			}

			typeDef.Accessibility = GetAccessibility(enumDecl.Modifiers);

			return typeDef;
		}

		/// <summary>
		/// 从类型声明中提取成员（方法、属性、字段等）。
		/// </summary>
		static void ExtractMembers(TypeDeclarationSyntax typeDecl, RoslynUnresolvedTypeDefinitionAdapter typeDef)
		{
			foreach (var member in typeDecl.Members) {
				var methodDecl = member as MethodDeclarationSyntax;
				if (methodDecl != null) {
					var m = CreateMethod(methodDecl.Identifier.ValueText, methodDecl, typeDef, NRSymbolKind.Method);
					typeDef.Members.Add(m);
					continue;
				}

				var constructorDecl = member as ConstructorDeclarationSyntax;
				if (constructorDecl != null) {
					var m = CreateMethod(".ctor", constructorDecl, typeDef, NRSymbolKind.Constructor);
					typeDef.Members.Add(m);
					continue;
				}

				var destructorDecl = member as DestructorDeclarationSyntax;
				if (destructorDecl != null) {
					var m = CreateMethod("Finalize", destructorDecl, typeDef, NRSymbolKind.Destructor);
					typeDef.Members.Add(m);
					continue;
				}

				var operatorDecl = member as OperatorDeclarationSyntax;
				if (operatorDecl != null) {
					var m = CreateMethod(operatorDecl.OperatorToken.ValueText, operatorDecl, typeDef, NRSymbolKind.Operator);
					typeDef.Members.Add(m);
					continue;
				}

				var propertyDecl = member as PropertyDeclarationSyntax;
				if (propertyDecl != null) {
					var m = new DefaultUnresolvedMethod(typeDef, propertyDecl.Identifier.ValueText);
					m.SymbolKind = NRSymbolKind.Property;
					m.UnresolvedFile = typeDef.UnresolvedFile;
					m.Accessibility = GetAccessibility(propertyDecl.Modifiers);
					m.IsStatic = propertyDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
					m.IsAbstract = propertyDecl.Modifiers.Any(SyntaxKind.AbstractKeyword);
					m.IsSealed = propertyDecl.Modifiers.Any(SyntaxKind.SealedKeyword);
					SetMemberRegions(propertyDecl, m);
					typeDef.Members.Add(m);
					continue;
				}

				var indexerDecl = member as IndexerDeclarationSyntax;
				if (indexerDecl != null) {
					var m = new DefaultUnresolvedMethod(typeDef, "this[]");
					m.SymbolKind = NRSymbolKind.Indexer;
					m.UnresolvedFile = typeDef.UnresolvedFile;
					m.Accessibility = GetAccessibility(indexerDecl.Modifiers);
					m.IsStatic = indexerDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
					m.IsAbstract = indexerDecl.Modifiers.Any(SyntaxKind.AbstractKeyword);
					SetMemberRegions(indexerDecl, m);
					typeDef.Members.Add(m);
					continue;
				}

				var eventDecl = member as EventDeclarationSyntax;
				if (eventDecl != null) {
					var m = new DefaultUnresolvedMethod(typeDef, eventDecl.Identifier.ValueText);
					m.SymbolKind = NRSymbolKind.Event;
					m.UnresolvedFile = typeDef.UnresolvedFile;
					m.Accessibility = GetAccessibility(eventDecl.Modifiers);
					m.IsStatic = eventDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
					SetMemberRegions(eventDecl, m);
					typeDef.Members.Add(m);
					continue;
				}

				var fieldDecl = member as FieldDeclarationSyntax;
				if (fieldDecl != null) {
					foreach (var variable in fieldDecl.Declaration.Variables) {
						var f = new DefaultUnresolvedField(typeDef, variable.Identifier.ValueText);
						f.UnresolvedFile = typeDef.UnresolvedFile;
						f.Accessibility = GetAccessibility(fieldDecl.Modifiers);
						f.IsStatic = fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
						f.IsReadOnly = fieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

						var varStartPos = variable.GetLocation().GetLineSpan().StartLinePosition;
						var varEndPos = variable.GetLocation().GetLineSpan().EndLinePosition;
						f.Region = new DomRegion(
							new TextLocation(varStartPos.Line + 1, varStartPos.Character + 1),
							new TextLocation(varEndPos.Line + 1, varEndPos.Character + 1)
						);

						typeDef.Members.Add(f);
					}
					continue;
				}

				// 嵌套类型已在 ExtractTypeDefinitions 中处理
			}
		}

		/// <summary>
		/// 创建方法成员。
		/// </summary>
		static DefaultUnresolvedMethod CreateMethod(string name, BaseMethodDeclarationSyntax methodDecl, IUnresolvedTypeDefinition declaringType, NRSymbolKind symbolKind)
		{
			var m = new DefaultUnresolvedMethod(declaringType, name);
			m.SymbolKind = symbolKind;
			m.UnresolvedFile = declaringType.UnresolvedFile;
			m.Accessibility = GetAccessibility(methodDecl.Modifiers);
			m.IsStatic = methodDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
			m.IsAbstract = methodDecl.Modifiers.Any(SyntaxKind.AbstractKeyword);
			m.IsSealed = methodDecl.Modifiers.Any(SyntaxKind.SealedKeyword);

			if (methodDecl.Modifiers.Any(SyntaxKind.AsyncKeyword))
				m.IsAsync = true;

			if (methodDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
				m.IsPartial = true;

			// 设置 HasBody
			m.HasBody = methodDecl.Body != null;

			SetMemberRegions(methodDecl, m);

			return m;
		}

		/// <summary>
		/// 为成员设置 Region 和 BodyRegion。
		/// </summary>
		static void SetMemberRegions(MemberDeclarationSyntax memberDecl, DefaultUnresolvedMethod method)
		{
			var startPos = memberDecl.GetLocation().GetLineSpan().StartLinePosition;
			var endPos = memberDecl.GetLocation().GetLineSpan().EndLinePosition;
			method.Region = new DomRegion(
				new TextLocation(startPos.Line + 1, startPos.Character + 1),
				new TextLocation(endPos.Line + 1, endPos.Character + 1)
			);

			var baseMethodDecl = memberDecl as BaseMethodDeclarationSyntax;
			if (baseMethodDecl != null && baseMethodDecl.Body != null) {
				var bodyStartPos = baseMethodDecl.Body.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition;
				var bodyEndPos = baseMethodDecl.Body.CloseBraceToken.GetLocation().GetLineSpan().EndLinePosition;
				method.BodyRegion = new DomRegion(
					new TextLocation(bodyStartPos.Line + 1, bodyStartPos.Character + 1),
					new TextLocation(bodyEndPos.Line + 1, bodyEndPos.Character + 1)
				);
			} else {
				// 属性/索引器等使用花括号
				var propertyDecl = memberDecl as BasePropertyDeclarationSyntax;
				if (propertyDecl != null && propertyDecl.AccessorList != null) {
					var accessorStartPos = propertyDecl.AccessorList.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition;
					var accessorEndPos = propertyDecl.AccessorList.CloseBraceToken.GetLocation().GetLineSpan().EndLinePosition;
					method.BodyRegion = new DomRegion(
						new TextLocation(accessorStartPos.Line + 1, accessorStartPos.Character + 1),
						new TextLocation(accessorEndPos.Line + 1, accessorEndPos.Character + 1)
					);
				}
			}
		}

		/// <summary>
		/// 从修饰符列表中获取可访问性。
		/// </summary>
		static NRAccessibility GetAccessibility(SyntaxTokenList modifiers)
		{
			if (modifiers.Any(SyntaxKind.PublicKeyword))
				return NRAccessibility.Public;
			if (modifiers.Any(SyntaxKind.PrivateKeyword))
				return NRAccessibility.Private;
			if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword))
				return NRAccessibility.ProtectedOrInternal;
			if (modifiers.Any(SyntaxKind.ProtectedKeyword))
				return NRAccessibility.Protected;
			if (modifiers.Any(SyntaxKind.InternalKeyword))
				return NRAccessibility.Internal;
			return NRAccessibility.None;
		}

		/// <summary>
		/// 获取语法节点所在的命名空间。
		/// </summary>
		static string GetNamespace(SyntaxNode node)
		{
			var ns = node.Parent as NamespaceDeclarationSyntax;
			if (ns == null)
				return string.Empty;

			// 递归获取完整命名空间名
			var parentNs = GetNamespace(ns);
			var currentName = ns.Name.ToString();
			if (!string.IsNullOrEmpty(parentNs))
				return parentNs + "." + currentName;
			return currentName;
		}

		public string FileName {
			get { return fileName; }
		}

		public DateTime? LastWriteTime { get; set; }

		public IList<IUnresolvedTypeDefinition> TopLevelTypeDefinitions {
			get { return topLevelTypeDefinitions; }
		}

		public IList<IUnresolvedAttribute> AssemblyAttributes {
			get { return EmptyList<IUnresolvedAttribute>.Instance; }
		}

		public IList<IUnresolvedAttribute> ModuleAttributes {
			get { return EmptyList<IUnresolvedAttribute>.Instance; }
		}

		public IList<Error> Errors {
			get { return errors; }
		}

		public IUnresolvedTypeDefinition GetTopLevelTypeDefinition(TextLocation location)
		{
			foreach (var td in topLevelTypeDefinitions) {
				if (td.Region.IsInside(location))
					return td;
			}
			return null;
		}

		public IUnresolvedTypeDefinition GetInnermostTypeDefinition(TextLocation location)
		{
			return GetInnermostTypeDefinition(topLevelTypeDefinitions, location);
		}

		/// <summary>
		/// 递归查找包含指定位置的最内层类型定义。
		/// </summary>
		IUnresolvedTypeDefinition GetInnermostTypeDefinition(IList<IUnresolvedTypeDefinition> typeDefs, TextLocation location)
		{
			foreach (var td in typeDefs) {
				if (td.Region.IsInside(location)) {
					// 先尝试在嵌套类型中查找
					var inner = GetInnermostTypeDefinition(td.NestedTypes, location);
					if (inner != null)
						return inner;
					return td;
				}
			}
			return null;
		}

		public IUnresolvedMember GetMember(TextLocation location)
		{
			var td = GetInnermostTypeDefinition(location);
			if (td != null) {
				foreach (var md in td.Members) {
					if (md.Region.IsInside(location) || md.BodyRegion.IsInside(location))
						return md;
				}
			}
			return null;
		}
	}
}
