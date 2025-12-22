# SnapMark (Windows)
**Instant Screenshot + Annotation Utility**

## 1. Overview

SnapMark is a Windows-native screenshot and annotation tool designed for speed, clarity, and keyboard-first workflows. It is inspired by CleanShot X but built specifically for Windows power users.

**Core promise:**  
Capture → annotate → copy or save in under 3 seconds.

---

## 2. Goals

### Primary Goals
- Sub-second screenshot capture
- Immediate annotation with zero perceptible lag
- Keyboard-first workflow with configurable global hotkeys
- Native Windows look and feel
- Low idle CPU and memory usage

### Explicit Non-Goals (v1)
- Cloud sync or accounts
- Team collaboration
- Video or GIF recording
- AI-assisted features
- Cross-platform support

---

## 3. Target Users

### Power Knowledge Workers
- 10–50 screenshots per day
- Needs arrows, blur, text, and fast copy-paste
- Examples: product, engineering, ops, real estate, QA

### Support / QA
- Numbered steps
- Redaction of sensitive data
- Consistent annotation styles

---

## 4. Core Use Cases

1. Capture region → draw arrow → paste into chat
2. Capture window → blur sensitive info → save
3. Capture long content → annotate once
4. Pin screenshot on screen while referencing another app
5. Extract text from screenshot (OCR)

---

## 5. Screenshot Capture

### Capture Modes
- Region
- Full screen
- Active window
- (Optional v1.1) Scrolling capture (browser-first)

### Behavior
- Fully DPI-aware (Per-Monitor v2)
- Multi-monitor support
- Configurable global hotkeys
- Optional cursor inclusion
- Optional window shadow exclusion

### Performance Targets
- Hotkey → crosshair: < 100 ms
- Capture → editor: < 150 ms

---

## 6. Annotation Editor

### Launch Behavior
- Opens immediately after capture
- Escape cancels
- Enter confirms default action (copy to clipboard)

### Tools (v1)
- Arrow
- Rectangle / rounded rectangle
- Line
- Text
- Highlight
- Blur / pixelate
- Numbered steps

### Editing
- Select, move, resize, recolor any annotation
- Z-order control
- Snap guides
- Unlimited undo / redo (session-scoped)

### Canvas Utilities
- Crop (non-destructive until export)
- Transparent or padded background
- Optional drop shadow

---

## 7. Export & Utilities

- Copy annotated image to clipboard (PNG)
- Save to file (PNG, optional JPG)
- Pin to screen (always-on-top overlay)
  - Draggable
  - Resizable
  - Adjustable opacity
  - Dismiss via Esc or Ctrl+W

---

## 8. OCR (Optional v1)

- On-device text extraction
- Copy recognized text to clipboard
- English language default
- Completion target: < 1s for 1080p capture

---

## 9. Settings

- Hotkey customization
- Default save location
- Filename templates
- Default annotation styles
- Auto-copy / auto-save behavior

Settings stored locally as JSON:
%AppData%/SnapMark/settings.json


---

## 10. Performance Requirements

- Cold start: < 500 ms (target)
- Idle CPU: < 1%
- Memory usage: < 200 MB resident
- No background activity when idle

---

## 11. Privacy & Security

- No network access by default
- No uploads
- No accounts
- OCR runs fully on device
- Telemetry opt-in only

---

## 12. Technical Constraints

- Windows 11 primary target
- Windows 10 (22H2+) optional
- WinUI 3 (.NET) recommended
- Avoid Electron
- No admin privileges required

---

## 13. Success Criteria

- Faster capture + annotate workflow than Windows Snipping Tool
- High shortcut adoption
- Zero noticeable lag during annotation
- Stable after 100+ captures per session
