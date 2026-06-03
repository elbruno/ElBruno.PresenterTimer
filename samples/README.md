# Sample Session JSON Files

This directory contains sample JSON session files for testing the Session Timeline Overlay application. Each file demonstrates different use cases and formats supported by the application.

## Sample Files

### 1. `short-demo.json`
- **Purpose**: Quick demonstration session
- **Total Duration**: ~10 minutes
- **Format**: MVP (minimal schema)
- **Sections**: 3 (Intro, Demo, Wrap-up)
- **Features**: No colors, no metadata, no per-section warnings
- **Use Case**: Testing minimal session structure and basic timeline rendering

### 2. `demo-mode.json`
- **Purpose**: Ultra-fast demo mode run
- **Total Duration**: 19 seconds
- **Format**: MVP (minimal schema)
- **Sections**: 3 (Intro, Feature Peek, Wrap-up)
- **Features**: Very short sections (5s, 6s, 8s) for quick demos
- **Use Case**: Fast end-to-end verification during presentations, recordings, and UI demos

### 3. `podcast.json`
- **Purpose**: Podcast episode with extended features
- **Total Duration**: ~30 minutes
- **Format**: Extended (with metadata, colors, warningAt)
- **Sections**: 6 (Intro, Guest Intro, Topic 1, Topic 2, Q&A, Outro)
- **Features**: Full metadata, per-section colors, per-section warnings
- **Use Case**: Testing extended schema with multiple colored sections and warning thresholds

### 4. `conference-talk.json`
- **Purpose**: Technical conference presentation
- **Total Duration**: ~45 minutes
- **Format**: Extended
- **Sections**: 7 (Intro, Context, Microservices Design, Live Demo, Advanced Patterns, Q&A, Wrap-up)
- **Features**: Extended metadata with conference info, colors, warnings
- **Use Case**: Testing longer sessions with live demo sections and audience Q&A

### 5. `workshop.json`
- **Purpose**: Hands-on training workshop with exercises
- **Total Duration**: ~60 minutes
- **Format**: Extended
- **Sections**: 7 (Welcome & Setup, Theory, Exercise 1, Break, Exercise 2, Exercise 3, Q&A)
- **Features**: Multiple exercises, break session, extended metadata with level field
- **Use Case**: Testing full-length workshop format with multiple interactive sections

### 6. `ai-agents-demo.json`
- **Purpose**: Reference example from PRD §17
- **Total Duration**: ~27 minutes
- **Format**: Extended
- **Sections**: 4 (Intro, Context, Demo, Wrap-up)
- **Features**: Matches exact PRD example specification with colors and warnings
- **Use Case**: Testing against documented specification; verifies correct parsing of reference example

### 7. `invalid-warning-exceeds-duration.json` ⚠️ **INTENTIONALLY INVALID**
- **Purpose**: Validation test file
- **Total Duration**: ~13 minutes (if valid)
- **Format**: Syntactically valid JSON, semantically invalid
- **Validation Violation**: The "Demo" section has `duration: "00:05:00"` but `warningAt: "00:06:00"` (warning exceeds duration)
- **Expected Behavior**: Application validation must reject this file with error message: `"Warning threshold (00:06:00) cannot exceed section duration (00:05:00)"`
- **Use Case**: Testing that the validator correctly catches and reports semantic violations per PRD §7.4

## Validation Rules Tested

These files collectively exercise the following validation rules from PRD §7.4:

- ✅ Valid JSON syntax (files 1-6)
- ✅ Valid JSON syntax with semantic error (file 7)
- ✅ Required fields present (title, sections, section title, duration)
- ✅ Duration format validation (HH:mm:ss)
- ✅ Section duration > 0
- ✅ Valid hex color format (in extended format files)
- ✅ WarningAt < section duration (files 1-6 where applicable)
- ❌ WarningAt > section duration (file 7 - intentional violation)

## Usage

To test with these samples:

1. Launch the application
2. Select "Import Session JSON" from the tray menu
3. Choose any of the valid files (1-6)
4. Preview the session and verify it renders correctly
5. Test file 7 to verify validation catches the warningAt violation

## Format Reference

**MVP Format Fields:**
- `title` (required)
- `description` (optional)
- `sections` (required array)
  - `title` (required)
  - `duration` (required, format: "HH:mm:ss")
  - `notes` (optional)

**Extended Format Additional Fields:**
- `metadata` (optional object)
  - `author`, `version`, `createdAt`, etc.
- `sections[].color` (optional hex color)
- `sections[].warningAt` (optional duration threshold)
