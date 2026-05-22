#!/usr/bin/env python3
"""
Convert all .csproj files in SharpDevelop.sln from old-style to SDK-style format.
Skips .vbproj files (VB.NET projects are handled separately).
"""

import os
import re
import xml.etree.ElementTree as ET
import shutil

SOLUTION_DIR = r"d:\PT\3rds\SharpDevelop"
SLN_FILE = os.path.join(SOLUTION_DIR, "SharpDevelop.sln")

# ---------- Helpers ----------

def get_csproj_paths_from_sln(sln_path):
    """Extract .csproj file paths (not .vbproj) from .sln"""
    projects = []
    with open(sln_path, 'r', encoding='utf-8') as f:
        for line in f:
            m = re.match(r'Project\("\{[^}]+\}"\) = "[^"]+", "([^"]+\.csproj)",', line)
            if m:
                rel_path = m.group(1)
                abs_path = os.path.normpath(os.path.join(os.path.dirname(sln_path), rel_path))
                projects.append((rel_path, abs_path))
    return projects

def read_file_content(path):
    with open(path, 'r', encoding='utf-8') as f:
        return f.read()

def write_file_content(path, content):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)

def namespace_to_tag(tag):
    """Strip the MSBuild namespace from a tag name."""
    if tag.startswith('{http://schemas.microsoft.com/developer/msbuild/2003}'):
        return tag.split('}', 1)[1]
    return tag

def is_sdk_format(csproj_content):
    """Check if already SDK format."""
    return 'Sdk="Microsoft.NET.Sdk"' in csproj_content

# ---------- Main conversion ----------

def convert_csproj_to_sdk(csproj_path, rel_path):
    """Convert a single .csproj from old-style to SDK-style."""
    print(f"Converting: {rel_path}")
    
    content = read_file_content(csproj_path)
    
    if is_sdk_format(content):
        print(f"  Already SDK format, skipping.")
        return
    
    # Parse the old XML
    root = ET.fromstring(content)
    
    # Namespace map
    ns = {'msbuild': 'http://schemas.microsoft.com/developer/msbuild/2003'}
    
    # ---- Extract properties ----
    props = {}
    debug_props = {}
    release_props = {}
    platform_props = {}
    config_platform_props_debug = {}
    config_platform_props_release = {}
    
    for pg in root.findall('msbuild:PropertyGroup', ns):
        condition = pg.get('Condition', '')
        if not condition:
            for child in pg:
                props[namespace_to_tag(child.tag)] = child.text or ''
        elif " '$(Configuration)' == 'Debug' " in condition:
            for child in pg:
                debug_props[namespace_to_tag(child.tag)] = child.text or ''
        elif " '$(Configuration)' == 'Release' " in condition:
            for child in pg:
                release_props[namespace_to_tag(child.tag)] = child.text or ''
        elif " '$(Platform)' == 'AnyCPU' " in condition or " '$(Platform)' == 'x86' " in condition:
            for child in pg:
                platform_props[namespace_to_tag(child.tag)] = child.text or ''
        elif " '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' " in condition:
            for child in pg:
                config_platform_props_debug[namespace_to_tag(child.tag)] = child.text or ''
        elif " '$(Configuration)|$(Platform)' == 'Release|AnyCPU' " in condition:
            for child in pg:
                config_platform_props_release[namespace_to_tag(child.tag)] = child.text or ''
    
    # Helper to get a property value from any group
    def get_prop(name):
        for d in [config_platform_props_debug, config_platform_props_release, debug_props, release_props, platform_props, props]:
            if name in d:
                return d[name]
        return None
    
    # ---- Extract assembly references ----
    framework_refs = set()
    nuget_refs = {}  # name -> hintpath
    other_refs = {}  # name -> {hintpath, private}
    
    for ref_elem in root.findall('.//msbuild:Reference', ns):
        include = ref_elem.get('Include', '')
        hint_path = ref_elem.find('msbuild:HintPath', ns)
        is_private = ref_elem.find('msbuild:Private', ns)
        hint_path_val = hint_path.text if hint_path is not None else ''
        private_val = is_private.text if is_private is not None else ''
        
        # System/GAC framework references
        system_refs = {'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Data',
                       'System.Data.DataSetExtensions', 'System.Drawing', 'System.Windows.Forms',
                       'System.Configuration', 'System.Design', 'System.Web.Services',
                       'System.Xaml', 'System.ServiceModel', 'System.Printing',
                       'System.Management.Automation', 'System.Runtime.Remoting',
                       'PresentationCore', 'PresentationFramework', 'PresentationFramework.Aero',
                       'WindowsBase', 'WindowsFormsIntegration', 'ReachFramework',
                       'Microsoft.CSharp', 'Microsoft.Build', 'Microsoft.Build.Framework',
                       'Microsoft.Build.Utilities.v4.0'}
        
        # Extract name without version/strong name
        name = include.split(',')[0].strip()
        
        # Determine if NuGet package or direct DLL
        if hint_path_val and ('packages\\' in hint_path_val or 'packages/' in hint_path_val):
            # It's a NuGet package reference
            package_match = re.match(r'.*?packages[\\/]([^\\/]+)[\\/]', hint_path_val)
            if package_match:
                pkg_name_with_version = package_match.group(1)
                # Try to extract package name and version
                # Common pattern: PackageName.Version
                parts = pkg_name_with_version.rsplit('.', 2)
                # Heuristic: try to split name and version
                if name in system_refs:
                    framework_refs.add(name)
                else:
                    if name not in nuget_refs:
                        nuget_refs[name] = hint_path_val
        elif name in system_refs:
            framework_refs.add(name)
        else:
            other_refs[name] = {'hintpath': hint_path_val, 'private': private_val}
    
    # ---- Extract project references ----
    project_refs = []
    for pr in root.findall('.//msbuild:ProjectReference', ns):
        include = pr.get('Include', '')
        proj_guid = pr.find('msbuild:Project', ns)
        name = pr.find('msbuild:Name', ns)
        is_private = pr.find('msbuild:Private', ns)
        
        project_refs.append({
            'include': include,
            'project': proj_guid.text if proj_guid is not None else '',
            'name': name.text if name is not None else '',
            'private': is_private.text if is_private is not None else '',
        })
    
    # ---- Extract all item types ----
    items_by_type = {}
    for item_group in root.findall('msbuild:ItemGroup', ns):
        for child in item_group:
            tag = namespace_to_tag(child.tag)
            if tag in ('Reference', 'ProjectReference', 'Folder'):
                continue  # Handle separately or skip
            if tag not in items_by_type:
                items_by_type[tag] = []
            
            item_data = {'include': child.get('Include', '')}
            for sub in child:
                sub_tag = namespace_to_tag(sub.tag)
                item_data[sub_tag] = sub.text or ''
                # Handle metadata attributes
                for attr_name, attr_val in sub.attrib.items():
                    if attr_name != '{http://www.w3.org/XML/1998/namespace}space' and not attr_name.startswith('{'):
                        item_data[f'@{sub_tag}_{attr_name}'] = attr_val
            
            items_by_type[tag].append(item_data)
    
    # ---- Determine output type ----
    output_type = get_prop('OutputType')
    if not output_type:
        output_type = 'Library'
    
    # Check for WPF app (has app manifest, xaml application)
    has_wpf = any('PresentationCore' in framework_refs or 'PresentationFramework' in framework_refs for _ in [1])
    has_winforms = any('System.Windows.Forms' in framework_refs for _ in [1])
    
    # Check for WinExe
    is_winexe = output_type == 'WinExe' or output_type == 'Exe'
    
    # ---- Build the new SDK-style content ----
    lines = []
    lines.append('<?xml version="1.0" encoding="utf-8"?>')
    lines.append('<Project Sdk="Microsoft.NET.Sdk">')
    lines.append('')
    lines.append('  <PropertyGroup>')
    
    # Target framework
    tfv = get_prop('TargetFrameworkVersion')
    tf_map = {
        'v2.0': 'net20', 'v3.0': 'net30', 'v3.5': 'net35',
        'v4.0': 'net40', 'v4.5': 'net45', 'v4.5.1': 'net451',
        'v4.5.2': 'net452', 'v4.6': 'net46', 'v4.6.1': 'net461',
        'v4.6.2': 'net462', 'v4.7': 'net47', 'v4.7.1': 'net471',
        'v4.7.2': 'net472', 'v4.8': 'net48',
    }
    target_framework = tf_map.get(tfv, 'net472') if tfv else 'net472'
    
    lines.append(f'    <TargetFramework>{target_framework}</TargetFramework>')
    
    if is_winexe:
        lines.append('    <OutputType>WinExe</OutputType>')
    else:
        lines.append(f'    <OutputType>{output_type}</OutputType>')
    
    lines.append('')
    lines.append('    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>')
    lines.append('    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>')
    
    # Keep project GUID for compatibility
    pg = get_prop('ProjectGuid')
    if pg:
        lines.append(f'    <ProjectGuid>{pg}</ProjectGuid>')
    
    # Assembly name
    an = get_prop('AssemblyName')
    if an:
        lines.append(f'    <AssemblyName>{an}</AssemblyName>')
    
    # Root namespace
    rns = get_prop('RootNamespace')
    if rns:
        lines.append(f'    <RootNamespace>{rns}</RootNamespace>')
    
    # Strong naming
    sign = get_prop('SignAssembly')
    if sign:
        lines.append(f'    <SignAssembly>{sign}</SignAssembly>')
    
    snk = get_prop('AssemblyOriginatorKeyFile')
    if snk:
        lines.append(f'    <AssemblyOriginatorKeyFile>{snk}</AssemblyOriginatorKeyFile>')
    
    # Warning level
    wl = get_prop('WarningLevel')
    if wl:
        lines.append(f'    <WarningLevel>{wl}</WarningLevel>')
    
    # NoWarn
    nw = get_prop('NoWarn')
    if nw:
        lines.append(f'    <NoWarn>{nw}</NoWarn>')
    
    # Allow unsafe
    au = get_prop('AllowUnsafeBlocks')
    if au:
        lines.append(f'    <AllowUnsafeBlocks>{au}</AllowUnsafeBlocks>')
    
    # TreatWarningsAsErrors
    twe = get_prop('TreatWarningsAsErrors')
    if twe:
        lines.append(f'    <TreatWarningsAsErrors>{twe}</TreatWarningsAsErrors>')
    
    # Application icon
    app_icon = get_prop('ApplicationIcon')
    if app_icon:
        lines.append(f'    <ApplicationIcon>{app_icon}</ApplicationIcon>')
    
    # Application manifest
    app_manifest = get_prop('ApplicationManifest')
    if app_manifest:
        lines.append(f'    <ApplicationManifest>{app_manifest}</ApplicationManifest>')
    
    # Prefer32Bit
    p32 = get_prop('Prefer32Bit')
    if p32:
        lines.append(f'    <Prefer32Bit>{p32}</Prefer32Bit>')
    
    # Platform target from platform props
    pt = platform_props.get('PlatformTarget', '')
    if pt:
        lines.append(f'    <PlatformTarget>{pt}</PlatformTarget>')
    
    # Base address
    ba = platform_props.get('BaseAddress', '') or get_prop('BaseAddress')
    if ba:
        lines.append(f'    <BaseAddress>{ba}</BaseAddress>')
    
    # File alignment
    fa = platform_props.get('FileAlignment', '') or get_prop('FileAlignment')
    if fa:
        lines.append(f'    <FileAlignment>{fa}</FileAlignment>')
    
    # Determine if we need UseWPF and UseWindowsForms
    needs_wpf = False
    needs_winforms = False
    for ref_name in framework_refs:
        if ref_name in ('PresentationCore', 'PresentationFramework', 'WindowsBase', 'System.Xaml',
                        'PresentationFramework.Aero', 'ReachFramework'):
            needs_wpf = True
        if ref_name in ('System.Windows.Forms', 'WindowsFormsIntegration'):
            needs_winforms = True
    
    if needs_wpf:
        lines.append('')
        lines.append('    <UseWPF>true</UseWPF>')
    if needs_winforms:
        lines.append('    <UseWindowsForms>true</UseWindowsForms>')
    
    # Output path from Debug prop if available
    # In SDK style, we usually use relative output path
    # But we'll set the output paths to match the old behavior
    outpath_debug = debug_props.get('OutputPath', '') or config_platform_props_debug.get('OutputPath', '') or get_prop('OutputPath') or props.get('OutputPath', '')
    outpath_release = release_props.get('OutputPath', '') or config_platform_props_release.get('OutputPath', '') or get_prop('OutputPath') or props.get('OutputPath', '')
    
    # Documentation file
    doc_file_release = config_platform_props_release.get('DocumentationFile', '') or get_prop('DocumentationFile') or ''
    
    lines.append('  </PropertyGroup>')
    
    # Debug configuration
    lines.append('')
    lines.append('  <PropertyGroup Condition=" \'$(Configuration)\' == \'Debug\' ">')
    ds = debug_props.get('DebugSymbols', '') or config_platform_props_debug.get('DebugSymbols', '')
    dt = debug_props.get('DebugType', '') or config_platform_props_debug.get('DebugType', '')
    opt = debug_props.get('Optimize', '') or config_platform_props_debug.get('Optimize', '')
    dc = debug_props.get('DefineConstants', '') or config_platform_props_debug.get('DefineConstants', '')
    
    if ds:
        lines.append(f'    <DebugSymbols>{ds}</DebugSymbols>')
    if dt:
        lines.append(f'    <DebugType>{dt}</DebugType>')
    else:
        lines.append('    <DebugType>full</DebugType>')
    if opt.lower() == 'false' or opt == '':
        lines.append('    <Optimize>False</Optimize>')
    else:
        lines.append(f'    <Optimize>{opt}</Optimize>')
    if dc:
        lines.append(f'    <DefineConstants>{dc}</DefineConstants>')
    else:
        lines.append('    <DefineConstants>DEBUG;TRACE</DefineConstants>')
    
    co = debug_props.get('CheckForOverflowUnderflow', '') or config_platform_props_debug.get('CheckForOverflowUnderflow', '')
    if co:
        lines.append(f'    <CheckForOverflowUnderflow>{co}</CheckForOverflowUnderflow>')
    
    if outpath_debug:
        lines.append(f'    <OutputPath>{outpath_debug}</OutputPath>')
    lines.append('  </PropertyGroup>')
    
    # Release configuration
    lines.append('')
    lines.append('  <PropertyGroup Condition=" \'$(Configuration)\' == \'Release\' ">')
    ds_r = release_props.get('DebugSymbols', '') or config_platform_props_release.get('DebugSymbols', '')
    dt_r = release_props.get('DebugType', '') or config_platform_props_release.get('DebugType', '')
    opt_r = release_props.get('Optimize', '') or config_platform_props_release.get('Optimize', '')
    dc_r = release_props.get('DefineConstants', '') or config_platform_props_release.get('DefineConstants', '')
    
    if ds_r:
        lines.append(f'    <DebugSymbols>{ds_r}</DebugSymbols>')
    if dt_r:
        lines.append(f'    <DebugType>{dt_r}</DebugType>')
    if opt_r:
        lines.append(f'    <Optimize>{opt_r}</Optimize>')
    if dc_r:
        lines.append(f'    <DefineConstants>{dc_r}</DefineConstants>')
    
    co_r = release_props.get('CheckForOverflowUnderflow', '') or config_platform_props_release.get('CheckForOverflowUnderflow', '')
    if co_r:
        lines.append(f'    <CheckForOverflowUnderflow>{co_r}</CheckForOverflowUnderflow>')
    
    if outpath_release:
        lines.append(f'    <OutputPath>{outpath_release}</OutputPath>')
    if doc_file_release:
        lines.append(f'    <DocumentationFile>{doc_file_release}</DocumentationFile>')
    lines.append('  </PropertyGroup>')
    
    # Framework references as PackageReference (system ones)
    # In SDK style, WPF/WindowsForms framework refs are implicit with UseWPF/UseWindowsForms
    # Other framework refs like System, System.Core, etc are implicit
    # We only need explicit refs for non-standard ones
    
    explicit_framework_refs = framework_refs - {
        'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Data',
        'System.Data.DataSetExtensions', 'System.Drawing', 'System.Windows.Forms',
        'System.Configuration', 'System.Design', 'System.Web.Services',
        'System.Xaml', 'System.Printing',
        'PresentationCore', 'PresentationFramework', 'PresentationFramework.Aero',
        'WindowsBase', 'WindowsFormsIntegration', 'ReachFramework',
        'Microsoft.CSharp', 'System.Runtime.Remoting',
    }
    
    # NuGet references (from packages folder)
    # We keep them as old-style Reference with HintPath since we don't have packages.config parsed
    # Actually, since we're using EnableDefaultItems=false, we'll keep all items exactly as they were
    
    # ---- Items (non-Reference, non-ProjectReference) ----
    # We need to keep explicit items to avoid auto-include issues
    lines.append('')
    lines.append('  <ItemGroup>')
    
    # Compile items
    for item in items_by_type.get('Compile', []):
        include = item['include']
        if not include:
            continue
        # Handle linker items
        if item.get('Link', ''):
            lines.append(f'    <Compile Include="{include}" Link="{item["Link"]}" />')
        elif item.get('DependentUpon', ''):
            lines.append(f'    <Compile Include="{include}" DependentUpon="{item["DependentUpon"]}" />')
        else:
            lines.append(f'    <Compile Include="{include}" />')
    
    lines.append('  </ItemGroup>')
    
    # Page items (WPF XAML)
    pages = items_by_type.get('Page', [])
    if pages:
        lines.append('')
        lines.append('  <ItemGroup>')
        for item in pages:
            include = item['include']
            if not include:
                continue
            generator = item.get('Generator', '')
            subtype = item.get('SubType', '')
            dep = item.get('DependentUpon', '')
            
            extra = ''
            if generator:
                extra += f' Generator="{generator}"'
            if subtype:
                extra += f' SubType="{subtype}"'
            if dep:
                extra += f' DependentUpon="{dep}"'
            
            if extra:
                lines.append(f'    <Page Include="{include}"{extra} />')
            else:
                lines.append(f'    <Page Include="{include}" />')
        lines.append('  </ItemGroup>')
    
    # EmbeddedResource items
    embedded = items_by_type.get('EmbeddedResource', [])
    if embedded:
        lines.append('')
        lines.append('  <ItemGroup>')
        for item in embedded:
            include = item['include']
            if not include:
                continue
            dep = item.get('DependentUpon', '')
            logical = item.get('LogicalName', '')
            generator = item.get('Generator', '')
            last_gen = item.get('LastGenOutput', '')
            
            extra = ''
            if dep:
                extra += f' DependentUpon="{dep}"'
            if logical:
                extra += f' LogicalName="{logical}"'
            if generator:
                extra += f' Generator="{generator}"'
            if last_gen:
                extra += f' LastGenOutput="{last_gen}"'
            
            if extra:
                lines.append(f'    <EmbeddedResource Include="{include}"{extra} />')
            else:
                lines.append(f'    <EmbeddedResource Include="{include}" />')
        lines.append('  </ItemGroup>')
    
    # Content items
    content = items_by_type.get('Content', [])
    if content:
        lines.append('')
        lines.append('  <ItemGroup>')
        for item in content:
            include = item['include']
            if not include:
                continue
            copyto = item.get('CopyToOutputDirectory', '')
            link = item.get('Link', '')
            
            extra = ''
            if copyto:
                extra += f' CopyToOutputDirectory="{copyto}"'
            if link:
                extra += f' Link="{link}"'
            
            if extra:
                lines.append(f'    <Content Include="{include}"{extra} />')
            else:
                lines.append(f'    <Content Include="{include}" />')
        lines.append('  </ItemGroup>')
    
    # None items
    none_items = items_by_type.get('None', [])
    if none_items:
        lines.append('')
        lines.append('  <ItemGroup>')
        for item in none_items:
            include = item['include']
            if not include:
                continue
            copyto = item.get('CopyToOutputDirectory', '')
            link = item.get('Link', '')
            
            extra = ''
            if copyto:
                extra += f' CopyToOutputDirectory="{copyto}"'
            if link:
                extra += f' Link="{link}"'
            
            if extra:
                lines.append(f'    <None Include="{include}"{extra} />')
            else:
                lines.append(f'    <None Include="{include}" />')
        lines.append('  </ItemGroup>')
    
    # Resource items
    resources = items_by_type.get('Resource', [])
    if resources:
        lines.append('')
        lines.append('  <ItemGroup>')
        for item in resources:
            include = item['include']
            if not include:
                continue
            lines.append(f'    <Resource Include="{include}" />')
        lines.append('  </ItemGroup>')
    
    # CodeAnalysisDictionary items
    cad = items_by_type.get('CodeAnalysisDictionary', [])
    if cad:
        lines.append('')
        lines.append('  <ItemGroup>')
        for item in cad:
            include = item['include']
            if not include:
                continue
            lines.append(f'    <CodeAnalysisDictionary Include="{include}" />')
        lines.append('  </ItemGroup>')
    
    # ApplicationDefinition items (like App.xaml)
    # In SDK style, App.xaml is auto-detected for WPF apps
    # But we'll keep explicit ApplicationDefinition if present
    app_def = items_by_type.get('ApplicationDefinition', [])
    if app_def:
        lines.append('')
        lines.append('  <ItemGroup>')
        for item in app_def:
            include = item['include']
            if not include:
                continue
            lines.append(f'    <ApplicationDefinition Include="{include}" />')
        lines.append('  </ItemGroup>')
    
    # Reference items (NuGet + other non-framework)
    # We keep these as explicit references
    non_framework_refs = {}
    for ref_elem in root.findall('.//msbuild:Reference', ns):
        include = ref_elem.get('Include', '')
        hint_path = ref_elem.find('msbuild:HintPath', ns)
        is_private = ref_elem.find('msbuild:Private', ns)
        hint_path_val = hint_path.text if hint_path is not None else ''
        private_val = is_private.text if is_private is not None else ''
        
        name = include.split(',')[0].strip()
        
        # Skip framework refs that are implicit
        system_refs = {
            'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Data',
            'System.Data.DataSetExtensions', 'System.Drawing', 'System.Windows.Forms',
            'System.Configuration', 'System.Design', 'System.Web.Services',
            'System.Xaml', 'System.ServiceModel', 'System.Printing',
            'PresentationCore', 'PresentationFramework', 'PresentationFramework.Aero',
            'WindowsBase', 'WindowsFormsIntegration', 'ReachFramework',
            'Microsoft.CSharp',
        }
        
        if name in system_refs:
            continue
        
        if hint_path_val or name not in system_refs:
            non_framework_refs[name] = {
                'include': include,
                'hintpath': hint_path_val,
                'private': private_val,
            }
    
    if non_framework_refs:
        lines.append('')
        lines.append('  <ItemGroup>')
        for ref_name, ref_data in non_framework_refs.items():
            extra = ''
            if ref_data['hintpath']:
                extra += f'\n      <HintPath>{ref_data["hintpath"]}</HintPath>'
            if ref_data['private']:
                extra += f'\n      <Private>{ref_data["private"]}</Private>'
            if extra:
                lines.append(f'    <Reference Include="{ref_data["include"]}">{extra}\n    </Reference>')
            else:
                lines.append(f'    <Reference Include="{ref_data["include"]}" />')
        lines.append('  </ItemGroup>')
    
    # ProjectReference items
    if project_refs:
        lines.append('')
        lines.append('  <ItemGroup>')
        for pr in project_refs:
            # Adjust project reference paths to account for new project directory layout
            # Since we're keeping files in the same location, paths remain the same
            extra = ''
            if pr['project']:
                extra += f'\n      <Project>{pr["project"]}</Project>'
            if pr['name']:
                extra += f'\n      <Name>{pr["name"]}</Name>'
            if pr['private']:
                extra += f'\n      <Private>{pr["private"]}</Private>'
            if extra:
                lines.append(f'    <ProjectReference Include="{pr["include"]}">{extra}\n    </ProjectReference>')
            else:
                lines.append(f'    <ProjectReference Include="{pr["include"]}" />')
        lines.append('  </ItemGroup>')
    
    # Import any custom targets that might exist (like PostBuildEvent.proj)
    imports = []
    for imp in root.findall('msbuild:Import', ns):
        project = imp.get('Project', '')
        if project not in ('$(MSBuildBinPath)\\Microsoft.CSharp.targets',
                           '$(MSBuildBinPath)\\Microsoft.CSharp.Targets',
                           '$(MSBuildToolsPath)\\Microsoft.CSharp.targets',
                           '$(MSBuildToolsPath)\\Microsoft.CSharp.Targets'):
            imports.append(project)
    
    for imp in imports:
        lines.append(f'  <Import Project="{imp}" />')
    
    # Target elements (like BeforeBuild)
    for target in root.findall('msbuild:Target', ns):
        target_name = target.get('Name', '')
        lines.append('')
        lines.append(f'  <Target Name="{target_name}">')
        for child in target:
            tag = namespace_to_tag(child.tag)
            if tag == 'MSBuild':
                msbuild_proj = child.get('Projects', '')
                msbuild_targets = child.get('Targets', '')
                msbuild_props = child.get('Properties', '')
                lines.append(f'    <MSBuild Projects="{msbuild_proj}" Targets="{msbuild_targets}" Properties="{msbuild_props}" />')
            elif tag == 'Exec':
                cmd = child.get('Command', '')
                timeout = child.get('Timeout', '')
                cond = child.get('Condition', '')
                extra = f' Command="{cmd}"'
                if timeout:
                    extra += f' Timeout="{timeout}"'
                if cond:
                    extra += f' Condition="{cond}"'
                lines.append(f'    <Exec{extra} />')
        lines.append(f'  </Target>')
    
    lines.append('')
    lines.append('</Project>')
    lines.append('')
    
    new_content = '\n'.join(lines)
    
    # Backup original
    backup_path = csproj_path + '.old'
    if not os.path.exists(backup_path):
        shutil.copy2(csproj_path, backup_path)
        print(f"  Backed up to: {backup_path}")
    
    # Write new content
    write_file_content(csproj_path, new_content)
    print(f"  Converted successfully!")


def main():
    projects = get_csproj_paths_from_sln(SLN_FILE)
    print(f"Found {len(projects)} .csproj files in solution.")
    
    for rel_path, abs_path in projects:
        if os.path.exists(abs_path):
            convert_csproj_to_sdk(abs_path, rel_path)
        else:
            print(f"  NOT FOUND: {rel_path}")
    
    print("\nConversion complete!")


if __name__ == '__main__':
    main()
