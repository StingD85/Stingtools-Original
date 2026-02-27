# -*- coding: utf-8 -*-
"""StingDocs Project Organizer - Persistent Dockable Panel"""
__title__ = "Project\nOrganizer"
__doc__ = "AI-Powered BIM Documentation Tools"
__author__ = "StingDocs"

import clr
clr.AddReference('PresentationFramework')
clr.AddReference('PresentationCore')
clr.AddReference('WindowsBase')
clr.AddReference('System.Windows.Forms')

from System.Windows.Markup import XamlReader
from System.IO import StreamReader, File
from System.Windows import Window
from System.Windows.Input import MouseButtonEventHandler, MouseButton
from System import EventHandler, Action
from System.Collections.Generic import List

from pyrevit import script, forms, revit, DB
from Autodesk.Revit.DB import (
    Transaction, XYZ, Viewport, ViewSheet, TextNote,
    ScheduleSheetInstance, ViewSchedule, Dimension,
    FilteredElementCollector, BuiltInParameter,
    CurveElement, FilledRegion, SpatialElement,
    UnitFormatUtils, UnitType, ElementId
)
from Autodesk.Revit.UI import IExternalEventHandler, ExternalEvent

import System.Windows.Forms as WinForms
import re
from collections import Counter

# ── pyRevit gives us __revit__ at module level ──────────────────────────────
_uiapp = __revit__                      # UIApplication
_uidoc  = _uiapp.ActiveUIDocument       # updated each command via property
_doc    = _uidoc.Document

def _get_doc():
    return _uiapp.ActiveUIDocument.Document

def _get_uidoc():
    return _uiapp.ActiveUIDocument

def _get_selection_elements():
    uidoc = _get_uidoc()
    doc   = _get_doc()
    return [doc.GetElement(eid) for eid in uidoc.Selection.GetElementIds()]

# Get the XAML file path
xaml_file = script.get_bundle_file('OrganizerUI.xaml')


# ═══════════════════════════════════════════════════════════════════════════════
# EXTERNAL EVENT HANDLER
# One handler, one command slot — thread-safe Revit API access
# ═══════════════════════════════════════════════════════════════════════════════

class RevitCommandHandler(IExternalEventHandler):
    """Single handler that executes whatever command_func is set to."""

    def __init__(self):
        self.command_func = None   # callable → returns {'message':..,'status':..}
        self.panel_ref    = None   # StingDocsPanel reference

    def Execute(self, uiapp):
        try:
            if self.command_func is None:
                return
            result = self.command_func()
            if result and self.panel_ref:
                msg    = result.get('message', '')
                status = result.get('status',  'info')
                # Marshal back to WPF thread
                self.panel_ref.window.Dispatcher.BeginInvoke(
                    Action(lambda: self.panel_ref.update_status(msg, status))
                )
        except Exception as exc:
            err = str(exc)
            if self.panel_ref:
                self.panel_ref.window.Dispatcher.BeginInvoke(
                    Action(lambda: self.panel_ref.update_status("Error: " + err, "error"))
                )

    def GetName(self):
        return "StingDocs Command Handler"


# ═══════════════════════════════════════════════════════════════════════════════
# HELPER – ask_for_string / selection dialogs must run on the UI thread
#           NOT inside ExternalEvent.Execute.  We call them BEFORE raising
#           the event, collect the user's input, then pass it in via closure.
# ═══════════════════════════════════════════════════════════════════════════════

def _ask_string(prompt, default='', title='Input'):
    """Show input dialog on WPF/main thread (safe to call from button handler)."""
    result = forms.ask_for_string(prompt=prompt, default=default, title=title)
    return result   # None if cancelled


def _ask_int(prompt, default=1, title='Input'):
    raw = _ask_string(prompt, str(default), title)
    if raw is None:
        return None
    try:
        return int(raw)
    except:
        forms.alert('Please enter a whole number.')
        return None


def _ask_float(prompt, default=0.5, title='Input'):
    raw = _ask_string(prompt, str(default), title)
    if raw is None:
        return None
    try:
        return float(raw)
    except:
        forms.alert('Please enter a number (e.g. 0.5).')
        return None


def _select_from_list(options, title='Select', multiselect=False):
    return forms.SelectFromList.show(
        sorted(options),
        title=title,
        button_name='Select',
        multiselect=multiselect
    )


# ═══════════════════════════════════════════════════════════════════════════════
# TITLE-CASE HELPER (NLP-aware)
# ═══════════════════════════════════════════════════════════════════════════════

_ACRONYMS = {
    'BIM','CAD','HVAC','MEP','ADA','OSHA','LEED','NEC','IBC','AIA','ASHRAE',
    'ASTM','ISO','ANSI','AWS','AISC','ACI','USA','UK','EU','NA','NTS','TYP',
    'REF','SIM','EQ','FFL','TOS','BOT','FFE','CLG','GWB','CMU','VCT','ACT'
}
_LOWERCASE_WORDS = {
    'a','an','the','and','but','or','nor','for','at','by','from','in',
    'into','of','on','to','with','as','per','via'
}

def _intelligent_title_case(text):
    words  = text.split()
    result = []
    for i, word in enumerate(words):
        if word.upper() in _ACRONYMS:
            result.append(word.upper())
        elif re.match(r"^\d+['\"]", word):      # dimension strings
            result.append(word)
        elif i == 0 or i == len(words) - 1:
            result.append(word.capitalize())
        elif word.lower() in _LOWERCASE_WORDS:
            result.append(word.lower())
        else:
            result.append(word.capitalize())
    return ' '.join(result)


# ═══════════════════════════════════════════════════════════════════════════════
# MAIN PANEL CLASS
# ═══════════════════════════════════════════════════════════════════════════════

class StingDocsPanel:

    def __init__(self):
        # External event (one, reused for every command)
        self.handler   = RevitCommandHandler()
        self.handler.panel_ref = self
        self.ext_event = ExternalEvent.Create(self.handler)

        # Load XAML
        with StreamReader(xaml_file) as s:
            self.window = XamlReader.Load(s.BaseStream)

        self.window.Topmost       = True
        self.window.ShowInTaskbar = False

        self.status_text = self.window.FindName('StatusText')

        self._enable_drag()
        self._setup_all_buttons()

        # Close button
        cb = self.window.FindName('CloseButton')
        if cb:
            cb.Click += EventHandler(lambda s, e: self.window.Close())

        self.update_status("Ready", "success")

    # ── Drag support ────────────────────────────────────────────────────────
    def _enable_drag(self):
        tb = self.window.FindName('TitleBorder')
        if not tb:
            return
        def _drag(sender, args):
            if args.ChangedButton == MouseButton.Left:
                try:
                    self.window.DragMove()
                except:
                    pass
        tb.MouseLeftButtonDown += MouseButtonEventHandler(_drag)

    # ── Show ────────────────────────────────────────────────────────────────
    def show(self):
        self.window.Show()

    # ── Status bar ──────────────────────────────────────────────────────────
    def update_status(self, message, status_type="info"):
        try:
            if not self.status_text:
                return
            self.status_text.Text = str(message)
            from System.Windows.Media import BrushConverter
            conv = BrushConverter()
            colours = {
                'success': "#FF00AA00",
                'error':   "#FFFF4444",
                'warning': "#FFFFAA00",
            }
            self.status_text.Foreground = conv.ConvertFromString(
                colours.get(status_type, "#FF999999")
            )
        except:
            pass

    # ── Generic button wiring ───────────────────────────────────────────────
    def _wire(self, name, fn):
        """Wire button by name to fn.  Uses default-arg capture to fix closure."""
        btn = self.window.FindName(name)
        if btn:
            # default arg `_fn=fn` captures the current value of fn
            btn.Click += EventHandler(lambda s, e, _fn=fn: _fn())

    def _fire(self, cmd_fn):
        """Set command and raise ExternalEvent."""
        self.handler.command_func = cmd_fn
        self.ext_event.Raise()
        self.update_status("Working…", "info")

    # ═══════════════════════════════════════════════════════════════════════
    # BUTTON SETUP – every button in the XAML
    # ═══════════════════════════════════════════════════════════════════════

    def _setup_all_buttons(self):

        # ── Viewport alignment ───────────────────────────────────────────
        self._wire('AlignViewportsTop',     self.do_align_top)
        self._wire('AlignViewportsMiddleY', self.do_align_mid_y)
        self._wire('AlignViewportsBottom',  self.do_align_bottom)
        self._wire('AlignViewportsLeft',    self.do_align_left)
        self._wire('AlignViewportsMiddleX', self.do_align_mid_x)
        self._wire('AlignViewportsRight',   self.do_align_right)

        # ── Viewport numbering ───────────────────────────────────────────
        self._wire('NumberViewportsByClick', self.do_number_by_click)
        self._wire('RenumberLeftToRight',    self.do_renumber_ltr)
        self._wire('RenumberTopToBottom',    self.do_renumber_ttb)
        self._wire('DetailNumberAdd1',       self.do_detail_add1)
        self._wire('DetailNumberSubtract1',  self.do_detail_sub1)
        self._wire('DetailNumberPrefix',     self.do_detail_prefix)
        self._wire('DetailNumberSuffix',     self.do_detail_suffix)

        # ── Viewport spacing ─────────────────────────────────────────────
        self._wire('OrderHorizontally', self.do_order_horiz)
        self._wire('OrderFromMiddle',   self.do_order_middle)

        # ── Sheet tools ──────────────────────────────────────────────────
        self._wire('ResetTitleOnSheet',     self.do_reset_title)
        self._wire('SheetNumberAdd1',       self.do_sheet_add1)
        self._wire('SheetNumberSubtract1',  self.do_sheet_sub1)
        self._wire('SheetNumberPrefix',     self.do_sheet_prefix)
        self._wire('SheetNumberSuffix',     self.do_sheet_suffix)
        self._wire('SheetNumberFindReplace',self.do_sheet_find_replace)

        # ── Schedule tools ───────────────────────────────────────────────
        self._wire('SyncSchedulePositions', self.do_sched_sync_pos)
        self._wire('SyncScheduleRotations', self.do_sched_sync_rot)
        self._wire('MatchColumnWidths',     self.do_sched_match_widths)
        self._wire('SetAllColumnWidths',    self.do_sched_set_widths)
        self._wire('ShowHiddenColumns',     self.do_sched_show_hidden)

        # ── Text note tools ──────────────────────────────────────────────
        self._wire('TextNoteLowerCase', self.do_text_lower)
        self._wire('TextNoteUpperCase', self.do_text_upper)
        self._wire('TextNoteTitleCase', self.do_text_title)

        # ── Dimension tools ──────────────────────────────────────────────
        self._wire('ResetDimensionOverrides', self.do_dim_reset_overrides)
        self._wire('ResetDimensionPositions', self.do_dim_reset_positions)
        self._wire('FindZeroDimensions',      self.do_dim_find_zeros)
        self._wire('DimensionFindReplace',    self.do_dim_find_replace)

        # ── Legend tools (stubs) ─────────────────────────────────────────
        self._wire('SyncLegendPositions', self.do_legend_stub)
        self._wire('SyncLegendTitleLine', self.do_legend_stub)
        self._wire('MakeLegendsSame',     self.do_legend_stub)

        # ── Title block tools (stubs) ────────────────────────────────────
        self._wire('ResetTitleBlock',    self.do_titleblock_stub)
        self._wire('RescueTitleBlocks',  self.do_titleblock_stub)

        # ── Revision tools (stubs) ───────────────────────────────────────
        self._wire('ShowAllRevisions',    self.do_revision_stub)
        self._wire('DeleteCloudsActive',  self.do_revision_stub)
        self._wire('DeleteCloudsSelected',self.do_revision_stub)

        # ── Measurement tools ────────────────────────────────────────────
        self._wire('MeasureLines',       self.do_measure_lines)
        self._wire('MeasureAreas',       self.do_measure_areas)
        self._wire('MeasurePerimeters',  self.do_measure_perimeters)
        self._wire('MeasureRoomAreas',   self.do_measure_rooms)

        # ── Utilities (stubs) ────────────────────────────────────────────
        self._wire('SwapElements',     self.do_util_stub)
        self._wire('ConvertRegions',   self.do_util_stub)
        self._wire('CleanDoubleSpaces',self.do_clean_spaces)


    # ═══════════════════════════════════════════════════════════════════════
    # SHARED UTILITIES
    # ═══════════════════════════════════════════════════════════════════════

    def _viewports(self):
        els = _get_selection_elements()
        return [e for e in els if isinstance(e, Viewport)]

    def _text_notes(self):
        els = _get_selection_elements()
        return [e for e in els if isinstance(e, TextNote)]

    def _sheets(self):
        els = _get_selection_elements()
        return [e for e in els if isinstance(e, ViewSheet)]

    def _dimensions(self):
        els = _get_selection_elements()
        return [e for e in els if isinstance(e, Dimension)]

    def _schedule_instances(self):
        els = _get_selection_elements()
        insts = [e for e in els if isinstance(e, ScheduleSheetInstance)]
        if not insts:
            # Fall back: all on active sheet
            av = _get_doc().ActiveView
            if isinstance(av, ViewSheet):
                insts = list(
                    FilteredElementCollector(_get_doc(), av.Id)
                    .OfClass(ScheduleSheetInstance)
                )
        return insts

    @staticmethod
    def _format_length(val_ft):
        try:
            return UnitFormatUtils.Format(
                _get_doc().GetUnits(), UnitType.UT_Length, val_ft, False, False
            )
        except:
            return "{:.4f} ft".format(val_ft)

    @staticmethod
    def _format_area(val_sqft):
        try:
            return UnitFormatUtils.Format(
                _get_doc().GetUnits(), UnitType.UT_Area, val_sqft, False, False
            )
        except:
            return "{:.4f} sqft".format(val_sqft)

    @staticmethod
    def _copy_to_clipboard(text):
        try:
            WinForms.Clipboard.SetText(str(text))
        except:
            pass


    # ═══════════════════════════════════════════════════════════════════════
    # VIEWPORT ALIGNMENT
    # All alignment functions align to the EXTREME edge, not to the centre
    # ═══════════════════════════════════════════════════════════════════════

    def do_align_top(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        # Find topmost EDGE
        def top_edge(vp):
            o = vp.GetBoxOutline()
            return o.MaximumPoint.Y
        max_top = max(top_edge(vp) for vp in vps)
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Align Viewports Top')
            t.Start()
            try:
                for vp in vps:
                    o   = vp.GetBoxOutline()
                    h   = o.MaximumPoint.Y - o.MinimumPoint.Y
                    c   = vp.GetBoxCenter()
                    vp.SetBoxCenter(XYZ(c.X, max_top - h / 2.0, c.Z))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Aligned {} viewports to top".format(len(vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_align_mid_y(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        avg_y = sum(vp.GetBoxCenter().Y for vp in vps) / len(vps)
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Align Viewports Middle Y')
            t.Start()
            try:
                for vp in vps:
                    c = vp.GetBoxCenter()
                    vp.SetBoxCenter(XYZ(c.X, avg_y, c.Z))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Centred {} viewports vertically".format(len(vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_align_bottom(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        def bot_edge(vp):
            return vp.GetBoxOutline().MinimumPoint.Y
        min_bot = min(bot_edge(vp) for vp in vps)
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Align Viewports Bottom')
            t.Start()
            try:
                for vp in vps:
                    o = vp.GetBoxOutline()
                    h = o.MaximumPoint.Y - o.MinimumPoint.Y
                    c = vp.GetBoxCenter()
                    vp.SetBoxCenter(XYZ(c.X, min_bot + h / 2.0, c.Z))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Aligned {} viewports to bottom".format(len(vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_align_left(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        min_left = min(vp.GetBoxOutline().MinimumPoint.X for vp in vps)
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Align Viewports Left')
            t.Start()
            try:
                for vp in vps:
                    o = vp.GetBoxOutline()
                    w = o.MaximumPoint.X - o.MinimumPoint.X
                    c = vp.GetBoxCenter()
                    vp.SetBoxCenter(XYZ(min_left + w / 2.0, c.Y, c.Z))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Aligned {} viewports to left".format(len(vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_align_mid_x(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        avg_x = sum(vp.GetBoxCenter().X for vp in vps) / len(vps)
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Align Viewports Middle X')
            t.Start()
            try:
                for vp in vps:
                    c = vp.GetBoxCenter()
                    vp.SetBoxCenter(XYZ(avg_x, c.Y, c.Z))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Centred {} viewports horizontally".format(len(vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_align_right(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        max_right = max(vp.GetBoxOutline().MaximumPoint.X for vp in vps)
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Align Viewports Right')
            t.Start()
            try:
                for vp in vps:
                    o = vp.GetBoxOutline()
                    w = o.MaximumPoint.X - o.MinimumPoint.X
                    c = vp.GetBoxCenter()
                    vp.SetBoxCenter(XYZ(max_right - w / 2.0, c.Y, c.Z))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Aligned {} viewports to right".format(len(vps)), 'status': 'success'}
        self._fire(_cmd)


    # ═══════════════════════════════════════════════════════════════════════
    # VIEWPORT NUMBERING
    # All user dialogs are called HERE (UI thread) before _fire()
    # ═══════════════════════════════════════════════════════════════════════

    def do_number_by_click(self):
        self.update_status("Number-by-click: select in Revit, not yet supported in panel mode", "warning")

    def _renumber(self, sort_key, reverse=False, label='Renumber'):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        start = _ask_int("Starting detail number:", 1, label)
        if start is None: return
        sorted_vps = sorted(vps, key=sort_key, reverse=reverse)
        captured   = list(sorted_vps)  # capture list
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, label)
            t.Start()
            try:
                for i, vp in enumerate(captured):
                    p = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
                    if p and not p.IsReadOnly:
                        p.Set(str(start + i))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Renumbered {} viewports".format(len(captured)), 'status': 'success'}
        self._fire(_cmd)

    def do_renumber_ltr(self):
        self._renumber(lambda vp: vp.GetBoxCenter().X, label='Renumber L→R')

    def do_renumber_ttb(self):
        self._renumber(lambda vp: vp.GetBoxCenter().Y, reverse=True, label='Renumber T→B')

    def _detail_number_offset(self, delta):
        vps = self._viewports()
        if not vps:
            self.update_status("Select viewports", "warning"); return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Modify Detail Numbers')
            t.Start()
            try:
                for vp in vps:
                    p = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
                    if p and not p.IsReadOnly:
                        m = re.search(r'\d+', p.AsString() or '')
                        n = max(1, (int(m.group()) if m else 1) + delta)
                        p.Set(str(n))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "{:+d} applied to {} detail numbers".format(delta, len(vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_detail_add1(self):  self._detail_number_offset(+1)
    def do_detail_sub1(self):  self._detail_number_offset(-1)

    def _detail_affix(self, is_prefix):
        vps = self._viewports()
        if not vps:
            self.update_status("Select viewports", "warning"); return
        label = "Prefix" if is_prefix else "Suffix"
        text  = _ask_string("Enter {} to add:".format(label.lower()), title='Detail Number ' + label)
        if text is None: return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Detail Number ' + label)
            t.Start()
            try:
                for vp in vps:
                    p = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
                    if p and not p.IsReadOnly:
                        cur = p.AsString() or ''
                        p.Set(text + cur if is_prefix else cur + text)
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Added {} to {} viewports".format(label.lower(), len(vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_detail_prefix(self): self._detail_affix(True)
    def do_detail_suffix(self): self._detail_affix(False)


    # ═══════════════════════════════════════════════════════════════════════
    # VIEWPORT SPACING
    # ═══════════════════════════════════════════════════════════════════════

    def do_order_horiz(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        gap = _ask_float("Horizontal gap (feet):", 0.5, "Horizontal Gap")
        if gap is None: return
        sorted_vps = sorted(vps, key=lambda vp: vp.GetBoxCenter().X)
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Order Viewports Horizontally')
            t.Start()
            try:
                for i in range(1, len(sorted_vps)):
                    prev_o  = sorted_vps[i-1].GetBoxOutline()
                    cur_o   = sorted_vps[i].GetBoxOutline()
                    cur_c   = sorted_vps[i].GetBoxCenter()
                    w       = cur_o.MaximumPoint.X - cur_o.MinimumPoint.X
                    new_x   = prev_o.MaximumPoint.X + gap + w / 2.0
                    sorted_vps[i].SetBoxCenter(XYZ(new_x, cur_c.Y, cur_c.Z))
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Ordered {} viewports horizontally".format(len(sorted_vps)), 'status': 'success'}
        self._fire(_cmd)

    def do_order_middle(self):
        vps = self._viewports()
        if len(vps) < 2:
            self.update_status("Select ≥2 viewports", "warning"); return
        gap = _ask_float("Horizontal gap (feet):", 0.5, "Order from Middle")
        if gap is None: return
        sorted_vps = sorted(vps, key=lambda vp: vp.GetBoxCenter().X)
        total_w    = sum(vp.GetBoxOutline().MaximumPoint.X - vp.GetBoxOutline().MinimumPoint.X
                         for vp in sorted_vps)
        total_w   += gap * (len(sorted_vps) - 1)
        avg_x  = sum(vp.GetBoxCenter().X for vp in sorted_vps) / len(sorted_vps)
        avg_y  = sum(vp.GetBoxCenter().Y for vp in sorted_vps) / len(sorted_vps)
        start_x = avg_x - total_w / 2.0
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Order Viewports from Middle')
            t.Start()
            try:
                cur_x = start_x
                for vp in sorted_vps:
                    o = vp.GetBoxOutline()
                    w = o.MaximumPoint.X - o.MinimumPoint.X
                    vp.SetBoxCenter(XYZ(cur_x + w / 2.0, avg_y, 0))
                    cur_x += w + gap
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Ordered {} viewports from middle".format(len(sorted_vps)), 'status': 'success'}
        self._fire(_cmd)


    # ═══════════════════════════════════════════════════════════════════════
    # SHEET TOOLS
    # ═══════════════════════════════════════════════════════════════════════

    def do_reset_title(self):
        sheets = self._sheets()
        if not sheets:
            self.update_status("Select sheets in Project Browser", "warning"); return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Reset Title on Sheet')
            t.Start()
            count = 0
            try:
                for sheet in sheets:
                    for vp_id in sheet.GetAllViewports():
                        vp    = doc.GetElement(vp_id)
                        view  = doc.GetElement(vp.ViewId)
                        param = vp.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION)
                        if param and not param.IsReadOnly:
                            param.Set(view.Name)
                            count += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Reset {} viewport titles".format(count), 'status': 'success'}
        self._fire(_cmd)

    def _sheet_number_offset(self, delta):
        sheets = self._sheets()
        if not sheets:
            self.update_status("Select sheets", "warning"); return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Sheet Number Offset')
            t.Start()
            try:
                for sh in sheets:
                    p = sh.get_Parameter(BuiltInParameter.SHEET_NUMBER)
                    if p and not p.IsReadOnly:
                        m = re.search(r'\d+', p.AsString() or '')
                        if m:
                            n     = max(0, int(m.group()) + delta)
                            start = p.AsString()[:m.start()]
                            end   = p.AsString()[m.end():]
                            p.Set(start + str(n) + end)
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "{:+d} applied to {} sheet numbers".format(delta, len(sheets)), 'status': 'success'}
        self._fire(_cmd)

    def do_sheet_add1(self):  self._sheet_number_offset(+1)
    def do_sheet_sub1(self):  self._sheet_number_offset(-1)

    def _sheet_affix(self, is_prefix):
        sheets = self._sheets()
        if not sheets:
            self.update_status("Select sheets", "warning"); return
        label = "Prefix" if is_prefix else "Suffix"
        text  = _ask_string("Enter {} to add:".format(label.lower()), title='Sheet ' + label)
        if text is None: return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Sheet Number ' + label)
            t.Start()
            try:
                for sh in sheets:
                    p = sh.get_Parameter(BuiltInParameter.SHEET_NUMBER)
                    if p and not p.IsReadOnly:
                        cur = p.AsString() or ''
                        p.Set(text + cur if is_prefix else cur + text)
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Added {} to {} sheets".format(label.lower(), len(sheets)), 'status': 'success'}
        self._fire(_cmd)

    def do_sheet_prefix(self): self._sheet_affix(True)
    def do_sheet_suffix(self): self._sheet_affix(False)

    def do_sheet_find_replace(self):
        sheets = self._sheets()
        if not sheets:
            self.update_status("Select sheets", "warning"); return
        find    = _ask_string("Find:", title='Sheet Number Find & Replace')
        if find is None: return
        replace = _ask_string("Replace with:", default='', title='Sheet Number Find & Replace')
        if replace is None: return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Sheet Number Find & Replace')
            t.Start()
            count = 0
            try:
                for sh in sheets:
                    p = sh.get_Parameter(BuiltInParameter.SHEET_NUMBER)
                    if p and not p.IsReadOnly:
                        cur = p.AsString() or ''
                        if find in cur:
                            p.Set(cur.replace(find, replace))
                            count += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Updated {} sheet numbers".format(count), 'status': 'success'}
        self._fire(_cmd)


    # ═══════════════════════════════════════════════════════════════════════
    # SCHEDULE TOOLS
    # ═══════════════════════════════════════════════════════════════════════

    def do_sched_sync_pos(self):
        insts = self._schedule_instances()
        if not insts:
            self.update_status("Select schedule instances", "warning"); return
        # Build option map on UI thread
        options = {}
        for inst in insts:
            sched = _get_doc().GetElement(inst.ScheduleId)
            if sched:
                owner = _get_doc().GetElement(inst.OwnerViewId)
                sheet_no = owner.SheetNumber if owner else '?'
                options["{} (Sheet {})".format(sched.Name, sheet_no)] = inst
        choice = _select_from_list(list(options.keys()), title='Select Master Position')
        if not choice: return
        master      = options[choice]
        master_pt   = master.Point
        master_sid  = master.ScheduleId
        master_id   = master.Id
        def _cmd():
            doc  = _get_doc()
            all_ = list(FilteredElementCollector(doc).OfClass(ScheduleSheetInstance))
            targets = [i for i in all_ if i.ScheduleId == master_sid and i.Id != master_id]
            t = Transaction(doc, 'Sync Schedule Positions')
            t.Start()
            try:
                for inst in targets:
                    inst.Point = master_pt
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Synced {} schedule instances".format(len(targets)), 'status': 'success'}
        self._fire(_cmd)

    def do_sched_sync_rot(self):
        self.update_status("Sync rotations: coming soon", "warning")

    def do_sched_match_widths(self):
        insts = self._schedule_instances()
        if len(insts) < 2:
            self.update_status("Select ≥2 schedule instances", "warning"); return
        scheds   = [_get_doc().GetElement(i.ScheduleId) for i in insts]
        scheds   = [s for s in scheds if isinstance(s, ViewSchedule)]
        names    = [s.Name for s in scheds]
        src_name = _select_from_list(names, title='Source schedule (copy widths FROM)')
        if not src_name: return
        src   = next(s for s in scheds if s.Name == src_name)
        src_id = src.Id
        def _cmd():
            doc   = _get_doc()
            src_s = doc.GetElement(src_id)
            defn  = src_s.Definition
            src_widths = []
            src_total  = 0.0
            for i in range(defn.GetFieldCount()):
                f = defn.GetField(i)
                if not f.IsHidden:
                    src_widths.append(f.ColumnWidth)
                    src_total += f.ColumnWidth
            count = 0
            t = Transaction(doc, 'Match Column Widths')
            t.Start()
            try:
                for s in [doc.GetElement(i.ScheduleId) for i in insts]:
                    if not isinstance(s, ViewSchedule) or s.Id == src_id: continue
                    td = s.Definition
                    vis = [td.GetField(i) for i in range(td.GetFieldCount()) if not td.GetField(i).IsHidden]
                    if not vis: continue
                    if len(vis) == len(src_widths):
                        for idx, f in enumerate(vis): f.ColumnWidth = src_widths[idx]
                    else:
                        per = src_total / len(vis)
                        for f in vis: f.ColumnWidth = per
                    count += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Matched widths for {} schedules".format(count), 'status': 'success'}
        self._fire(_cmd)

    def do_sched_set_widths(self):
        insts = self._schedule_instances()
        if not insts:
            self.update_status("Select schedule instances", "warning"); return
        width = _ask_float("Column width in feet:", 1.0, "Set Column Widths")
        if width is None: return
        def _cmd():
            doc   = _get_doc()
            total = 0
            t = Transaction(doc, 'Set All Column Widths')
            t.Start()
            try:
                for inst in insts:
                    s = doc.GetElement(inst.ScheduleId)
                    if not isinstance(s, ViewSchedule): continue
                    d = s.Definition
                    for i in range(d.GetFieldCount()):
                        f = d.GetField(i)
                        if not f.IsHidden:
                            f.ColumnWidth = width
                            total += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Set {} columns to {:.3f} ft".format(total, width), 'status': 'success'}
        self._fire(_cmd)

    def do_sched_show_hidden(self):
        insts = self._schedule_instances()
        if not insts:
            self.update_status("Select schedule instances", "warning"); return
        def _cmd():
            doc   = _get_doc()
            total = 0
            t = Transaction(doc, 'Show Hidden Columns')
            t.Start()
            try:
                for inst in insts:
                    s = doc.GetElement(inst.ScheduleId)
                    if not isinstance(s, ViewSchedule): continue
                    d = s.Definition
                    for i in range(d.GetFieldCount()):
                        f = d.GetField(i)
                        if f.IsHidden:
                            f.IsHidden = False
                            total += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Revealed {} hidden columns".format(total), 'status': 'success'}
        self._fire(_cmd)


    # ═══════════════════════════════════════════════════════════════════════
    # TEXT NOTE TOOLS
    # ═══════════════════════════════════════════════════════════════════════

    def _convert_text(self, converter_fn, label):
        notes = self._text_notes()
        if not notes:
            self.update_status("Select text notes", "warning"); return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, label)
            t.Start()
            try:
                for tn in notes:
                    tn.Text = converter_fn(tn.Text)
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "{} – {} text notes".format(label, len(notes)), 'status': 'success'}
        self._fire(_cmd)

    def do_text_lower(self): self._convert_text(str.lower,             'Convert to lowercase')
    def do_text_upper(self): self._convert_text(str.upper,             'Convert to UPPERCASE')
    def do_text_title(self): self._convert_text(_intelligent_title_case,'Convert to Title Case')


    # ═══════════════════════════════════════════════════════════════════════
    # DIMENSION TOOLS
    # ═══════════════════════════════════════════════════════════════════════

    def do_dim_reset_overrides(self):
        dims = self._dimensions()
        if not dims:
            self.update_status("Select dimensions", "warning"); return
        def _cmd():
            doc   = _get_doc()
            t = Transaction(doc, 'Reset Dimension Overrides')
            t.Start()
            count = 0
            try:
                for d in dims:
                    if d.ValueOverride:
                        d.ValueOverride = ""   # empty string clears override
                        count += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Cleared {} dimension overrides".format(count), 'status': 'success'}
        self._fire(_cmd)

    def do_dim_reset_positions(self):
        dims = self._dimensions()
        if not dims:
            self.update_status("Select dimensions", "warning"); return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Reset Dimension Text Positions')
            t.Start()
            count = 0
            try:
                for d in dims:
                    if hasattr(d, 'ResetTextPosition'):
                        d.ResetTextPosition()
                        count += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Reset {} dimension text positions".format(count), 'status': 'success'}
        self._fire(_cmd)

    def do_dim_find_zeros(self):
        dims = self._dimensions()
        if not dims:
            self.update_status("Select dimensions", "warning"); return
        zeros = []
        for d in dims:
            try:
                v = d.Value
                if v is not None and abs(v) < 0.001:
                    zeros.append(d)
            except:
                pass
        if zeros:
            ids = List[ElementId]([d.Id for d in zeros])
            _get_uidoc().Selection.SetElementIds(ids)
            self.update_status("Found {} zero dimensions – selected".format(len(zeros)), "warning")
        else:
            self.update_status("No zero dimensions found", "success")

    def do_dim_find_replace(self):
        dims = self._dimensions()
        if not dims:
            self.update_status("Select dimensions", "warning"); return
        find    = _ask_string("Find in override text:", title='Dimension Find & Replace')
        if find is None: return
        replace = _ask_string("Replace with:", default='', title='Dimension Find & Replace')
        if replace is None: return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Dimension Find & Replace')
            t.Start()
            count = 0
            try:
                for d in dims:
                    if d.ValueOverride and find in d.ValueOverride:
                        d.ValueOverride = d.ValueOverride.replace(find, replace)
                        count += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Updated {} dimension overrides".format(count), 'status': 'success'}
        self._fire(_cmd)


    # ═══════════════════════════════════════════════════════════════════════
    # MEASUREMENT TOOLS
    # ═══════════════════════════════════════════════════════════════════════

    def do_measure_lines(self):
        els = _get_selection_elements()
        lines = [e for e in els if isinstance(e, CurveElement) or hasattr(e, 'GeometryCurve')]
        if not lines:
            self.update_status("Select lines/curves", "warning"); return
        total = 0.0
        for e in lines:
            try:
                c = e.GeometryCurve if hasattr(e, 'GeometryCurve') else e.GetCurve()
                if c: total += c.Length
            except: pass
        fmt = self._format_length(total)
        self._copy_to_clipboard(fmt)
        self.update_status("Total length: {} (copied)".format(fmt), "success")

    def do_measure_areas(self):
        els = _get_selection_elements()
        regions = [e for e in els if isinstance(e, FilledRegion)]
        if not regions:
            self.update_status("Select filled regions", "warning"); return
        total = 0.0
        for r in regions:
            try:
                p = r.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)
                if p: total += p.AsDouble()
            except: pass
        fmt = self._format_area(total)
        self._copy_to_clipboard(fmt)
        self.update_status("Total area: {} (copied)".format(fmt), "success")

    def do_measure_perimeters(self):
        els = _get_selection_elements()
        regions = [e for e in els if isinstance(e, FilledRegion)]
        if not regions:
            self.update_status("Select filled regions", "warning"); return
        total = 0.0
        for r in regions:
            try:
                for loop in r.GetBoundaries():
                    for c in loop: total += c.Length
            except: pass
        fmt = self._format_length(total)
        self._copy_to_clipboard(fmt)
        self.update_status("Total perimeter: {} (copied)".format(fmt), "success")

    def do_measure_rooms(self):
        els = _get_selection_elements()
        rooms = [e for e in els if isinstance(e, SpatialElement) and e.Area > 0]
        if not rooms:
            self.update_status("Select rooms/spaces", "warning"); return
        total = sum(r.Area for r in rooms)
        fmt   = self._format_area(total)
        self._copy_to_clipboard(fmt)
        self.update_status("{} rooms, total: {} (copied)".format(len(rooms), fmt), "success")


    # ═══════════════════════════════════════════════════════════════════════
    # UTILITIES
    # ═══════════════════════════════════════════════════════════════════════

    def do_clean_spaces(self):
        notes = self._text_notes()
        if not notes:
            self.update_status("Select text notes", "warning"); return
        def _cmd():
            doc = _get_doc()
            t = Transaction(doc, 'Clean Double Spaces')
            t.Start()
            count = 0
            try:
                for tn in notes:
                    cleaned = re.sub(r' {2,}', ' ', tn.Text).strip()
                    if cleaned != tn.Text:
                        tn.Text = cleaned
                        count += 1
                t.Commit()
            except:
                t.RollBack(); raise
            return {'message': "Cleaned {} text notes".format(count), 'status': 'success'}
        self._fire(_cmd)

    # ── Stubs for features not yet implemented ───────────────────────────
    def do_legend_stub(self):
        self.update_status("Legend tools: coming soon", "warning")

    def do_titleblock_stub(self):
        self.update_status("Title block tools: coming soon", "warning")

    def do_revision_stub(self):
        self.update_status("Revision tools: coming soon", "warning")

    def do_util_stub(self):
        self.update_status("Feature coming soon", "warning")


# ═══════════════════════════════════════════════════════════════════════════════
# ENTRY POINT
# pyRevit runs script.py directly — __name__ is NOT '__main__', so we
# simply instantiate the panel at module level.
# ═══════════════════════════════════════════════════════════════════════════════

try:
    _panel = StingDocsPanel()
    _panel.show()
except Exception as _ex:
    forms.alert('StingDocs failed to load:\n\n' + str(_ex), title='StingDocs Error')
