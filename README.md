# Zoomy

A tiny, no-frills screen magnifier for Windows 10.

1. Right-click the desktop (or the empty space in any folder) and choose **Zoom in on screen**, or press **Ctrl+Alt+Z** from anywhere.
2. Drag a box over whatever you want to look at. The screen freezes while you drag, like the Snipping Tool, but it isn't dimmed.
3. Let go, and a small window opens with that area blown up.

In the zoom window:

- **X** closes it (or press **Esc**).
- **Right-click** gives you **Save image...**, **Copy image**, and **Smooth scaling**. Smooth scaling is off by default, so you see the real pixels.
- Drag the window edges, or scroll the mouse wheel, to make it bigger or smaller.

You can have several zoom windows open at once. Zoomy doesn't run in the background: each zoom starts it, and closing the window ends it.

## Setup

1. Get `Zoomy.exe`. Either download it from the **Actions** tab (latest "Build" run, under **Artifacts**) or build it yourself (see below).
2. Put it somewhere permanent, such as `C:\Tools\Zoomy\Zoomy.exe`.
3. Double-click it and choose **Yes**.

That adds the right-click entry and a Start Menu shortcut with the Ctrl+Alt+Z hotkey. Both are for your user account only, so you don't need admin rights. If you move the exe later, double-click it again to update the paths.

To change the hotkey, open the Start Menu, right-click **Zoomy**, choose **Open file location**, and edit the shortcut's properties.

To remove Zoomy, run `uninstall.bat` (or `Zoomy.exe uninstall`), then delete the exe.

## Building

Run `build.bat`. It uses the C# compiler that already comes with Windows 10, so you don't need to install anything. The result is a single `Zoomy.exe` of about 50 KB (most of that is the icon).

## Why a hotkey too?

Windows doesn't have one right-click menu shared by every app. Browsers, editors, and other programs each draw their own. The desktop and folder background menu is the only one Zoomy can join. Ctrl+Alt+Z covers the other cases, such as zooming into part of a web page without leaving the browser.
