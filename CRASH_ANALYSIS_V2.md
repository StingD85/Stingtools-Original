# STINGTOOLS — Deep Crash Analysis V2

**Date:** 2026-03-07  
**Context:** Previous report's fixes were applied (IPanelCommand interface, ComplianceScan gating, null checks, assembly resolver) but Revit still crashes at most buttons across: Create Params, Data Tagging, Visual Tagging, Tag Operations, Temp, Docs, and Create tabs.

---

## THE SINGLE ROOT CAUSE: IPanelCommand Was Never Implemented on Any Command

The previous fix created the `IPanelCommand` interface and added a check in `RunCommand<T>()`:

```csharp
if (cmd is Core.IPanelCommand panelCmd)
{
    panelCmd.Execute(app);
    return;
}
// Fallback: fabricate ExternalCommandData for commands not yet migrated
```

**But zero of the 250 IExternalCommand classes implement `IPanelCommand`.** Every single button click still falls through to `CreateCommandData(app)` which fabricates a fake `ExternalCommandData` using `RuntimeHelpers.GetUninitializedObject()` — a fundamentally broken approach that produces corrupted native COM wrappers.

Proof:
```
$ grep -rn "IPanelCommand" *.cs
IPanelCommand.cs:12:    public interface IPanelCommand
StingCommandHandler.cs:794:    // Prefer IPanelCommand
StingCommandHandler.cs:797:    if (cmd is Core.IPanelCommand panelCmd)
```

No command class anywhere contains `: IPanelCommand` or `implements IPanelCommand`. The interface exists but is never used. **This is why every button still crashes.**

---

## REQUIRED FIX: Eliminate ExternalCommandData From the Panel Path Entirely

The `RunCommand<T>` method must be rewritten to **never fabricate ExternalCommandData**. Instead, it should extract `UIApplication` → `UIDocument` → `Document` directly and call each command's logic.

### Fix Option A: Rewrite RunCommand to bypass ExternalCommandData completely

Replace the current `RunCommand<T>` with a version that does NOT call `CreateCommandData` at all:

```csharp
private static void RunCommand<T>(UIApplication app) where T : IExternalCommand, new()
{
    try
    {
        // Validate we have a valid context before even creating the command
        var uidoc = app.ActiveUIDocument;
        if (uidoc == null)
        {
            TaskDialog.Show("STING Tools", "No document is open.");
            return;
        }

        var cmd = new T();
        string message = "";
        var elements = new ElementSet();

        // USE THE REAL UIApplication to create a proper wrapper
        // that does NOT use reflection or GetUninitializedObject.
        // Since ExternalCommandData cannot be properly constructed
        // outside Revit's internal pipeline, we must provide the
        // UIApplication through a side channel.
        StingCommandHandler.CurrentApp = app;
        
        // The commands access commandData.Application.ActiveUIDocument.Document
        // so we need commandData.Application to return a real UIApplication.
        // The ONLY safe way is to use the actual Revit-provided app object.
        cmd.Execute(null, ref message, elements);
    }
    catch (NullReferenceException)
    {
        // Command tried to access commandData directly - it needs migration
        StingLog.Error($"RunCommand<{typeof(T).Name}>: command accesses null commandData");
        TaskDialog.Show("STING Tools",
            $"{typeof(T).Name} needs migration to panel-safe pattern.\n" +
            "Use StingCommandHandler.CurrentApp instead of commandData.");
    }
    catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
    catch (Exception ex)
    {
        StingLog.Error($"RunCommand<{typeof(T).Name}> failed", ex);
        TaskDialog.Show("STING Tools", $"{typeof(T).Name} failed:\n{ex.Message}");
    }
}
```

**But this will crash too**, because every command does `commandData.Application.ActiveUIDocument.Document` on line 1 and `commandData` would be null.

### Fix Option B (THE ACTUAL FIX): Migrate every command's Execute method

Every command must be changed from:

```csharp
public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
{
    Document doc = commandData.Application.ActiveUIDocument.Document;
    // ...
}
```

To:

```csharp
public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
{
    // Panel-safe: use CurrentApp when commandData is fabricated/unreliable
    UIApplication uiApp = commandData?.Application 
        ?? StingCommandHandler.CurrentApp;
    if (uiApp == null) { message = "No application context"; return Result.Failed; }
    
    UIDocument uidoc = uiApp.ActiveUIDocument;
    if (uidoc == null) { message = "No document open"; return Result.Failed; }
    
    Document doc = uidoc.Document;
    // ...
}
```

**This is a mechanical find-and-replace across all 250 commands.** The pattern varies slightly:

---

## COMPLETE LIST OF PATTERNS TO FIX

### Pattern 1: Direct Document access (174 occurrences across all files)

**Find:**
```csharp
Document doc = commandData.Application.ActiveUIDocument.Document;
```
**Replace with:**
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
if (uiApp?.ActiveUIDocument == null) { message = "No document open"; return Result.Failed; }
Document doc = uiApp.ActiveUIDocument.Document;
```

**Files affected:** AutoTagCommand.cs, BatchTagCommand.cs, CheckDataCommand.cs, CombineParametersCommand.cs, ConfigEditorCommand.cs, CreateParametersCommand.cs, DataPipelineCommands.cs, DocAutomationCommands.cs, DocAutomationExtCommands.cs, FamilyCommands.cs, FamilyStagePopulateCommand.cs, FormulaEvaluatorCommand.cs, LegendBuilderCommands.cs, LoadSharedParamsCommand.cs, MasterSetupCommand.cs, MaterialCommands.cs, ParagraphDepthCommand.cs, PresentationModeCommand.cs, PreTagAuditCommand.cs, ResolveAllIssuesCommand.cs, RichTagDisplayCommands.cs, ScheduleCommands.cs, ScheduleEnhancementCommands.cs, SheetIndexCommand.cs, SheetOrganizerCommand.cs, SmartTagPlacementCommand.cs, SyncParameterSchemaCommand.cs, SystemParamPushCommand.cs, TagAndCombineCommand.cs, TagConfigCommand.cs, TagFamilyCreatorCommand.cs, TemplateCommands.cs, TemplateExtCommands.cs, TemplateManagerCommands.cs, TokenWriterCommands.cs, TransmittalCommand.cs, ValidateTagsCommand.cs, ViewAutomationCommands.cs, ViewOrganizerCommand.cs, ViewportCommands.cs, ColorCommands.cs, CategorySelectCommands.cs, StateSelectCommands.cs

### Pattern 2: UIDocument access with Selection (50+ occurrences)

**Find:**
```csharp
UIDocument uidoc = commandData.Application.ActiveUIDocument;
```
**Replace with:**
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
UIDocument uidoc = uiApp?.ActiveUIDocument;
if (uidoc == null) { message = "No document open"; return Result.Failed; }
```

**Files affected:** AutoTagCommand.cs, CategorySelectCommands.cs, ColorCommands.cs, DocAutomationExtCommands.cs, FamilyStagePopulateCommand.cs, LegendBuilderCommands.cs, ParagraphDepthCommand.cs, RichTagDisplayCommands.cs, SmartTagPlacementCommand.cs, StateSelectCommands.cs, TagOperationCommands.cs (uses `cmd` not `commandData`)

### Pattern 3: Inner Application access (6 occurrences — MOST DANGEROUS)

**Find:**
```csharp
Autodesk.Revit.ApplicationServices.Application app = commandData.Application.Application;
```
**Replace with:**
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
if (uiApp == null) { message = "No application context"; return Result.Failed; }
Autodesk.Revit.ApplicationServices.Application app = uiApp.Application;
```

**Files affected:** LoadSharedParamsCommand.cs (line 28), TemplateManagerCommands.cs (lines 2443, 2444, 2517, 2569, 2572)

### Pattern 4: TagOperationCommands uses `cmd` parameter name

**Find:**
```csharp
public Result Execute(ExternalCommandData cmd, ref string msg, ElementSet el)
{
    UIDocument uidoc = cmd.Application.ActiveUIDocument;
```
**Replace with:**
```csharp
public Result Execute(ExternalCommandData cmd, ref string msg, ElementSet el)
{
    UIApplication uiApp = cmd?.Application ?? UI.StingCommandHandler.CurrentApp;
    UIDocument uidoc = uiApp?.ActiveUIDocument;
    if (uidoc == null) { msg = "No document open"; return Result.Failed; }
```

**File:** TagOperationCommands.cs (20+ command classes in this file)

### Pattern 5: Nullable Document access

**Find:**
```csharp
Document doc = commandData.Application.ActiveUIDocument?.Document;
```
**Replace with:**
```csharp
UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
Document doc = uiApp?.ActiveUIDocument?.Document;
if (doc == null) { message = "No document open"; return Result.Failed; }
```

**Files affected:** DataPipelineCommands.cs (lines 1030, 1891, 2101, 2215)

---

## ALSO REQUIRED: Fix RunCommand to pass null-safe commandData

After the command patterns are fixed, `RunCommand<T>` should simply pass `null` for commandData since every command now falls back to `CurrentApp`:

```csharp
private static void RunCommand<T>(UIApplication app) where T : IExternalCommand, new()
{
    try
    {
        var cmd = new T();
        string message = "";
        var elements = new ElementSet();
        
        // Set CurrentApp so commands can use it as fallback
        CurrentApp = app;
        
        // Pass null for commandData — all commands now use CurrentApp fallback
        cmd.Execute(null, ref message, elements);
    }
    catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
    catch (Exception ex)
    {
        StingLog.Error($"RunCommand<{typeof(T).Name}> failed", ex);
        TaskDialog.Show("STING Tools", $"{typeof(T).Name} failed:\n{ex.Message}");
    }
}
```

**Delete the entire `CreateCommandData` method** — it is no longer needed.

---

## SECONDARY ISSUES (fix after primary migration)

### Issue S1: MasterSetupCommand passes commandData to sub-commands

**File:** MasterSetupCommand.cs  
MasterSetup creates sub-command instances and calls their Execute:
```csharp
RunCommand(new Tags.LoadSharedParamsCommand(), commandData, elements)
```
After the migration, if MasterSetup's own `commandData` is null, the sub-commands need `CurrentApp` too. Ensure `CurrentApp` is set before the MasterSetup chain runs.

### Issue S2: StingAutoTagger.cs may trigger on document modification thread

If the IUpdater fires during a command's transaction, it could access stale state. Ensure `StingAutoTagger` is disabled during batch operations.

### Issue S3: TemplateManager.LoadCategoryBindings() file access during LoadSharedParams

`LoadSharedParamsCommand` (line 99) calls `Temp.TemplateManager.LoadCategoryBindings()`. If this method fails (file not found, parse error), it should return an empty dictionary, not throw. Verify this.

### Issue S4: SharedParamGuids.BuildCategorySet could fail on unknown categories

`SharedParamGuids.BuildCategorySet(doc, SharedParamGuids.AllCategoryEnums)` at line 60 of LoadSharedParamsCommand.cs resolves BuiltInCategory enums to Category objects. If any enum is not valid in the current Revit version, this could throw. Ensure per-category try-catch.

### Issue S5: DocAutomationExtCommands batch operations

Several DocAutomation commands (BatchCreateViews, BatchCreateSheets, BatchCreateSections, BatchCreateElevations) create many elements without progress or cancellation. While this won't crash Revit, it can cause apparent hangs.

---

## VALIDATION CHECKLIST FOR CLAUDE CODE

After applying fixes, verify:

1. [ ] `grep -rn "IPanelCommand" *.cs` — should show 0 implementing classes (interface can be deleted)
2. [ ] `grep -rn "CreateCommandData" *.cs` — should show 0 results (method deleted)
3. [ ] `grep -rn "GetUninitializedObject" *.cs` — should show 0 results
4. [ ] `grep -rn "commandData\.Application\." *.cs` — should show 0 results (all migrated to `uiApp`)
5. [ ] `grep -rn "commandData\?" *.cs` — should show 250+ results (null-safe access everywhere)
6. [ ] Every command's first lines should be:
   ```csharp
   UIApplication uiApp = commandData?.Application ?? UI.StingCommandHandler.CurrentApp;
   ```
7. [ ] `RunCommand<T>` should NOT call `CreateCommandData` — just set `CurrentApp = app` and call `Execute(null, ...)`

---

## EXECUTION PLAN FOR CLAUDE CODE

**Step 1:** Delete `CreateCommandData` method from StingCommandHandler.cs  
**Step 2:** Rewrite `RunCommand<T>` to set `CurrentApp = app` and pass `null` for commandData  
**Step 3:** Mechanical find-and-replace across all 55 .cs command files:
  - Replace `commandData.Application.ActiveUIDocument.Document` → null-safe `CurrentApp` fallback pattern
  - Replace `commandData.Application.ActiveUIDocument` → null-safe pattern
  - Replace `commandData.Application.Application` → null-safe pattern
  - Replace `cmd.Application.ActiveUIDocument` → null-safe pattern (TagOperationCommands.cs)
**Step 4:** Fix MasterSetupCommand to ensure `CurrentApp` is available for sub-command chain  
**Step 5:** Build and test  
**Step 6:** Delete `IPanelCommand.cs` (no longer needed)

**Total scope:** ~250 command Execute methods need a 2-line addition at the top. This is a mechanical, safe refactor — the UIApplication from `CurrentApp` is the exact same object that Revit provides to the IExternalEventHandler, so all API calls will work identically.
