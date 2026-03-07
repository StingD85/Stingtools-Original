# STINGTOOLS — Crash Fix Instructions for Claude Code

## CRASH EVIDENCE FROM REVIT CRASH DUMPS

Revit CER (Customer Error Report) protobuf files confirm:
- **Crash Date:** 03/07/2026
- **CrashUUID:** df5197a8-527c-482a-a5ee-71feeeb1be34
- **Revit Version:** 2025.4 (build 25.4.30.30, 20250815_1515)
- **OS:** Windows 11 Pro x64 (Build 22621)
- **Hardware:** Dell Precision 3571, i7-12800H, Intel Iris Xe, 32GB RAM
- **EXCEPTION_INFO_POINTER:** 0x7FFEAB70C9E8 — **this is a native/unmanaged address**
- **Thread:** 9836, PID: 11592
- **Journal:** journal.0310.txt

The exception pointer being in the 0x7FFE range confirms this is a **native access violation** (unmanaged crash) — not a .NET exception. This is exactly what happens when `RuntimeHelpers.GetUninitializedObject()` creates an `ExternalCommandData` with corrupted native COM pointers.

8 Revit restarts recorded in a single day's log file confirms repeated crashes.

---

## ROOT CAUSE (CONFIRMED)

`IPanelCommand` interface was created but **ZERO of 250 commands implement it**:

```
$ grep -rn ": IPanelCommand" *.cs
(no results)
```

Every button click follows this path:
1. `StingCommandHandler.Execute(UIApplication app)` receives a **valid** `UIApplication`
2. Dispatches to `RunCommand<T>(app)` 
3. `RunCommand<T>` checks `if (cmd is Core.IPanelCommand)` → **always false** (nothing implements it)
4. Falls through to `CreateCommandData(app)` which uses `RuntimeHelpers.GetUninitializedObject(typeof(ExternalCommandData))` — creates an uninitialized native COM wrapper with garbage memory
5. Command calls `commandData.Application.ActiveUIDocument.Document` → accesses corrupted native pointer → **ACCESS VIOLATION → Revit crashes**

The `CreateCommandData` method and `GetUninitializedObject` are still in the codebase:
- Line 806: `ExternalCommandData cmdData = CreateCommandData(app);`
- Line 894: `.GetUninitializedObject(typeof(ExternalCommandData))`

**222 call sites** still access `commandData.Application` directly across **41 files**.
**Zero** use the `CurrentApp` fallback.

---

## THE FIX (Three Steps)

### STEP 1: Rewrite `RunCommand<T>` in StingCommandHandler.cs

Replace the current `RunCommand<T>` method (around line 788) with this version that **passes null for commandData** and relies on `CurrentApp`:

```csharp
private static void RunCommand<T>(UIApplication app) where T : IExternalCommand, new()
{
    try
    {
        // Validate context
        if (app?.ActiveUIDocument == null)
        {
            TaskDialog.Show("STING Tools", "No document is open.");
            return;
        }

        // Set CurrentApp so all commands can use it as their UIApplication source
        CurrentApp = app;

        var cmd = new T();
        string message = "";
        var elements = new ElementSet();
        
        // Pass null commandData — commands use CurrentApp fallback
        cmd.Execute(null, ref message, elements);
    }
    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
    {
        // User cancelled — silent
    }
    catch (Exception ex)
    {
        StingLog.Error($"RunCommand<{typeof(T).Name}> failed", ex);
        TaskDialog.Show("STING Tools", $"{typeof(T).Name} failed:\n{ex.Message}");
    }
}
```

Then **delete the entire `CreateCommandData` method** (lines ~836-959). It is no longer called.

Also delete the `IPanelCommand` check block (lines 794-801) since we no longer need it — all commands go through the same null-safe path.

### STEP 2: Add fallback header to EVERY command's Execute method

Every command that accesses `commandData.Application` must be changed to fall back to `StingCommandHandler.CurrentApp` when `commandData` is null. There are 5 distinct patterns to find-and-replace across 41 files.

#### Pattern A: `Document doc = commandData.Application.ActiveUIDocument.Document;` (93 occurrences)

Find this line (or close variant) and replace with:
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
if (uiApp?.ActiveUIDocument?.Document == null) { message = "No document open"; return Result.Failed; }
Document doc = uiApp.ActiveUIDocument.Document;
```

Files: AutoTagCommand.cs, BatchTagCommand.cs, CombineParametersCommand.cs, ConfigEditorCommand.cs, CreateParametersCommand.cs, DataPipelineCommands.cs, DocAutomationCommands.cs, DocAutomationExtCommands.cs, FamilyCommands.cs, FormulaEvaluatorCommand.cs, LegendBuilderCommands.cs, MasterSetupCommand.cs, MaterialCommands.cs, PreTagAuditCommand.cs, PresentationModeCommand.cs, ResolveAllIssuesCommand.cs, ScheduleCommands.cs, ScheduleEnhancementCommands.cs, SheetIndexCommand.cs, SheetOrganizerCommand.cs, TagAndCombineCommand.cs, TagFamilyCreatorCommand.cs, TemplateCommands.cs, TemplateExtCommands.cs, TemplateManagerCommands.cs, TokenWriterCommands.cs, TransmittalCommand.cs, ValidateTagsCommand.cs, ViewAutomationCommands.cs, ViewOrganizerCommand.cs, ViewportCommands.cs, WorkflowEngine.cs

#### Pattern B: `UIDocument uidoc = commandData.Application.ActiveUIDocument;` (80 occurrences)

Find and replace with:
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
UIDocument uidoc = uiApp?.ActiveUIDocument;
if (uidoc == null) { message = "No document open"; return Result.Failed; }
```

Files: AutoTagCommand.cs, CategorySelectCommands.cs, ColorCommands.cs, DocAutomationExtCommands.cs, FamilyStagePopulateCommand.cs, LegendBuilderCommands.cs, ParagraphDepthCommand.cs, RichTagDisplayCommands.cs, SmartTagPlacementCommand.cs, StateSelectCommands.cs, SystemParamPushCommand.cs

#### Pattern C: `commandData.Application.Application` — inner Application access (6 occurrences, MOST DANGEROUS)

Find and replace with:
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
if (uiApp == null) { message = "No application context"; return Result.Failed; }
Autodesk.Revit.ApplicationServices.Application revitApp = uiApp.Application;
```

Files: LoadSharedParamsCommand.cs (line 28), TemplateManagerCommands.cs (lines 2443, 2444, 2517, 2569, 2572)

#### Pattern D: `cmd.Application.ActiveUIDocument` — TagOperationCommands uses `cmd` not `commandData` (42 occurrences)

Find and replace with:
```csharp
UIApplication uiApp = cmd?.Application ?? UI.StingCommandHandler.CurrentApp;
UIDocument uidoc = uiApp?.ActiveUIDocument;
if (uidoc == null) { msg = "No document open"; return Result.Failed; }
Document doc = uidoc.Document;
```

File: TagOperationCommands.cs (all 20+ command classes in this file use `cmd`)

#### Pattern E: Nullable Document access `commandData.Application.ActiveUIDocument?.Document` (scattered)

Find and replace with:
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
Document doc = uiApp?.ActiveUIDocument?.Document;
if (doc == null) { message = "No document open"; return Result.Failed; }
```

Files: DataPipelineCommands.cs (multiple locations)

### STEP 3: Fix MasterSetupCommand sub-command chain

MasterSetupCommand.cs has its own `RunCommand` helper (line 312) that passes `commandData` to sub-commands:

```csharp
private static Result RunCommand(IExternalCommand cmd,
    ExternalCommandData data, ElementSet elems)
{
    string msg = "";
    return cmd.Execute(data, ref msg, elems);
}
```

After Step 2, all sub-commands handle null `commandData` via the `CurrentApp` fallback. But MasterSetupCommand's own Execute method also needs the Step 2 fix, AND `CurrentApp` must be set before the chain runs. Since MasterSetupCommand is called from the panel via `RunCommand<T>`, `CurrentApp` will already be set by Step 1.

No additional changes needed here beyond applying Pattern A to MasterSetupCommand's own Execute method.

---

## COMPLETE FILE LIST (41 files, 222 call sites)

```
AutoTagCommand.cs
BatchTagCommand.cs
CategorySelectCommands.cs
ColorCommands.cs
CombineParametersCommand.cs
ConfigEditorCommand.cs
DataPipelineCommands.cs
DocAutomationCommands.cs
DocAutomationExtCommands.cs
FamilyCommands.cs
FamilyStagePopulateCommand.cs
FormulaEvaluatorCommand.cs
LegendBuilderCommands.cs
LoadSharedParamsCommand.cs
MasterSetupCommand.cs
MaterialCommands.cs
ParagraphDepthCommand.cs
PreTagAuditCommand.cs
PresentationModeCommand.cs
ResolveAllIssuesCommand.cs
RichTagDisplayCommands.cs
ScheduleCommands.cs
ScheduleEnhancementCommands.cs
SheetIndexCommand.cs
SheetOrganizerCommand.cs
SmartTagPlacementCommand.cs
StateSelectCommands.cs
SystemParamPushCommand.cs
TagAndCombineCommand.cs
TagFamilyCreatorCommand.cs
TagOperationCommands.cs
TemplateCommands.cs
TemplateExtCommands.cs
TemplateManagerCommands.cs
TokenWriterCommands.cs
TransmittalCommand.cs
ValidateTagsCommand.cs
ViewAutomationCommands.cs
ViewOrganizerCommand.cs
ViewportCommands.cs
WorkflowEngine.cs
```

---

## VERIFICATION AFTER FIX

Run these checks to confirm the fix is complete:

```bash
# 1. CreateCommandData and GetUninitializedObject MUST be gone
grep -rn "CreateCommandData\|GetUninitializedObject" *.cs
# Expected: 0 results

# 2. No direct commandData.Application access without null-safe fallback
grep -rn "commandData\.Application\." *.cs | grep -v "commandData?\.Application\|??"
# Expected: 0 results

# 3. No direct cmd.Application access without null-safe fallback  
grep -rn "cmd\.Application\." *.cs | grep -v "cmd?\.Application\|??"
# Expected: 0 results

# 4. CurrentApp fallback present in all command files
grep -rn "StingCommandHandler\.CurrentApp" *.cs | wc -l
# Expected: 200+ (one per command Execute method)

# 5. IPanelCommand can be deleted (optional cleanup)
grep -rn "IPanelCommand" *.cs
# Expected: only in IPanelCommand.cs definition (can delete entire file)
```

---

## WHY THIS FIX WORKS

- `StingCommandHandler.Execute(UIApplication app)` receives the **real** `UIApplication` directly from Revit's `IExternalEventHandler` contract — this is a genuine, properly-initialized native object
- Setting `CurrentApp = app` before calling any command stores this valid reference
- Commands fall back to `CurrentApp` when `commandData` is null (which it always will be from the panel)
- The `UIApplication` from `CurrentApp` is the **exact same object** Revit provides — all API calls through it (ActiveUIDocument, Document, Application, Selection, etc.) work correctly
- When commands are invoked from the Revit ribbon (not the panel), `commandData` will be non-null and the `??` operator uses it normally — full backward compatibility

---

## OPTIONAL CLEANUP

After confirming the fix works:
1. Delete `IPanelCommand.cs` — no longer needed
2. Delete any `IPanelCommand` references in `StingCommandHandler.cs`
3. Remove the `CreateCommandData` method entirely (should already be done in Step 1)
4. Remove the `_cmdDataFieldsLogged` static field
