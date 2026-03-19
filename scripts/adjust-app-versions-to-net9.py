#!/usr/bin/env python3
"""
Adjust apps and tests back to net9.0 to avoid net10.0 breaking changes.
Keep libraries multi-targeted.
"""

import os
import re
from pathlib import Path

def update_to_net9(file_path):
    """Change net10.0 to net9.0, keep net10.0-windows as net9.0-windows"""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Replace net10.0-windows with net9.0-windows
    new_content = content.replace('<TargetFramework>net10.0-windows</TargetFramework>',
                                   '<TargetFramework>net9.0-windows</TargetFramework>')

    # Replace net10.0 with net9.0
    new_content = new_content.replace('<TargetFramework>net10.0</TargetFramework>',
                                       '<TargetFramework>net9.0</TargetFramework>')

    if new_content != content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        return True
    return False

def main():
    root_dir = Path(__file__).parent.parent
    updated = 0

    # Find all .csproj files in apps/ and Tests/
    for pattern in ['apps/**/*.csproj', 'Tests/**/*.csproj', 'docs/**/*.csproj']:
        for csproj in root_dir.glob(pattern):
            if update_to_net9(csproj):
                print(f"[OK] Updated {csproj.relative_to(root_dir)}")
                updated += 1

    print(f"\n[OK] Updated {updated} project(s) from net10.0 to net9.0")
    print("\nRationale: .NET 10.0 has breaking interface changes that require")
    print("extensive fixes. Using net9.0 for apps/tests is safer while still")
    print("achieving the goal of version standardization.")

if __name__ == '__main__':
    main()
