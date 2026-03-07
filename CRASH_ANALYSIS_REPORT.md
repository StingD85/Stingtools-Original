# STINGTOOLS — Comprehensive Crash Analysis Report

**Date:** 2026-03-07  
**Scope:** All 234 IExternalCommand classes, StingCommandHandler (4,652 lines), inline helpers, and Temp tab commands  
**Purpose:** Identify root causes for Revit crashes when buttons are clicked, particularly Temp tab buttons

---

## CRITICAL ISSUE #1: ExternalCommandData Fabrication (ROOT CAUSE OF MOST CRASHES)

**File:** `StingCommandHandler.cs` lines 766–922  
**Severity:** CRITICAL — affects ALL 234 commands dispatched from the dockable panel

### Problem

`RunCommand<T>()` calls `CreateCommandData(app)` which attempts to fabricate an `ExternalCommandData` object using reflection hacks:

1. **Strategy 1 (lines 808–850):** Discovers internal constructors via reflection and guesses parameter values. The Revit API's `ExternalCommandData` is a native interop wrapper — its internal constructor may require native COM pointers that cannot be synthesized.

2. **Strategy 2 (lines 857–922):** Uses `RuntimeHelpers.GetUninitializedObject()` to create an uninitialized `ExternalCommandData`, then sets fields via reflection. This bypasses all native initialization, meaning **the underlying COM/native state is garbage memory**.

### Why This Crashes Revit

Every single command does `commandData.Application.ActiveUIDocument.Document` on its first line. When `ExternalCommandData` is created via `GetUninitializedObject`:

- The `Application` property may return a partially-initialized or corrupted `UIApplication` reference
- The native interop layer expects properly marshalled COM objects — reflection-set fields skip the marshalling
- Any access to `commandData.Application` properties that touch native Revit internals (e.g., `ActiveUIDocument`, `ActiveView`, journal data) can trigger an access violation in unmanaged code
- Access violations in unmanaged code = **Revit process crash** (not a catchable .NET exception)

### Evidence

Line 914 even admits the problem:
```csharp
if (!appSet)
    StingLog.Error("CreateCommandData: FAILED to set Application field — commands WILL crash!");
```

The log file shows multiple restarts (lines 1, 4, 9, 862, 867) suggesting Revit keeps crashing and restarting.

### Fix Required

**Option A (Recommended):** Refactor all 234 commands to NOT require `ExternalCommandData`. Instead:
- Pass `UIApplication app` directly from `Execute(UIApplication app)` in the IExternalEventHandler
- Each command should accept `UIApplication` and derive `Document` from it
- Create a `CommandContext` class that wraps `UIApplication` without fabricating `ExternalCommandData`

**Option B:** For each command, create a secondary entry point:
```csharp
public Result ExecuteFromPanel(UIApplication app) { ... }
```

**Option C (Quick fix):** In `RunCommand<T>`, bypass `ExternalCommandData` entirely and call the command logic directly using the `UIApplication` reference available in `Execute(UIApplication app)`.

---

## CRITICAL ISSUE #2: 185+ Commands Access `commandData.Application` Without Null Safety

**Files:** All command files (AutoTagCommand.cs, BatchTagCommand.cs, FamilyCommands.cs, TemplateCommands.cs, etc.)  
**Severity:** CRITICAL

### Problem

Every command does this on its first line:
```csharp
Document doc = commandData.Application.ActiveUIDocument.Document;
```

This is a triple-dereference chain with no null checks. If the fabricated `ExternalCommandData` from Issue #1 has a null or partially-initialized `Application` property, this NullReferenceException occurs in the command's `Execute` method.

While the `RunCommand<T>` catch block (line 791) should catch this, the issue is that:
- The `ExternalCommandData` might have a non-null but corrupted `Application` reference
- Accessing corrupted native objects triggers unmanaged access violations, not managed exceptions
- Unmanaged access violations crash the Revit process entirely

### Fix Required

Add null-safety guards at the top of every command:
```csharp
var uiApp = commandData?.Application;
var uidoc = uiApp?.ActiveUIDocument;
if (uidoc?.Document == null) { message = "No document open"; return Result.Failed; }
Document doc = uidoc.Document;
```

Or better, use Option A from Issue #1 to avoid `commandData` entirely.

---

## CRITICAL ISSUE #3: `IsolateElementsTemporary` / `HideElementsTemporary` Without View Validity Check

**File:** `StingCommandHandler.cs` lines 930–974  
**Severity:** HIGH

### Problem

```csharp
private static void ViewIsolateSelected(UIApplication app)
{
    var uidoc = app.ActiveUIDocument;
    if (uidoc?.ActiveView == null) return;
    var ids = uidoc.Selection.GetElementIds();
    if (ids.Count == 0) { TaskDialog.Show("Isolate", "Select elements first."); return; }
    uidoc.ActiveView.IsolateElementsTemporary(ids);  // CAN CRASH
}
```

`IsolateElementsTemporary` throws an unhandled `Autodesk.Revit.Exceptions.InvalidOperationException` if:
- The view is a schedule, legend, or drafting view (no element isolation support)
- The view has a view template applied that locks temporary hide/isolate
- Any element ID in the collection is from a different document or is invalid/deleted

Similar issue in `ViewHideSelected` (line 945) and `ViewRevealHidden` (line 948).

### Fix Required

Wrap in try-catch and validate view type:
```csharp
if (view is ViewSchedule || view is ViewDrafting) return;
try { view.IsolateElementsTemporary(ids); }
catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) { ... }
```

---

## CRITICAL ISSUE #4: `get_BoundingBox(view)` Returns Null — Not Checked in 8+ Places

**File:** `StingCommandHandler.cs` lines 3024, 3608, 3623, 3668, 4189, 4202, 4252, 4305  
**Severity:** HIGH

### Problem

`Element.get_BoundingBox(view)` returns `null` when:
- Element is not visible in the view
- Element has zero extent (e.g., some annotation types)
- Element is in a linked model and the view doesn't show links

Multiple AI selection helpers (SelectNearby, SelectEdgeElements, SelectQuadrant, SelectByBoundingBox, SelectOnGrid) use bounding boxes without null checks. A `NullReferenceException` on `bb.Min` or `bb.Max` would crash these operations.

### Fix Required

Add null checks: `if (bb == null) continue;` before accessing `bb.Min` or `bb.Max`.

---

## CRITICAL ISSUE #5: ComplianceScan Runs After Every Single Command (Performance/Crash Risk)

**File:** `StingCommandHandler.cs` lines 739–761  
**Severity:** HIGH

### Problem

After EVERY command dispatch, the handler runs:
```csharp
var scan = ComplianceScan.Scan(doc);
StingDockPanel.UpdateComplianceStatus(scan.StatusBarText, scan.RAGStatus);
```

The `ComplianceScan.Scan(doc)` iterates all elements using `FilteredElementCollector` and checks tag parameters. If:
- The document was just modified by a command (e.g., material creation, 815 materials)
- The Revit model needs regeneration
- A transaction was just committed and the model is in a transitional state

Then the compliance scan can trigger an access to stale element data, causing `Autodesk.Revit.Exceptions.InvalidObjectException` or worse. The empty catch block `catch { }` (line 761) hides these errors but may not catch unmanaged crashes.

### Fix Required

- Only run ComplianceScan for tag-related commands (already partially done with the switch block, but `Scan()` still runs unconditionally after every command)
- Add proper null/validity checks in ComplianceScan
- Defer scan to an idle event rather than running immediately after command

---

## HIGH ISSUE #6: Inline Helpers That Modify Document Without Transactions

**File:** `StingCommandHandler.cs`  
**Severity:** HIGH

### Problem

Several inline helpers call Revit API methods that require a transaction but don't open one:

1. **`ViewIsolateSelected` (line 936):** `IsolateElementsTemporary` — this is actually OK (temporary view operations don't require transactions)
2. **BUT `ViewRevealHidden` (line 956-959):** Uses reflection to call `EnableTemporaryViewMode` — if the reflection fails silently and falls through to other code paths, unexpected state can result

The bigger concern is `SetHalftone`, `PermanentHide`, `PermanentUnhide`, `UnhideCategory` — these DO wrap in transactions correctly, but there is no check for whether the view supports graphic overrides.

### Fix Required

Add view type validation before calling `SetElementOverrides`, `HideElements`, `UnhideElements`. These fail on schedule views, legend views, and some 3D views.

---

## HIGH ISSUE #7: Tag Operations on IndependentTag Without Reference Validation

**Files:** `StingCommandHandler.cs` (NudgeTags, SnapElbowDirect, FindOrphanedTags, CloneTagLayout, SelectAnnotationTags, SelectHostElements)  
**Severity:** HIGH

### Problem

Multiple inline helpers call:
```csharp
tag.GetTaggedLocalElementIds()
tag.GetTaggedReferences()
tag.SetLeaderElbow(refs.First(), elbowPos)
tag.TagHeadPosition
```

These methods throw if:
- The tag's host element has been deleted (orphaned tag)
- The tag is a multi-category tag with references in a linked model
- `GetTaggedReferences()` returns empty but `.First()` is called on it
- `LeaderEndCondition` is set to a mode incompatible with `SetLeaderElbow`

The SnapElbowDirect method (line 3923) does have try-catch per tag, but NudgeTags (line 3203) delegates to `NudgeTagsCommand.NudgeInDirection` — need to verify that method also handles exceptions per-tag.

### Fix Required

Add per-tag try-catch with `continue` in all tag iteration loops. Validate `refs.Count > 0` before calling `.First()`.

---

## HIGH ISSUE #8: MasterSetupCommand Chain — Cascading Failure Without Proper Recovery

**File:** `MasterSetupCommand.cs` lines 98–230  
**Severity:** HIGH

### Problem

MasterSetup runs 17 steps sequentially within a single `TransactionGroup`. Each step calls `RunCommand()` which creates a NEW fabricated `ExternalCommandData`. If step 1 (LoadSharedParams) succeeds but step 3 (CreateBLEMaterials, creating 815 materials in one transaction) fails partway through:

- The TransactionGroup.Assimilate() may leave the document in an inconsistent state
- Subsequent steps may operate on partially-created data
- Memory consumption from 815+ material creation operations in a single transaction can exceed Revit's internal limits

### Fix Required

- Add explicit memory management between large batch steps
- Consider breaking large operations (815 materials, 168 schedules) into smaller TransactionGroups
- Add `doc.Regenerate()` between steps to ensure model consistency

---

## HIGH ISSUE #9: CSV Column Index Out-of-Range in Material/Family Creation

**Files:** `MaterialCommands.cs`, `FamilyCommands.cs`  
**Severity:** HIGH

### Problem

`MaterialPropertyHelper` uses hardcoded column indices (ColName=6, ColBaseMaterial=34, ColColor=36, etc. up to ColShadingRgb=67). The code only checks `cols.Length > ColName` (line 152 in FamilyCommands.cs) but NOT whether `cols.Length > 67` before accessing higher indices.

`GetCol(cols, ColBaseMaterial)` (called at MaterialCommands.cs line 52) — need to verify this helper does bounds checking. If CSV rows are shorter than expected, accessing `cols[67]` throws `IndexOutOfRangeException`.

The MEP_MATERIALS.csv has 464 rows — if any row has fewer columns than expected, this will crash during material creation.

### Fix Required

Ensure `GetCol()` helper does bounds checking: `return idx < cols.Length ? cols[idx].Trim() : "";`

---

## HIGH ISSUE #10: `ParameterFilterElement.Create` With Empty Category List

**File:** `ScheduleCommands.cs` lines 111–116, `TemplateCommands.cs`  
**Severity:** MODERATE-HIGH

### Problem

When creating view filters, the code tries to resolve category names from CSV to `BuiltInCategory` IDs. If no categories match (e.g., category name misspelling in CSV), the code checks `catIds.Count > 0` before calling `ParameterFilterElement.Create` — this is fine.

However, in `TemplateCommands.cs` CreateFiltersCommand, the discipline filters (line 30-67) use hardcoded `BuiltInCategory` arrays. Some of these may not exist in all Revit versions:
- `BuiltInCategory.OST_FireAlarmDevices` — not available in older Revit versions
- `BuiltInCategory.OST_CommunicationDevices` — version-dependent
- `BuiltInCategory.OST_NurseCallDevices` — version-dependent

Creating multi-category filters with invalid category IDs crashes Revit.

### Fix Required

Wrap each `BuiltInCategory` lookup in try-catch or validate with `Category.GetCategory(doc, bic) != null` before including in filter definitions.

---

## MODERATE ISSUE #11: CompoundStructure Layer Creation With Invalid Materials

**File:** `FamilyCommands.cs` lines 343–684  
**Severity:** MODERATE

### Problem

The log shows: `Type create failed: STING - BASEMENT WALL: One or more layers is not valid.`

`CreateWallType`, `CreateFloorType`, etc. create `CompoundStructureLayer` objects and call `CompoundStructure.SetLayers()`. If:
- Layer thickness is 0 or negative
- Layer material ID is invalid
- Layer function is incompatible with the compound structure type

The `CompoundStructure.SetLayers()` call throws `ArgumentException` with "One or more layers is not valid." This is caught, but the error handling only logs — the command still reports partial success.

### Fix Required

Validate each layer before adding: thickness > 0, material exists in document, function is compatible.

---

## MODERATE ISSUE #12: Thread-Safety of Static State in StingCommandHandler

**File:** `StingCommandHandler.cs`  
**Severity:** MODERATE

### Problem

Multiple static fields store mutable state:
- `_memorySlots` (line 927) — selection memory
- `_conditions` (line 4049) — conditional selection builder
- `CurrentApp` (line 42) — last UIApplication reference

These are set in the `Execute` method which runs on Revit's main thread, so technically safe. However:
- If an external event fires while the panel is still processing the previous command
- If two dockable panels are somehow registered (restart bug)
- The `_memorySlots` dictionary can hold stale `ElementId` references from a closed/reloaded document

### Fix Required

Clear `_memorySlots` when document changes. Add document hash validation to stored ElementIds.

---

## MODERATE ISSUE #13: Worksheet Operations in ScheduleCommands on Non-Workshared Projects

**File:** `TemplateCommands.cs` — CreateWorksetsCommand  
**Severity:** MODERATE

### Problem

`CreateWorksetsCommand` creates 35 ISO 19650 worksets. If the document is NOT workshared (single-user mode), calling `Workset.Create(doc, name)` throws `Autodesk.Revit.Exceptions.InvalidOperationException`.

### Fix Required

Check `doc.IsWorkshared` before creating worksets.

---

## MODERATE ISSUE #14: FormulaEvaluator With Circular Dependencies or Missing Parameters

**File:** `FormulaEvaluatorCommand.cs`  
**Severity:** MODERATE

### Problem

The formula engine evaluates 199 formulas with an expression parser. If:
- A formula references a parameter that doesn't exist (shared params not loaded)
- Circular dependencies exist between formulas
- A formula produces an infinite value or NaN

The recursive descent parser could stack overflow on circular deps, or `SetString`/`Set` could fail on invalid values.

### Fix Required

Add cycle detection in the dependency resolver and validate formula outputs before writing.

---

## MODERATE ISSUE #15: DocAutomationExtCommands Batch Operations Without Cancellation

**File:** `DocAutomationExtCommands.cs`  
**Severity:** MODERATE

### Problem

Batch operations like `BatchCreateViewsCommand`, `BatchCreateSheetsCommand`, `BatchCreateSectionsCommand` iterate over many elements and create views/sheets in bulk. There is no:
- Progress reporting to the user
- Cancellation check (`Keyboard.IsKeyDown(Key.Escape)`)
- Memory management between iterations

For large models, these can hang Revit for minutes with no feedback, and users may force-close Revit thinking it crashed.

### Fix Required

Add `StingProgressDialog` or at minimum an `IsCancellationRequested` check every N iterations.

---

## MODERATE ISSUE #16: ClosedXML Dependency in BOQExport — Assembly Load Failure

**File:** `DataPipelineCommands.cs` (BOQExportCommand)  
**Severity:** MODERATE

### Problem

ClosedXML 0.104.2 has multiple transitive dependencies (DocumentFormat.OpenXml, etc.). If these assemblies are not in the same directory as StingTools.dll, the BOQ export will throw `FileNotFoundException` on first use. This manifests as a crash when the BOQExport button is clicked.

The log shows BOQ export starting (line 865) but no completion log — suggesting it may have crashed.

### Fix Required

Ensure all ClosedXML dependencies are deployed alongside StingTools.dll. Add assembly resolve handler in `OnStartup`:
```csharp
AppDomain.CurrentDomain.AssemblyResolve += (s, args) => { ... };
```

---

## MODERATE ISSUE #17: `view.Scale = 1` on Views That Don't Support Scale Changes

**File:** `StingCommandHandler.cs` line 2268 (LegendUniformSize)  
**Severity:** MODERATE

### Problem

Setting `view.Scale = 1` can throw if:
- The view has a view template that locks the scale parameter
- The view is a dependent view with scale controlled by parent
- Scale value 1 (1:1) is not valid for the view type

### Fix Required

Wrap in try-catch per view and check `view.IsViewTemplateAssigned()` before modifying.

---

## MODERATE ISSUE #18: Category.get_Visible / set_Visible on Unsupported Categories

**File:** `StingCommandHandler.cs` line 1345 (UnhideCategory)  
**Severity:** MODERATE

### Problem

```csharp
foreach (Category cat in doc.Settings.Categories)
{
    try { if (cat.get_Visible(view) == false) cat.set_Visible(view, true); }
    catch { }
}
```

Many categories throw `Autodesk.Revit.Exceptions.InvalidOperationException` on `get_Visible` or `set_Visible` because they are model categories being queried in annotation context, or vice versa. The catch block handles this, but iterating ALL categories is also very slow.

### Fix Required

Filter to only `CategoryType.Model` and `CategoryType.Annotation` categories.

---

## LOW ISSUE #19: TaskDialogCommandLinkId Enum Mismatch

**File:** `StingCommandHandler.cs` line 1641  
**Severity:** LOW

### Problem

```csharp
td.AddCommandLink((TaskDialogCommandLinkId)(1001 + linkCount), name);
```

`TaskDialogCommandLinkId` only supports `CommandLink1` through `CommandLink4` (values 1001–1004). If `names.Take(4)` returns exactly 4, the enum values 1001-1004 are used correctly. But the cast `(TaskDialogCommandLinkId)(1001 + linkCount)` is fragile — if the enum values change or are different on some Revit versions, this will produce invalid links.

### Fix Required

Use the named enum values directly.

---

## LOW ISSUE #20: StingLog.Info Called With Large Strings (Memory Pressure)

**File:** `StingCommandHandler.cs` line 868 (ExternalCommandData field logging)  
**Severity:** LOW

### Problem

In `CreateCommandData` Strategy 2 (line 867–868):
```csharp
foreach (var f in fields)
    StingLog.Info($"  ExternalCommandData field: {f.Name} ({f.FieldType.Name})");
```

This logs EVERY field of `ExternalCommandData` for EVERY command invocation, not just the first time. This can generate significant log I/O.

### Fix Required

Add a static `bool _fieldsLogged = false;` guard.

---

## SUMMARY — Priority Fix Order for Claude Code

| Priority | Issue # | Description | Impact |
|----------|---------|-------------|--------|
| **P0** | #1 | ExternalCommandData fabrication via reflection | Root cause of most crashes |
| **P0** | #2 | Null-unsafe `commandData.Application` chain | Every command can crash |
| **P1** | #5 | ComplianceScan after every command | Performance + secondary crashes |
| **P1** | #4 | `get_BoundingBox` null not checked (8 places) | AI Select commands crash |
| **P1** | #3 | View operations without type validation | View isolate/hide crashes |
| **P1** | #7 | Tag operations without reference validation | Organise tab crashes |
| **P1** | #10 | Version-dependent BuiltInCategory enums | Filter creation crashes |
| **P1** | #16 | ClosedXML assembly load failure | BOQ export crash |
| **P2** | #8 | MasterSetup cascading failure | Master Setup crash |
| **P2** | #9 | CSV column out-of-range | Material creation crash |
| **P2** | #6 | View type validation for graphic overrides | Various crashes |
| **P2** | #11 | CompoundStructure invalid layers | Wall/floor type creation |
| **P2** | #13 | Workset creation on non-workshared doc | CreateWorksets crash |
| **P2** | #14 | Formula circular dependencies | FormulaEvaluator crash |
| **P2** | #15 | Batch operations without cancellation | Apparent hang/crash |
| **P3** | #12 | Static state stale ElementIds | Memory slot corruption |
| **P3** | #17 | View scale on locked views | LegendUniform crash |
| **P3** | #18 | Category visibility iteration | Slow + exceptions |
| **P3** | #19 | TaskDialogCommandLinkId cast fragility | Minor UI issue |
| **P3** | #20 | Excessive logging per command | Performance |

---

## RECOMMENDED ARCHITECTURE FIX (Issue #1 Resolution)

The cleanest fix for Issue #1 (which eliminates Issue #2 as well) is to create a parallel execution path for panel-dispatched commands:

```csharp
// New interface for panel-invokable commands
public interface IPanelCommand
{
    Result ExecuteFromPanel(UIApplication app, ref string message);
}

// RunCommand now uses UIApplication directly
private static void RunCommand<T>(UIApplication app) where T : IExternalCommand, new()
{
    var cmd = new T();
    
    // If command implements IPanelCommand, use the direct path
    if (cmd is IPanelCommand panelCmd)
    {
        string message = "";
        panelCmd.ExecuteFromPanel(app, ref message);
        return;
    }
    
    // Fallback: use UIApplication directly
    var uidoc = app.ActiveUIDocument;
    if (uidoc == null) { TaskDialog.Show("STING", "No document open."); return; }
    
    // Call Execute with null commandData — commands must handle this
    string msg = "";
    var elements = new ElementSet();
    cmd.Execute(null, ref msg, elements);  // Commands need null-safety
}
```

This avoids the reflection hack entirely and makes the dockable panel a first-class execution context.
