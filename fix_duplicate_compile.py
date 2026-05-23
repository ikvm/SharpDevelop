#!/usr/bin/env python3
"""
Fix NETSDK1022: Add <EnableDefaultItems>false</EnableDefaultItems> to all SDK-style .csproj files
that have explicit <Compile> items but no EnableDefaultItems setting.
"""
import os
import re

SRC_DIR = r"d:\PT\3rds\SharpDevelop\src"

def fix_project(csproj_path):
    with open(csproj_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Skip non-SDK projects
    if 'Sdk="Microsoft.NET.Sdk"' not in content:
        return False
    
    # Skip if already has EnableDefaultItems
    if '<EnableDefaultItems>' in content or '<EnableDefaultItems ' in content:
        return False
    
    # Skip if no explicit Compile items (no duplicates possible)
    if '<Compile Include=' not in content:
        return False
    
    # Add EnableDefaultItems after GenerateTargetFrameworkAttribute or after the first PropertyGroup
    # Find the first </PropertyGroup> and add before it
    pattern = r'(<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>)'
    replacement = r'\1\n    <EnableDefaultItems>false</EnableDefaultItems>'
    
    if re.search(pattern, content):
        content = re.sub(pattern, replacement, content, count=1)
    else:
        # Fallback: add at end of first PropertyGroup
        pattern = r'(  </PropertyGroup>\n)'
        replacement = r'    <EnableDefaultItems>false</EnableDefaultItems>\n\1'
        content = re.sub(pattern, replacement, content, count=1)
    
    with open(csproj_path, 'w', encoding='utf-8') as f:
        f.write(content)
    
    return True

def main():
    count = 0
    for root, dirs, files in os.walk(SRC_DIR):
        for f in files:
            if f.endswith('.csproj'):
                path = os.path.join(root, f)
                if fix_project(path):
                    rel = os.path.relpath(path, SRC_DIR)
                    print(f"Fixed: {rel}")
                    count += 1
    
    print(f"\nUpdated {count} project file(s).")

if __name__ == '__main__':
    main()
