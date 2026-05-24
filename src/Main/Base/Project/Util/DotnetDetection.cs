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

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;


namespace ICSharpCode.SharpDevelop
{
	public static class DotnetDetection
	{
		/// <summary>
		/// Gets whether .NET 3.5 is installed and has at least SP1.  
		/// </summary>
		public static bool IsDotnet35SP1Installed()
		{
			using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5")) {
				return key != null && (key.GetValue("SP") as int?) >= 1;
			}
		}

		/// <summary>
		/// Gets whether any .NET 4.x runtime is installed.
		/// </summary>		
		public static bool IsDotnet40Installed()
		{
			return true; // required for SD to run
		}
		
		/// <summary>
		/// Gets whether the .NET 4.5 runtime (or a later version of .NET 4.x) is installed.
		/// </summary>
		public static bool IsDotnet45Installed()
		{
			return GetDotnet4Release() >= 378389;
		}
		
		/// <summary>
		/// Gets whether the .NET 4.5.1 runtime (or a later version of .NET 4.x) is installed.
		/// </summary>
		public static bool IsDotnet451Installed()
		{
			// According to: http://blogs.msdn.com/b/astebner/archive/2013/11/11/10466402.aspx
			// 378675 is .NET 4.5.1 on Win8
			// 378758 is .NET 4.5.1 on Win7
			return GetDotnet4Release() >= 378675;
		}
		
		public static bool IsDotnet452Installed()
		{
			// 379893 is .NET 4.5.2 on my Win7 machine
			return GetDotnet4Release() >= 379893;
		}
		
		public static bool IsDotnet46Installed()
		{
			// 393295 - On Windows 10
			// 393297 - On all other Windows operating systems
			return GetDotnet4Release() >= 393295;
		}

		public static bool IsDotnet461Installed()
		{
			// 394254 - On Windows 10 November Update systems
			// 394271 - On all other Windows operating systems (including Windows 10)
			return GetDotnet4Release() >= 394254;
		}
		
		public static bool IsDotnet462Installed()
		{
			// 394802 - On Windows 10 Anniversary Update and Windows Server 2016
			// 394806 - On all other Windows operating systems (including other Windows 10 operating systems)
			return GetDotnet4Release() >= 394802;
		}
		
		public static bool IsDotnet47Installed()
		{
			// 460798 - On Windows 10 Creators Update
			// 460805- On all other Windows operating systems (including other Windows 10 operating systems)
			return GetDotnet4Release() >= 460798;
		}
		
		public static bool IsDotnet471Installed()
		{
			// 461308 - On Windows 10 Fall Creators Update and Windows Server, version 1709
			// 461310 - On all other Windows operating systems (including other Windows 10 operating systems)
			return GetDotnet4Release() >= 461308;
		}
		
		public static bool IsDotnet472Installed()
		{
			// 461808 - On Windows 10 April 2018 Update and Windows Server, version 1803
			// 461814 - On all Windows operating systems other than Windows 10 April 2018 Update and Windows Server, version 1803
			return GetDotnet4Release() >= 461808;
		}
		
		public static bool IsDotnet48Installed()
		{
			// 528040 - On Windows 10 May 2019 Update and Windows 10 November 2019 Update
			// 528049 - On all other Windows operating systems (including other Windows 10 operating systems)
			// 528372 - On Windows 10 May 2020 Update, October 2020 Update, May 2021 Update, November 2021 Update, and 2022 Update
			// 528449 - On Windows 11 and Windows Server 2022
			return GetDotnet4Release() >= 528040;
		}
		
		public static bool IsDotnet481Installed()
		{
			// 533320 - On Windows 11 2022 Update
			// 533325 - All other Windows operating systems
			return GetDotnet4Release() >= 533320;
		}
		
		/// <summary>
		/// Gets the .NET 4.x release number.
		/// The numbers are documented on http://msdn.microsoft.com/en-us/library/hh925568.aspx
		/// </summary>
		static int? GetDotnet4Release()
		{
			using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")) {
				if (key != null)
					return key.GetValue("Release") as int?;
			}
			return null;
		}
		
		/// <summary>
		/// Gets whether the Microsoft Build Tools 2013 (MSBuild 12.0) is installed.
		/// </summary>
		public static bool IsBuildTools2013Installed()
		{
			// HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\DevDiv\BuildTools\Servicing\12.0
			using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DevDiv\BuildTools\Servicing\12.0\MSBuild")) {
				return key != null && key.GetValue("Install") as int? >= 1;
			}
		}
		
		/// <summary>
		/// Gets whether the Microsoft Build Tools 2015 (MSBuild 14.0) is installed.
		/// </summary>
		public static bool IsBuildTools2015Installed()
		{
			// HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\DevDiv\BuildTools\Servicing\14.0
			using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DevDiv\BuildTools\Servicing\14.0\MSBuild")) {
				return key != null && key.GetValue("Install") as int? >= 1;
			}
		}
	}

	public class DotNetCoreDetector
	{
		public static List<DotNetVersionInfo> DetectDotNetCoreVersions()
		{
			var versions = new List<DotNetVersionInfo>();

			try
			{
				// 检测 .NET SDK 版本
				var sdkVersions = GetDotNetSdkVersions();
				versions.AddRange(sdkVersions);

				// 检测 .NET 运行时版本
				var runtimeVersions = GetDotNetRuntimeVersions();
				versions.AddRange(runtimeVersions);
			}
			catch (Exception ex)
			{
				// 记录错误日志
				Debug.WriteLine($"检测.NET Core版本时出错: {ex.Message}");
			}

			return versions;
		}

		private static List<DotNetVersionInfo> GetDotNetSdkVersions()
		{
			var versions = new List<DotNetVersionInfo>();

			try
			{
				var processInfo = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = "--list-sdks",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				};

				using (var process = Process.Start(processInfo))
				{
					var output = process.StandardOutput.ReadToEnd();
					process.WaitForExit();

					// 解析输出，例如：8.0.100 [C:\program files\dotnet\sdk]
					var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

					foreach (var line in lines)
					{
						var match = Regex.Match(line, @"^(\d+\.\d+\.\d+)\s+$$(.+)$$$");
						if (match.Success)
						{
							versions.Add(new DotNetVersionInfo
							{
								Version = match.Groups[1].Value,
								Type = "SDK",
								InstallationPath = match.Groups[2].Value
							});
						}
					}
				}
			}
			catch (Exception)
			{
				// dotnet 命令可能不存在
			}

			return versions;
		}

		private static List<DotNetVersionInfo> GetDotNetRuntimeVersions()
		{
			var versions = new List<DotNetVersionInfo>();

			try
			{
				var processInfo = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = "--list-runtimes",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				};

				using (var process = Process.Start(processInfo))
				{
					var output = process.StandardOutput.ReadToEnd();
					process.WaitForExit();

					// 解析输出，例如：Microsoft.NETCore.App 8.0.6 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
					var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

					foreach (var line in lines)
					{
						var match = Regex.Match(line, @"^(\S+)\s+(\d+\.\d+\.\d+)\s+$$(.+)$$$");
						if (match.Success)
						{
							versions.Add(new DotNetVersionInfo
							{
								RuntimeName = match.Groups[1].Value,
								Version = match.Groups[2].Value,
								Type = "Runtime",
								InstallationPath = match.Groups[3].Value
							});
						}
					}
				}
			}
			catch (Exception)
			{
				// dotnet 命令可能不存在
			}

			return versions;
		}
	}

	public class DotNetVersionInfo
	{
		public string Version { get; set; }
		public string Type { get; set; } // SDK 或 Runtime
		public string RuntimeName { get; set; } // 运行时名称，如 Microsoft.NETCore.App
		public string InstallationPath { get; set; }
	}

	public class DotNetFileSystemDetector
	{
		public static List<DotNetVersionInfo> DetectViaFileSystem()
		{
			var versions = new List<DotNetVersionInfo>();

			// 常见的 .NET 安装路径
			var possiblePaths = new[]
			{
						Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
						Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet"),
						Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "dotnet"),
						Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "dotnet")
				};

			foreach (var basePath in possiblePaths)
			{
				if (Directory.Exists(basePath))
				{
					// 检测 SDK 版本
					var sdkPath = Path.Combine(basePath, "sdk");
					if (Directory.Exists(sdkPath))
					{
						var sdkDirs = Directory.GetDirectories(sdkPath);
						foreach (var sdkDir in sdkDirs)
						{
							var dirName = Path.GetFileName(sdkDir);
							if (Regex.IsMatch(dirName, @"^\d+\.\d+\.\d+"))
							{
								versions.Add(new DotNetVersionInfo
								{
									Version = dirName,
									Type = "SDK",
									InstallationPath = sdkDir
								});
							}
						}
					}

					// 检测运行时版本
					var sharedPath = Path.Combine(basePath, "shared");
					if (Directory.Exists(sharedPath))
					{
						var runtimeDirs = Directory.GetDirectories(sharedPath);
						foreach (var runtimeDir in runtimeDirs)
						{
							var runtimeName = Path.GetFileName(runtimeDir);
							var versionDirs = Directory.GetDirectories(runtimeDir);

							foreach (var versionDir in versionDirs)
							{
								var version = Path.GetFileName(versionDir);
								if (Regex.IsMatch(version, @"^\d+\.\d+\.\d+"))
								{
									versions.Add(new DotNetVersionInfo
									{
										RuntimeName = runtimeName,
										Version = version,
										Type = "Runtime",
										InstallationPath = versionDir
									});
								}
							}
						}
					}
				}
			}

			return versions;
		}
	}

	public class DotNetFrameworkDetector
	{
		public static List<DotNetVersionInfo> DetectFrameworkVersions()
		{
			var versions = new List<DotNetVersionInfo>();

			// 检测 .NET Framework 4.x
			try
			{
				using (var ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
				{
					if (ndpKey != null)
					{
						var release = ndpKey.GetValue("Release") as int?;
						if (release.HasValue)
						{
							var version = GetFrameworkVersionFromRelease(release.Value);
							if (version != null)
							{
								versions.Add(new DotNetVersionInfo
								{
									Version = version,
									Type = "Framework",
									RuntimeName = ".NET Framework"
								});
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"检测.NET Framework时出错: {ex.Message}");
			}

			return versions;
		}

		private static string GetFrameworkVersionFromRelease(int release)
		{
			// 根据 Release DWORD 值确定 .NET Framework 版本
			// 参考：https://docs.microsoft.com/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
			if (release >= 533320) return "4.8.1";
			if (release >= 528040) return "4.8";
			if (release >= 461808) return "4.7.2";
			if (release >= 461308) return "4.7.1";
			if (release >= 460798) return "4.7";
			if (release >= 394802) return "4.6.2";
			if (release >= 394254) return "4.6.1";
			if (release >= 393295) return "4.6";
			if (release >= 379893) return "4.5.2";
			if (release >= 378675) return "4.5.1";
			if (release >= 378389) return "4.5";

			return null;
		}
	}

}
