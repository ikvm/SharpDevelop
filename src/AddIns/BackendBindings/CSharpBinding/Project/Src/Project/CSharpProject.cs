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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Project.Converter;
using ICSharpCode.SharpDevelop.Refactoring;
using Microsoft.CSharp;

namespace CSharpBinding
{
	/// <summary>
	/// IProject implementation for .csproj files.
	/// </summary>
	public class CSharpProject : CompilableProject
	{
		Properties globalPreferences;
		FileName globalSettingsFileName;
		
		public override IAmbience GetAmbience()
		{
			return new CSharpAmbience();
		}
		
		public override string Language {
			get { return CSharpProjectBinding.LanguageName; }
		}
		
		public Version LanguageVersion {
			get {
				string toolsVersion;
				lock (SyncRoot) toolsVersion = this.ToolsVersion;
				Version version;
				if (!Version.TryParse(toolsVersion, out version))
					version = new Version(4, 0); // use 4.0 as default if ToolsVersion attribute is missing/malformed
				if (version == new Version(4, 0) && DotnetDetection.IsDotnet45Installed())
					return new Version(5, 0);
				return version;
			}
		}
		
		void Init()
		{
			globalPreferences = new Properties();
			
			reparseReferencesSensitiveProperties.Add("TargetFrameworkVersion");
			reparseCodeSensitiveProperties.Add("DefineConstants");
			reparseCodeSensitiveProperties.Add("AllowUnsafeBlocks");
			reparseCodeSensitiveProperties.Add("CheckForOverflowUnderflow");
		}
		
		public CSharpProject(ProjectLoadInformation loadInformation)
			: base(loadInformation)
		{
			Init();
			if (loadInformation.InitializeTypeSystem)
				InitializeProjectContent(new CSharpProjectContent());
		}
		
		public const string DefaultTargetsFile = @"$(MSBuildToolsPath)\Microsoft.CSharp.targets";
		
		public CSharpProject(ProjectCreateInformation info)
			: base(info)
		{
			Init();
			
			this.AddImport(DefaultTargetsFile, null);
			
			SetProperty("Debug", null, "CheckForOverflowUnderflow", "True",
			            PropertyStorageLocations.ConfigurationSpecific, true);
			SetProperty("Release", null, "CheckForOverflowUnderflow", "False",
			            PropertyStorageLocations.ConfigurationSpecific, true);
			
			SetProperty("Debug", null, "DefineConstants", "DEBUG;TRACE",
			            PropertyStorageLocations.ConfigurationSpecific, false);
			SetProperty("Release", null, "DefineConstants", "TRACE",
			            PropertyStorageLocations.ConfigurationSpecific, false);
			
			if (info.InitializeTypeSystem)
				InitializeProjectContent(new CSharpProjectContent());
		}
		
		public override Task<bool> BuildAsync(ProjectBuildOptions options, IBuildFeedbackSink feedbackSink, IProgressMonitor progressMonitor)
		{
			if (this.MinimumSolutionVersion == SolutionFormatVersion.VS2019) {
				return SD.MSBuildEngine.BuildAsync(
					this, options, feedbackSink, progressMonitor.CancellationToken,
					new [] { Path.Combine(FileUtility.ApplicationRootPath, @"bin\SharpDevelop.CheckMSBuild35Features.targets") });
			} else {
				return base.BuildAsync(options, feedbackSink, progressMonitor);
			}
		}
		
		volatile CompilerSettings compilerSettings;
		
		public CompilerSettings CompilerSettings {
			get {
				if (compilerSettings == null)
					CreateCompilerSettings();
				return compilerSettings;
			}
		}
		
		public Properties GlobalPreferences
		{
			get {
				return globalPreferences;
			}
		}
		
		public override void ProjectLoaded()
		{
			base.ProjectLoaded();
			
			// Load SD settings file
			globalSettingsFileName = new FileName(FileName + ".sdsettings");
			if (File.Exists(globalSettingsFileName)) {
				globalPreferences = Properties.Load(globalSettingsFileName);
			}
			if (globalPreferences == null)
				globalPreferences = new Properties();
		}
		
		public override void Save(string fileName)
		{
			// Save project extensions
			if (globalPreferences != null && globalPreferences.IsDirty) {
				globalPreferences.Save(new FileName(fileName + ".sdsettings"));
				globalPreferences.IsDirty = false;
			}
			base.Save(fileName);
		}
		
		protected override object CreateCompilerSettings()
		{
			// This method gets called when the project content is first created;
			// or when any of the ReparseSensitiveProperties has changed.
			CompilerSettings settings = new CompilerSettings();
			settings.AllowUnsafeBlocks = GetBoolProperty("AllowUnsafeBlocks") ?? false;
			settings.CheckForOverflow = GetBoolProperty("CheckForOverflowUnderflow") ?? false;
			
			string symbols = GetEvaluatedProperty("DefineConstants");
			if (symbols != null) {
				foreach (string symbol in symbols.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
					settings.ConditionalSymbols.Add(symbol.Trim());
				}
			}
			settings.Freeze();
			compilerSettings = settings;
			return settings;
		}
		
		bool? GetBoolProperty(string propertyName)
		{
			string val = GetEvaluatedProperty(propertyName);
			if ("true".Equals(val, StringComparison.OrdinalIgnoreCase))
				return true;
			if ("false".Equals(val, StringComparison.OrdinalIgnoreCase))
				return false;
			return null;
		}
		
		protected override ProjectBehavior CreateDefaultBehavior()
		{
			return new CSharpProjectBehavior(this, base.CreateDefaultBehavior());
		}
		
		public override CodeDomProvider CreateCodeDomProvider()
		{
			return new CSharpCodeProvider();
		}
		
		ILanguageBinding language;
		
		public override ILanguageBinding LanguageBinding {
			get {
				if (language == null)
					language = SD.LanguageService.GetLanguageByName("CSharp");
				return language;
			}
		}
	}
	
	public class CSharpProjectBehavior : ProjectBehavior
	{
		public CSharpProjectBehavior(CSharpProject project, ProjectBehavior next = null)
			: base(project, next)
		{
			
		}
		
		public override ItemType GetDefaultItemType(string fileName)
		{
			if (string.Equals(Path.GetExtension(fileName), ".cs", StringComparison.OrdinalIgnoreCase))
				return ItemType.Compile;
			else
				return base.GetDefaultItemType(fileName);
		}
		
		static readonly CompilerVersion msbuild80 = new CompilerVersion(new Version(8, 0), "C# 8.0");
		static readonly CompilerVersion msbuild100 = new CompilerVersion(new Version(10, 0), "C# 10.0");
		static readonly CompilerVersion msbuild140 = new CompilerVersion(new Version(14, 0), "C# 14.0");// DotnetDetection.IsDotnet45Installed() ? "C# 14.0" : "C# 4.0");
		
		public override CompilerVersion CurrentCompilerVersion {
			get {
				switch (Project.MinimumSolutionVersion) {
					case SolutionFormatVersion.VS2019:
						return msbuild80;
					case SolutionFormatVersion.VS2022:
						return msbuild100;
					case SolutionFormatVersion.VS2026:
						return msbuild140;
					default:
						throw new NotSupportedException();
				}
			}
		}
		
		public override IEnumerable<CompilerVersion> GetAvailableCompilerVersions()
		{
			List<CompilerVersion> versions = new List<CompilerVersion>();
			//if (DotnetDetection.IsDotnet35SP1Installed()) {
				versions.Add(msbuild80);
				versions.Add(msbuild100);
			//}
			versions.Add(msbuild140);
			return versions;
		}
		
		public override ISymbolSearch PrepareSymbolSearch(ISymbol entity)
		{
			return CompositeSymbolSearch.Create(new CSharpSymbolSearch(Project, entity), base.PrepareSymbolSearch(entity));
		}
	}
}
