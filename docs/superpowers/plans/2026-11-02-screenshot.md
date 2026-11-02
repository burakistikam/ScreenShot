# ScreenShot Implementation Plan

> Execute inline with executing-plans; independent whole-app reviewer at completion.

Goal: ship a usable local Windows screenshot and annotation app.
Architecture: pure selection/history model, WPF pixel canvas/editor, native capture/hotkey services, dashboard/tray/settings.
Tech Stack: .NET 10, WPF, Windows Forms interop. No third-party runtime dependencies.
Spec: ../specs/2026-11-02-screenshot-design.md

## Constraints and review focus
Local only. Export excludes UI. Correct reverse/clamped selections, negative monitor origins, history branching, immutable stroke points, shortcut conflicts, bitmap/handle disposal. Mixed-DPI hardware is manually qualified only when available.

## Tasks
- [x] Core: create ScreenShot.Core geometry and immutable annotation/document history. Write executable tests for reverse/clamped drag, move/resize bounds, undo/redo/edit/redo invalidation first; observe red then green.
- [x] Desktop: create app project/manifest, native pixel capture, hotkey registration with rollback, persistent settings, tray/dashboard and shortcut editor. Verify build and launch.
- [x] Editor: overlay with pixel-coordinate selection and vector tools; selectable movable annotations and editable text; toolbar color/width, undo/redo, PNG/JPG export and clipboard. Test real renderer and exported dimensions/UI exclusion with Windows integration checks.
- [x] Delivery: independent review, fix concrete defects, run build/test and interactive smoke, publish portable executable and zip, document limitations, commit/push to requested public repo.

## Ledger
- User asked to build now after accepting proposal, with no online sharing. Proceeding without further design questions.
- Existing checkout is empty and dedicated to this task; reused on codex/initial-app rather than creating a second worktree.

## Completion evidence
- Core selection/history tests: 9 passed.
- WPF rendering/export/input-cancellation tests: 8 passed; optional physical HWND bounds smoke: passed.
- Release build: zero warnings/errors. Self-contained win-x64 executable and portable ZIP generated.
- Independent reviewer findings fixed: wrapped text and long tokens, independent style attributes, small-crop edge regions, lost mouse capture.
- Dashboard launch verified through native accessibility. Interactive mouse/keyboard E2E blocked by desktop input access (GetCursorPos access denied); mixed-DPI physical hardware remains unqualified.
- Synthetic dashboard/editor previews rendered and visually inspected; fixed unreadable width dropdown text.
