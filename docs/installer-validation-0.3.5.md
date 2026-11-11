# Installer validation — 0.3.5

Validated on Windows x64, 2026-11-10. Build: Inno Setup 6.7.3, .NET 10 self-contained application.

- Release build: zero warnings/errors; 9 core and 17 WPF integration checks passed.
- Running application: silent Setup rejected installation while the application's mutex existed; it did not close or overwrite the running app.
- Existing manual installation: Setup installed successfully into the existing per-user folder; executable SHA256 matched the published build.
- Repeat installation: completed successfully with the same installation identity. Settings file SHA256 stayed unchanged.
- Startup, Desktop and Start Menu shortcuts: targets read back correctly; Startup includes `--background`.
- Windows uninstall registration: version 0.3.5 and expected per-user installation path verified.
- Uninstall: removed the executable and three shortcuts, preserving the preferences file unchanged.
- Final installer: reinstalled successfully after uninstall, preserved preferences, matched executable hash and launched in background.
- Independent static review found no installer code blockers. One README location description was corrected.

Local test logs are in ignored `artifacts/setup-*.log`. Tests used silent installer mode; interactive wizard navigation, a different Windows account, and an actual reboot were not tested. The installer and application are unsigned. Updates require manually downloading a newer installer. Uninstall intentionally retains user preferences and never targets saved screenshots.

The compiler was used as a portable build tool from the Tools.InnoSetup 6.7.3 NuGet package; ISCC.exe's Authenticode signature validated with publisher Pyrsys B.V. The compiler is not shipped with the application.
