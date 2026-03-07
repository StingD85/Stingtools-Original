# STINGTOOLS — Crash Fix V3 (With Minidump & Journal Evidence)

## CRASH DIAGNOSIS FROM ACTUAL CRASH FILES

### Minidump Analysis
- **Exception code: `0xC00000FD` = STACK_OVERFLOW** (NOT access violation)
- **Faulting module: `coreclr.dll`** at offset 0x16FC27
- **Thread: 9836** (same as CER report)
- **StingTools.dll loaded at: `0x00000243D24B0000`** (size 0x162000 = 1.4MB)

### Journal Analysis (journal.0310.txt)
The **very last entry before crash** (line 2177-2182):
```
Jrn.AddInEvent "AddInJournaling", "ApiDockablePane(STING Tools,StingTools\.UI\.StingDockPanel)
  .WpfTabControl(0,tabMain).Select(3,System\.Windows\.Controls\.TabItem Header:TEMP Content:)"

' 0:< ANTECEDENT:      ← Revit's crash marker
'editor ArrowEditor

DBG_INFO: readParamDatabase could not find a value for 'HIDEWHENNOVALUE',
  defaulting to 'false': line 861 of ExternalParamDatabase.cpp.
```
**The user clicked the TEMP tab. Revit immediately crashed with a stack overflow.**

No button was clicked. No command was dispatched. The crash happens during WPF tab content initialization.

### Additional Journal Findings
- **Duplicate addin warning** (line 251): `StingTools.addin` exists in BOTH `%AppData%\Roaming\Autodesk\Revit\Addins\2025\` AND `%ProgramData%\Autodesk\Revit\Addins\2025\`
- **Three STING addins loaded simultaneously:** StingTools.dll, StingBIM.AI.Revit.dll, plus pyRevit STINGTags/STINGTemp/StingDocs extensions
- **StingTools.dll: 0x162000 bytes (1.4MB)** — very large for a Revit plugin assembly

---

## ROOT CAUSE: WPF Visual Tree Stack Overflow

The StingDockPanel.xaml contains:
- **488 buttons** with `Click="Cmd_Click"`  
- **654 StaticResource references**
- **1,655 lines** of XAML
- **6 TabItems** each containing deep StackPanel/WrapPanel/Border nesting

When WPF first loads a tab, it instantiates the entire visual tree for that tab's content. The TEMP tab alone has **60+ buttons** inside multiple nested Border/StackPanel/WrapPanel containers. The WPF layout engine performs a recursive measure/arrange pass that walks every element. With this many nested elements plus StaticResource resolution at each level, the call stack exceeds the default thread stack size (1MB for .NET threads in Revit's hosting context).

**Why it happens on TEMP tab specifically:** The TEMP tab is the largest tab with the deepest nesting (setup section + materials + family types + schedules + schedule enhancements + templates/views + template manager + styles + data QA + workflows + advanced automation). It likely pushes the already-deep WPF stack past the overflow threshold.

**Why `coreclr.dll` is the crash module:** The stack overflow occurs inside the CLR's stack probing mechanism. When the managed stack grows too deep, coreclr.dll's stack probe at offset 0x16FC27 detects it has no more stack space and raises STATUS_STACK_OVERFLOW.

---

## REQUIRED FIXES (Ordered by Priority)

### FIX 1 (CRITICAL): Reduce WPF Visual Tree Depth — Virtualize Tabs

**The tabs must use lazy/deferred loading.** Currently all 6 tabs' content is instantiated when the panel loads or when a tab is first selected. The TEMP tab has too many elements.

**Option A — Virtualized TabControl with ContentTemplateSelector:**
Replace the static TabItem content with DataTemplates that only instantiate when selected:

```xml
<!-- Instead of 6 TabItems with inline content, use ContentTemplate -->
<TabControl x:Name="tabMain" SelectionChanged="TabMain_SelectionChanged">
    <TabItem Header="SELECT" Tag="SELECT"/>
    <TabItem Header="ORGANISE" Tag="ORGANISE"/>
    <TabItem Header="DOCS" Tag="DOCS"/>
    <TabItem Header="TEMP" Tag="TEMP"/>
    <TabItem Header="CREATE" Tag="CREATE"/>
    <TabItem Header="VIEW" Tag="VIEW"/>
</TabControl>
```

Then in code-behind, load tab content on-demand:
```csharp
private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (tabMain.SelectedItem is TabItem tab && tab.Content == null)
    {
        tab.Content = CreateTabContent(tab.Tag?.ToString());
    }
}
```

**Option B (Simpler) — Split TEMP tab into sub-sections with Expanders:**
Replace the monolithic TEMP tab ScrollViewer/StackPanel with collapsible Expander sections that start collapsed. Only the expanded section's content participates in layout.

**Option C (Quickest) — Break TEMP tab into two tabs:**
Split TEMP into "TEMP" (setup + materials + families + schedules) and "STYLE" (templates + styles + QA + workflows). This halves the visual tree depth per tab.

### FIX 2 (CRITICAL): Remove Duplicate Addin

The journal shows:
```
DBG_WARN: The addin file -StingTools.addin- in all user folder is duplicated.
Duplicate addins: 
  C:\Users\del\AppData\Roaming\Autodesk\Revit\Addins\2025\StingTools.addin
  C:\ProgramData\Autodesk\Revit\Addins\2025\StingTools.addin
```

**Delete one copy.** Duplicate addins can cause double-registration of dockable panes, external events, and updaters — leading to memory doubling and potential race conditions. Keep only the `%AppData%\Roaming` copy.

### FIX 3 (HIGH): Fix the ExternalCommandData Fabrication

The previous reports' fix (IPanelCommand + CurrentApp fallback) was never implemented. **This still needs to be done** for when buttons are actually clicked. But the minidump proves the CURRENT crash is the WPF stack overflow, not the ExternalCommandData issue. Fix the stack overflow first, then fix the ExternalCommandData path so buttons work after the tab loads.

The ExternalCommandData fix from CRASH_FIX_FOR_CLAUDE_CODE.md is still fully valid — apply it after fixing the WPF issue.

### FIX 4 (MODERATE): Reduce pyRevit Duplication

Three separate pyRevit-based STING extensions are also loaded:
- `pyRevit_2025_407afc133c62ed55_StingDocs.dll`
- `pyRevit_2025_08efda263a45ea23_STINGTags.dll`  
- `pyRevit_2025_abaa8f2776181c24_STINGTemp.dll`

Plus `StingBIM.AI.Revit.dll`. These are the legacy pyRevit extensions that StingTools.dll was meant to replace. Having both loaded doubles memory usage, adds conflicting ribbon buttons, and increases total managed heap pressure — all of which reduce available stack space.

**Remove the legacy pyRevit STING extensions** from `%AppData%\Roaming\pyRevit\Extensions\` since StingTools.dll now provides all their functionality.

---

## EXECUTION PLAN FOR CLAUDE CODE

**Step 1: Split or virtualize the WPF TEMP tab** (fixes the actual crash)
- Either split TEMP into two smaller tabs OR use lazy content loading
- Target: no single tab should have more than 40 buttons
- Test by opening Revit and clicking each tab

**Step 2: Apply ExternalCommandData fix** (from CRASH_FIX_FOR_CLAUDE_CODE.md)
- Rewrite RunCommand<T> to pass null and use CurrentApp
- Add `commandData?.Application ?? StingCommandHandler.CurrentApp` fallback to all 222 command call sites
- Delete CreateCommandData method

**Step 3: Document the deployment fix**
- Add a note that only ONE copy of StingTools.addin should exist
- Add a note that pyRevit STING extensions should be disabled when StingTools.dll is active

---

## VERIFICATION

After applying fixes, the following should hold:
1. Clicking every tab in the dockable panel should NOT crash Revit
2. Clicking buttons in each tab should show TaskDialogs (not crash)
3. The StingTools log should show `CreateCommandData` entries have been eliminated
4. Only one StingTools.addin should exist in the addins folders
