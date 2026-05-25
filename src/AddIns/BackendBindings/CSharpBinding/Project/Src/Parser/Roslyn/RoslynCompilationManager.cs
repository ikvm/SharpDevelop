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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using ICSharpCode.Core;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Project;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSharpBinding.Parser.Roslyn
{
	/// <summary>
	/// 管理 Roslyn CSharpCompilation 的创建和更新。
	/// 为每个项目维护 CSharpCompilation 实例缓存，
	/// 并在项目引用变更时使缓存失效。
	/// </summary>
	public class RoslynCompilationManager
	{
		/// <summary>
		/// 缓存每个项目的 CSharpCompilation，键为项目文件全路径
		/// </summary>
		static readonly ConcurrentDictionary<string, CSharpCompilation> compilationCache = new ConcurrentDictionary<string, CSharpCompilation>();

		/// <summary>
		/// 缓存每个项目的引用哈希，用于检测引用是否变更
		/// </summary>
		static readonly ConcurrentDictionary<string, int> referenceHashCache = new ConcurrentDictionary<string, int>();

		/// <summary>
		/// 为指定项目创建或获取 Roslyn CSharpCompilation。
		/// 如果缓存失效（引用变更），则创建新的 Compilation。
		/// </summary>
		public static CSharpCompilation GetOrCreateCompilation(IProject project)
		{
			if (project == null)
				throw new ArgumentNullException(nameof(project));

			string cacheKey = project.FileName;

			// 计算当前引用的哈希值，检测是否需要刷新
			int currentHash = ComputeReferenceHash(project);
			referenceHashCache.TryGetValue(cacheKey, out int cachedHash);

			if (currentHash != cachedHash) {
				// 引用已变更，移除旧缓存
				CacheInvalidation(cacheKey);
			}

			return compilationCache.GetOrAdd(cacheKey, _ => CreateCompilation(project));
		}

		/// <summary>
		/// 使用更新后的源文件刷新 Compilation。
		/// 增量更新：仅替换变更文件的 SyntaxTree。
		/// </summary>
		public static CSharpCompilation UpdateCompilation(CSharpCompilation compilation, string fileName, SourceText newSource)
		{
			if (compilation == null)
				throw new ArgumentNullException(nameof(compilation));
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			var oldTree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == fileName);
			if (oldTree != null)
				return compilation.ReplaceSyntaxTree(oldTree, CSharpSyntaxTree.ParseText(newSource, path: fileName));
			else
				return compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(newSource, path: fileName));
		}

		/// <summary>
		/// 为单个文件创建最小 Compilation（用于非项目文件）。
		/// 包含基本的 .NET Framework 引用，足以进行基本的语义分析。
		/// </summary>
		public static CSharpCompilation CreateCompilationForSingleFile(string fileName, string source)
		{
			var syntaxTree = CSharpSyntaxTree.ParseText(source, path: fileName);

			var references = GetFrameworkReferences();
			var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

			return CSharpCompilation.Create(
				assemblyName: "SingleFileCompilation",
				syntaxTrees: new[] { syntaxTree },
				references: references,
				options: options
			);
		}

		/// <summary>
		/// 使指定项目的编译缓存失效
		/// </summary>
		public static void CacheInvalidation(string projectFileName)
		{
			CSharpCompilation removed;
			compilationCache.TryRemove(projectFileName, out removed);
			int removedHash;
			referenceHashCache.TryRemove(projectFileName, out removedHash);
		}

		/// <summary>
		/// 清除所有编译缓存
		/// </summary>
		public static void ClearAllCache()
		{
			compilationCache.Clear();
			referenceHashCache.Clear();
		}

		/// <summary>
		/// 从 IProject 创建完整的 CSharpCompilation
		/// </summary>
		static CSharpCompilation CreateCompilation(IProject project)
		{
			// 1. 获取编译选项
			var options = GetCompilationOptions(project);

			// 2. 收集所有引用
			var references = new List<MetadataReference>();
			references.AddRange(GetFrameworkReferences());
			references.AddRange(GetProjectReferences(project));

			// 3. 收集项目中的源文件
			var syntaxTrees = GetProjectSyntaxTrees(project);

			// 4. 创建 Compilation
			var compilation = CSharpCompilation.Create(
				assemblyName: project.AssemblyName ?? "Unknown",
				syntaxTrees: syntaxTrees,
				references: references,
				options: options
			);

			// 更新引用哈希缓存
			string cacheKey = project.FileName;
			referenceHashCache[cacheKey] = ComputeReferenceHash(project);

			return compilation;
		}

		/// <summary>
		/// 从项目获取 MetadataReference 列表。
		/// 包括程序集引用和项目引用。
		/// </summary>
		static IEnumerable<MetadataReference> GetProjectReferences(IProject project)
		{
			var references = new List<MetadataReference>();

			try {
				var referenceItems = project.ResolveAssemblyReferences(CancellationToken.None);
				foreach (var reference in referenceItems) {
					var projectRef = reference as ProjectReferenceProjectItem;
					if (projectRef != null) {
						// 项目引用：使用引用项目的输出程序集路径
						var refProject = projectRef.ReferencedProject;
						if (refProject != null) {
							var outputPath = refProject.OutputAssemblyFullPath;
							if (outputPath != null && File.Exists(outputPath)) {
								try {
									references.Add(MetadataReference.CreateFromFile(outputPath));
								} catch (IOException ex) {
									LoggingService.Warn("无法加载项目引用: " + outputPath, ex);
								}
							}
						}
					} else {
						// 程序集引用：使用引用的文件路径
						var refPath = reference.FileName;
						if (refPath != null && File.Exists(refPath)) {
							try {
								references.Add(MetadataReference.CreateFromFile(refPath));
							} catch (IOException ex) {
								LoggingService.Warn("无法加载程序集引用: " + refPath, ex);
							} catch (BadImageFormatException ex) {
								LoggingService.Warn("无效的程序集引用: " + refPath, ex);
							}
						}
					}
				}
			} catch (Exception ex) {
				LoggingService.Warn("解析项目引用时出错: " + project.Name, ex);
			}

			return references;
		}

		/// <summary>
		/// 获取 .NET Framework 基本引用。
		/// 使用运行时目录来定位框架程序集。
		/// </summary>
		static IEnumerable<MetadataReference> GetFrameworkReferences()
		{
			var references = new List<MetadataReference>();

			// 获取 .NET Framework 运行时目录
			string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();

			// 基本框架程序集列表
			string[] frameworkAssemblies = {
				"mscorlib.dll",
				"System.dll",
				"System.Core.dll",
				"System.Runtime.dll",
				"System.Collections.dll",
				"System.Linq.dll",
				"System.Threading.dll",
				"System.Threading.Tasks.dll",
				"System.Text.RegularExpressions.dll",
				"System.Globalization.dll",
				"System.Reflection.dll",
				"System.IO.dll",
				"System.Xml.dll",
				"System.Xml.Linq.dll",
				"Microsoft.CSharp.dll",
				"System.Net.dll",
				"System.Data.dll"
			};

			foreach (var assemblyName in frameworkAssemblies) {
				string path = Path.Combine(runtimeDir, assemblyName);
				if (File.Exists(path)) {
					try {
						references.Add(MetadataReference.CreateFromFile(path));
					} catch (IOException ex) {
						LoggingService.Warn("无法加载框架程序集: " + path, ex);
					}
				}
			}

			// 如果运行时目录中找不到某些程序集，尝试 .NET Framework 目录
			if (references.Count == 0 || !references.Any()) {
				string frameworkDir = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.Windows),
					@"Microsoft.NET\Framework\v4.0.30319");

				if (Directory.Exists(frameworkDir)) {
					foreach (var assemblyName in frameworkAssemblies) {
						string path = Path.Combine(frameworkDir, assemblyName);
						if (File.Exists(path)) {
							try {
								references.Add(MetadataReference.CreateFromFile(path));
							} catch (IOException ex) {
								LoggingService.Warn("无法加载框架程序集: " + path, ex);
							}
						}
					}
				}
			}

			return references;
		}

		/// <summary>
		/// 从 IProject 获取 CSharpCompilationOptions。
		/// 根据项目的输出类型、平台目标、安全设置等创建编译选项。
		/// </summary>
		static CSharpCompilationOptions GetCompilationOptions(IProject project)
		{
			var compilableProject = project as CompilableProject;
			if (compilableProject == null) {
				// 非编译项目使用默认选项
				return new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
			}

			// 确定输出类型
			OutputKind outputKind;
			switch (compilableProject.OutputType) {
				case OutputType.Exe:
					outputKind = OutputKind.ConsoleApplication;
					break;
				case OutputType.WinExe:
					outputKind = OutputKind.WindowsApplication;
					break;
				case OutputType.Library:
					outputKind = OutputKind.DynamicallyLinkedLibrary;
					break;
				case OutputType.Module:
					outputKind = OutputKind.NetModule;
					break;
				default:
					outputKind = OutputKind.DynamicallyLinkedLibrary;
					break;
			}

			// 确定平台目标
			Platform platform = Platform.AnyCpu;
			string platformTarget = compilableProject.GetEvaluatedProperty("PlatformTarget");
			if (!string.IsNullOrEmpty(platformTarget)) {
				switch (platformTarget.ToLowerInvariant()) {
					case "x86":
						platform = Platform.X86;
						break;
					case "x64":
						platform = Platform.X64;
						break;
					case "itanium":
						platform = Platform.Itanium;
						break;
					case "anycpu":
						platform = Platform.AnyCpu;
						break;
					case "anycpu32bitpreferred":
						platform = Platform.AnyCpu32BitPreferred;
						break;
				}
			}

			// 检查是否允许不安全代码
			bool allowUnsafe = compilableProject.GetEvaluatedProperty("AllowUnsafeBlocks")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

			// 检查是否启用溢出检查
			bool checkOverflow = compilableProject.GetEvaluatedProperty("CheckForOverflowUnderflow")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

			var options = new CSharpCompilationOptions(
				outputKind,
				platform: platform,
				allowUnsafe: allowUnsafe,
				checkOverflow: checkOverflow,
				optimizationLevel: OptimizationLevel.Debug,
				sourceReferenceResolver: SourceFileResolver.Default
			);

			return options;
		}

		/// <summary>
		/// 获取项目中所有源文件的 SyntaxTree 列表。
		/// </summary>
		static IEnumerable<SyntaxTree> GetProjectSyntaxTrees(IProject project)
		{
			var syntaxTrees = new List<SyntaxTree>();

			foreach (var item in project.GetItemsOfType(ItemType.Compile)) {
				var fileItem = item as FileProjectItem;
				if (fileItem == null || string.IsNullOrEmpty(fileItem.FileName))
					continue;

				string filePath = fileItem.FileName;
				if (!File.Exists(filePath))
					continue;

				try {
					string source = File.ReadAllText(filePath);
					var syntaxTree = CSharpSyntaxTree.ParseText(source, path: filePath);
					syntaxTrees.Add(syntaxTree);
				} catch (IOException ex) {
					LoggingService.Warn("无法读取源文件: " + filePath, ex);
				} catch (UnauthorizedAccessException ex) {
					LoggingService.Warn("无权限读取源文件: " + filePath, ex);
				}
			}

			return syntaxTrees;
		}

		/// <summary>
		/// 计算项目引用的哈希值，用于检测引用是否变更。
		/// </summary>
		static int ComputeReferenceHash(IProject project)
		{
			int hash = 17;
			try {
				var referenceItems = project.ResolveAssemblyReferences(CancellationToken.None);
				foreach (var reference in referenceItems) {
					hash = hash * 31 + (reference.FileName?.GetHashCode() ?? 0);
				}
			} catch {
				// 如果无法解析引用，返回不同的哈希值以强制刷新
				hash = -1;
			}

			// 包含项目属性的哈希，以检测编译选项变更
			var compilableProject = project as CompilableProject;
			if (compilableProject != null) {
				hash = hash * 31 + (compilableProject.OutputType.GetHashCode());
				hash = hash * 31 + (compilableProject.GetEvaluatedProperty("PlatformTarget")?.GetHashCode() ?? 0);
				hash = hash * 31 + (compilableProject.GetEvaluatedProperty("AllowUnsafeBlocks")?.GetHashCode() ?? 0);
				hash = hash * 31 + (compilableProject.GetEvaluatedProperty("DefineConstants")?.GetHashCode() ?? 0);
			}

			return hash;
		}
	}
}
