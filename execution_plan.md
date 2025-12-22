# SnapMark — Execution Plan

This document defines the execution plan derived from the authoritative PRD.
Follow task order and dependencies strictly.
Do not expand scope without explicit instruction.

---

## Milestone M0 — Scaffold & Capture Foundation

### EPIC-0: Repository & App Scaffold

**TASK-0.1** — WinUI 3 App Shell  
- Initialize WinUI 3 solution
- Tray icon support
- Clean startup and shutdown

**TASK-0.2** — Global Hotkeys  
- Register global hotkeys via Win32
- Handle collisions gracefully

**TASK-0.3** — CI & Repo Hygiene  
- CI build pipeline
- Create docs stubs:
  - ARCHITECTURE.md
  - THIRD_PARTY_NOTICES.md

**Definition of Done**
- App launches
- Tray icon visible
- Hotkeys trigger callbacks

---

### EPIC-1: Screenshot Capture Engine

**TASK-1.1** — Region Capture  
- Crosshair selector
- DPI-aware bitmap capture
- Multi-monitor support

**TASK-1.2** — Full Screen & Window Capture  
- Accurate active window bounds
- Optional shadow exclusion

**TASK-1.3** — Capture Instrumentation  
- Log timings:
  - Hotkey → crosshair
  - Crosshair → bitmap ready

**Definition of Done**
- Reliable capture across DPI scenarios
- Performance metrics logged locally

---

## Milestone M1 — Annotation Editor MVP

### EPIC-2: Editor Core

**TASK-2.1** — Annotation Canvas  
- Implement high-performance canvas
- Render captured bitmap
- Smooth redraw

**TASK-2.2** — Core Tools  
- Arrow
- Rectangle
- Line
- Text
- Editable properties

**TASK-2.3** — Undo / Redo System  
- Command-based model
- Covers create, move, resize, delete

**TASK-2.4** — Export  
- Copy PNG to clipboard
- Save PNG to file

**Definition of Done**
- Capture → annotate → paste workflow works end-to-end

---

## Milestone M2 — Pro Annotations & Pinning

### EPIC-3: Advanced Annotation Tools

**TASK-3.1** — Blur / Pixelation  
- Adjustable strength
- Non-destructive

**TASK-3.2** — Highlight & Numbered Steps  
- Adjustable opacity
- Auto-increment steps

---

### EPIC-4: Pin to Screen

**TASK-4.1** — Overlay Window  
- Borderless
- Always-on-top
- No focus stealing

**TASK-4.2** — Overlay Controls  
- Drag
- Resize
- Opacity
- Esc / Ctrl+W to dismiss

**Definition of Done**
- Pinned image remains visible across app switches
- No flicker or performance regression

---

## Milestone M3 — Quality & Optional Features

### EPIC-5: OCR & Scrolling Capture

**TASK-5.1** — OCR Integration  
- Use Windows.Media.Ocr
- Copy recognized text to clipboard

**TASK-5.2** — Scrolling Capture (Browser-First)  
- Stitch vertically scrolled content
- Graceful failure for unsupported apps

**Definition of Done**
- Meets PRD performance targets
- Stable under extended usage

---

## Global Execution Rules

- Do not start a milestone until the previous milestone’s Definition of Done is met
- Keep capture engine and editor decoupled
- Maintain THIRD_PARTY_NOTICES.md for all dependencies
- Log performance metrics locally
- No scope expansion without explicit instruction

