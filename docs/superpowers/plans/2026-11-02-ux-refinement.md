# ScreenShot 0.2 UX refinement

User delegated UI decisions and iterative testing/fixing. Keep local-only capture scope. Reuse WPF; polish rather than add cloud, OCR or recording.

- Restyle dashboard with clear capture action, concise guide and recognizable capture illustration; focus/hover states.
- Editor: vector icons with labels, selected tool indicator, quick color swatches, readable stroke control, keyboard tool shortcuts, contextual guidance.
- Correct text workflow: explicit Apply/Cancel, Enter applies and Shift+Enter adds a line, Ctrl+S commits and saves, existing text can be reopened. Deleting all text then applying removes that annotation.
- Accurate stroke hit testing instead of bounding-box-only selection. Synchronize selected annotation style controls without editing it.
- Verify regressions via real WPF handlers/render/export and synthetic previews, including small screens. Try live UI; record access barriers honestly. Independent review, package 0.2.0 and update public repo/release.

Acceptance: automated regressions green; visually inspect actual WPF render previews at normal/min size; fix concrete issues and recheck changed paths. Do not claim manual mouse E2E while Windows tool reports access denied.

## Results
- Three refinement passes: initial dashboard/editor redesign; responsive/contrast corrections; text color and keyboard Cancel regressions.
- Core 9 and WPF integration 13 pass; physical HWND smoke passed. Build: 0 warnings/errors.
- Published executable starts and a second launch exits 0 while the first remains running; activation event replaces the old blocking already-running message.
- Reviewer identified Enter-on-Cancel committing text; reproduced with routed KeyDown/Click, fixed, regression passes.
- WPF previews at 840x720 and 640x600 inspected; small view collapses illustration and scrolls rather than clipping content.
- Live mouse automation again blocked by GetCursorPos access denied and window activation failure. Full real-desktop interaction remains unverified; no claim of manual E2E completion.
