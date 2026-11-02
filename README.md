# ScreenShot

A small, local-first screenshot and annotation app for Windows, inspired by Lightshot's quick capture workflow. No account, uploads, telemetry, or screenshot server.

**v0.1.0 — first preview.** Windows x64. The portable build includes .NET; extract the ZIP and run `ScreenShot.exe`.

![Editor preview using synthetic sample content](docs/images/editor.png)

## Capture and annotate

1. Start ScreenShot. Press **Print Screen** or click **Capture an area**.
2. Drag over the area to capture. Resize its edges/corners as needed.
3. Use **Arrow**, **Pen**, **Line**, **Box**, **Highlight**, or **Text**.
4. **Ctrl+C** copies the annotated image; **Ctrl+S** saves PNG or JPEG. **Esc** cancels.

The **Move** tool selects existing annotations. Drag to move them, **Delete** to remove, or double-click text / press **F2** to edit it. Drag an empty part of the selection with Move to reposition the crop. Color and width controls apply to the selected annotation, or to future marks. Text wraps; **Ctrl+Enter** applies it. **Ctrl+Z** undoes, **Ctrl+Y** or **Ctrl+Shift+Z** redoes. **Ctrl+A** selects the full virtual desktop. Reselect changes the crop without deleting annotations.

Click the shortcut on the home screen to assign a new key combination. If Print Screen is occupied, the app tries **Ctrl+Shift+S** and reports the fallback; you can choose another shortcut. It does not change Windows settings. Closing the home window leaves the tray icon running; **tray → Exit** quits. The app does not add itself to Windows startup.

Settings: `%LOCALAPPDATA%\ScreenShot\settings.json`. Screenshots are written only when you choose Save or Copy.

## Build

Install the **.NET 10 SDK** on Windows. Then:

```powershell
./scripts/build.ps1             # release build + core and WPF rendering tests
./scripts/build.ps1 -Publish    # also creates artifacts/ScreenShot-0.1.0-win-x64.zip
dotnet run --project src/ScreenShot
```

The projects have no third-party runtime packages. WPF handles UI/drawing, Win32 handles global shortcuts and desktop positioning, and Windows Forms supplies the tray icon and color picker.

Optional Windows integration/preview check:

```powershell
dotnet run --project tests/ScreenShot.IntegrationTests -- --window-smoke --previews
```

This briefly opens a synthetic overlay and verifies its physical HWND bounds. Preview images use synthetic content, not a real desktop screenshot.

## Current validation and limits

Automated coverage includes reversed/clamped selections, small selections, movement, immutable undo/redo history, independent style changes, capture-loss cancellation, text wrapping (including unbroken URLs), all tool rendering, crop pixel/dimension checks, and PNG/JPEG round trips. A real HWND positioning smoke check passed on the development machine; the app launched and exposed its dashboard controls.

Interactive mouse/keyboard end-to-end testing was blocked by the development session's desktop input access. Actual multi-monitor mixed-DPI operation, clipboard contention, and HDR hardware have **not** been manually qualified. The current capture backend is SDR GDI; protected/video surfaces and secure desktops may appear black or fail. The overlay cancels if the display configuration changes. This first build is unsigned.

Screenshots are never uploaded. Link sharing, OCR, scrolling capture, recording, and cloud history are outside this version's scope.
