# -*- coding: utf-8 -*-
"""
STINGTemp dockable window - main interface for the extension.

Registers a WPF dockable pane containing all 35 tool buttons organised
in 6 collapsible sections (Expanders), plus a data-file status section.
Each button executes the corresponding script.py from the STINGTemp.tab
directory tree.

Loaded automatically by pyRevit at extension initialisation.
Compatible with Revit 2025-2027.

Version: 6.0.0
"""

import os
import sys

EXTENSION_ROOT = os.path.dirname(os.path.abspath(__file__))
LIB_DIR = os.path.join(EXTENSION_ROOT, 'lib')
TAB_DIR = os.path.join(EXTENSION_ROOT, 'STINGTemp.tab')
if LIB_DIR not in sys.path:
    sys.path.insert(0, LIB_DIR)

try:
    import clr
    clr.AddReference('RevitAPI')
    clr.AddReference('RevitAPIUI')
    clr.AddReference('PresentationFramework')
    clr.AddReference('PresentationCore')
    clr.AddReference('WindowsBase')

    import System
    from System import Guid, EventHandler
    from System.Windows import (
        Thickness, TextWrapping, HorizontalAlignment, VerticalAlignment,
        CornerRadius, RoutedEventArgs,
    )
    from System.Windows.Controls import (
        StackPanel, TextBlock, ScrollViewer, Border, Button, Expander,
        Orientation, WrapPanel, ToolTip, Grid, RowDefinition, ColumnDefinition,
    )
    from System.Windows.Media import (
        SolidColorBrush, Brushes, Color as WpfColor, FontFamily,
    )
    from System.Windows.Input import Cursors
    from Autodesk.Revit.UI import DockablePaneId, IDockablePaneProvider

    import data_loader

    PANEL_GUID = Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")

    # -----------------------------------------------------------------------
    # Button definitions per section
    # (panel_folder, pushbutton_folder, display_label, tooltip)
    # -----------------------------------------------------------------------
    SECTIONS = [
        {
            'title': '1  Setup',
            'color_rgb': (41, 98, 255),
            'expanded': True,
            'buttons': [
                ('1_Setup.panel', 'Batch Add Family Params.pushbutton',
                 'Batch add family params',
                 'Add shared parameters to families from CSV data (973 params, 47 categories)'),
                ('1_Setup.panel', 'Create Parameters.pushbutton',
                 'Create parameters',
                 'Bind shared parameters to the active project'),
                ('1_Setup.panel', 'Check openpyxl Installation.pushbutton',
                 'Check openpyxl',
                 'Verify openpyxl and list data files with SHA hashes'),
                ('1_Setup.panel', 'Check pyRevit Version.pushbutton',
                 'Extension info',
                 'Show pyRevit version, extension version, data file inventory'),
            ],
        },
        {
            'title': '2  Materials',
            'color_rgb': (0, 150, 80),
            'expanded': False,
            'buttons': [
                ('2_Materials.panel', 'Clean CSV Duplicate Columns.pushbutton',
                 'Clean CSV duplicates',
                 'Detect and remove duplicate columns in material CSVs'),
                ('2_Materials.panel', '2. Create Base Materials.pushbutton',
                 'Create base materials',
                 'Create Revit materials from BLE + MEP libraries (1279 total)'),
                ('2_Materials.panel', '3. Create BLE Materials (Py3).pushbutton',
                 'Create BLE materials',
                 'Building-element materials with appearance assets (815 materials)'),
                ('2_Materials.panel', '3. Create MEP Materials.pushbutton',
                 'Create MEP materials',
                 'MEP materials from MEP_MATERIALS.csv (464 materials)'),
            ],
        },
        {
            'title': '3  BLE families',
            'color_rgb': (180, 100, 20),
            'expanded': False,
            'buttons': [
                ('3_BLE_Families.panel', '1. Create Walls.pushbutton',
                 'Create walls', 'Wall types from BLE_MATERIALS.csv'),
                ('3_BLE_Families.panel', '2. Create Ceilings.pushbutton',
                 'Create ceilings', 'Ceiling types from BLE_MATERIALS.csv'),
                ('3_BLE_Families.panel', '3. Create Floors.pushbutton',
                 'Create floors', 'Floor types from BLE_MATERIALS.csv'),
                ('3_BLE_Families.panel', '4. Create Roofs.pushbutton',
                 'Create roofs', 'Roof types from BLE_MATERIALS.csv'),
            ],
        },
        {
            'title': '4  MEP families',
            'color_rgb': (200, 60, 60),
            'expanded': False,
            'buttons': [
                ('4_MEP_Families.panel', 'Create Cable Trays.pushbutton',
                 'Create cable trays', 'Cable tray types from MEP_MATERIALS.csv'),
                ('4_MEP_Families.panel', 'Create Conduits.pushbutton',
                 'Create conduits', 'Conduit types from MEP_MATERIALS.csv'),
                ('4_MEP_Families.panel', 'Create Ducts.pushbutton',
                 'Create ducts', 'Duct types from MEP_MATERIALS.csv'),
                ('4_MEP_Families.panel', 'Create Pipes.pushbutton',
                 'Create pipes', 'Pipe types from MEP_MATERIALS.csv'),
            ],
        },
        {
            'title': '5  Schedules',
            'color_rgb': (120, 60, 180),
            'expanded': False,
            'buttons': [
                ('5_Schedules.panel', 'Universal AutoPopulate.pushbutton',
                 'AutoPopulate', 'Apply field remaps across categories (42 remaps)'),
                ('5_Schedules.panel', 'Create Material Schedules.pushbutton',
                 'Material schedules', 'Create material takeoff schedules'),
                ('5_Schedules.panel', 'Batch Create Schedules.pushbutton',
                 'Batch create schedules', 'Multi-discipline schedule creation (168 defs)'),
                ('5_Schedules.panel', 'Export Schedules to CSV.pushbutton',
                 'Export to CSV', 'Export schedule data to CSV files'),
                ('5_Schedules.panel', 'Extract Data.pushbutton',
                 'Extract data', 'Export element parameters to CSV'),
                ('5_Schedules.panel', 'Populate Takeoff Params.pushbutton',
                 'Populate takeoff', 'Apply formulas to elements (197 formulas)'),
            ],
        },
        {
            'title': '6  Templates',
            'color_rgb': (80, 80, 80),
            'expanded': False,
            'buttons': [
                ('6_Templates.panel', 'Apply Filters to Views.pushbutton',
                 'Apply filters', 'Apply view filters to selected views'),
                ('6_Templates.panel', 'Apply VG Overrides.pushbutton',
                 'VG overrides', 'Apply visibility/graphics overrides'),
                ('6_Templates.panel', 'Configure Objects.pushbutton',
                 'Object styles', 'Set object styles for model categories'),
                ('6_Templates.panel', 'Create Dim Styles.pushbutton',
                 'Dim styles', 'Create dimension types'),
                ('6_Templates.panel', 'Create Filters.pushbutton',
                 'Create filters', 'Create view filters'),
                ('6_Templates.panel', 'Create Line Patterns.pushbutton',
                 'Line patterns', 'Create line patterns'),
                ('6_Templates.panel', 'Create Line Styles.pushbutton',
                 'Line styles', 'Create line styles'),
                ('6_Templates.panel', 'Create Phases.pushbutton',
                 'Phases', 'Create project phases'),
                ('6_Templates.panel', 'Create Schedules.pushbutton',
                 'Schedules', 'Create schedule views'),
                ('6_Templates.panel', 'Create Text Styles.pushbutton',
                 'Text styles', 'Create text types'),
                ('6_Templates.panel', 'Create VG Schemes.pushbutton',
                 'VG schemes', 'Create VG schemes'),
                ('6_Templates.panel', 'Create View Templates.pushbutton',
                 'View templates', 'Create view templates'),
                ('6_Templates.panel', 'Create Worksets.pushbutton',
                 'Worksets', 'Create worksets (46 definitions)'),
            ],
        },
    ]

    # -------------------------------------------------------------------
    # Script execution via pyRevit's executor
    # -------------------------------------------------------------------
    def _run_script(script_path):
        """Execute a button script using pyRevit's script execution engine."""
        if not os.path.isfile(script_path):
            from Autodesk.Revit.UI import TaskDialog
            TaskDialog.Show("STINGTemp",
                            "Script not found:\n{}".format(script_path))
            return
        try:
            from pyrevit import script as _prs
            from pyrevit.loader import sessionmgr as _sm
            # Use pyRevit command execution where available
            _sm.execute_script(script_path)
        except (ImportError, AttributeError):
            # Fallback: direct execution
            globs = {'__file__': script_path, '__name__': '__main__'}
            with open(script_path, 'r') as fh:
                code = compile(fh.read(), script_path, 'exec')
            exec(code, globs)

    def _make_click_handler(path):
        """Create a click-event closure for a script path."""
        def handler(sender, args):
            _run_script(path)
        return handler

    # -------------------------------------------------------------------
    # WPF panel builder
    # -------------------------------------------------------------------
    class STINGTempDockableWindow(IDockablePaneProvider):
        """Dockable window with 35 buttons in 6 collapsible sections."""

        def SetupDockablePane(self, data):
            data.FrameworkElement = self._build()

        def _build(self):
            scroll = ScrollViewer()
            scroll.VerticalScrollBarVisibility = 1   # Auto
            scroll.HorizontalScrollBarVisibility = 3  # Disabled

            root = StackPanel()
            root.Margin = Thickness(6)

            # -- Blue header banner ------------------------------------
            hdr_border = Border()
            hdr_border.Background = SolidColorBrush(
                WpfColor.FromRgb(41, 98, 255))
            hdr_border.CornerRadius = CornerRadius(4)
            hdr_border.Padding = Thickness(10, 8, 10, 8)
            hdr_border.Margin = Thickness(0, 0, 0, 6)

            hdr_stack = StackPanel()

            title_tb = TextBlock()
            title_tb.Text = "STINGTemp"
            title_tb.FontSize = 16
            title_tb.FontWeight = System.Windows.FontWeights.Bold
            title_tb.Foreground = Brushes.White
            hdr_stack.Children.Add(title_tb)

            sub_tb = TextBlock()
            sub_tb.Text = "ISO 19650-3:2020 BIM Template Toolkit v6.0"
            sub_tb.FontSize = 9
            sub_tb.Foreground = SolidColorBrush(
                WpfColor.FromRgb(200, 215, 255))
            sub_tb.Margin = Thickness(0, 2, 0, 0)
            hdr_stack.Children.Add(sub_tb)

            hdr_border.Child = hdr_stack
            root.Children.Add(hdr_border)

            # -- Tool sections -----------------------------------------
            for section in SECTIONS:
                root.Children.Add(self._build_section(section))

            # -- Data files status -------------------------------------
            root.Children.Add(self._build_data_section())

            # -- Footer ------------------------------------------------
            footer = TextBlock()
            footer.Text = (
                "STINGTemp v6.0.0  |  Revit 2025-2027  |  35 tools"
            )
            footer.FontSize = 8
            footer.Foreground = Brushes.Gray
            footer.HorizontalAlignment = HorizontalAlignment.Center
            footer.Margin = Thickness(0, 8, 0, 4)
            root.Children.Add(footer)

            scroll.Content = root
            return scroll

        # ---------------------------------------------------------------
        def _build_section(self, section):
            """Collapsible Expander with WrapPanel of buttons."""
            r, g, b = section['color_rgb']
            accent = SolidColorBrush(WpfColor.FromRgb(r, g, b))
            light_bg = SolidColorBrush(WpfColor.FromArgb(20, r, g, b))

            exp = Expander()
            exp.IsExpanded = section.get('expanded', False)
            exp.Margin = Thickness(0, 2, 0, 2)

            hdr_tb = TextBlock()
            hdr_tb.Text = section['title']
            hdr_tb.FontSize = 11
            hdr_tb.FontWeight = System.Windows.FontWeights.SemiBold
            hdr_tb.Foreground = accent
            exp.Header = hdr_tb

            content_border = Border()
            content_border.Background = light_bg
            content_border.CornerRadius = CornerRadius(3)
            content_border.Padding = Thickness(4)

            wrap = WrapPanel()
            wrap.Orientation = Orientation.Horizontal

            for (pfolder, bfolder, label, tip) in section['buttons']:
                spath = os.path.join(TAB_DIR, pfolder, bfolder, 'script.py')
                wrap.Children.Add(
                    self._make_button(label, tip, accent, spath)
                )

            content_border.Child = wrap
            exp.Content = content_border
            return exp

        # ---------------------------------------------------------------
        def _make_button(self, label, tooltip_text, accent, script_path):
            """Styled WPF button wired to run a script on click."""
            btn = Button()
            btn.Margin = Thickness(2)
            btn.Padding = Thickness(8, 5, 8, 5)
            btn.MinWidth = 105
            btn.Cursor = Cursors.Hand

            tb = TextBlock()
            tb.Text = label
            tb.FontSize = 9.5
            tb.TextWrapping = TextWrapping.Wrap
            tb.TextAlignment = System.Windows.TextAlignment.Center
            btn.Content = tb

            # Tooltip
            tt = ToolTip()
            tt_tb = TextBlock()
            tt_tb.Text = tooltip_text
            tt_tb.FontSize = 9
            tt_tb.TextWrapping = TextWrapping.Wrap
            tt_tb.MaxWidth = 280
            tt.Content = tt_tb
            btn.ToolTip = tt

            if not os.path.isfile(script_path):
                btn.IsEnabled = False
                tb.Foreground = Brushes.LightGray
            else:
                btn.Click += EventHandler[RoutedEventArgs](
                    _make_click_handler(script_path)
                )

            return btn

        # ---------------------------------------------------------------
        def _build_data_section(self):
            """Collapsible data-file inventory section."""
            exp = Expander()
            exp.IsExpanded = False
            exp.Margin = Thickness(0, 4, 0, 2)

            hdr = TextBlock()
            hdr.Text = "Data files"
            hdr.FontSize = 11
            hdr.FontWeight = System.Windows.FontWeights.SemiBold
            hdr.Foreground = Brushes.DimGray
            exp.Header = hdr

            stack = StackPanel()
            stack.Margin = Thickness(4)

            try:
                inv = data_loader.data_file_inventory()
                for fname, info in sorted(inv.items()):
                    row = StackPanel()
                    row.Orientation = Orientation.Horizontal
                    row.Margin = Thickness(0, 1, 0, 1)

                    name_tb = TextBlock()
                    name_tb.Text = fname
                    name_tb.FontSize = 8.5
                    name_tb.Width = 200
                    row.Children.Add(name_tb)

                    size_kb = info['size'] / 1024.0
                    size_tb = TextBlock()
                    if size_kb >= 1:
                        size_tb.Text = "{:.0f} KB".format(size_kb)
                    else:
                        size_tb.Text = "{} B".format(info['size'])
                    size_tb.FontSize = 8
                    size_tb.Foreground = Brushes.Gray
                    size_tb.Width = 50
                    row.Children.Add(size_tb)

                    hash_tb = TextBlock()
                    hash_tb.Text = info['hash']
                    hash_tb.FontSize = 7.5
                    hash_tb.Foreground = Brushes.DarkGray
                    hash_tb.FontFamily = FontFamily("Consolas")
                    row.Children.Add(hash_tb)

                    stack.Children.Add(row)

                # Summary
                sep = Border()
                sep.Height = 1
                sep.Background = SolidColorBrush(
                    WpfColor.FromRgb(220, 220, 220))
                sep.Margin = Thickness(0, 4, 0, 4)
                stack.Children.Add(sep)

                total_mb = sum(
                    v['size'] for v in inv.values()
                ) / (1024.0 * 1024.0)
                summary = TextBlock()
                summary.Text = "{} files  |  {:.1f} MB total".format(
                    len(inv), total_mb
                )
                summary.FontSize = 8.5
                summary.Foreground = Brushes.Gray
                stack.Children.Add(summary)

            except Exception as e:
                err = TextBlock()
                err.Text = "Error: {}".format(str(e)[:100])
                err.FontSize = 9
                err.Foreground = Brushes.Red
                err.TextWrapping = TextWrapping.Wrap
                stack.Children.Add(err)

            exp.Content = stack
            return exp

    # -------------------------------------------------------------------
    # Registration
    # -------------------------------------------------------------------
    def register():
        try:
            from pyrevit import HOST_APP
            uiapp = HOST_APP.uiapp
            pid = DockablePaneId(PANEL_GUID)
            try:
                existing = uiapp.GetDockablePane(pid)
                if existing:
                    return
            except Exception:
                pass
            uiapp.RegisterDockablePane(
                pid, "STINGTemp", STINGTempDockableWindow()
            )
        except Exception:
            pass

    register()

except Exception:
    # Running outside Revit; skip registration silently
    pass
