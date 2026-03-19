#!/usr/bin/env python3
"""
Standardize .NET versions across Hazina framework

Strategy:
- Core libraries (src/Core/*): Multi-target net8.0;net9.0;net10.0
- Tool libraries (src/Tools/*): Multi-target net8.0;net9.0;net10.0
- Apps (apps/*): Upgrade to net10.0
- Tests (*Tests*): Upgrade to net10.0
- Demos (apps/Demos/*): Upgrade to net10.0
"""

import os
import re
import sys
from pathlib import Path
from collections import defaultdict

def analyze_csproj(file_path):
    """Extract current target framework from .csproj"""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find TargetFramework or TargetFrameworks
    single_match = re.search(r'<TargetFramework>(.*?)</TargetFramework>', content)
    multi_match = re.search(r'<TargetFrameworks>(.*?)</TargetFrameworks>', content)

    if multi_match:
        return multi_match.group(1).strip(), 'multi'
    elif single_match:
        return single_match.group(1).strip(), 'single'
    else:
        return None, None

def categorize_project(file_path):
    """Determine project category based on path"""
    path_str = str(file_path).replace('\\', '/')

    # Tests
    if 'Tests' in path_str or 'test' in path_str.lower():
        return 'test'

    # Core libraries
    if '/src/Core/' in path_str:
        return 'core'

    # Tool libraries
    if '/src/Tools/' in path_str:
        return 'tool'

    # Apps (including Demos, CLI, Desktop, etc.)
    if '/apps/' in path_str:
        return 'app'

    # Docs/Examples
    if '/docs/' in path_str or '/examples/' in path_str:
        return 'example'

    # Default to library
    return 'library'

def determine_target_framework(category, current_fw):
    """Determine what the target framework should be"""

    # Skip if already correct
    if category in ['core', 'tool', 'library']:
        desired = 'net8.0;net9.0;net10.0'
        if current_fw == desired:
            return None  # Already correct
        # Handle Windows-specific targets
        if 'windows' in current_fw:
            return 'net8.0-windows;net9.0-windows;net10.0-windows'
        return desired

    elif category in ['app', 'test', 'example']:
        # Handle Windows-specific apps
        if current_fw and 'windows' in current_fw:
            return 'net10.0-windows'
        return 'net10.0'

    return None

def update_csproj(file_path, new_target, current_type):
    """Update .csproj file with new target framework"""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Determine if we need TargetFramework or TargetFrameworks
    if ';' in new_target:
        # Multi-targeting
        tag_name = 'TargetFrameworks'
    else:
        # Single target
        tag_name = 'TargetFramework'

    # Replace existing TargetFramework or TargetFrameworks
    if current_type == 'multi':
        # Replace TargetFrameworks with potentially different tag
        pattern = r'<TargetFrameworks>.*?</TargetFrameworks>'
        replacement = f'<{tag_name}>{new_target}</{tag_name}>'
        new_content = re.sub(pattern, replacement, content)
    else:
        # Replace TargetFramework with potentially different tag
        pattern = r'<TargetFramework>.*?</TargetFramework>'
        replacement = f'<{tag_name}>{new_target}</{tag_name}>'
        new_content = re.sub(pattern, replacement, content)

    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_content)

    return True

def main():
    # Find all .csproj files
    root_dir = Path(__file__).parent.parent
    csproj_files = list(root_dir.glob('**/*.csproj'))

    print(f"Found {len(csproj_files)} .csproj files")
    print("=" * 80)

    # Analyze and categorize
    categories = defaultdict(list)
    updates_needed = []
    already_correct = []
    skipped = []

    for csproj in csproj_files:
        current_fw, fw_type = analyze_csproj(csproj)

        if current_fw is None:
            print(f"[WARN]  SKIP: No TargetFramework found in {csproj.relative_to(root_dir)}")
            skipped.append(csproj)
            continue

        category = categorize_project(csproj)
        categories[category].append(csproj)

        new_target = determine_target_framework(category, current_fw)

        if new_target is None:
            already_correct.append((csproj, category, current_fw))
        else:
            updates_needed.append((csproj, category, current_fw, new_target))

    # Summary
    print("\nCATEGORY SUMMARY:")
    for cat, files in sorted(categories.items()):
        print(f"  {cat:12} : {len(files):3} projects")

    print(f"\n[OK] Already correct: {len(already_correct)}")
    print(f"[UPDATE] Updates needed: {len(updates_needed)}")
    print(f"[SKIP] Skipped: {len(skipped)}")

    # Show what will be updated
    if updates_needed:
        print("\n" + "=" * 80)
        print("UPDATES TO BE APPLIED:")
        print("=" * 80)

        for csproj, category, current, new in updates_needed[:20]:  # Show first 20
            rel_path = csproj.relative_to(root_dir)
            print(f"\n[{category.upper()}] {rel_path}")
            print(f"  Current: {current}")
            print(f"  New:     {new}")

        if len(updates_needed) > 20:
            print(f"\n... and {len(updates_needed) - 20} more")

    # Ask for confirmation
    print("\n" + "=" * 80)
    response = input(f"\n[?] Apply {len(updates_needed)} updates? (yes/no): ").strip().lower()

    if response != 'yes':
        print("[X] Aborted")
        return 1

    # Apply updates
    print("\n[UPDATE] Applying updates...")
    success = 0
    failed = 0

    for csproj, category, current, new in updates_needed:
        try:
            fw_type = 'multi' if ';' in current else 'single'
            update_csproj(csproj, new, fw_type)
            print(f"[OK] {csproj.relative_to(root_dir)}")
            success += 1
        except Exception as e:
            print(f"[X] {csproj.relative_to(root_dir)}: {e}")
            failed += 1

    print("\n" + "=" * 80)
    print(f"[OK] Successfully updated: {success}")
    if failed > 0:
        print(f"[X] Failed: {failed}")

    print("\n[OK] .NET version standardization complete!")
    print("\nNext steps:")
    print("1. Test build: dotnet build")
    print("2. Review changes: git diff")
    print("3. Commit and create PR")

    return 0 if failed == 0 else 1

if __name__ == '__main__':
    sys.exit(main())
