# Work Tray Icons

This directory can contain custom .ico files for different states.

## Icon Files

- `idle.ico` - Gray circle (no active work)
- `working.ico` - Blue circle (1-2 agents working)
- `busy.ico` - Green circle (3+ agents working)
- `warning.ico` - Orange circle (stale work detected)
- `error.ico` - Red circle (error state)

## Fallback

If .ico files are not present, the application will generate icons programmatically using GDI+.

## Creating Custom Icons

You can create custom 32x32 .ico files and place them here. The application will automatically use them if present.

Recommended tools:
- GIMP (free)
- Paint.NET (free)
- IconWorkshop (commercial)
- Online: favicon.ico generators

## Icon Specifications

- Format: .ico (Windows Icon)
- Size: 32x32 pixels (system tray standard)
- Bit depth: 32-bit with alpha channel (for transparency)
- Background: Transparent recommended
