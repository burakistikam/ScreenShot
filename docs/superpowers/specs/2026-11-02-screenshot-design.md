# ScreenShot v0.1 design

Approved scope: a friendly Windows screenshot app inspired by Lightshot. Print Screen or a configurable global shortcut opens selection. Draw arrows, pen strokes, lines, rectangles, highlights and text; change color/width; select and move annotations, edit text, undo/redo; copy or save PNG/JPG. Local only, no upload, accounts, telemetry or network calls in the app.

Implementation decision: C#/.NET 10 WPF plus Windows Forms tray icon and Win32 hotkeys. WPF provides a native desktop UI and vector editing without a browser runtime. Electron would add a runtime and memory overhead; WinUI adds packaging complexity for this small tool. User delegated implementation after accepting the proposed feature scope.

Capture freezes the virtual desktop in physical pixels before showing a borderless overlay. A fixed pixel canvas maps through a Viewbox to the HWND; selection and export share the same coordinate system. Selection supports drag, move, edge/corner resize. Annotations remain vector records until export. Clipboard/save contain only selected pixels plus annotations, never handles or toolbars. Small selections, off-screen toolbars, mixed DPI, negative monitor origins and shortcut conflicts must be handled.

The dashboard stays accessible from the tray; closing hides it, tray Exit stops the app. Settings live under LocalAppData/ScreenShot. No automatic startup registration. Portable self-contained Windows x64 publish, source and build instructions in GitHub. Unsigned first build. HDR/protected surfaces and actual mixed-DPI hardware qualification are limitations to report, not claims to invent.
