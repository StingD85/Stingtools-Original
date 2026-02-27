# -*- coding: utf-8 -*-
"""
STINGTags v9.4 — Smart Tag Intelligence Engine
Modeless panel. All handlers wired.

v9.3 additions over v9.2
─────────────────────────
1.  Tag text alignment   — _tag_text_align() sets TagTextHorizontalAlignment
                           Left / Centre / Right on selected tags (Revit 2022+).
2.  Naviate Config full  — IsoNaviateConfig_Click shows all 13 ISO 19650-1 tokens
                           with formula, separator, skip-empty, GUID table.
                           GUIDs auto-filled from parameter_categories.csv if present.
3.  All 13 ISO setters   — Manual override buttons for all tokens 1–13.
4.  Extended SELECT tab  — Naviate-parity + extra: Workset, Phase, DesignOption,
                           Group, Assembly, Connected, BBox, Pinned/Unpinned,
                           Isolate, Hide, Reveal, ResetTemp, PermHide, PermUnhide,
                           UnhideCategory, Halftone, ResetOverride.
5.  Colouriser           — Fill colour swatches + optional separate outline colour
                           (toggle + outline swatches). Auto-colour by Category /
                           Level / Phase / Workset / System / Parameter.
                           Clear all overrides in view. Fill + outline preview.
6.  VIEW tab             — 4th tab grouping Colouriser + View Controls (temp/perm
                           hide/isolate, graphic overrides).
"""

__title__ = "STING\nTags"
__author__ = "Author"
__persistentengine__ = True   # keep IronPython engine alive for modeless panel

import os, sys, math, random

# Extension root: pushbutton → panel → tab → extension
_ext_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(__file__))))
_lib  = os.path.join(_ext_root, 'lib')
_data = os.path.join(_ext_root, 'data')
_cfg  = os.path.join(_ext_root, 'config')
if _lib not in sys.path:
    sys.path.insert(0, _lib)

try:
    from selection_engine import (
        Vec2, SelectionEngine, SmartOrganizer, PatternLearner,
        ForceEngine, SimAnneal, GeneticAlg, LayoutScorer,
        KMeans, DBSCAN,
    )
    _ENGINE = True
except Exception:
    _ENGINE = False
    class Vec2(object):
        __slots__ = ['x', 'y']
        def __init__(self, x=0, y=0): self.x, self.y = float(x), float(y)
        def dist(self, o): return math.sqrt((self.x-o.x)**2+(self.y-o.y)**2)
    # Pure-Python engine implementations — used when the C# module is absent

    class ForceEngine(object):
        """Force-directed repulsion to separate overlapping tags."""
        def __init__(self, data, spacing):
            self._data = data
            self._sp   = float(spacing)

        def run(self, iters=40):
            data = self._data
            sp   = self._sp
            for _ in range(iters):
                for i in range(len(data)):
                    pi = data[i]['pos']
                    fx = fy = 0.0
                    for j in range(len(data)):
                        if i == j: continue
                        pj = data[j]['pos']
                        dx = pi.x - pj.x
                        dy = pi.y - pj.y
                        d  = math.sqrt(dx * dx + dy * dy) or 1e-6
                        if d < sp:
                            push = (sp - d) / d * 0.3
                            fx += dx * push
                            fy += dy * push
                    # Weak attraction toward host element (limit leader length)
                    ei = data[i]['elem']
                    ex = ei.x - pi.x
                    ey = ei.y - pi.y
                    ed = math.sqrt(ex * ex + ey * ey)
                    max_ldr = sp * 3.0
                    if ed > max_ldr:
                        pull = (ed - max_ldr) / ed * 0.15
                        fx += ex * pull
                        fy += ey * pull
                    pi.x += fx
                    pi.y += fy

    class SimAnneal(object):
        """Simulated annealing for global tag-layout optimisation."""
        def __init__(self, data, spacing):
            self._data = data
            self._sp   = float(spacing)

        def _score(self):
            data = self._data
            sp   = self._sp
            total = 0.0
            for i in range(len(data)):
                pi = data[i]['pos']
                ei = data[i]['elem']
                for j in range(i + 1, len(data)):
                    pj = data[j]['pos']
                    d  = math.sqrt((pi.x - pj.x) ** 2 + (pi.y - pj.y) ** 2)
                    if d < sp:
                        total += (sp - d) ** 2
                # Leader-length penalty
                total += math.sqrt((pi.x - ei.x) ** 2 + (pi.y - ei.y) ** 2) * 0.1
            return total

        def run(self, iters=600):
            data = self._data
            sp   = self._sp
            best_score = self._score()
            best_pos   = [(d['pos'].x, d['pos'].y) for d in data]
            T          = sp * 2.0
            cooling    = math.exp(math.log(0.01) / max(iters, 1))  # T → 0.01*T0
            for _ in range(iters):
                idx  = random.randint(0, len(data) - 1)
                p    = data[idx]['pos']
                ox, oy = p.x, p.y
                p.x += (random.random() - 0.5) * T
                p.y += (random.random() - 0.5) * T
                score = self._score()
                delta = score - best_score
                if delta < 0 or random.random() < math.exp(-delta / max(T, 1e-9)):
                    if score < best_score:
                        best_score = score
                        best_pos   = [(d['pos'].x, d['pos'].y) for d in data]
                else:
                    p.x, p.y = ox, oy
                T *= cooling
            for d, (bx, by) in zip(data, best_pos):
                d['pos'].x, d['pos'].y = bx, by

    class GeneticAlg(object):
        """Genetic algorithm for tag layout — population of position sets."""
        def __init__(self, data, spacing):
            self._data = data
            self._sp   = float(spacing)

        def _encode(self):
            return [(d['pos'].x, d['pos'].y) for d in self._data]

        def _apply(self, chrom):
            for d, (x, y) in zip(self._data, chrom):
                d['pos'].x, d['pos'].y = x, y

        def _score(self):
            data = self._data
            sp   = self._sp
            total = 0.0
            for i in range(len(data)):
                pi = data[i]['pos']
                ei = data[i]['elem']
                for j in range(i + 1, len(data)):
                    pj = data[j]['pos']
                    d  = math.sqrt((pi.x - pj.x) ** 2 + (pi.y - pj.y) ** 2)
                    if d < sp:
                        total += (sp - d) ** 2
                total += math.sqrt((pi.x - ei.x) ** 2 + (pi.y - ei.y) ** 2) * 0.1
            return total

        def run(self, pop_size=20, gens=30):
            sp   = self._sp
            base = self._encode()
            # Seed population with random perturbations
            pop  = [[(x + (random.random() - 0.5) * sp * 0.5,
                      y + (random.random() - 0.5) * sp * 0.5)
                     for (x, y) in base]
                    for _ in range(pop_size - 1)]
            pop.append(base)
            best_chrom = base
            best_score = float('inf')
            for _gen in range(gens):
                self._apply(best_chrom)
                scored = []
                for chrom in pop:
                    self._apply(chrom)
                    scored.append((self._score(), list(chrom)))
                scored.sort(key=lambda x: x[0])
                if scored[0][0] < best_score:
                    best_score  = scored[0][0]
                    best_chrom  = scored[0][1]
                survivors = [c for _, c in scored[:max(2, pop_size // 2)]]
                next_pop  = list(survivors)
                while len(next_pop) < pop_size:
                    a   = random.choice(survivors)
                    b   = random.choice(survivors)
                    cut = random.randint(1, max(1, len(a) - 1))
                    child = list(a[:cut]) + list(b[cut:])
                    if random.random() < 0.2:
                        idx    = random.randint(0, len(child) - 1)
                        cx, cy = child[idx]
                        child[idx] = (cx + (random.random() - 0.5) * sp * 0.3,
                                      cy + (random.random() - 0.5) * sp * 0.3)
                    next_pop.append(child)
                pop = next_pop
            self._apply(best_chrom)

    class PatternLearner(object):
        """Accumulate tag-placement offsets per category; apply as averaged nudges."""
        def __init__(self):
            self._offsets = {}   # category → [(dx, dy), ...]

        def learn(self, data):
            count = 0
            for d in data:
                try:
                    cat = (d['tag'].Category.Name
                           if d['tag'].Category else 'Unknown')
                    dx  = d['pos'].x - d['elem'].x
                    dy  = d['pos'].y - d['elem'].y
                    self._offsets.setdefault(cat, []).append((dx, dy))
                    count += 1
                except Exception:
                    pass
            return '{} patterns learned ({} categories)'.format(
                count, len(self._offsets))

        def apply(self, data):
            count = 0
            for d in data:
                try:
                    cat  = (d['tag'].Category.Name
                            if d['tag'].Category else 'Unknown')
                    offs = self._offsets.get(cat) or self._offsets.get('Unknown') or []
                    if offs:
                        avg_dx = sum(o[0] for o in offs) / len(offs)
                        avg_dy = sum(o[1] for o in offs) / len(offs)
                        d['pos'].x = d['elem'].x + avg_dx
                        d['pos'].y = d['elem'].y + avg_dy
                        count += 1
                except Exception:
                    pass
            return '{} tags repositioned from learned pattern'.format(count)

    class _S(object):
        """Pure-Python SmartOrganizer. Each pass does actual layout work."""
        PASSES = ['Analyse', 'Repulse', 'Attract', 'Physics', 'Anneal',
                  'Leaders', 'Polish', 'Score']
        def __init__(self, doc=None, view=None, spacing=0.25, iters=50, **k):
            self.pass_num = 0
            self._doc = doc
            self._view = view
            self._sp = float(spacing) if spacing else 0.25
            self._tags = []
            self._data = []
            self._orig = {}

        def load(self, tags):
            self._tags = list(tags)
            self._data = []
            self._orig = {}
            for tag in tags:
                try:
                    h = tag.TagHeadPosition
                    self._orig[tag.Id.IntegerValue] = (h.X, h.Y, h.Z)
                    hosts = list(tag.GetTaggedLocalElements())
                    bb = hosts[0].get_BoundingBox(self._view) if hosts else None
                    if bb:
                        ex = (bb.Min.X + bb.Max.X) / 2
                        ey = (bb.Min.Y + bb.Max.Y) / 2
                    else:
                        ex, ey = h.X, h.Y
                    self._data.append({
                        'tag': tag, 'pos': Vec2(h.X, h.Y),
                        'elem': Vec2(ex, ey), 'z': h.Z
                    })
                except Exception: pass

        def run_pass(self):
            self.pass_num += 1
            idx = min(self.pass_num - 1, len(self.PASSES) - 1)
            pname = self.PASSES[idx]
            data = self._data
            sp = self._sp
            if not data:
                return 'Pass {}: no data'.format(self.pass_num)

            if pname in ('Repulse', 'Physics', 'Polish'):
                iters = {'Repulse': 20, 'Physics': 40, 'Polish': 10}.get(pname, 20)
                for _ in range(iters):
                    for i in range(len(data)):
                        pi = data[i]['pos']
                        fx = fy = 0.0
                        for j in range(len(data)):
                            if i == j: continue
                            pj = data[j]['pos']
                            dx = pi.x - pj.x; dy = pi.y - pj.y
                            d = math.sqrt(dx*dx + dy*dy) or 1e-6
                            if d < sp * 1.5:
                                push = (sp - d) / d * 0.3
                                fx += dx * push; fy += dy * push
                        pi.x += fx; pi.y += fy
            elif pname == 'Attract':
                max_ldr = sp * 3.0
                for d in data:
                    ex = d['elem'].x - d['pos'].x
                    ey = d['elem'].y - d['pos'].y
                    ed = math.sqrt(ex*ex + ey*ey)
                    if ed > max_ldr:
                        pull = (ed - max_ldr) / ed * 0.15
                        d['pos'].x += ex * pull; d['pos'].y += ey * pull
            elif pname == 'Anneal':
                SimAnneal(data, sp).run(300)
            elif pname in ('Analyse', 'Score'):
                overlaps = sum(1 for i in range(len(data))
                               for j in range(i+1, len(data))
                               if data[i]['pos'].dist(data[j]['pos']) < sp)
                return 'Pass {}/{}: {} ({} overlaps, {} tags)'.format(
                    self.pass_num, len(self.PASSES), pname, overlaps, len(data))
            return 'Pass {}/{}: {} ({} tags)'.format(
                self.pass_num, len(self.PASSES), pname, len(data))

        def apply(self):
            for d in self._data:
                try: d['tag'].TagHeadPosition = XYZ(d['pos'].x, d['pos'].y, d['z'])
                except Exception: pass

        def reset(self):
            for t in self._tags:
                try:
                    orig = self._orig.get(t.Id.IntegerValue)
                    if orig: t.TagHeadPosition = XYZ(*orig)
                except Exception: pass
            for d in self._data:
                orig = self._orig.get(d['tag'].Id.IntegerValue)
                if orig:
                    d['pos'].x = orig[0]; d['pos'].y = orig[1]
            self.pass_num = 0
        def run(self, *a, **k): return []

    class _KMeansStub(object):
        """K-means clustering stub."""
        def __init__(self, points):
            self._pts = points
        def run(self, k=3):
            pts = self._pts
            if not pts: return [], []
            k = min(k, len(pts))
            # Simple partition into k equal groups
            centroids = []
            clusters = [[] for _ in range(k)]
            for i, p in enumerate(pts):
                clusters[i % k].append(p)
            for c_list in clusters:
                if c_list:
                    cx = sum(p.x for p in c_list) / len(c_list)
                    cy = sum(p.y for p in c_list) / len(c_list)
                    centroids.append(Vec2(cx, cy))
                else:
                    centroids.append(Vec2(0, 0))
            return centroids, clusters

    class _DBSCANStub(object):
        """DBSCAN clustering stub."""
        def __init__(self, points, eps=1.0, min_pts=2):
            self._pts = points
            self._eps = eps
        def run(self):
            return [self._pts] if self._pts else []

    SelectionEngine = SmartOrganizer = _S
    LayoutScorer = _S
    KMeans = _KMeansStub
    DBSCAN = _DBSCANStub

try:
    import tag_config as TC
    import tag_logic  as TL
    _ISO_LIBS = True
except Exception:
    TC = TL = None
    _ISO_LIBS = False

from pyrevit import forms
from pyrevit.forms import WPFWindow
from Autodesk.Revit.DB import (
    FilteredElementCollector, FilteredWorksetCollector, IndependentTag, Transaction,
    XYZ, Reference, TagMode, TagOrientation, ElementId,
    BuiltInCategory, OverrideGraphicSettings, Color,
    FamilySymbol, ViewSheet, Viewport, View,
    SpatialElementTag, SpatialElement, ViewSchedule, ScheduleSheetInstance,
    RevitLinkInstance, BoundingBoxXYZ, ParameterFilterElement, FillPatternElement,
    LeaderEndCondition, BuiltInParameter,
    Grid, StorageType, CategoryType, InstanceBinding, CategorySet,
    DesignOption, Group, AssemblyInstance,
    BoundingBoxIntersectsFilter, Outline,
    WorksetKind, WorksetFilter,
    SectionType, UnitUtils,
)
from Autodesk.Revit.UI import IExternalEventHandler, ExternalEvent

# ── Revit version-safe imports (2023 vs 2024+ API changes) ────────────────
try:
    from Autodesk.Revit.DB import (
        ParameterType, BuiltInParameterGroup,
        ExternalDefinitionCreationOptions,
    )
    _HAS_PARAMETER_TYPE = True
except ImportError:
    _HAS_PARAMETER_TYPE = False
    try:
        from Autodesk.Revit.DB import (
            ForgeTypeId, SpecTypeId, GroupTypeId,
            ExternalDefinitionCreationOptions,
        )
    except ImportError:
        pass

try:
    from Autodesk.Revit.DB import TagTextHorizontalAlignment as TTHA
except ImportError:
    TTHA = None

try:
    from Autodesk.Revit.DB import IsolateElementsTemporaryView
except ImportError:
    IsolateElementsTemporaryView = None

try:
    from Autodesk.Revit.DB import (
        PDFExportOptions, PDFPaperSize, PDFColorDepthType,
    )
except ImportError:
    PDFExportOptions = PDFPaperSize = PDFColorDepthType = None

try:
    from Autodesk.Revit.DB import (
        ScheduleDefinition, ScheduleFieldType, SchedulableField,
        ScheduleFilter, ScheduleFilterType,
    )
except ImportError:
    ScheduleDefinition = ScheduleFieldType = SchedulableField = None
    ScheduleFilter = ScheduleFilterType = None

try:
    from Autodesk.Revit.DB import ConnectorManager
    from Autodesk.Revit.DB.Plumbing import PipingSystem
    from Autodesk.Revit.DB.Mechanical import MechanicalSystem
except ImportError:
    ConnectorManager = PipingSystem = MechanicalSystem = None

try:
    from Autodesk.Revit.UI.Selection import ObjectType
except ImportError:
    ObjectType = None

# ── WPF / System.Windows imports ──────────────────────────────────────────
from System.Collections.Generic import List
from System.Windows.Input import Key
from System.Windows.Threading import Dispatcher, DispatcherPriority, DispatcherTimer
from System.Windows.Media import Brushes, SolidColorBrush
try:
    from System.Windows.Media import Color as MColor
    from System.Windows.Media import Colors
except ImportError:
    MColor = None
    Colors = None

from System.Windows import LogicalTreeHelper, DependencyObject
try:
    from System.Windows import Thickness
except ImportError:
    Thickness = None

try:
    from System.Windows.Input import FocusManager
except ImportError:
    try:
        from System.Windows import FocusManager
    except ImportError:
        FocusManager = None

from System.Windows.Controls import Button as _WBtn
try:
    from System.Windows.Controls import (
        TextBox, ComboBoxItem, CheckBox, StackPanel, TextBlock, Separator,
    )
    CBI = ComboBoxItem  # alias used throughout
except ImportError:
    TextBox = ComboBoxItem = CheckBox = StackPanel = TextBlock = Separator = None
    CBI = None

try:
    from System.ComponentModel import CancelEventHandler
except ImportError:
    CancelEventHandler = None

try:
    from System.Windows import RoutedEventHandler as _REH
except ImportError:
    _REH = None

try:
    from collections import Counter, defaultdict
except ImportError:
    pass

try:
    from pyrevit import revit as _pyrevit_revit
except ImportError:
    _pyrevit_revit = None

import System
import os
import io
import json
import csv as _csv
import tempfile
import math as _m

# ── Convenience aliases used throughout handlers ──────────────────────────
BIP = BuiltInParameter
_TB = TextBox
SP = StackPanel
TB = TextBlock
WColor = MColor
_r = _pyrevit_revit
_os = os


# ─────────────────────────────────────────────────────────────────────────────
# ExternalEvent handler — marshals modeless-panel actions to the Revit API thread
# ─────────────────────────────────────────────────────────────────────────────
class _RevitCallbackHandler(IExternalEventHandler):
    """Runs an arbitrary Python callable on the Revit API thread."""
    def __init__(self):
        self._fn = None        # callable queued by the WPF click wrapper
        self._panel = None     # back-ref for error logging
    def Execute(self, uiapp):
        fn = self._fn
        self._fn = None
        if fn:
            try:
                fn()
            except Exception as ex:
                try:
                    if self._panel:
                        self._panel._log(str(ex), '!')
                except Exception:
                    pass
    def GetName(self):
        return 'STINGTagsCallback'

_revit_cb = _RevitCallbackHandler()
try:
    _revit_event = ExternalEvent.Create(_revit_cb)
except Exception:
    _revit_event = None


# ─────────────────────────────────────────────────────────────────────────────
# forms.alert compatibility wrapper
# ─────────────────────────────────────────────────────────────────────────────
_original_alert = forms.alert

def _compat_alert(msg, **kwargs):
    """Wrapper for forms.alert that normalises keyword arguments across
    different pyRevit versions (ok_button vs ok, cancel_button vs cancel)."""
    # Normalise keywords
    if 'ok_button' in kwargs:
        kwargs.setdefault('ok', kwargs.pop('ok_button'))
    if 'cancel_button' in kwargs:
        kwargs.setdefault('cancel', kwargs.pop('cancel_button'))
    try:
        return _original_alert(msg, **kwargs)
    except TypeError:
        # Fall back to positional-only call
        try:
            return _original_alert(str(msg))
        except Exception:
            return False

forms.alert = _compat_alert


# ─────────────────────────────────────────────────────────────────────────────
# Module-level state persisted across open/close cycles
# ─────────────────────────────────────────────────────────────────────────────
_last_tab_index = 0       # enhancement 6: tab memory


# ─────────────────────────────────────────────────────────────────────────────
# Constants
# ─────────────────────────────────────────────────────────────────────────────
_MEP_BICS_NAMES = [
    'OST_LightingFixtures', 'OST_ElectricalEquipment', 'OST_ElectricalFixtures',
    'OST_MechanicalEquipment', 'OST_PlumbingFixtures', 'OST_DuctTerminal',
    'OST_Sprinklers', 'OST_PipeCurves', 'OST_PipeFitting', 'OST_PipeAccessory',
    'OST_DuctCurves', 'OST_DuctFitting', 'OST_DuctAccessory', 'OST_DuctInsulations',
    'OST_Conduit', 'OST_ConduitFitting', 'OST_CableTray', 'OST_CableTrayFitting',
    'OST_FlexDuctCurves', 'OST_FlexPipeCurves', 'OST_Furniture',
    'OST_Doors', 'OST_Windows', 'OST_GenericModel', 'OST_SpecialityEquipment',
    'OST_FireAlarmDevices', 'OST_CommunicationDevices', 'OST_DataDevices',
    'OST_NurseCallDevices', 'OST_SecurityDevices', 'OST_TelephoneDevices',
]
_MEP_BICS = []
for _bname in _MEP_BICS_NAMES:
    try:
        _MEP_BICS.append(getattr(BuiltInCategory, _bname))
    except AttributeError:
        pass
_BIC_MAP = {
    'lights':    BuiltInCategory.OST_LightingFixtures,
    'elec':      BuiltInCategory.OST_ElectricalEquipment,
    'mech':      BuiltInCategory.OST_MechanicalEquipment,
    'plumb':     BuiltInCategory.OST_PlumbingFixtures,
    'air':       BuiltInCategory.OST_DuctTerminal,
    'furn':      BuiltInCategory.OST_Furniture,
    'doors':     BuiltInCategory.OST_Doors,
    'windows':   BuiltInCategory.OST_Windows,
    'rooms':     BuiltInCategory.OST_Rooms,
    'pipes':     BuiltInCategory.OST_PipeCurves,
    'ducts':     BuiltInCategory.OST_DuctCurves,
    'conduit':   BuiltInCategory.OST_Conduit,
    'cable':     BuiltInCategory.OST_CableTray,
    'sprinkler': BuiltInCategory.OST_Sprinklers,
}
# Full ISO 19650-1:2018 Table 1 token set (14 params + assembled tag)
_ISO_PARAMS = [
    'ASS_PROJECT_COD_TXT',      # 1  Project / client code
    'ASS_ORIGINATOR_COD_TXT',   # 2  Originator (author studio)
    'ASS_VOLUME_COD_TXT',       # 3  Volume / system (building wing / plant)
    'ASS_LVL_COD_TXT',          # 4  Level
    'ASS_DISCIPLINE_COD_TXT',   # 5  Discipline / role code
    'ASS_LOC_TXT',              # 6  Location
    'ASS_ZONE_TXT',             # 7  Zone
    'ASS_SYSTEM_TYPE_TXT',      # 8  System type
    'ASS_FUNC_TXT',             # 9  Function
    'ASS_PRODCT_COD_TXT',       # 10 Product / type code
    'ASS_SEQ_NUM_TXT',          # 11 Sequence number
    'ASS_STATUS_COD_TXT',       # 12 Status (S0–S7)
    'ASS_REV_COD_TXT',          # 13 Revision (P1, C1 …)
]
# Assembled tag output field — computed by Naviate / Auto Populate
_ISO_TAG_FIELD  = 'ASS_TAG_1_TXT'
# Short display names for reports
_ISO_SHORT = {
    'ASS_PROJECT_COD_TXT':    'PROJECT',
    'ASS_ORIGINATOR_COD_TXT': 'ORIG',
    'ASS_VOLUME_COD_TXT':     'VOL',
    'ASS_LVL_COD_TXT':        'LVL',
    'ASS_DISCIPLINE_COD_TXT': 'DISC',
    'ASS_LOC_TXT':            'LOC',
    'ASS_ZONE_TXT':           'ZONE',
    'ASS_SYSTEM_TYPE_TXT':    'SYS',
    'ASS_FUNC_TXT':           'FUNC',
    'ASS_PRODCT_COD_TXT':     'PROD',
    'ASS_SEQ_NUM_TXT':        'SEQ',
    'ASS_STATUS_COD_TXT':     'STATUS',
    'ASS_REV_COD_TXT':        'REV',
}
_SPACING_VALID_COLOR  = '#B0BEC5'
_SPACING_INVALID_COLOR = '#E53935'


# ─────────────────────────────────────────────────────────────────────────────
# Utilities
# ─────────────────────────────────────────────────────────────────────────────
def _fresh():
    try:
        uid = __revit__.ActiveUIDocument
        return uid.Document, uid
    except Exception:
        pass
    try:
        return _pyrevit_revit.doc, _pyrevit_revit.uidoc
    except Exception:
        return None, None


def _bb_center(elem, view):
    try:
        bb = elem.get_BoundingBox(view)
        if bb:
            return XYZ((bb.Min.X + bb.Max.X) / 2,
                       (bb.Min.Y + bb.Max.Y) / 2,
                       (bb.Min.Z + bb.Max.Z) / 2)
    except Exception:
        pass
    return None


def _view_tags(doc, view):
    try:
        return list(FilteredElementCollector(doc, view.Id)
                    .OfClass(IndependentTag).ToElements())
    except Exception:
        return []


def _sel_tags(doc, uidoc):
    return [doc.GetElement(eid) for eid in uidoc.Selection.GetElementIds()
            if isinstance(doc.GetElement(eid), IndependentTag)]


def _sel_elems(doc, uidoc):
    return [doc.GetElement(eid) for eid in uidoc.Selection.GetElementIds()
            if doc.GetElement(eid) is not None
            and not isinstance(doc.GetElement(eid), IndependentTag)]


def _get_host_center(tag, view):
    """Get the center of a tag's host element. Returns XYZ or None.
    Tries four methods to find the host element."""
    elem = None
    # Method 1: GetTaggedLocalElements (Revit 2022+)
    try:
        hosts = list(tag.GetTaggedLocalElements())
        if hosts:
            elem = hosts[0]
    except Exception:
        pass
    # Method 2: GetTaggedLocalElement (Revit 2018-2021)
    if elem is None:
        try:
            elem = tag.GetTaggedLocalElement()
        except Exception:
            pass
    # Method 3: TaggedElementId property
    if elem is None:
        try:
            teid = tag.TaggedElementId
            # TaggedElementId is a LinkElementId in 2022+
            try:
                eid = teid.HostElementId
            except Exception:
                eid = teid
            if eid and eid != ElementId.InvalidElementId:
                elem = tag.Document.GetElement(eid)
        except Exception:
            pass
    # Method 4: TaggedLocalElementId (older API)
    if elem is None:
        try:
            eid = tag.TaggedLocalElementId
            if eid and eid != ElementId.InvalidElementId:
                elem = tag.Document.GetElement(eid)
        except Exception:
            pass
    if elem is None:
        return None
    return _bb_center(elem, view)


def _get_tagged_ref(tag):
    """Get a single Reference to the tagged element.
    Returns Reference or None. Tries multiple API paths."""
    # Method 1: GetTaggedReferences (Revit 2022+ ISet<Reference>)
    try:
        refs = tag.GetTaggedReferences()
        # IronPython: iterate ISet
        for r in refs:
            return r
    except Exception:
        pass
    # Method 2: Build Reference from tagged element
    try:
        hosts = list(tag.GetTaggedLocalElements())
        if hosts:
            return Reference(hosts[0])
    except Exception:
        pass
    try:
        host = tag.GetTaggedLocalElement()
        if host:
            return Reference(host)
    except Exception:
        pass
    # Method 3: From TaggedElementId
    try:
        teid = tag.TaggedElementId
        try:
            eid = teid.HostElementId
        except Exception:
            eid = teid
        if eid and eid != ElementId.InvalidElementId:
            elem = tag.Document.GetElement(eid)
            if elem:
                return Reference(elem)
    except Exception:
        pass
    return None


def _get_elbow(tag):
    """Safely read leader elbow position. Returns XYZ or None."""
    if not tag.HasLeader:
        return None
    # Method 1: Revit 2022+ SetLeaderElbow/GetLeaderElbow(Reference)
    ref = _get_tagged_ref(tag)
    if ref:
        try:
            eb = tag.GetLeaderElbow(ref)
            if eb is not None:
                return eb
        except Exception:
            pass
    # Method 2: Legacy property
    try:
        eb = tag.LeaderElbow
        if eb is not None:
            return eb
    except Exception:
        pass
    return None


def _set_elbow(tag, xyz):
    """Safely set leader elbow position. Returns True on success.
    Tries Reference-based API first (Revit 2022+), then legacy."""
    if not tag.HasLeader:
        try:
            tag.HasLeader = True
        except Exception:
            return False
    # Method 1: Revit 2022+ SetLeaderElbow(Reference, XYZ)
    ref = _get_tagged_ref(tag)
    if ref:
        try:
            tag.SetLeaderElbow(ref, xyz)
            return True
        except Exception:
            pass
    # Method 2: Legacy property
    try:
        tag.LeaderElbow = xyz
        return True
    except Exception:
        pass
    return False


def _get_leader_end(tag):
    """Get the leader end point. Returns XYZ or None."""
    if not tag.HasLeader:
        return None
    ref = _get_tagged_ref(tag)
    if ref:
        try:
            return tag.GetLeaderEnd(ref)
        except Exception:
            pass
    try:
        return tag.LeaderEnd
    except Exception:
        pass
    return None


def _set_leader_end(tag, xyz):
    """Set leader end point. Returns True on success."""
    if not tag.HasLeader:
        try:
            tag.HasLeader = True
        except Exception:
            return False
    ref = _get_tagged_ref(tag)
    if ref:
        try:
            tag.SetLeaderEnd(ref, xyz)
            return True
        except Exception:
            pass
    try:
        tag.LeaderEnd = xyz
        return True
    except Exception:
        pass
    return False


def _make_leader_straight(tag, view):
    """Remove elbow bend by placing it co-linear on the leader."""
    try:
        h = tag.TagHeadPosition
        end = _get_host_center(tag, view)
        if not end:
            end = _get_leader_end(tag)
        if not end:
            return False
        frac = 1.0 / 3.0
        mid = XYZ(h.X + (end.X - h.X) * frac,
                  h.Y + (end.Y - h.Y) * frac, h.Z)
        return _set_elbow(tag, mid)
    except Exception:
        return False


def _create_elbow_at_midpoint(tag, view, perp_fraction=0.25):
    """Create an elbow at the perpendicular midpoint between tag and host.
    Returns the elbow XYZ on success, None on failure."""
    if not tag.HasLeader:
        try:
            tag.HasLeader = True
        except Exception:
            return None
    c = _get_host_center(tag, view)
    if not c:
        return None
    h = tag.TagHeadPosition
    dx = h.X - c.X
    dy = h.Y - c.Y
    dist = math.sqrt(dx * dx + dy * dy)
    if dist < 0.01:
        return None
    mx = (h.X + c.X) / 2
    my = (h.Y + c.Y) / 2
    perp = dist * perp_fraction
    elbow_pt = XYZ(mx + (-dy / dist) * perp,
                   my + (dx / dist) * perp, h.Z)
    if _set_elbow(tag, elbow_pt):
        return elbow_pt
    return None


def _add_multi_leader(tag, doc, view):
    """Add an additional leader to a tag (Revit 2022+ multi-leader).
    Returns True on success."""
    try:
        ref = _get_tagged_ref(tag)
        if ref:
            tag.AddLeader(ref)
            return True
    except Exception:
        pass
    return False


def _set_ids(uidoc, elems):
    uidoc.Selection.SetElementIds(List[ElementId]([e.Id for e in elems]))
    return len(elems)


def _tag_data(tags, view):
    data = []
    for tag in tags:
        try:
            h = tag.TagHeadPosition
            hosts = list(tag.GetTaggedLocalElements())
            bb = hosts[0].get_BoundingBox(view) if hosts else None
            ep = Vec2((bb.Min.X + bb.Max.X) / 2,
                      (bb.Min.Y + bb.Max.Y) / 2) if bb else Vec2(h.X, h.Y)
            data.append({'tag': tag, 'pos': Vec2(h.X, h.Y), 'elem': ep, 'z': h.Z})
        except Exception:
            pass
    return data


def _iso_get(el, pname):
    try:
        from Autodesk.Revit.DB import StorageType
        p = el.LookupParameter(pname)
        if p and p.StorageType == StorageType.String:
            return p.AsString() or ''
    except Exception:
        pass
    return ''


def _iso_set(el, pname, val, overwrite=False):
    try:
        from Autodesk.Revit.DB import StorageType
        p = el.LookupParameter(pname)
        if p and not p.IsReadOnly and p.StorageType == StorageType.String:
            if overwrite or not _iso_get(el, pname):
                p.Set(val)
                return True
    except Exception:
        pass
    return False


def _dispatch_ui(fn):
    """Run fn on the WPF dispatcher so mid-loop log updates paint immediately."""
    try:
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background,
                                             System.Action(fn))
    except Exception:
        try:
            fn()
        except Exception:
            pass


def _elem_phase_created(el, doc):
    """Return the IntegerValue of the element's Phase Created, or -1."""
    try:
        from Autodesk.Revit.DB import BuiltInParameter as BIP
        p = el.get_Parameter(BIP.PHASE_CREATED)
        return p.AsElementId().IntegerValue if p else -1
    except Exception:
        return -1


def _elem_phase_name(el, doc):
    """Return display name of the element's Phase Created."""
    try:
        from Autodesk.Revit.DB import BuiltInParameter as BIP
        p = el.get_Parameter(BIP.PHASE_CREATED)
        if p:
            ph = doc.GetElement(p.AsElementId())
            return ph.Name if ph else 'Unknown'
    except Exception:
        pass
    return 'Unknown'


def _elem_design_option(el):
    """Return IntegerValue of the element's DesignOption, or -1."""
    try:
        from Autodesk.Revit.DB import BuiltInParameter as BIP
        p = el.get_Parameter(BIP.DESIGN_OPTION_ID)
        return p.AsElementId().IntegerValue if p else -1
    except Exception:
        return -1


# ─────────────────────────────────────────────────────────────────────────────
# Panel
# ─────────────────────────────────────────────────────────────────────────────
class STINGTagsPanel(WPFWindow):

    def __init__(self, doc, uidoc):
        xaml = os.path.join(os.path.dirname(__file__), 'STINGTagsPanel.xaml')
        WPFWindow.__init__(self, xaml)
        # ── Snapshot ALL module globals while they are still accessible ──
        # WPF callbacks in IronPython persistent-engine mode lose access
        # to module-level names. We capture the entire namespace here and
        # restore it inside every callback via globals().update().
        self._g = dict(globals())
        # ExternalEvent for marshalling to the Revit API thread
        self._revit_event = _revit_event
        _revit_cb._panel = self
        # ── Module-level → instance: WPF callbacks lose module globals ──
        self._fresh = _fresh
        self._bb_center = _bb_center
        self._view_tags = _view_tags
        self._sel_tags = _sel_tags
        self._sel_elems = _sel_elems
        self._get_host_center = _get_host_center
        self._get_elbow = _get_elbow
        self._set_elbow = _set_elbow
        self._create_elbow = _create_elbow_at_midpoint
        self._get_leader_end = _get_leader_end
        self._set_leader_end = _set_leader_end
        self._make_straight = _make_leader_straight
        self._add_multi_leader = _add_multi_leader
        self._set_ids = _set_ids
        self._tag_data = _tag_data
        self._iso_get = _iso_get
        self._iso_set = _iso_set
        self._dispatch_ui = _dispatch_ui
        self._elem_phase_created = _elem_phase_created
        self._elem_phase_name = _elem_phase_name
        self._elem_design_option = _elem_design_option
        self._MEP_BICS = _MEP_BICS
        self._BIC_MAP = _BIC_MAP
        self._ISO_PARAMS = _ISO_PARAMS
        self._ISO_TAG_FIELD = _ISO_TAG_FIELD
        self._ISO_SHORT = _ISO_SHORT
        self._ENGINE = _ENGINE
        self._ISO_LIBS = _ISO_LIBS
        self._TC = TC
        self._TL = TL
        self._doc0, self._uidoc0 = doc, uidoc
        self._organizer   = None
        self._pattern     = PatternLearner()
        self._mem         = [[], [], []]
        self._iso_scope   = 'view'
        self._iso_overwrite = False
        self._undo_stack  = {}          # keyed by view.Id.IntegerValue, list of (label, snapshots)
        self._tag_sym_id  = None
        self._esc_was_empty = False

        # v9.4 colouriser state
        self._active_fill_hex    = '#2196F3'
        self._active_outline_hex = '#1565C0'
        self._outline_enabled    = False
        self._grad_from_hex      = '#2196F3'
        self._grad_to_hex        = '#F44336'
        self._grad_picking       = None   # 'from' or 'to' — swatch next click goes here

        # v9.4 param lookup state
        self._param_db   = None         # loaded lazily from param_db.json
        self._param_cond_rows = 1       # 1, 2, or 3 active condition rows
        self._iso_dash_rows  = []       # current dashboard data
        self._placement_history = self._load_placement_history()
        self._dashboard_rows = []       # ISO dashboard rows (WPF binding)

        # v9.4 viewport sync — track last view IntegerValue
        self._last_view_id = None

        # Restore last tab
        try:
            self.MainTabControl.SelectedIndex = _last_tab_index
        except Exception:
            pass

        try:
            self.SpacingPresets.SelectedIndex = -1
        except Exception:
            pass

        try:
            self.PassCountBadge.Text = '- /8'
        except Exception:
            pass

        # v9.5 / v9.6 state — initialised here, BEFORE timer and _wire_events,
        # so no handler can ever find these attributes missing.
        self._bulk_param_map       = {}
        self._locked_leader_ends   = {}
        self._filter_toggle_refs   = []
        self._health_metrics       = []
        self._anomaly_results      = []
        self._anomaly_elements     = []
        self._ai_place_snapshot    = {}
        self._ai_place_options     = {
            'clearance_ft': 0.5,
            'max_leader_ft': 3.0,
            'preferred_quadrant': 'auto',
        }

        # Load param DB lazily — populate category filter
        self._load_param_db()
        self._populate_param_cat_filter()
        # Load colour schemes from config
        self._refresh_colour_scheme_combo()

        # Wire all events explicitly (pyrevit XAML does not auto-wire without x:Class)
        self._wire_events()

        # DispatcherTimer — 500 ms: sel count + view sync.
        # Started AFTER _wire_events so no tick can fire against uninitialised state.
        try:
            self._sel_timer = DispatcherTimer()
            self._sel_timer.Interval = System.TimeSpan.FromMilliseconds(500)
            self._sel_timer.Tick += self._sel_timer_tick
            self._sel_timer.Start()
        except Exception:
            self._sel_timer = None

    def _wire_events(self):
        """Wire every XAML event handler.

        Button wiring strategy (228 buttons, none with x:Name):
          1. Walk the LOGICAL tree with LogicalTreeHelper.
             Unlike VisualTreeHelper, the logical tree contains ALL
             elements immediately after XAML parse — including content
             inside inactive TabItems that has never been rendered.
          2. For every Button whose Tag ends with '_Click', look up
             the matching method on self and wire button.Click directly.
          3. Use a closure factory (_make_click) so each delegate
             captures the method name by value, not by reference.

        Non-button controls (ComboBox, TextBox, Slider) are wired
        individually by x:Name at the end.
        """
        # (module-level import)
        # (module-level import)

        panel_ref = self           # prevent IronPython 2.7 closure scope issues
        wired     = [0]            # mutable int for nested-function write access
        missed    = [0]

        # ── Closure factory: bake method_name by VALUE, not ref ──────
        # Queues handler to run on Revit API thread via ExternalEvent
        _evt  = self._revit_event   # capture locally for closure
        _cb   = _revit_cb           # capture locally for closure

        def _make_click(method_name):
            """Return a delegate-compatible function for one button."""
            def _handler(sender, args):
                def _do():
                    try:
                        globals().update(panel_ref._g)
                        fn = getattr(panel_ref, method_name, None)
                        if fn:
                            fn(sender, args)
                        else:
                            panel_ref._log('No handler: ' + method_name, '!')
                    except Exception as ex:
                        try:
                            panel_ref._log(method_name + ': ' + str(ex), '!')
                        except Exception:
                            pass
                if _evt:
                    _cb._fn = _do
                    _evt.Raise()
                else:
                    _do()
            return _handler

        # ── Recursive walk through the LOGICAL tree ──────────────────
        def _walk(element):
            # Only inspect DependencyObjects (skip strings etc.)
            if not isinstance(element, DependencyObject):
                return
            # Wire this element if it is a Button with a Tag
            if isinstance(element, _WBtn):
                try:
                    raw_tag = element.Tag
                    if raw_tag is not None:
                        tag_str = str(raw_tag)
                        if tag_str.endswith('_Click'):
                            if hasattr(panel_ref, tag_str):
                                element.Click += _make_click(tag_str)
                                wired[0] += 1
                            else:
                                missed[0] += 1
                except Exception:
                    pass
            # Recurse into children
            try:
                for child in LogicalTreeHelper.GetChildren(element):
                    _walk(child)
            except Exception:
                pass

        # ── Execute the walk ─────────────────────────────────────────
        try:
            _walk(self)
        except Exception as ex:
            try:
                self._log('Tree walk error: ' + str(ex), '!')
            except Exception:
                pass

        # Log result (visible in the panel status bar)
        if wired[0] > 0:
            self._log('v9.6 ready — {} buttons wired'.format(wired[0]), '*')

            # Deferred auto-refresh: populate combos after the window is visible
            def _deferred_init():
                try: self.RefreshTagFamilies_Click(None, None)
                except Exception: pass
                try: self.RefreshLiveParams_Click(None, None)
                except Exception: pass
            try:
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    System.Action(_deferred_init))
            except Exception: pass
        else:
            self._log('WARNING: 0 buttons wired — tree walk failed', '!')

        # ── Event wrapper: run on Revit API thread via ExternalEvent ──
        # Use for handlers that create Transactions or modify Revit elements
        def _wrap_evt(method):
            """Wrap an instance method so it runs on the Revit API thread."""
            def _wrapped(sender, args):
                def _do():
                    try:
                        globals().update(panel_ref._g)
                        method(sender, args)
                    except Exception as ex:
                        try:
                            panel_ref._log(str(ex), '!')
                        except Exception:
                            pass
                if _evt:
                    _cb._fn = _do
                    _evt.Raise()
                else:
                    _do()
            return _wrapped

        # ── UI-only event wrapper: runs directly, just restores globals ──
        # Use for SelectionChanged, TextChanged, GotFocus, ValueChanged,
        # Closing, and other events that only update WPF controls.
        def _wrap_ui(method):
            """Wrap an instance method, restoring globals but without ExternalEvent."""
            def _wrapped(sender, args):
                try:
                    globals().update(panel_ref._g)
                    method(sender, args)
                except Exception as ex:
                    try:
                        panel_ref._log(str(ex), '!')
                    except Exception:
                        pass
            return _wrapped

        # ── Window-level events ──────────────────────────────────────
        try:
            # (module-level import)
            self.Closing += CancelEventHandler(_wrap_ui(self._on_panel_closing))
        except Exception:
            try:
                self.Closing += _wrap_ui(self._on_panel_closing)
            except Exception:
                pass

        try: self.PreviewKeyDown += _wrap_ui(self.Window_PreviewKeyDown)
        except Exception: pass

        # Double-click on title bar (HeaderBar) to collapse/expand
        try:
            if hasattr(self, 'HeaderBar'):
                self.HeaderBar.MouseLeftButtonDown += _wrap_ui(self._header_mouse_down)
        except Exception: pass
        # Prevent double-click from maximizing the window
        try:
            self.StateChanged += _wrap_ui(self._on_state_changed)
        except Exception: pass
        self._collapsed = False
        self._expanded_height = None
        self._suppress_maximize = False

        # ── Named non-button events ──────────────────────────────────
        try: self.MainTabControl.SelectionChanged += _wrap_ui(self.TabControl_SelectionChanged)
        except Exception: pass
        try: self.BulkParamValue.KeyDown += _wrap_ui(self.BulkParamValue_KeyDown)
        except Exception: pass
        try: self.LiveParamCombo.SelectionChanged += _wrap_ui(self.LiveParamCombo_SelectionChanged)
        except Exception: pass
        try: self.ParamSearchBox.TextChanged += _wrap_ui(self.ParamSearch_TextChanged)
        except Exception: pass
        try: self.ParamCatFilter.SelectionChanged += _wrap_ui(self.ParamCatFilter_SelectionChanged)
        except Exception: pass
        try: self.ParamResultsList.SelectionChanged += _wrap_ui(self.ParamResult_SelectionChanged)
        except Exception: pass
        try: self.UndoHistoryCombo.SelectionChanged += _wrap_ui(self.UndoHistoryCombo_SelectionChanged)
        except Exception: pass
        try: self.DashboardFilterSlider.ValueChanged += _wrap_ui(self.DashboardFilterSlider_ValueChanged)
        except Exception: pass
        try: self.DashboardRAGFilter.SelectionChanged += _wrap_ui(self.DashboardRAGFilter_SelectionChanged)
        except Exception: pass
        try: self.IsoDashboardGrid.SelectionChanged += _wrap_ui(self.IsoDashboardGrid_SelectionChanged)
        except Exception: pass
        try: self.ColourSchemeCombo.SelectionChanged += _wrap_ui(self.ColourSchemeCombo_SelectionChanged)
        except Exception: pass
        try: self.PaletteSelector.SelectionChanged += _wrap_ui(self.PaletteSelector_SelectionChanged)
        except Exception: pass
        try: self.Cond1Param.GotFocus += _wrap_ui(self.FilterParamName_GotFocus)
        except Exception: pass
        try: self.CustomHexInput.GotFocus += _wrap_ui(self.CustomHexInput_GotFocus)
        except Exception: pass
        try: self.CustomHexInput.KeyDown += _wrap_ui(self.CustomHexInput_KeyDown)
        except Exception: pass
        try: self.TransparencySlider.ValueChanged += _wrap_ui(self.TransparencySlider_ValueChanged)
        except Exception: pass
        try: self.SpacingSlider.ValueChanged += _wrap_ui(self.SpacingSlider_ValueChanged)
        except Exception: pass
        try: self.SpacingInput.KeyUp += _wrap_ui(self.SpacingInput_KeyUp)
        except Exception: pass
        try: self.SpacingPresets.SelectionChanged += _wrap_ui(self.SpacingPresets_SelectionChanged)
        except Exception: pass

        # ── Named button Click events (no Tag, need explicit wiring) ──
        try: self.TopmostBtn.Click += _wrap_ui(self.ToggleTopmost_Click)
        except Exception: pass
        try: self.SmartOrgBtn.Click += _wrap_evt(self.SmartOrganize_Click)
        except Exception: pass
        try: self.IsoScopeBtn.Click += _wrap_ui(self.IsoToggleScope_Click)
        except Exception: pass
        try: self.IsoOverwriteBtn.Click += _wrap_ui(self.IsoToggleOverwrite_Click)
        except Exception: pass
        try: self.GradFromBtn.Click += _wrap_ui(self.GradFrom_Click)
        except Exception: pass
        try: self.GradToBtn.Click += _wrap_ui(self.GradTo_Click)
        except Exception: pass
        try: self.ColourOutlineToggleBtn.Click += _wrap_ui(self.ColourToggleOutline_Click)
        except Exception: pass

        # ── Swatch panels — bubbled Click from child buttons ──────────
        # (module-level import)
        try: self.FillSwatchRow1.AddHandler(
            _WBtn.ClickEvent, _REH(_wrap_evt(self.ColourSwatch_Click)))
        except Exception: pass
        try: self.FillSwatchRow2.AddHandler(
            _WBtn.ClickEvent, _REH(_wrap_evt(self.ColourSwatch_Click)))
        except Exception: pass
        try: self.OutlineSwatchPanel.AddHandler(
            _WBtn.ClickEvent, _REH(_wrap_evt(self.OutlineSwatch_Click)))
        except Exception: pass

    def _history_path(self):
        return os.path.join(_data, 'placement_history.json')

    def _load_placement_history(self):
        """Load per-category placement offset history from lib/placement_history.json."""
        try:
            with io.open(self._history_path(), encoding='utf-8') as f:
                return json.load(f)
        except Exception:
            return {}   # {category_name: {'count': int, 'sum_dx': float, 'sum_dy': float}}

    def _save_placement_history(self):
        """Persist placement history to data/placement_history.json."""
        try:
            p = self._history_path()
            try:
                os.makedirs(os.path.dirname(p))
            except OSError:
                pass
            with io.open(p, 'w', encoding='utf-8') as f:
                json.dump(self._placement_history, f, indent=2)
        except Exception:
            pass

    def _record_tag_observation(self, el, tag, view):
        """Record the offset (tag head → element center) for learning.
        Called after each successful tag placement."""
        try:
            cat = el.Category.Name if el.Category else 'Unknown'
            c   = self._bb_center(el, view)
            if not c: return
            h   = tag.TagHeadPosition
            dx  = h.X - c.X
            dy  = h.Y - c.Y
            rec = self._placement_history.setdefault(
                cat, {'count': 0, 'sum_dx': 0.0, 'sum_dy': 0.0})
            rec['count']  += 1
            rec['sum_dx'] += dx
            rec['sum_dy'] += dy
            # Keep running totals capped at 200 observations per category
            if rec['count'] > 200:
                rec['count']  = 100
                rec['sum_dx'] = (rec['sum_dx'] / 2.0)
                rec['sum_dy'] = (rec['sum_dy'] / 2.0)
        except Exception:
            pass

    def _on_panel_closing(self, sender, e):
        """Save placement history and stop the timer when panel closes."""
        try:
            self._save_placement_history()
        except Exception:
            pass
        try:
            if self._sel_timer:
                self._sel_timer.Stop()
        except Exception:
            pass

    # ── Enhancement 2: live selection count timer tick ─────────────────────────
    def _sel_timer_tick(self, s, e):
        """Polls uidoc selection count every 500 ms; updates badge + viewport sync."""
        globals().update(self._g)
        try:
            doc, uidoc = self._fd()
            count = uidoc.Selection.GetElementIds().Count if uidoc else 0
            self.SelCountBadge.Text = str(count)
        except Exception:
            pass
        # v9.4 Viewport sync — detect view change
        try:
            doc, uidoc = self._fd()
            if doc and uidoc:
                cur_view = doc.ActiveView
                cur_id   = cur_view.Id.IntegerValue if cur_view else None
                if cur_id and cur_id != self._last_view_id:
                    self._last_view_id = cur_id
                    self._on_view_changed(doc, uidoc, cur_view)
        except Exception:
            pass

    def _refresh_colour_scheme_combo(self):
        """Alias for _refresh_scheme_combo — called from __init__."""
        self._refresh_scheme_combo()

    def _on_view_changed(self, doc, uidoc, view):
        """Called when active Revit view changes."""
        try:
            self._refresh_undo_combo()
            self.ViewSyncText.Text = '⟳ ' + view.Name[:12]
        except Exception:
            pass

    def _check_view_sync(self, s=None, e=None):
        pass   # viewport sync handled inside _sel_timer_tick

    def _fd(self):
        """Return (doc, uidoc) — always prefers live __revit__ context."""
        doc, uidoc = self._fresh()
        return doc or self._doc0, uidoc or self._uidoc0

    def _spacing(self):
        try:
            v = float(self.SpacingInput.Text)
            if 0.01 <= v <= 10.0:
                return v
        except Exception:
            pass
        return 0.25

    # ── Log / status ──────────────────────────────────────────────────────────
    def _log(self, txt, icon='*'):
        try:
            self.ResultText.Text   = str(txt)
            first = str(txt).split('\n')[0][:60]
            self.StatusText.Text   = first
            self.LastToolText.Text = first[:55]
            self.LastToolIcon.Text = str(icon)
            # Make errors more visible
            if icon == '!':
                try:
                    self.StatusText.Foreground = SolidColorBrush(
                        MColor.FromRgb(220, 30, 30)) if MColor else Brushes.Red
                except Exception:
                    pass
            else:
                try:
                    self.StatusText.Foreground = SolidColorBrush(
                        MColor.FromRgb(50, 50, 90)) if MColor else Brushes.Black
                except Exception:
                    pass
        except Exception:
            pass

    def _iso_log(self, txt, icon='✔'):
        """Enhancement 7: prefix every CREATE action log with active scope badge."""
        scope_labels = {'view': 'View', 'selection': 'Sel', 'project': 'Proj'}
        badge = '[Scope: {}]  '.format(scope_labels.get(self._iso_scope, '*'))
        self._log(badge + txt, icon)

    @staticmethod
    def _open_folder(path):
        """Open a folder (or the parent folder of a file) in Windows Explorer.
        Falls back gracefully if not on Windows or if path is invalid.
        """
        try:
            folder = path if os.path.isdir(path) else os.path.dirname(path)
            if not os.path.exists(folder):
                return
            import subprocess
            subprocess.Popen(['explorer', folder])
        except Exception:
            pass

    @staticmethod
    def _open_file_safe(path):
        """Open a file with system default app, falling back to Explorer if that fails."""
        try:
            if not os.path.exists(path):
                return
            import subprocess
            subprocess.Popen(['explorer', path])
        except Exception:
            pass

    def ClearLog_Click(self, s, e):
        try:
            self.ResultText.Text = ''
            self.StatusText.Text = 'Log cleared'
        except Exception:
            pass

    def ToggleTopmost_Click(self, s, e):
        self.Topmost = not self.Topmost
        self._log('Always on top: {}'.format('ON' if self.Topmost else 'OFF'), '[Pin]')

    def _header_mouse_down(self, s, e):
        """Handle title bar clicks: double-click to collapse/expand."""
        try:
            if e.ClickCount == 2:
                self._suppress_maximize = True
                self._toggle_collapse()
                e.Handled = True
            elif e.ClickCount == 1 and e.ChangedButton.ToString() == 'Left':
                # Allow window dragging
                try: self.DragMove()
                except Exception: pass
        except Exception:
            pass

    def _on_state_changed(self, s, e):
        """Prevent window from going to Maximized state on title double-click."""
        try:
            if self._suppress_maximize:
                self._suppress_maximize = False
                if self.WindowState != 0:  # 0 = Normal
                    self.WindowState = 0   # Force back to Normal
        except Exception:
            pass

    def _window_double_click(self, s, e):
        """Fallback double-click handler on the window itself."""
        self._toggle_collapse()

    def _toggle_collapse(self):
        """Collapse window to title bar only, or expand back."""
        try:
            if not self._collapsed:
                self._expanded_height = self.ActualHeight
                self._expanded_min_h = getattr(self, 'MinHeight', 0) or 0
                # Force Normal state first
                self.WindowState = 0  # Normal
                self.SizeToContent = 0  # Manual
                self.MinHeight = 0
                self.Height = 60
                self.ResizeMode = 0  # NoResize
                self._collapsed = True
                self._log('Panel collapsed (double-click header to expand)', '*')
            else:
                h = self._expanded_height or 700
                self.ResizeMode = 3  # CanResizeWithGrip
                self.Height = h
                try: self.MinHeight = self._expanded_min_h or 200
                except Exception: pass
                self._collapsed = False
                self._log('Panel expanded', '*')
        except Exception as ex:
            self._log('Collapse error: ' + str(ex))

    # ── Enhancement 6: tab memory ─────────────────────────────────────────────
    def TabControl_SelectionChanged(self, s, e):
        # (using module-level _last_tab_index via closure)
        try:
            _last_tab_index = self.MainTabControl.SelectedIndex
        except Exception:
            pass

    # ── Enhancement 1: arrow-key nudge + enhancement 6: Esc guard ────────────
    def Window_PreviewKeyDown(self, s, e):
        """Intercept arrow keys before Revit or a focused TextBox consumes them.
        Esc: first press deselects; second press (when already empty) offers to close.
        """
        # FocusManager imported at module level
        focused = FocusManager.GetFocusedElement(self) if FocusManager else None
        # TextBox imported at module level
        if isinstance(focused, TextBox):
            return

        def _queue_nudge(dx, dy):
            """Queue a nudge operation to run on the Revit API thread."""
            panel = self
            def _do():
                globals().update(panel._g)
                panel._nudge(dx, dy)
            if self._revit_event:
                _revit_cb._fn = _do
                self._revit_event.Raise()

        if e.Key == Key.Up:
            _queue_nudge(0, 1);  e.Handled = True
        elif e.Key == Key.Down:
            _queue_nudge(0, -1); e.Handled = True
        elif e.Key == Key.Left:
            _queue_nudge(-1, 0); e.Handled = True
        elif e.Key == Key.Right:
            _queue_nudge(1, 0);  e.Handled = True
        elif e.Key == Key.Escape:
            # Enhancement 6: check if selection is already empty
            try:
                _, uidoc = self._fd()
                is_empty = (uidoc.Selection.GetElementIds().Count == 0)
            except Exception:
                is_empty = True
            if is_empty and self._esc_was_empty:
                # Second Esc with empty selection - offer close
                if forms.alert('Close STINGTags panel?',
                               ok=True, cancel=True, title='STINGTags'):
                    try:
                        if self._sel_timer:
                            self._sel_timer.Stop()
                    except Exception:
                        pass
                    self.Close()
            else:
                def _do_clear():
                    globals().update(self._g)
                    self.SelClear_Click(s, e)
                if self._revit_event:
                    _revit_cb._fn = _do_clear
                    self._revit_event.Raise()
                self._esc_was_empty = is_empty
            e.Handled = True

    # ── Enhancement 5: spacing validation + slider sync ───────────────────────
    def SpacingInput_KeyUp(self, s, e):
        try:
            v = float(self.SpacingInput.Text)
            valid = 0.01 <= v <= 10.0
        except Exception:
            valid = False
        # (module-level import)
        color = Color.FromRgb(0xB0, 0xBE, 0xC5) if valid \
                else Color.FromRgb(0xE5, 0x39, 0x35)
        self.SpacingInput.BorderBrush = SolidColorBrush(color)
        if valid:
            self.StatusText.Text = 'Spacing: {}ft'.format(v)
            # Enhancement 3: keep slider in sync (clamp to slider range 0.01–2.0)
            try:
                self.SpacingSlider.Value = max(0.01, min(2.0, v))
            except Exception:
                pass
        else:
            self.StatusText.Text = 'Invalid spacing (0.01–10.0 ft)'

    # ── Enhancement 3: spacing slider ─────────────────────────────────────────
    def SpacingSlider_ValueChanged(self, s, e):
        """Slider changed → push new value into the text box."""
        try:
            v = round(self.SpacingSlider.Value, 3)
            # Only update text if materially different (avoids feedback loop)
            try:
                existing = float(self.SpacingInput.Text)
                if abs(existing - v) < 0.0005:
                    return
            except Exception:
                pass
            self.SpacingInput.Text = str(v)
            # (module-level import)
            self.SpacingInput.BorderBrush = SolidColorBrush(
                Color.FromRgb(0xB0, 0xBE, 0xC5))
            self.StatusText.Text = 'Spacing: {}ft'.format(v)
        except Exception:
            pass

    # ── Enhancement 9: spacing presets ────────────────────────────────────────
    def SpacingPresets_SelectionChanged(self, s, e):
        try:
            item = self.SpacingPresets.SelectedItem
            if item is None:
                return
            tag = item.Tag
            if tag:
                self.SpacingInput.Text = str(tag)
                # (module-level import)
                self.SpacingInput.BorderBrush = SolidColorBrush(
                    Color.FromRgb(0xB0, 0xBE, 0xC5))
                # Enhancement 3: sync slider
                try:
                    self.SpacingSlider.Value = max(0.01, min(2.0, float(tag)))
                except Exception:
                    pass
                self._log('Spacing preset: {}ft  ({})'.format(
                    tag, item.Content), '[Align]')
            self.SpacingPresets.SelectedIndex = -1
        except Exception:
            pass

    # ─────────────────────────────────────────────────────────────────────────
    # SELECT TAB
    # ─────────────────────────────────────────────────────────────────────────

    # ── AI Smart ──────────────────────────────────────────────────────────────
    def SmartPredict_Click(self, s, e):
        """3-layer heuristic selector biased by persistent placement history.

        Layer 1 — Any untagged MEP elements in the view (most common need).
        Layer 2 — Dominant category in current selection (extend like kind).
        Layer 3 — History-ranked MEP elements (categories user tags most often).
        Layer 4 — All MEP elements (fallback).
        """
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView

        # Collect already-tagged element IDs
        tagged_ids = set()
        for t in self._view_tags(doc, view):
            try:
                for el in t.GetTaggedLocalElements():
                    tagged_ids.add(el.Id.IntegerValue)
            except Exception:
                pass

        # Layer 1: untagged MEP elements
        untagged = []
        for bic in self._MEP_BICS:
            try:
                for el in FilteredElementCollector(doc, view.Id).OfCategory(bic) \
                        .WhereElementIsNotElementType().ToElements():
                    if el.Id.IntegerValue not in tagged_ids:
                        untagged.append(el)
            except Exception:
                pass
        if untagged:
            # Sort by history frequency — most-tagged categories first
            hist = self._placement_history
            def _priority(el):
                try:
                    rec = hist.get(el.Category.Name, {})
                    return -(rec.get('count', 0))
                except Exception:
                    return 0
            untagged.sort(key=_priority)
            n = self._set_ids(uidoc, untagged)
            hint = ''
            if hist:
                top = sorted(hist.items(), key=lambda x: x[1].get('count', 0), reverse=True)
                if top: hint = '  (history: {} most common)'.format(top[0][0])
            self._log('PREDICT → {} untagged elements{}'.format(n, hint), '🔮'); return

        # Layer 2: extend selection to full dominant category
        cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds()
               if doc.GetElement(i)]
        if cur:
            cats = {}
            for el in cur:
                try: cats[el.Category.Name] = cats.get(el.Category.Name, 0) + 1
                except Exception: pass
            if cats:
                dominant = max(cats, key=cats.get)
                matched  = [el for el in
                            FilteredElementCollector(doc, view.Id)
                            .WhereElementIsNotElementType().ToElements()
                            if el.Category and el.Category.Name == dominant]
                n = self._set_ids(uidoc, matched)
                self._log('PREDICT → {} "{}" elements'.format(n, dominant), '🔮'); return

        # Layer 3: history-ranked — surface the most frequently tagged category
        if self._placement_history:
            ranked = sorted(self._placement_history.items(),
                            key=lambda x: x[1].get('count', 0), reverse=True)
            for cat_name, _ in ranked:
                candidates = [el for el in
                              FilteredElementCollector(doc, view.Id)
                              .WhereElementIsNotElementType().ToElements()
                              if el.Category and el.Category.Name == cat_name]
                if candidates:
                    n = self._set_ids(uidoc, candidates)
                    self._log('PREDICT → {} "{}" elements (from history)'.format(
                        n, cat_name), '🔮'); return

        # Layer 4: all MEP elements
        all_mep = []
        for bic in self._MEP_BICS:
            try:
                all_mep.extend(FilteredElementCollector(doc, view.Id)
                               .OfCategory(bic).WhereElementIsNotElementType().ToElements())
            except Exception:
                pass
        n = self._set_ids(uidoc, all_mep)
        self._log('PREDICT → {} MEP elements (full view)'.format(n), '🔮')

    def SelectSimilar_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds() if doc.GetElement(i)]
            if not cur: self._log('Select elements first'); return
            type_ids = set(el.GetTypeId().IntegerValue for el in cur
                           if el.GetTypeId() and el.GetTypeId() != ElementId.InvalidElementId)
            matched = [el for el in FilteredElementCollector(doc, doc.ActiveView.Id)
                       .WhereElementIsNotElementType().ToElements()
                       if el.GetTypeId() and el.GetTypeId().IntegerValue in type_ids]
            n = self._set_ids(uidoc, matched)
            self._log('Similar types: {} elements'.format(n), '*')

        except Exception as ex:
            self._log(str(ex))
    def SelectChain_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds() if doc.GetElement(i)]
            if not cur: self._log('Select seed elements first'); return
            view = doc.ActiveView
            radius = self._spacing() * 5
            all_elems = list(FilteredElementCollector(doc, view.Id)
                             .WhereElementIsNotElementType().ToElements())
            visited = set(el.Id.IntegerValue for el in cur)
            queue, result = list(cur), list(cur)
            while queue:
                el = queue.pop()
                c = self._bb_center(el, view)
                if not c: continue
                for other in all_elems:
                    if other.Id.IntegerValue in visited: continue
                    oc = self._bb_center(other, view)
                    if oc and math.sqrt((oc.X-c.X)**2+(oc.Y-c.Y)**2) < radius:
                        visited.add(other.Id.IntegerValue)
                        queue.append(other); result.append(other)
            n = self._set_ids(uidoc, result)
            self._log('Chain: {} connected elements'.format(n), '*')

        except Exception as ex:
            self._log(str(ex))
    def SelectCluster_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        all_elems = list(FilteredElementCollector(doc, view.Id)
                         .WhereElementIsNotElementType().ToElements())
        positions, idx_map = [], {}
        for el in all_elems:
            c = self._bb_center(el, view)
            if c:
                idx_map[len(positions)] = el
                positions.append(Vec2(c.X, c.Y))
        if len(positions) < 3: self._log('Need 3+ elements'); return
        try:
            clusters = DBSCAN(positions, eps=self._spacing()*4, min_pts=2).run()
        except Exception as ex:
            self._log('DBSCAN error: ' + str(ex)); return
        valid = {k: v for k, v in clusters.items() if k != -1}
        if not valid: self._log('No clusters found'); return
        cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds() if doc.GetElement(i)]
        if cur:
            cpts = [self._bb_center(el, view) for el in cur if self._bb_center(el, view)]
            if cpts:
                scx = sum(c.X for c in cpts)/len(cpts)
                scy = sum(c.Y for c in cpts)/len(cpts)
                best_k = min(valid, key=lambda k: sum(
                    math.sqrt((positions[i].x-scx)**2+(positions[i].y-scy)**2)
                    for i in valid[k]) / max(len(valid[k]), 1))
                result = [idx_map[i] for i in valid[best_k] if i in idx_map]
                n = self._set_ids(uidoc, result)
                self._log('Nearest cluster: {} elements'.format(n), '*'); return
        biggest = max(valid, key=lambda k: len(valid[k]))
        result = [idx_map[i] for i in valid[biggest] if i in idx_map]
        n = self._set_ids(uidoc, result)
        self._log('Largest cluster: {} elements'.format(n), '*')

    def SelectPattern_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds() if doc.GetElement(i)]
            if len(cur) < 3: self._log('Select 3+ elements to detect grid'); return
            view = doc.ActiveView
            pts = [self._bb_center(el, view) for el in cur if self._bb_center(el, view)]
            xs = sorted(set(round(p.X, 2) for p in pts))
            ys = sorted(set(round(p.Y, 2) for p in pts))
            dx = min((xs[i+1]-xs[i] for i in range(len(xs)-1)), default=None)
            dy = min((ys[i+1]-ys[i] for i in range(len(ys)-1)), default=None)
            if not dx and not dy: self._log('Could not detect grid spacing'); return
            step = min(dx or dy, dy or dx)
            tol = step * 0.35
            base_x = pts[0].X; base_y = pts[0].Y
            all_elems = list(FilteredElementCollector(doc, view.Id)
                             .WhereElementIsNotElementType().ToElements())
            matched = []
            for el in all_elems:
                c = self._bb_center(el, view)
                if not c: continue
                on_x = dx and (abs((c.X - base_x) % dx) < tol or abs(dx-(c.X-base_x)%dx) < tol)
                on_y = dy and (abs((c.Y - base_y) % dy) < tol or abs(dy-(c.Y-base_y)%dy) < tol)
                if on_x or on_y: matched.append(el)
            n = self._set_ids(uidoc, matched)
            self._log('Grid (Δx={:.3f} Δy={:.3f}): {} elements'.format(dx or 0, dy or 0, n), '[Align]')

        except Exception as ex:
            self._log(str(ex))
    def SelectBoundary_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            edges = ['all', 'top', 'bottom', 'left', 'right']
            picked = forms.SelectFromList.show(edges, title='Select Edge Region')
            if not picked: return
            view = doc.ActiveView
            all_pts = [(el, self._bb_center(el, view)) for el in
                       FilteredElementCollector(doc, view.Id)
                       .WhereElementIsNotElementType().ToElements()]
            all_pts = [(el, c) for el, c in all_pts if c]
            if not all_pts: return
            xs = [c.X for _, c in all_pts]; ys = [c.Y for _, c in all_pts]
            min_x, max_x = min(xs), max(xs); min_y, max_y = min(ys), max(ys)
            mx = (max_x-min_x)*0.15; my = (max_y-min_y)*0.15
            def _in(c):
                if picked == 'all':    return c.X<min_x+mx or c.X>max_x-mx or c.Y<min_y+my or c.Y>max_y-my
                if picked == 'left':   return c.X < min_x+mx
                if picked == 'right':  return c.X > max_x-mx
                if picked == 'bottom': return c.Y < min_y+my
                if picked == 'top':    return c.Y > max_y-my
            matched = [el for el, c in all_pts if _in(c)]
            n = self._set_ids(uidoc, matched)
            self._log('Boundary ({}): {} elements'.format(picked, n), '[Place]')

        except Exception as ex:
            self._log(str(ex))
    def SelectOutliers_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        all_elems = list(FilteredElementCollector(doc, view.Id)
                         .WhereElementIsNotElementType().ToElements())
        positions, idx_map = [], {}
        for el in all_elems:
            c = self._bb_center(el, view)
            if c:
                idx_map[len(positions)] = el
                positions.append(Vec2(c.X, c.Y))
        if len(positions) < 5: self._log('Need 5+ elements'); return
        try:
            clusters = DBSCAN(positions, eps=self._spacing()*4, min_pts=3).run()
            outliers = [idx_map[i] for i in clusters.get(-1, []) if i in idx_map]
        except Exception as ex:
            self._log('DBSCAN error: ' + str(ex)); return
        n = self._set_ids(uidoc, outliers)
        self._log('Outliers: {} isolated elements'.format(n), '[Find]')

    def SelectDense_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            view = doc.ActiveView
            all_elems = list(FilteredElementCollector(doc, view.Id)
                             .WhereElementIsNotElementType().ToElements())
            positions, idx_map = [], {}
            for el in all_elems:
                c = self._bb_center(el, view)
                if c:
                    idx_map[len(positions)] = el
                    positions.append(Vec2(c.X, c.Y))
            if len(positions) < 5: self._log('Need 5+ elements'); return
            r = self._spacing() * 4
            density = [(i, sum(1 for j, q in enumerate(positions)
                               if i != j and positions[i].dist(q) < r))
                       for i in range(len(positions))]
            density.sort(key=lambda x: -x[1])
            top = max(1, len(density)//4)
            result = [idx_map[i] for i, _ in density[:top] if i in idx_map]
            n = self._set_ids(uidoc, result)
            self._log('Dense (top 25%): {} elements'.format(n), '*')

        # ── Category shortcuts ─────────────────────────────────────────────────────
        except Exception as ex:
            self._log(str(ex))
    def SelLights_Click(self, s, e):    self._sel_bic('lights')
    def SelElec_Click(self, s, e):      self._sel_bic('elec')
    def SelMech_Click(self, s, e):      self._sel_bic('mech')
    def SelPlumb_Click(self, s, e):     self._sel_bic('plumb')
    def SelAir_Click(self, s, e):       self._sel_bic('air')
    def SelFurn_Click(self, s, e):      self._sel_bic('furn')
    def SelDoors_Click(self, s, e):     self._sel_bic('doors')
    def SelWindows_Click(self, s, e):   self._sel_bic('windows')
    def SelRooms_Click(self, s, e):     self._sel_bic('rooms')
    def SelPipes_Click(self, s, e):     self._sel_bic('pipes')
    def SelDucts_Click(self, s, e):     self._sel_bic('ducts')
    def SelConduit_Click(self, s, e):   self._sel_bic('conduit')
    def SelCable_Click(self, s, e):     self._sel_bic('cable')
    def SelSprinkler_Click(self, s, e): self._sel_bic('sprinkler')

    def _sel_bic(self, key):
        doc, uidoc = self._fd()
        if not doc or key not in self._BIC_MAP: return
        try:
            elems = list(FilteredElementCollector(doc, doc.ActiveView.Id)
                         .OfCategory(self._BIC_MAP[key])
                         .WhereElementIsNotElementType().ToElements())
            n = self._set_ids(uidoc, elems)
            self._log('{}: {} elements'.format(key, n))
        except Exception as ex:
            self._log('Error: ' + str(ex))

    def SelAllCats_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        cats = {}
        for el in FilteredElementCollector(doc, doc.ActiveView.Id)\
                .WhereElementIsNotElementType().ToElements():
            try:
                cat = el.Category
                if cat and cat.Name and not cat.Name.startswith('<'):
                    cats.setdefault(cat.Name, []).append(el)
            except Exception:
                pass
        if not cats: self._log('No categories found'); return
        picked = forms.SelectFromList.show(sorted(cats), title='Select Category',
                                           multiselect=False, width=380, height=480)
        if picked:
            n = self._set_ids(uidoc, cats[picked])
            self._log('{}: {} elements'.format(picked, n))

    def SelViewCats_Click(self, s, e): self.SelAllCats_Click(s, e)

    def SelGrid_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        grids = list(FilteredElementCollector(doc).OfClass(Grid).ToElements())
        if not grids: self._log('No grid lines in model'); return
        grid_xs, grid_ys = set(), set()
        for g in grids:
            try:
                p1 = g.Curve.GetEndPoint(0); p2 = g.Curve.GetEndPoint(1)
                if abs(p1.X-p2.X) < 0.01: grid_xs.add(round((p1.X+p2.X)/2, 2))
                if abs(p1.Y-p2.Y) < 0.01: grid_ys.add(round((p1.Y+p2.Y)/2, 2))
            except Exception:
                pass
        view = doc.ActiveView; tol = 0.5
        matched = []
        for el in FilteredElementCollector(doc, view.Id)\
                .WhereElementIsNotElementType().ToElements():
            c = self._bb_center(el, view)
            if c and (any(abs(c.X-gx) < tol for gx in grid_xs)
                      or any(abs(c.Y-gy) < tol for gy in grid_ys)):
                matched.append(el)
        n = self._set_ids(uidoc, matched)
        self._log('On grid lines: {} elements'.format(n))

    # ── State ─────────────────────────────────────────────────────────────────
    def SelUntagged_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        tagged_ids = set()
        for t in self._view_tags(doc, view):
            try:
                for el in t.GetTaggedLocalElements(): tagged_ids.add(el.Id.IntegerValue)
            except Exception: pass
        untagged = []
        for bic in self._MEP_BICS:
            try:
                for el in FilteredElementCollector(doc, view.Id).OfCategory(bic)\
                        .WhereElementIsNotElementType().ToElements():
                    if el.Id.IntegerValue not in tagged_ids: untagged.append(el)
            except Exception: pass
        n = self._set_ids(uidoc, untagged)
        self._log('Untagged: {} elements'.format(n), '*')

    def SelTagged_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        host_ids = set()
        for t in self._view_tags(doc, view):
            try:
                for el in t.GetTaggedLocalElements(): host_ids.add(el.Id.IntegerValue)
            except Exception: pass
        tagged = [doc.GetElement(ElementId(i)) for i in host_ids
                  if doc.GetElement(ElementId(i))]
        n = self._set_ids(uidoc, tagged)
        self._log('Tagged: {} elements'.format(n))

    def SelEmptyMark_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        empty = []
        for el in FilteredElementCollector(doc, view.Id)\
                .WhereElementIsNotElementType().ToElements():
            try:
                p = el.LookupParameter('Mark')
                if p and not p.AsString(): empty.append(el)
            except Exception: pass
        n = self._set_ids(uidoc, empty)
        self._log('Empty Mark: {} elements'.format(n))

    def SelVisible_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            view = doc.ActiveView
            visible = [el for el in FilteredElementCollector(doc, view.Id)
                       .WhereElementIsNotElementType().ToElements()
                       if self._bb_center(el, view)]
            n = self._set_ids(uidoc, visible)
            self._log('Visible (approx): {} elements'.format(n))

        # ── Spatial ───────────────────────────────────────────────────────────────
        except Exception as ex:
            self._log(str(ex))
    def SelNear_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds() if doc.GetElement(i)]
            if not cur: self._log('Select seed elements first'); return
            view = doc.ActiveView; radius = self._spacing() * 5
            seed_pts = [self._bb_center(el, view) for el in cur if self._bb_center(el, view)]
            matched = []
            for el in FilteredElementCollector(doc, view.Id)\
                    .WhereElementIsNotElementType().ToElements():
                c = self._bb_center(el, view)
                if c and any(math.sqrt((c.X-sp.X)**2+(c.Y-sp.Y)**2) < radius for sp in seed_pts):
                    matched.append(el)
            n = self._set_ids(uidoc, matched)
            self._log('Near ({:.2f}ft): {} elements'.format(radius, n))

        except Exception as ex:
            self._log(str(ex))
    def SelInRoom_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds() if doc.GetElement(i)]
        if not cur: self._log('Select element(s) first'); return
        room_names = set()
        for el in cur:
            for pname in ['Room: Name', 'Space: Name', 'Room Name']:
                try:
                    p = el.LookupParameter(pname)
                    if p and p.AsString(): room_names.add(p.AsString()); break
                except Exception: pass
        if not room_names:
            self._log('No room information on selected elements\n'
                      '(Tip: elements need Room: Name parameter)'); return
        view = doc.ActiveView; matched = []
        for el in FilteredElementCollector(doc, view.Id)\
                .WhereElementIsNotElementType().ToElements():
            for pname in ['Room: Name', 'Space: Name', 'Room Name']:
                try:
                    p = el.LookupParameter(pname)
                    if p and p.AsString() in room_names: matched.append(el); break
                except Exception: pass
        n = self._set_ids(uidoc, matched)
        self._log('In room(s) {}: {} elements'.format(
            ', '.join(list(room_names)[:3]), n))

    def SelLevel_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        cur = [doc.GetElement(i) for i in uidoc.Selection.GetElementIds() if doc.GetElement(i)]
        if not cur: self._log('Select elements first'); return
        level_ids = set()
        for el in cur:
            try:
                lid = el.LevelId
                if lid and lid != ElementId.InvalidElementId: level_ids.add(lid.IntegerValue)
            except Exception: pass
        if not level_ids: self._log('No level info on selected elements'); return
        view = doc.ActiveView; matched = []
        for el in FilteredElementCollector(doc, view.Id)\
                .WhereElementIsNotElementType().ToElements():
            try:
                if el.LevelId and el.LevelId.IntegerValue in level_ids: matched.append(el)
            except Exception: pass
        n = self._set_ids(uidoc, matched)
        self._log('Same level: {} elements'.format(n))

    def SelQuadrant_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            q = forms.SelectFromList.show(['NW','NE','SW','SE'], title='View Quadrant')
            if not q: return
            view = doc.ActiveView
            all_pts = [(el, self._bb_center(el, view)) for el in
                       FilteredElementCollector(doc, view.Id)
                       .WhereElementIsNotElementType().ToElements()]
            all_pts = [(el, c) for el, c in all_pts if c]
            if not all_pts: return
            cx = sum(c.X for _, c in all_pts)/len(all_pts)
            cy = sum(c.Y for _, c in all_pts)/len(all_pts)
            def _in(c):
                if q == 'NW': return c.X <= cx and c.Y >= cy
                if q == 'NE': return c.X >= cx and c.Y >= cy
                if q == 'SW': return c.X <= cx and c.Y <= cy
                if q == 'SE': return c.X >= cx and c.Y <= cy
            matched = [el for el, c in all_pts if _in(c)]
            n = self._set_ids(uidoc, matched)
            self._log('Quadrant {}: {} elements'.format(q, n))

        except Exception as ex:
            self._log(str(ex))
    def SelEdge_Click(self, s, e): self.SelectBoundary_Click(s, e)

    # ── Parameter shortcuts ────────────────────────────────────────────────────
    def SelByMark_Click(self, s, e):   self._sel_param_dialog('Mark')
    def SelByType_Click(self, s, e):   self._sel_param_dialog('Type Name')
    def SelByFamily_Click(self, s, e): self._sel_param_dialog('Family Name')
    def SelBySystem_Click(self, s, e): self._sel_param_dialog('System Name')

    def _sel_param_dialog(self, pname):
        doc, uidoc = self._fd()
        if not doc: return
        val = forms.ask_for_string(prompt='Contains (case-insensitive):', title=pname)
        if val is None: return
        view = doc.ActiveView; matched = []
        for el in FilteredElementCollector(doc, view.Id)\
                .WhereElementIsNotElementType().ToElements():
            try:
                p = el.LookupParameter(pname)
                v = p.AsString() if p else ''
                if v and val.lower() in v.lower(): matched.append(el)
            except Exception: pass
        n = self._set_ids(uidoc, matched)
        self._log('{} contains "{}": {} elements'.format(pname, val, n))

    # ── Enhancement 10: inline filter builder ─────────────────────────────────
    def FilterParamName_GotFocus(self, s, e):
        """Clear placeholder text on first focus."""
        try:
            if self.Cond1Param.Text == 'Parameter name':
                self.Cond1Param.Text = ''
                # (module-level import)
                self.Cond1Param.Foreground = SolidColorBrush(Colors.Black)
        except Exception:
            pass

    def FilterBuilder_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            pname = self.Cond1Param.Text.strip()
            if not pname or pname == 'Parameter name':
                self._log('Enter a parameter name in the filter field'); return
            op_item = self.Cond1Op.SelectedItem
            op = str(op_item.Content).strip() if op_item else 'contains'
            val = self.Cond1Val.Text.strip()
        except Exception as ex:
            self._log('Filter builder read error: ' + str(ex)); return

        view = doc.ActiveView; matched = []
        # (module-level import)
        for el in FilteredElementCollector(doc, view.Id)\
                .WhereElementIsNotElementType().ToElements():
            try:
                p = el.LookupParameter(pname)
                if p is None: continue
                if p.StorageType == StorageType.String:
                    v = p.AsString() or ''
                elif p.StorageType in (StorageType.Double, StorageType.Integer):
                    v = str(int(p.AsDouble())) if p.StorageType == StorageType.Double \
                        else str(p.AsInteger())
                else:
                    v = ''
                if   op == 'is empty'   and not v: matched.append(el)
                elif op == 'has value'  and v:     matched.append(el)
                elif op == 'equals'     and v.lower() == val.lower(): matched.append(el)
                elif op == 'contains'   and val.lower() in v.lower(): matched.append(el)
                elif op == 'starts with' and v.lower().startswith(val.lower()): matched.append(el)
            except Exception:
                pass
        n = self._set_ids(uidoc, matched)
        self._log('[Filter] {} {} "{}": {} elements'.format(pname, op, val, n), '▶')

    def SelCustomParam_Click(self, s, e):
        """Legacy alias — redirects to inline builder."""
        self.FilterBuilder_Click(s, e)

    # ── Selection ops ──────────────────────────────────────────────────────────
    def SelAll_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView; all_mep = []
        for bic in self._MEP_BICS:
            try:
                all_mep.extend(FilteredElementCollector(doc, view.Id)
                               .OfCategory(bic).WhereElementIsNotElementType().ToElements())
            except Exception: pass
        n = self._set_ids(uidoc, all_mep)
        self._log('All MEP in view: {} elements'.format(n))

    def SelClear_Click(self, s, e):
        _, uidoc = self._fd()
        if uidoc:
            uidoc.Selection.SetElementIds(List[ElementId]())
            self._log('Selection cleared', '⊘')

    def DeleteSelection_Click(self, s, e):
        """Delete all currently selected elements (tags or any elements)."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Nothing selected'); return
        try:
            proceed = forms.alert(
                'Delete {} selected elements?\nThis cannot be undone.'.format(len(ids)),
                ok=True, cancel=True)
        except Exception:
            proceed = True  # fallback if forms.alert fails
        if not proceed: return
        t = Transaction(doc, 'STINGTags Delete Selection'); t.Start()
        deleted = 0
        for eid in ids:
            try:
                doc.Delete(eid); deleted += 1
            except Exception: pass
        t.Commit()
        self._log('Deleted: {} / {} elements'.format(deleted, len(ids)))

    def SelInvert_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            cur_ids = set(uidoc.Selection.GetElementIds())
            inverted = [el for el in FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType().ToElements()
                        if el.Id not in cur_ids]
            n = self._set_ids(uidoc, inverted)
            self._log('Inverted: {} elements'.format(n))

        except Exception as ex:
            self._log(str(ex))
    def SelAdd_Click(self, s, e):
        _, uidoc = self._fd()
        if not uidoc: return
        cur = set(uidoc.Selection.GetElementIds())
        mem_ids = set(ElementId(i) for i in self._mem[0])
        combined = list(cur | mem_ids)
        uidoc.Selection.SetElementIds(List[ElementId](combined))
        self._log('Added M1 to selection: {} total'.format(len(combined)))

    def SelSubtract_Click(self, s, e):
        _, uidoc = self._fd()
        if not uidoc: return
        cur = set(uidoc.Selection.GetElementIds())
        mem_ids = set(ElementId(i) for i in self._mem[0])
        remaining = list(cur - mem_ids)
        uidoc.Selection.SetElementIds(List[ElementId](remaining))
        self._log('Subtracted M1: {} remaining'.format(len(remaining)))

    def SelIntersect_Click(self, s, e):
        _, uidoc = self._fd()
        if not uidoc: return
        cur = set(uidoc.Selection.GetElementIds())
        mem_ids = set(ElementId(i) for i in self._mem[0])
        intersection = list(cur & mem_ids)
        uidoc.Selection.SetElementIds(List[ElementId](intersection))
        self._log('Intersection with M1: {} elements'.format(len(intersection)))

    def SelTags_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        sel_ids = set(eid.IntegerValue for eid in uidoc.Selection.GetElementIds())
        view = doc.ActiveView; result = []
        for t in self._view_tags(doc, view):
            try:
                host_ids = set(el.Id.IntegerValue for el in t.GetTaggedLocalElements())
                if host_ids & sel_ids: result.append(t)
            except Exception: pass
        n = self._set_ids(uidoc, result)
        self._log('Tags of selection: {} tags'.format(n))

    def SelElements_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc); hosts = []
        for t in tags:
            try:
                for el in t.GetTaggedLocalElements(): hosts.append(el)
            except Exception: pass
        n = self._set_ids(uidoc, hosts)
        self._log('Host elements of selected tags: {}'.format(n))

    # ── Memory ────────────────────────────────────────────────────────────────
    def _mem_save(self, slot):
        _, uidoc = self._fd()
        if not uidoc: return
        self._mem[slot] = [i.IntegerValue for i in uidoc.Selection.GetElementIds()]
        self._log('M{} saved: {} elements'.format(slot+1, len(self._mem[slot])))

    def _mem_load(self, slot):
        doc, uidoc = self._fd()
        if not uidoc: return
        raw = self._mem[slot]
        if not raw:
            self._log('M{} is empty'.format(slot+1)); return
        # Validate IDs exist in the current document
        valid = []
        for i in raw:
            try:
                el = doc.GetElement(ElementId(i))
                if el is not None:
                    valid.append(ElementId(i))
            except Exception:
                pass
        stale = len(raw) - len(valid)
        if not valid:
            self._log('M{}: all {} IDs stale — not in this document'.format(slot+1, len(raw))); return
        uidoc.Selection.SetElementIds(List[ElementId](valid))
        msg = 'M{} loaded: {} elements'.format(slot+1, len(valid))
        if stale:
            msg += '  ({} stale IDs skipped)'.format(stale)
        self._log(msg)

    def MemSave1_Click(self, s, e): self._mem_save(0)
    def MemLoad1_Click(self, s, e): self._mem_load(0)
    def MemSave2_Click(self, s, e): self._mem_save(1)
    def MemLoad2_Click(self, s, e): self._mem_load(1)
    def MemSave3_Click(self, s, e): self._mem_save(2)
    def MemLoad3_Click(self, s, e): self._mem_load(2)

    def MemSwap_Click(self, s, e):
        self._mem[0], self._mem[1] = self._mem[1], self._mem[0]
        self._log('Swapped M1 ↔ M2 ({} / {} elements)'.format(
            len(self._mem[0]), len(self._mem[1])))

    def MemInfo_Click(self, s, e):
        self._log('MEMORY SLOTS\nM1: {} elements\nM2: {} elements\nM3: {} elements'.format(
            len(self._mem[0]), len(self._mem[1]), len(self._mem[2])), '[Save]')

    # ─────────────────────────────────────────────────────────────────────────
    # ORGANISE TAB
    # ─────────────────────────────────────────────────────────────────────────

    def _build_data(self):
        doc, uidoc = self._fd()
        if not doc: return None, None, None
        view = doc.ActiveView
        tags = self._sel_tags(doc, uidoc) or self._view_tags(doc, view)
        if len(tags) < 2:
            self._log('Need 2+ tags — select tags or ensure view has tags')
            return None, None, None
        return doc, view, self._tag_data(tags, view)

    # ── Enhancement 4: per-view undo stack ───────────────────────────────────
    def _push_undo(self, tags):
        """Snapshot current positions keyed by the active view's IntegerValue.
        Each view keeps up to 5 independent snapshots.
        """
        doc, _ = self._fd()
        if not doc:
            return
        try:
            view_key = doc.ActiveView.Id.IntegerValue
        except Exception:
            view_key = 0
        snap = {}
        for tag in tags:
            try:
                h = tag.TagHeadPosition
                snap[tag.Id.IntegerValue] = (h.X, h.Y, h.Z)
            except Exception:
                pass
        if not snap:
            return
        if view_key not in self._undo_stack:
            self._undo_stack[view_key] = []
        stack = self._undo_stack[view_key]
        stack.append(snap)
        if len(stack) > 5:
            stack.pop(0)

    def UndoOrganise_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc:
            return
        try:
            view_key = doc.ActiveView.Id.IntegerValue
        except Exception:
            view_key = 0
        stack = self._undo_stack.get(view_key, [])
        if not stack:
            self._log('Nothing to undo in this view — stack empty'); return
        snap = stack.pop()
        t = Transaction(doc, 'STINGTags Undo'); t.Start()
        restored = 0
        for id_int, (x, y, z) in snap.items():
            try:
                tag = doc.GetElement(ElementId(id_int))
                if tag:
                    tag.TagHeadPosition = XYZ(x, y, z)
                    restored += 1
            except Exception:
                pass
        t.Commit()
        self._log('Undo: restored {} tag positions  ({} steps remaining in this view)'.format(
            restored, len(stack)), '⎌')

    # ── AI Organise ───────────────────────────────────────────────────────────
    def SmartOrganize_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        tags = self._sel_tags(doc, uidoc) or self._view_tags(doc, view)
        if len(tags) < 2: self._log('Need 2+ tags'); return
        self._push_undo(tags)
        try:
            if not self._organizer or self._organizer.pass_num >= len(SmartOrganizer.PASSES):
                self._organizer = SmartOrganizer(doc, view, self._spacing(), 50)
                self._organizer.load(tags)
            t = Transaction(doc, 'STINGTags Smart Organise'); t.Start()
            result = self._organizer.run_pass()
            self._organizer.apply()
            t.Commit()
            # Enhancement 8: update pass counter badge
            try:
                total = len(SmartOrganizer.PASSES)
                current = min(self._organizer.pass_num, total)
                self.PassCountBadge.Text = '{}/{}'.format(current, total)
            except Exception:
                pass
            self._log(result, '[Brain]')
        except Exception as ex:
            import traceback as _tb
            self._log('Smart Organise error: {}\n{}'.format(
                str(ex), _tb.format_exc()[-200:]))

    def QuickFix_Click(self, s, e):
        doc, view, data = self._build_data()
        if not data: return
        self._push_undo([d['tag'] for d in data])
        try:
            t = Transaction(doc, 'STINGTags Quick Fix'); t.Start()
            ForceEngine(data, self._spacing()).run(40)
            for d in data: d['tag'].TagHeadPosition = XYZ(d['pos'].x, d['pos'].y, d['z'])
            t.Commit()
            self._log('Quick Fix: {} tags separated'.format(len(data)), '*')
        except Exception as ex:
            import traceback as _tb
            self._log('Quick Fix error: {}\n{}'.format(
                str(ex), _tb.format_exc()[-200:]))

    def DeepOptimize_Click(self, s, e):
        doc, view, data = self._build_data()
        if not data: return
        self._push_undo([d['tag'] for d in data])
        try:
            t = Transaction(doc, 'STINGTags Deep Optimise'); t.Start()
            GeneticAlg(data, self._spacing()).run()
            for d in data: d['tag'].TagHeadPosition = XYZ(d['pos'].x, d['pos'].y, d['z'])
            t.Commit()
            self._log('Deep Optimise: {} tags (genetic)'.format(len(data)), '*')
        except Exception as ex:
            import traceback as _tb
            self._log('Deep Optimise error: {}\n{}'.format(
                str(ex), _tb.format_exc()[-200:]))

    def AnnealOpt_Click(self, s, e):
        doc, view, data = self._build_data()
        if not data: return
        self._push_undo([d['tag'] for d in data])
        try:
            t = Transaction(doc, 'STINGTags Anneal'); t.Start()
            SimAnneal(data, self._spacing()).run(600)
            for d in data: d['tag'].TagHeadPosition = XYZ(d['pos'].x, d['pos'].y, d['z'])
            t.Commit()
            self._log('Annealing: {} tags'.format(len(data)), '*')
        except Exception as ex:
            import traceback as _tb
            self._log('Anneal error: {}\n{}'.format(
                str(ex), _tb.format_exc()[-200:]))

    def ResetOrganize_Click(self, s, e):
        if self._organizer:
            doc, uidoc = self._fd()
            if doc:
                try:
                    t = Transaction(doc, 'STINGTags Reset'); t.Start()
                    self._organizer.reset(); self._organizer.apply()
                    t.Commit()
                except Exception: pass
            self._organizer = None
        # Enhancement 8: reset pass badge
        try:
            self.PassCountBadge.Text = '- /8'
        except Exception:
            pass
        self._log('Positions restored to session-start state', '↩')

    def AutoSpace_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView; spacing_ft = 0.25
        try:
            scale = view.Scale
            spacing_ft = round(max(0.08, min(2.0, 0.25 * scale / 50.0)), 3)
        except Exception: pass
        tags = self._view_tags(doc, view)
        if len(tags) > 1:
            pts = [Vec2(t.TagHeadPosition.X, t.TagHeadPosition.Y) for t in tags]
            dists = sorted(pts[i].dist(pts[j])
                           for i in range(len(pts))
                           for j in range(i+1, len(pts)))
            if dists:
                median = dists[len(dists)//2]
                spacing_ft = round(max(0.08, min(2.0, median * 0.35)), 3)
        self.SpacingInput.Text = str(spacing_ft)
        # (module-level import)
        self.SpacingInput.BorderBrush = SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5))
        self._log('Auto-space: {:.3f}ft  (view 1:{}, {} tags)'.format(
            spacing_ft, view.Scale, len(tags)), '[Brain]')

    # ── Enhancement 1: grouped tag family picker ──────────────────────────────
    def RefreshTagFamilies_Click(self, s, e):
        """Scan ALL loaded annotation FamilySymbols including tag containers."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            syms = list(FilteredElementCollector(doc)
                        .OfClass(FamilySymbol).ToElements())

            _DISC_MAP = [
                ('Structural', ['Structural', 'Beam', 'Column', 'Foundation',
                                'Brace', 'Truss', 'Rebar']),
                ('Mechanical', ['Mechanical', 'Duct', 'Air Terminal', 'HVAC',
                                'Flex Duct', 'Duct Fitting', 'Duct Accessory',
                                'Duct Insulation']),
                ('Electrical', ['Electrical', 'Lighting', 'Power', 'Switch',
                                'Panel', 'Conduit', 'Cable Tray', 'Communication',
                                'Fire Alarm', 'Nurse Call', 'Security', 'Data',
                                'Telephone']),
                ('Plumbing',   ['Plumbing', 'Pipe', 'Sprinkler', 'Sanitary',
                                'Pipe Fitting', 'Pipe Accessory', 'Flex Pipe']),
                ('Architectural', ['Door', 'Window', 'Room', 'Space', 'Floor',
                                   'Wall', 'Ceiling', 'Stair', 'Roof',
                                   'Furniture', 'Generic', 'Casework',
                                   'Curtain', 'Parking', 'Area', 'Mass']),
            ]

            def _disc_of(sym):
                try:
                    cname = sym.Category.Name if sym.Category else ''
                    fname = sym.Family.Name if sym.Family else ''
                    check = (cname + ' ' + fname).lower()
                    for label, keywords in _DISC_MAP:
                        if any(k.lower() in check for k in keywords):
                            return label
                except Exception:
                    pass
                return 'Other'

            def _is_annotation_family(sym):
                """Detect any annotation family: tags, tag containers,
                keynotes, multi-category tags, symbols, callouts, etc."""
                try:
                    cat = sym.Category
                    if not cat:
                        return False
                    cname = (cat.Name or '').lower()

                    # Quick wins: name contains 'tag', 'keynote', 'symbol',
                    # 'callout', 'annotation', 'label'
                    if any(kw in cname for kw in (
                        'tag', 'keynote', 'symbol', 'annotation',
                        'label', 'callout', 'mark', 'leader'
                    )):
                        return True

                    # Check CategoryType for annotation
                    try:
                        ct_str = str(cat.CategoryType)
                        if 'annotation' in ct_str.lower():
                            return True
                    except Exception:
                        pass

                    # Check Family.FamilyCategory
                    try:
                        fam = sym.Family
                        if fam:
                            fc = fam.FamilyCategory
                            if fc:
                                fcn = (fc.Name or '').lower()
                                if 'tag' in fcn or 'annotation' in fcn:
                                    return True
                                # FamilyCategory.CategoryType
                                try:
                                    if 'annotation' in str(fc.CategoryType).lower():
                                        return True
                                except Exception:
                                    pass
                    except Exception:
                        pass

                    # Check BuiltInCategory Id range for annotation tags
                    try:
                        bic = cat.Id.IntegerValue
                        # Annotation tag categories in Revit are in these ranges
                        if -2010000 < bic < -2005000:
                            return True
                        if -2009030 < bic < -2009015:
                            return True
                        if -2008130 < bic < -2008120:
                            return True
                        # Multi-Category Tags
                        if bic == -2009030 or bic == -2009031:
                            return True
                    except Exception:
                        pass

                    # Check if the family is a "Tag" family by its FamilyPlacementType
                    try:
                        if hasattr(sym.Family, 'FamilyPlacementType'):
                            fpt_str = str(sym.Family.FamilyPlacementType)
                            if 'tag' in fpt_str.lower():
                                return True
                    except Exception:
                        pass

                except Exception:
                    pass
                return False

            groups = {}
            for sym in syms:
                try:
                    if _is_annotation_family(sym):
                        disc = _disc_of(sym)
                        try:
                            label = '{} : {}'.format(sym.Family.Name, sym.Name)
                        except Exception:
                            label = str(sym.Name)
                        groups.setdefault(disc, []).append((label, sym))
                except Exception:
                    pass

            for disc in groups:
                groups[disc].sort(key=lambda t: t[0])

            self.TagFamilyCombo.Items.Clear()
            auto_item = ComboBoxItem()
            auto_item.Content = '[Auto — Revit default]'
            auto_item.Tag = None
            self.TagFamilyCombo.Items.Add(auto_item)
            self.TagFamilyCombo.SelectedIndex = 0
            self._tag_sym_id = None

            total = 0
            group_order = ['Architectural', 'Mechanical', 'Electrical',
                           'Plumbing', 'Structural', 'Other']
            populated = []
            for disc in group_order:
                items = groups.get(disc, [])
                if not items:
                    continue
                sep = Separator()
                self.TagFamilyCombo.Items.Add(sep)
                hdr = ComboBoxItem()
                hdr.Content = '── {} ({}) ──'.format(disc.upper(), len(items))
                hdr.IsEnabled = False
                hdr.FontSize = 7
                self.TagFamilyCombo.Items.Add(hdr)
                for label, sym in items:
                    ci = ComboBoxItem()
                    ci.Content = label
                    ci.Tag = sym.Id.IntegerValue
                    self.TagFamilyCombo.Items.Add(ci)
                    total += 1
                populated.append('{} {}'.format(disc, len(items)))

            info = '  |  '.join(populated) if populated else 'No tag families'
            try:
                self.TagFamilyGroupInfo.Text = info
            except Exception:
                pass
            if total == 0:
                anno_count = 0
                for s2 in syms:
                    try:
                        ct = str(s2.Category.CategoryType) if s2.Category else ''
                        if 'annot' in ct.lower():
                            anno_count += 1
                    except Exception:
                        pass
                self._log('Tags: 0 loaded ({} symbols, {} annotation). '
                          'Ensure tag families are loaded in project.'.format(
                              len(syms), anno_count))
            else:
                self._log('Tag families: {} in {} groups'.format(
                    total, len(populated)), '↻')
        except Exception as ex:
            self._log('Refresh error: ' + str(ex))

    def _get_tag_sym_id(self):
        """Return the selected FamilySymbol ElementId, or None for [Auto]."""
        try:
            item = self.TagFamilyCombo.SelectedItem
            if item and item.Tag:
                return ElementId(int(item.Tag))
        except Exception:
            pass
        return None

    def _place_tag(self, doc, view, el, sp):
        """Place IndependentTag, then swap family if a specific one is selected."""
        c = self._bb_center(el, view)
        if not c:
            return None
        tag = None
        tag_pt = XYZ(c.X, c.Y + sp, c.Z)
        try:
            # Revit 2022+ API
            tag = IndependentTag.Create(
                doc, view.Id, Reference(el), False,
                TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal,
                tag_pt)
        except TypeError:
            try:
                # Revit 2023+ with LinkElementId
                from Autodesk.Revit.DB import LinkElementId
                tag = IndependentTag.Create(
                    doc, view.Id, Reference(el), False,
                    TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal,
                    LinkElementId(el.Id), tag_pt)
            except Exception:
                pass
        except Exception:
            pass
        sym_id = self._get_tag_sym_id()
        if tag and sym_id:
            try:
                tag.ChangeTypeId(sym_id)
            except Exception:
                pass
        return tag

    # ── Tag operations ─────────────────────────────────────────────────────────
    def TagSelected_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        elems = self._sel_elems(doc, uidoc)
        if not elems: self._log('Select non-tag elements first'); return
        sp = self._spacing()
        sym_label = ''
        try:
            item = self.TagFamilyCombo.SelectedItem
            if item and item.Tag: sym_label = '  [{}]'.format(item.Content)
        except Exception: pass
        # Build per-category fallback symbol map from loaded tag families
        _fallback_syms = {}
        try:
            # (module-level import)
            for sym in FilteredElementCollector(doc).OfClass(FamilySymbol).ToElements():
                try:
                    cat = sym.Category
                    if cat and cat.CategoryType == CategoryType.AnnotationCategory                             and 'Tag' in cat.Name and sym.IsActive:
                        _fallback_syms.setdefault(cat.Name, sym.Id)
                except Exception:
                    pass
        except Exception:
            pass
        t = Transaction(doc, 'STINGTags Tag Selected'); t.Start()
        count = 0
        for el in elems:
            try:
                tag = self._place_tag(doc, view, el, sp)
                if tag:
                    count += 1
                else:
                    # Explicit-family path failed — try category fallback
                    try:
                        cat_name = el.Category.Name + ' Tags' if el.Category else ''
                        fb_id = _fallback_syms.get(cat_name)
                        if not fb_id:
                            fb_id = next(
                                (v for k, v in _fallback_syms.items()
                                 if el.Category and el.Category.Name.lower() in k.lower()),
                                None)
                        if fb_id:
                            c = self._bb_center(el, view)
                            if c:
                                tag2 = IndependentTag.Create(
                                    doc, view.Id, Reference(el), False,
                                    TagMode.TM_ADDBY_CATEGORY,
                                    TagOrientation.Horizontal,
                                    XYZ(c.X, c.Y + sp, c.Z))
                                if tag2:
                                    try: tag2.ChangeTypeId(fb_id)
                                    except Exception: pass
                                    count += 1
                    except Exception:
                        pass
            except Exception: pass
        t.Commit()
        self._organizer = None
        # Record observations for placement history
        doc2, _ = self._fd()
        if doc2:
            try:
                view2 = doc2.ActiveView
                for el2 in elems:
                    try:
                        tg2 = next(
                            (t3 for t3 in self._view_tags(doc2, view2)
                             if any(e2.Id == el2.Id
                                    for e2 in t3.GetTaggedLocalElements())),
                            None)
                        if tg2: self._record_tag_observation(el2, tg2, view2)
                    except Exception: pass
            except Exception: pass
        self._log('Tagged: {} elements{}'.format(count, sym_label), '*')

    def TagByCategory_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        _bic_entries = [
            ('Lighting Fixtures',    'OST_LightingFixtures'),
            ('Electrical Equipment', 'OST_ElectricalEquipment'),
            ('Electrical Fixtures',  'OST_ElectricalFixtures'),
            ('Mechanical Equipment', 'OST_MechanicalEquipment'),
            ('Plumbing Fixtures',    'OST_PlumbingFixtures'),
            ('Air Terminals',        'OST_DuctTerminal'),
            ('Furniture',            'OST_Furniture'),
            ('Doors',                'OST_Doors'),
            ('Windows',              'OST_Windows'),
            ('Sprinklers',           'OST_Sprinklers'),
            ('Pipes',                'OST_PipeCurves'),
            ('Pipe Fittings',        'OST_PipeFitting'),
            ('Pipe Accessories',     'OST_PipeAccessory'),
            ('Ducts',                'OST_DuctCurves'),
            ('Duct Fittings',        'OST_DuctFitting'),
            ('Duct Accessories',     'OST_DuctAccessory'),
            ('Conduit',              'OST_Conduit'),
            ('Conduit Fittings',     'OST_ConduitFitting'),
            ('Cable Trays',          'OST_CableTray'),
            ('Cable Tray Fittings',  'OST_CableTrayFitting'),
            ('Flex Ducts',           'OST_FlexDuctCurves'),
            ('Flex Pipes',           'OST_FlexPipeCurves'),
            ('Generic Models',       'OST_GenericModel'),
            ('Specialty Equipment',  'OST_SpecialityEquipment'),
            ('Fire Alarm Devices',   'OST_FireAlarmDevices'),
            ('Communication Devices','OST_CommunicationDevices'),
            ('Rooms',                'OST_Rooms'),
            ('Walls',                'OST_Walls'),
            ('Floors',               'OST_Floors'),
            ('Ceilings',             'OST_Ceilings'),
            ('Structural Columns',   'OST_StructuralColumns'),
            ('Structural Framing',   'OST_StructuralFraming'),
        ]
        cat_map = {}
        for label, bic_name in _bic_entries:
            try:
                cat_map[label] = getattr(BuiltInCategory, bic_name)
            except AttributeError: pass
        # Filter to categories that actually have elements in the current view
        view = doc.ActiveView
        available = {}
        for label, bic in cat_map.items():
            try:
                count = FilteredElementCollector(doc, view.Id)\
                    .OfCategory(bic).WhereElementIsNotElementType()\
                    .GetElementCount()
                if count > 0:
                    available['{} ({})'.format(label, count)] = bic
            except Exception: pass
        if not available:
            self._log('No taggable categories found in view'); return
        picked = forms.SelectFromList.show(sorted(available), title='Tag By Category',
                                           button_name='Tag Selected Category')
        if not picked: return
        tagged_ids = set()
        for tag in self._view_tags(doc, view):
            try:
                for el in tag.GetTaggedLocalElements(): tagged_ids.add(el.Id.IntegerValue)
            except Exception: pass
        elems = list(FilteredElementCollector(doc, view.Id)
                     .OfCategory(available[picked])
                     .WhereElementIsNotElementType().ToElements())
        sp = self._spacing()
        t = Transaction(doc, 'STINGTags Tag Category'); t.Start()
        count = 0
        for el in elems:
            if el.Id.IntegerValue in tagged_ids: continue
            try:
                if self._place_tag(doc, view, el, sp): count += 1
            except Exception: pass
        t.Commit()
        self._organizer = None
        self._log('{}: {} new tags placed'.format(picked, count))

    def TagUntagged_Click(self, s, e):
        """Enhancement 4: live progress feedback during loop."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        tagged_ids = set()
        for tag in self._view_tags(doc, view):
            try:
                for el in tag.GetTaggedLocalElements(): tagged_ids.add(el.Id.IntegerValue)
            except Exception: pass
        to_tag = []
        for bic in self._MEP_BICS:
            try:
                for el in FilteredElementCollector(doc, view.Id).OfCategory(bic)\
                        .WhereElementIsNotElementType().ToElements():
                    if el.Id.IntegerValue not in tagged_ids: to_tag.append(el)
            except Exception: pass
        if not to_tag: self._log('All MEP elements are already tagged'); return
        sp = self._spacing(); total = len(to_tag)
        t = Transaction(doc, 'STINGTags Tag All Untagged'); t.Start()
        count = 0
        for i, el in enumerate(to_tag):
            try:
                if self._place_tag(doc, view, el, sp):
                    count += 1
            except Exception: pass
            # Progress update every 10 elements via dispatcher repaint
            if i % 10 == 0:
                msg = 'Tagging… {}/{}'.format(i+1, total)
                def _upd(m=msg): self._log(m, '*')
                try:
                    self._dispatch_ui(_upd)
                except Exception: pass
        t.Commit()
        self._organizer = None
        self._log('Tagged all untagged: {} new tags  ({} skipped)'.format(
            count, total - count), '*')

    def DeleteTags_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        if not forms.alert('Delete {} selected tags?'.format(len(tags)), ok=True, cancel=True): return
        t = Transaction(doc, 'STINGTags Delete Tags'); t.Start()
        for tag in tags:
            try: doc.Delete(tag.Id)
            except Exception: pass
        t.Commit()
        self._organizer = None
        self._log('Deleted: {} tags'.format(len(tags)), '*')

    def OrphanedTags_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView; orphaned = []
        for tag in self._view_tags(doc, view):
            try:
                if not list(tag.GetTaggedLocalElements()): orphaned.append(tag)
            except Exception: orphaned.append(tag)
        if not orphaned: self._log('No orphaned tags found'); return
        t = Transaction(doc, 'STINGTags Delete Orphaned'); t.Start()
        for tag in orphaned:
            try: doc.Delete(tag.Id)
            except Exception: pass
        t.Commit()
        self._log('Deleted {} orphaned tags'.format(len(orphaned)))

    def TagAudit_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            view = doc.ActiveView; tags = self._view_tags(doc, view)
            leaders = sum(1 for t in tags if t.HasLeader)
            horiz   = sum(1 for t in tags if t.TagOrientation == TagOrientation.Horizontal)
            sp = self._spacing()
            clashes = sum(1 for i in range(len(tags)) for j in range(i+1, len(tags))
                          if math.sqrt((tags[j].TagHeadPosition.X-tags[i].TagHeadPosition.X)**2
                                      +(tags[j].TagHeadPosition.Y-tags[i].TagHeadPosition.Y)**2) < sp)
            self._log('TAG AUDIT  —  view: {}\nTotal: {}  Leaders: {}\n'
                      'Horizontal: {}  Vertical: {}\nClashes (< {:.2f}ft): {}'.format(
                          view.Name, len(tags), leaders, horiz, len(tags)-horiz, sp, clashes), '[Chart]')

        # ── Enhancement 5: tag audit CSV export ───────────────────────────────────
        except Exception as ex:
            self._log(str(ex))
    def TagAuditExport_Click(self, s, e):
        """Write audit table to CSV: Id, Category, HasLeader, Orientation, X, Y, NN-dist."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        tags = self._view_tags(doc, view)
        if not tags:
            self._log('No tags in view — nothing to export'); return
        try:
            # Build rows
            rows = []
            pts = []
            for tag in tags:
                try:
                    h   = tag.TagHeadPosition
                    cat = ''
                    try:
                        hosts = list(tag.GetTaggedLocalElements())
                        if hosts:
                            cat = hosts[0].Category.Name if hosts[0].Category else ''
                    except Exception:
                        pass
                    rows.append({
                        'Id':          tag.Id.IntegerValue,
                        'Category':    cat,
                        'HasLeader':   '1' if tag.HasLeader else '0',
                        'Orientation': 'H' if tag.TagOrientation == TagOrientation.Horizontal else 'V',
                        'X':           round(h.X, 4),
                        'Y':           round(h.Y, 4),
                    })
                    pts.append((h.X, h.Y))
                except Exception:
                    pass

            # Nearest-neighbour distances
            for i, row in enumerate(rows):
                min_d = float('inf')
                xi, yi = pts[i]
                for j, (xj, yj) in enumerate(pts):
                    if i == j: continue
                    d = math.sqrt((xj - xi) ** 2 + (yj - yi) ** 2)
                    if d < min_d:
                        min_d = d
                row['NN_dist_ft'] = round(min_d, 4) if min_d < float('inf') else ''

            # Write CSV
            fname = 'STINGTags_Audit_{}.csv'.format(
                view.Name.replace(' ', '_').replace('/', '-'))
            out_path = os.path.join(tempfile.gettempdir(), fname)
            fields = ['Id', 'Category', 'HasLeader', 'Orientation', 'X', 'Y', 'NN_dist_ft']
            with io.open(out_path, 'w', newline='') as f:
                w = _csv.DictWriter(f, fieldnames=fields)
                w.writeheader()
                w.writerows(rows)

            self._log('Audit CSV: {} tags → {}'.format(len(rows), out_path), '[List]')
            try:
                self._open_folder(out_path)
            except Exception: pass
            except Exception:
                pass
        except Exception as ex:
            self._log('Audit export error: ' + str(ex))

    def SelectClashing_Click(self, s, e):
        """Detect overlapping tags using bounding-box intersection + spatial grid."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            view = doc.ActiveView
            tags = self._view_tags(doc, view)
            if not tags:
                self._log('No tags in active view'); return

            # Build (tag, bbox) pairs — use BoundingBoxXYZ for true overlap
            tagged_boxes = []
            for tag in tags:
                try:
                    bb = tag.get_BoundingBox(view)
                    if bb:
                        tagged_boxes.append((tag, bb.Min.X, bb.Min.Y, bb.Max.X, bb.Max.Y))
                except Exception:
                    try:  # fallback: point with spacing radius
                        h = tag.TagHeadPosition
                        sp = self._spacing() * 0.5
                        tagged_boxes.append((tag, h.X-sp, h.Y-sp, h.X+sp, h.Y+sp))
                    except Exception:
                        pass

            # Spatial grid: bucket each bbox by integer cell (cell_size = spacing)
            sp = self._spacing()
            cell = max(sp, 0.1)
            grid = {}
            for idx, (tag, x0, y0, x1, y1) in enumerate(tagged_boxes):
                cx0, cy0 = int(x0/cell)-1, int(y0/cell)-1
                cx1, cy1 = int(x1/cell)+1, int(y1/cell)+1
                for cx in range(cx0, cx1+1):
                    for cy in range(cy0, cy1+1):
                        grid.setdefault((cx, cy), []).append(idx)

            # Check only candidates sharing a grid cell
            clashing = set()
            checked = set()
            for cell_idxs in grid.values():
                for a in range(len(cell_idxs)):
                    for b in range(a+1, len(cell_idxs)):
                        ia, ib = cell_idxs[a], cell_idxs[b]
                        pair = (min(ia,ib), max(ia,ib))
                        if pair in checked: continue
                        checked.add(pair)
                        ta, xa0,ya0,xa1,ya1 = tagged_boxes[ia]
                        tb, xb0,yb0,xb1,yb1 = tagged_boxes[ib]
                        # AABB intersection test
                        if xa0 < xb1 and xa1 > xb0 and ya0 < yb1 and ya1 > yb0:
                            clashing.add(ta.Id)
                            clashing.add(tb.Id)

            if clashing:
                uidoc.Selection.SetElementIds(List[ElementId](list(clashing)))
                self._log('{} overlapping tags selected'.format(len(clashing)), '⚠')
            else:
                self._log('No overlapping tags found in active view')
        except Exception as ex:
            self._log('Clash detection error: ' + str(ex))

    # ── Orient ────────────────────────────────────────────────────────────────
    def RotateTags_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Rotate'); t.Start()
        for tag in tags:
            try:
                tag.TagOrientation = (TagOrientation.Vertical
                                      if tag.TagOrientation == TagOrientation.Horizontal
                                      else TagOrientation.Horizontal)
            except Exception: pass
        t.Commit()
        self._log('Toggled H↔V: {} tags'.format(len(tags)))

    def AllHorizontal_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        t = Transaction(doc, 'STINGTags All Horizontal'); t.Start()
        for tag in tags:
            try: tag.TagOrientation = TagOrientation.Horizontal
            except Exception: pass
        t.Commit()
        self._log('All Horizontal: {} tags'.format(len(tags)))

    def AllVertical_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        t = Transaction(doc, 'STINGTags All Vertical'); t.Start()
        for tag in tags:
            try: tag.TagOrientation = TagOrientation.Vertical
            except Exception: pass
        t.Commit()
        self._log('All Vertical: {} tags'.format(len(tags)))

    def FlipH_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView; self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Flip H'); t.Start()
        for tag in tags:
            try:
                h = tag.TagHeadPosition
                hosts = list(tag.GetTaggedLocalElements())
                c = self._bb_center(hosts[0], view) if hosts else None
                if c: tag.TagHeadPosition = XYZ(2*c.X - h.X, h.Y, h.Z)
            except Exception: pass
        t.Commit()
        self._log('Flipped H: {} tags'.format(len(tags)))

    def FlipV_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView; self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Flip V'); t.Start()
        for tag in tags:
            try:
                h = tag.TagHeadPosition
                hosts = list(tag.GetTaggedLocalElements())
                c = self._bb_center(hosts[0], view) if hosts else None
                if c: tag.TagHeadPosition = XYZ(h.X, 2*c.Y - h.Y, h.Z)
            except Exception: pass
        t.Commit()
        self._log('Flipped V: {} tags'.format(len(tags)))

    def SmartOrient_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Smart Orient'); t.Start()
        for tag in tags:
            try:
                h = tag.TagHeadPosition
                hosts = list(tag.GetTaggedLocalElements())
                c = self._bb_center(hosts[0], view) if hosts else None
                if c:
                    angle = abs(math.degrees(math.atan2(h.Y-c.Y, h.X-c.X)))
                    tag.TagOrientation = (TagOrientation.Vertical if 45<=angle<=135
                                          else TagOrientation.Horizontal)
            except Exception: pass
        t.Commit()
        self._log('Smart Orient: {} tags'.format(len(tags)), '[Brain]')

    # ── Nudge ─────────────────────────────────────────────────────────────────
    def _nudge(self, dx, dy):
        doc, uidoc = self._fd()
        if not doc: return
        sp = self._spacing()
        tags = self._sel_tags(doc, uidoc)
        if not tags:
            self._log('Select tags first')
            return
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Nudge')
        t.Start()
        try:
            moved = 0
            for tag in tags:
                try:
                    h = tag.TagHeadPosition
                    tag.TagHeadPosition = XYZ(h.X + dx * sp, h.Y + dy * sp, h.Z)
                    moved += 1
                except Exception:
                    pass
            t.Commit()
            self._log('Nudged: {} tags ({:+.3f}, {:+.3f})ft'.format(
                moved, dx * sp, dy * sp))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Nudge error: ' + str(ex))

    def NudgeUp_Click(self, s, e):    self._nudge(0,  1)
    def NudgeDown_Click(self, s, e):  self._nudge(0, -1)
    def NudgeLeft_Click(self, s, e):  self._nudge(-1, 0)
    def NudgeRight_Click(self, s, e): self._nudge(1,  0)

    def OffsetClose_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView; self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Offset Close'); t.Start()
        for tag in tags:
            try:
                hosts = list(tag.GetTaggedLocalElements())
                c = self._bb_center(hosts[0], view) if hosts else None
                if c: tag.TagHeadPosition = XYZ(c.X, c.Y+0.25, tag.TagHeadPosition.Z)
            except Exception: pass
        t.Commit()
        self._log('Offset 0.25ft: {} tags'.format(len(tags)))

    def OffsetFar_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView; self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Offset Far'); t.Start()
        for tag in tags:
            try:
                hosts = list(tag.GetTaggedLocalElements())
                c = self._bb_center(hosts[0], view) if hosts else None
                if c: tag.TagHeadPosition = XYZ(c.X, c.Y+1.0, tag.TagHeadPosition.Z)
            except Exception: pass
        t.Commit()
        self._log('Offset 1.0ft: {} tags'.format(len(tags)))

    def SmartOffset_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        all_pts = [Vec2(t.TagHeadPosition.X, t.TagHeadPosition.Y) for t in self._view_tags(doc, view)]
        sp = self._spacing(); self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Smart Offset'); t.Start()
        for tag in tags:
            try:
                hosts = list(tag.GetTaggedLocalElements())
                c = self._bb_center(hosts[0], view) if hosts else None
                if not c: continue
                hz = tag.TagHeadPosition.Z
                candidates = [XYZ(c.X+sp,c.Y,hz), XYZ(c.X-sp,c.Y,hz),
                              XYZ(c.X,c.Y+sp,hz), XYZ(c.X,c.Y-sp,hz)]
                def congestion(pos):
                    return sum(1 for p in all_pts
                               if math.sqrt((p.x-pos.X)**2+(p.y-pos.Y)**2) < sp*2)
                tag.TagHeadPosition = min(candidates, key=congestion)
            except Exception: pass
        t.Commit()
        self._log('Smart Offset: {} tags in least-congested quadrant'.format(len(tags)), '[Brain]')

    # ── Align & Distribute ─────────────────────────────────────────────────────
    def _align(self, direction):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 2:
            self._log('Select 2+ tags to align')
            return
        positions = [(tag, tag.TagHeadPosition) for tag in tags]
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Align')
        t.Start()
        try:
            if direction == 'left':
                tgt = min(p.X for _, p in positions)
                for tg, h in positions:
                    tg.TagHeadPosition = XYZ(tgt, h.Y, h.Z)
            elif direction == 'right':
                tgt = max(p.X for _, p in positions)
                for tg, h in positions:
                    tg.TagHeadPosition = XYZ(tgt, h.Y, h.Z)
            elif direction == 'top':
                tgt = max(p.Y for _, p in positions)
                for tg, h in positions:
                    tg.TagHeadPosition = XYZ(h.X, tgt, h.Z)
            elif direction == 'bottom':
                tgt = min(p.Y for _, p in positions)
                for tg, h in positions:
                    tg.TagHeadPosition = XYZ(h.X, tgt, h.Z)
            elif direction == 'centerh':
                tgt = sum(p.X for _, p in positions) / len(positions)
                for tg, h in positions:
                    tg.TagHeadPosition = XYZ(tgt, h.Y, h.Z)
            elif direction == 'centerv':
                tgt = sum(p.Y for _, p in positions) / len(positions)
                for tg, h in positions:
                    tg.TagHeadPosition = XYZ(h.X, tgt, h.Z)
            t.Commit()
            self._log('Aligned {}: {} tags'.format(direction, len(tags)))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Align error: ' + str(ex))

    def AlignLeft_Click(self, s, e):    self._align('left')
    def AlignRight_Click(self, s, e):   self._align('right')
    def AlignTop_Click(self, s, e):     self._align('top')
    def AlignBottom_Click(self, s, e):  self._align('bottom')
    def AlignCenterH_Click(self, s, e): self._align('centerh')
    def AlignCenterV_Click(self, s, e): self._align('centerv')

    def DistributeH_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 3:
            self._log('Select 3+ tags to distribute')
            return
        try:
            st = sorted(tags, key=lambda t: t.TagHeadPosition.X)
            x1 = st[0].TagHeadPosition.X
            x2 = st[-1].TagHeadPosition.X
            step = (x2 - x1) / max(1, len(st) - 1)
            self._push_undo(tags)
            t = Transaction(doc, 'STINGTags Distribute H')
            t.Start()
            for i, tag in enumerate(st):
                h = tag.TagHeadPosition
                tag.TagHeadPosition = XYZ(x1 + step * i, h.Y, h.Z)
            t.Commit()
            self._log('Distribute H: {} tags, {:.2f}ft spread'.format(
                len(tags), x2 - x1))
        except Exception as ex:
            self._log('Distribute H error: ' + str(ex))

    def DistributeV_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 3:
            self._log('Select 3+ tags to distribute')
            return
        try:
            st = sorted(tags, key=lambda t: t.TagHeadPosition.Y)
            y1 = st[0].TagHeadPosition.Y
            y2 = st[-1].TagHeadPosition.Y
            step = (y2 - y1) / max(1, len(st) - 1)
            self._push_undo(tags)
            t = Transaction(doc, 'STINGTags Distribute V')
            t.Start()
            for i, tag in enumerate(st):
                h = tag.TagHeadPosition
                tag.TagHeadPosition = XYZ(h.X, y1 + step * i, h.Z)
            t.Commit()
            self._log('Distribute V: {} tags, {:.2f}ft spread'.format(
                len(tags), y2 - y1))
        except Exception as ex:
            self._log('Distribute V error: ' + str(ex))

    def DistFixedH_Click(self, s, e):
        """Distribute horizontally with fixed pitch from the spacing slider."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 2:
            self._log('Select 2+ tags')
            return
        try:
            sp = self._spacing()
            st = sorted(tags, key=lambda t: t.TagHeadPosition.X)
            base_x = st[0].TagHeadPosition.X
            self._push_undo(tags)
            t = Transaction(doc, 'STINGTags Dist Fixed H')
            t.Start()
            for i, tag in enumerate(st):
                h = tag.TagHeadPosition
                tag.TagHeadPosition = XYZ(base_x + i * sp, h.Y, h.Z)
            t.Commit()
            self._log('Fixed H: {} tags, {:.3f}ft pitch'.format(len(tags), sp))
        except Exception as ex:
            self._log('Dist fixed H error: ' + str(ex))

    def DistFixedV_Click(self, s, e):
        """Distribute vertically with fixed pitch from the spacing slider."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 2:
            self._log('Select 2+ tags')
            return
        try:
            sp = self._spacing()
            st = sorted(tags, key=lambda t: -t.TagHeadPosition.Y)
            base_y = st[0].TagHeadPosition.Y
            self._push_undo(tags)
            t = Transaction(doc, 'STINGTags Dist Fixed V')
            t.Start()
            for i, tag in enumerate(st):
                h = tag.TagHeadPosition
                tag.TagHeadPosition = XYZ(h.X, base_y - i * sp, h.Z)
            t.Commit()
            self._log('Fixed V: {} tags, {:.3f}ft pitch'.format(len(tags), sp))
        except Exception as ex:
            self._log('Dist fixed V error: ' + str(ex))
    def SmartAlign_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            tags = self._sel_tags(doc, uidoc)
            if len(tags) < 3: self._log('Select 3+ tags'); return
            sp = self._spacing(); rows = {}
            for tag in tags:
                h = tag.TagHeadPosition
                bucket = round(round(h.Y/sp)*sp, 6)
                rows.setdefault(bucket, []).append((tag, h))
            self._push_undo(tags)
            t = Transaction(doc, 'STINGTags Smart Align'); t.Start()
            aligned = 0
            for members in rows.values():
                if len(members) > 1:
                    avg_y = sum(h.Y for _,h in members)/len(members)
                    for tag, h in members:
                        tag.TagHeadPosition = XYZ(h.X, avg_y, h.Z); aligned += 1
            t.Commit()
            self._log('Smart Align: {} tags across {} rows'.format(
                aligned, sum(1 for m in rows.values() if len(m)>1)), '[Brain]')

        except Exception as ex:
            self._log(str(ex))
    def ToGrid_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            tags = self._sel_tags(doc, uidoc)
            if len(tags) < 4: self._log('Select 4+ tags'); return
            cols = int(math.ceil(math.sqrt(len(tags))))
            rows_count = int(math.ceil(len(tags)/cols))
            positions = [(tag, tag.TagHeadPosition) for tag in tags]
            min_x = min(p.X for _,p in positions); max_x = max(p.X for _,p in positions)
            min_y = min(p.Y for _,p in positions); max_y = max(p.Y for _,p in positions)
            step_x = (max_x-min_x)/max(1,cols-1)
            step_y = (max_y-min_y)/max(1,rows_count-1) if rows_count>1 else step_x
            self._push_undo(tags)
            t = Transaction(doc, 'STINGTags To Grid'); t.Start()
            for i, (tag, pos) in enumerate(positions):
                tag.TagHeadPosition = XYZ(min_x+(i%cols)*step_x, max_y-(i//cols)*step_y, pos.Z)
            t.Commit()
            self._log('Grid {}×{}: {} tags'.format(cols, rows_count, len(tags)))

        except Exception as ex:
            self._log(str(ex))
    def ToCircle_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            tags = self._sel_tags(doc, uidoc)
            if len(tags) < 3: self._log('Select 3+ tags'); return
            hp = [tag.TagHeadPosition for tag in tags]
            cx = sum(p.X for p in hp)/len(hp); cy = sum(p.Y for p in hp)/len(hp)
            radius = max((math.sqrt((p.X-cx)**2+(p.Y-cy)**2) for p in hp), default=self._spacing()*2)
            if radius < 0.01: radius = self._spacing()*2
            self._push_undo(tags)
            t = Transaction(doc, 'STINGTags To Circle'); t.Start()
            for i, tag in enumerate(tags):
                angle = 2*math.pi*i/len(tags); h = tag.TagHeadPosition
                tag.TagHeadPosition = XYZ(cx+radius*math.cos(angle), cy+radius*math.sin(angle), h.Z)
            t.Commit()
            self._log('Circle: {} tags (r={:.2f}ft)'.format(len(tags), radius))

        except Exception as ex:
            self._log(str(ex))
    def ToStack_Click(self, s, e):
        """Stack tags vertically (aligned X, stepped Y)."""
        self._stack_tags('V')

    def ToStackH_Click(self, s, e):
        """Stack tags horizontally (aligned Y, stepped X)."""
        self._stack_tags('H')

    def _stack_tags(self, direction='V'):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 2:
            self._log('Select 2+ tags')
            return
        sp = self._spacing()
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Stack {}'.format(direction))
        t.Start()
        try:
            if direction == 'V':
                sorted_tags = sorted(tags, key=lambda tg: -tg.TagHeadPosition.Y)
                base_x = sorted_tags[0].TagHeadPosition.X
                base_y = sorted_tags[0].TagHeadPosition.Y
                for i, tag in enumerate(sorted_tags):
                    h = tag.TagHeadPosition
                    tag.TagHeadPosition = XYZ(base_x, base_y - i * sp, h.Z)
            else:
                sorted_tags = sorted(tags, key=lambda tg: tg.TagHeadPosition.X)
                base_x = sorted_tags[0].TagHeadPosition.X
                base_y = sorted_tags[0].TagHeadPosition.Y
                for i, tag in enumerate(sorted_tags):
                    h = tag.TagHeadPosition
                    tag.TagHeadPosition = XYZ(base_x + i * sp, base_y, h.Z)
            t.Commit()
            self._log('Stacked {}: {} tags @ {:.3f}ft'.format(
                direction, len(tags), sp))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Stack error: ' + str(ex))

    def MirrorPattern_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        hp = [tag.TagHeadPosition for tag in tags]
        cx = sum(p.X for p in hp)/len(hp); self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Mirror'); t.Start()
        for tag in tags:
            try:
                h = tag.TagHeadPosition; tag.TagHeadPosition = XYZ(2*cx-h.X, h.Y, h.Z)
            except Exception: pass
        t.Commit()
        self._log('Mirrored: {} tags (axis x={:.2f}ft)'.format(len(tags), cx))

    def ToRadial_Click(self, s, e): self.ToCircle_Click(s, e)

    # ── Leaders ───────────────────────────────────────────────────────────────

    # ─── ADD / REMOVE ─────────────────────────────────────────────────────────

    def AddLeaders_Click(self, s, e):
        """Enable leader on selected tags."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags:
            tags = self._view_tags(doc, doc.ActiveView)
            if not tags: self._log('No tags selected or in view'); return
        t = Transaction(doc, 'STINGTags Add Leaders')
        t.Start()
        try:
            added = 0
            for tag in tags:
                try:
                    if not tag.HasLeader:
                        tag.HasLeader = True
                        added += 1
                except Exception:
                    pass
            t.Commit()
            self._log('Leaders added: {}/{}'.format(added, len(tags)))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def RemoveLeaders_Click(self, s, e):
        """Remove leader from selected tags."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        t = Transaction(doc, 'STINGTags Remove Leaders')
        t.Start()
        try:
            removed = 0
            for tag in tags:
                try:
                    if tag.HasLeader:
                        tag.HasLeader = False
                        removed += 1
                except Exception:
                    pass
            t.Commit()
            self._log('Leaders removed: {}/{}'.format(removed, len(tags)))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def AddMultiLeader_Click(self, s, e):
        """Add additional leader by duplicating tag at an offset position.
        Creates a second tag for the same host element, positioned on the
        opposite side, giving the visual effect of multiple leaders."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        sp = self._spacing()
        t = Transaction(doc, 'STINGTags Multi-Leader')
        t.Start()
        try:
            added = 0
            new_ids = []
            for tag in tags:
                try:
                    # Get the tagged element
                    host = None
                    try:
                        hosts = list(tag.GetTaggedLocalElements())
                        if hosts:
                            host = hosts[0]
                    except Exception:
                        pass
                    if host is None:
                        try:
                            host = tag.GetTaggedLocalElement()
                        except Exception:
                            pass
                    if host is None:
                        continue
                    # Calculate offset position (opposite side from current)
                    h = tag.TagHeadPosition
                    c = _get_host_center(tag, view)
                    if c:
                        # Place new tag on opposite side of host
                        dx = h.X - c.X
                        dy = h.Y - c.Y
                        new_pt = XYZ(c.X - dx, c.Y - dy, h.Z)
                    else:
                        new_pt = XYZ(h.X + sp, h.Y, h.Z)
                    # Create duplicate tag
                    new_tag = None
                    ref = Reference(host)
                    try:
                        new_tag = IndependentTag.Create(
                            doc, view.Id, ref, False,
                            TagMode.TM_ADDBY_CATEGORY,
                            TagOrientation.Horizontal, new_pt)
                    except TypeError:
                        try:
                            from Autodesk.Revit.DB import LinkElementId
                            new_tag = IndependentTag.Create(
                                doc, view.Id, ref, False,
                                TagMode.TM_ADDBY_CATEGORY,
                                TagOrientation.Horizontal,
                                LinkElementId(host.Id), new_pt)
                        except Exception:
                            pass
                    if new_tag:
                        new_tag.HasLeader = True
                        # Match tag type
                        try:
                            new_tag.ChangeTypeId(tag.GetTypeId())
                        except Exception:
                            pass
                        new_ids.append(new_tag.Id)
                        added += 1
                except Exception:
                    pass
            t.Commit()
            if new_ids:
                try:
                    uidoc.Selection.SetElementIds(
                        List[ElementId]([i for i in new_ids]))
                except Exception:
                    pass
            self._log('Multi-leader: {} duplicate tags created'.format(added))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def CombineLeaders_Click(self, s, e):
        """Remove duplicate tags for the same host element.
        Groups selected tags by their tagged host, keeps the tag closest
        to the host center, deletes the rest."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 2: self._log('Select 2+ tags to combine'); return
        view = doc.ActiveView

        def _host_id(tag):
            """Get the element ID of the tagged host."""
            try:
                hosts = list(tag.GetTaggedLocalElements())
                if hosts:
                    return hosts[0].Id.IntegerValue
            except Exception:
                pass
            try:
                h = tag.GetTaggedLocalElement()
                if h:
                    return h.Id.IntegerValue
            except Exception:
                pass
            return None

        # Group by tagged element
        by_host = {}
        for tag in tags:
            hid = _host_id(tag)
            if hid is not None:
                if hid not in by_host:
                    by_host[hid] = []
                by_host[hid].append(tag)

        # Count how many groups have duplicates
        dup_groups = {k: v for k, v in by_host.items() if len(v) > 1}
        if not dup_groups:
            self._log('No duplicate tags found ({} tags, {} unique hosts)'.format(
                len(tags), len(by_host)))
            return

        t = Transaction(doc, 'STINGTags Combine Leaders')
        t.Start()
        try:
            deleted = 0
            for hid, tag_list in dup_groups.items():
                # Keep the tag closest to host center
                best = tag_list[0]
                best_dist = float('inf')
                for tg in tag_list:
                    try:
                        c = _get_host_center(tg, view)
                        if c:
                            h = tg.TagHeadPosition
                            d = math.sqrt((h.X - c.X)**2 + (h.Y - c.Y)**2)
                            if d < best_dist:
                                best_dist = d
                                best = tg
                    except Exception:
                        pass
                # Enable leader on the keeper
                try:
                    if not best.HasLeader:
                        best.HasLeader = True
                except Exception:
                    pass
                # Delete the rest
                for tg in tag_list:
                    if tg.Id.IntegerValue == best.Id.IntegerValue:
                        continue
                    try:
                        doc.Delete(tg.Id)
                        deleted += 1
                    except Exception:
                        pass
            t.Commit()
            self._log('Combined: {} duplicates removed across {} host groups'.format(
                deleted, len(dup_groups)))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    # ─── ELBOW ────────────────────────────────────────────────────────────────

    def CreateLeaderElbow_Click(self, s, e):
        """Create a 45-degree elbow bend between tag head and host element."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Create Elbows')
        t.Start()
        try:
            created = 0
            first_err = None
            for tag in tags:
                try:
                    if not tag.HasLeader:
                        tag.HasLeader = True
                    c = _get_host_center(tag, view)
                    if not c:
                        if first_err is None:
                            first_err = 'No host found'
                        continue
                    h = tag.TagHeadPosition
                    dx = h.X - c.X
                    dy = h.Y - c.Y
                    dist = math.sqrt(dx * dx + dy * dy)
                    if dist < 0.01:
                        if first_err is None:
                            first_err = 'Tag too close to host'
                        continue
                    mx = (h.X + c.X) / 2.0
                    my = (h.Y + c.Y) / 2.0
                    perp = dist * 0.25
                    elbow_pt = XYZ(
                        mx + (-dy / dist) * perp,
                        my + (dx / dist) * perp, h.Z)
                    if _set_elbow(tag, elbow_pt):
                        created += 1
                    elif first_err is None:
                        first_err = 'SetElbow failed'
                except Exception as ex2:
                    if first_err is None:
                        first_err = str(ex2)
            t.Commit()
            if created > 0:
                self._log('Elbows: {}/{}'.format(created, len(tags)))
            else:
                # Run diagnostic probe on first tag
                diag = self._probe_leader_api(tags[0], view)
                self._log('Elbows: 0/{} | {} | {}'.format(
                    len(tags), first_err or '?', diag))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def _probe_leader_api(self, tag, view):
        """Test which leader API methods work on this tag. Returns diagnostic string."""
        doc = tag.Document
        parts = []
        # Test host resolution (read-only, no transaction needed)
        try:
            hosts = list(tag.GetTaggedLocalElements())
            parts.append('Host:Y({})'.format(len(hosts)))
        except Exception as ex:
            parts.append('Host:N({})'.format(type(ex).__name__))
            try:
                h = tag.GetTaggedLocalElement()
                parts.append('HostLegacy:{}'.format('Y' if h else 'N'))
            except Exception:
                parts.append('HostLegacy:N')
        # Test Reference-based API
        first_ref = None
        try:
            refs = tag.GetTaggedReferences()
            ref_count = 0
            for r in refs:
                ref_count += 1
                if first_ref is None:
                    first_ref = r
            parts.append('Refs:Y({})'.format(ref_count))
        except Exception as ex:
            parts.append('Refs:N({})'.format(type(ex).__name__))
        # Test elbow get (read-only)
        if first_ref:
            try:
                tag.GetLeaderElbow(first_ref)
                parts.append('GetElbow:Y')
            except Exception as ex:
                parts.append('GetElbow:N({})'.format(type(ex).__name__))
        try:
            tag.LeaderElbow
            parts.append('LegacyGet:Y')
        except Exception as ex:
            parts.append('LegacyGet:N({})'.format(type(ex).__name__))
        # Test elbow set (needs transaction, rolled back)
        tp = Transaction(doc, 'STINGTags Probe')
        tp.Start()
        try:
            test_pt = tag.TagHeadPosition
            if first_ref:
                try:
                    tag.SetLeaderElbow(first_ref, test_pt)
                    parts.append('SetElbow:Y')
                except Exception as ex:
                    parts.append('SetElbow:N({})'.format(type(ex).__name__))
            try:
                tag.LeaderElbow = test_pt
                parts.append('LegacySet:Y')
            except Exception as ex:
                parts.append('LegacySet:N({})'.format(type(ex).__name__))
        except Exception:
            pass
        tp.RollBack()
        return ' '.join(parts)

    def MakeLeaderStraight_Click(self, s, e):
        """Remove elbow bend from leaders, making them straight lines."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = [tg for tg in self._sel_tags(doc, uidoc) if tg.HasLeader]
        if not tags: self._log('Select tags with leaders'); return
        view = doc.ActiveView
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Straight Leaders')
        t.Start()
        try:
            straightened = 0
            first_err = None
            for tag in tags:
                try:
                    h = tag.TagHeadPosition
                    end_pt = _get_host_center(tag, view)
                    if end_pt is None:
                        end_pt = _get_leader_end(tag)
                    if end_pt is None:
                        if first_err is None:
                            first_err = 'No host/end for tag {}'.format(
                                tag.Id.IntegerValue)
                        continue
                    frac = 1.0 / 3.0
                    straight_pt = XYZ(
                        h.X + (end_pt.X - h.X) * frac,
                        h.Y + (end_pt.Y - h.Y) * frac, h.Z)
                    if _set_elbow(tag, straight_pt):
                        straightened += 1
                    elif first_err is None:
                        first_err = 'SetElbow failed tag {}'.format(
                            tag.Id.IntegerValue)
                except Exception as ex2:
                    if first_err is None:
                        first_err = str(ex2)
            t.Commit()
            msg = 'Straightened: {}/{}'.format(straightened, len(tags))
            if first_err and straightened == 0:
                msg += ' | ' + first_err
            self._log(msg)
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def FlipElbow_Click(self, s, e):
        """Mirror elbow to the opposite side of the leader line."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = [tg for tg in self._sel_tags(doc, uidoc) if tg.HasLeader]
        if not tags: self._log('Select tags with leaders'); return
        view = doc.ActiveView
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Flip Elbows')
        t.Start()
        try:
            flipped = 0
            first_err = None
            for tag in tags:
                try:
                    h = tag.TagHeadPosition
                    end_pt = _get_host_center(tag, view)
                    if end_pt is None:
                        if first_err is None:
                            first_err = 'No host for tag {}'.format(
                                tag.Id.IntegerValue)
                        continue
                    elbow = _get_elbow(tag)
                    if elbow is None:
                        elbow = _create_elbow_at_midpoint(tag, view, 0.25)
                    if elbow is None:
                        if first_err is None:
                            first_err = 'Cannot create elbow tag {}'.format(
                                tag.Id.IntegerValue)
                        continue
                    lx = end_pt.X - h.X
                    ly = end_pt.Y - h.Y
                    line_len_sq = lx * lx + ly * ly
                    if line_len_sq < 1e-9:
                        continue
                    ex = elbow.X - h.X
                    ey = elbow.Y - h.Y
                    dot = (ex * lx + ey * ly) / line_len_sq
                    proj_x = h.X + dot * lx
                    proj_y = h.Y + dot * ly
                    new_elbow = XYZ(
                        2.0 * proj_x - elbow.X,
                        2.0 * proj_y - elbow.Y,
                        elbow.Z)
                    if _set_elbow(tag, new_elbow):
                        flipped += 1
                    elif first_err is None:
                        first_err = 'SetElbow failed tag {}'.format(
                            tag.Id.IntegerValue)
                except Exception as ex2:
                    if first_err is None:
                        first_err = str(ex2)
            t.Commit()
            msg = 'Flipped: {}/{}'.format(flipped, len(tags))
            if first_err and flipped == 0:
                msg += ' | ' + first_err
            self._log(msg)
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    # ─── SNAP POSITION ────────────────────────────────────────────────────────

    def _snap_angle(self, snap_deg):
        """Snap tag head position to nearest angle increment from host center."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        snap_rad = math.radians(snap_deg)
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Snap {}deg'.format(snap_deg))
        t.Start()
        try:
            count = 0
            no_host = 0
            first_err = None
            for tag in tags:
                try:
                    h = tag.TagHeadPosition
                    c = _get_host_center(tag, view)
                    if not c:
                        no_host += 1
                        continue
                    dx = h.X - c.X
                    dy = h.Y - c.Y
                    dist = math.sqrt(dx * dx + dy * dy)
                    if dist < 0.001:
                        continue
                    angle = math.atan2(dy, dx)
                    snapped = round(angle / snap_rad) * snap_rad
                    new_pos = XYZ(
                        c.X + dist * math.cos(snapped),
                        c.Y + dist * math.sin(snapped),
                        h.Z)
                    tag.TagHeadPosition = new_pos
                    count += 1
                except Exception as ex2:
                    if first_err is None:
                        first_err = str(ex2)
            t.Commit()
            msg = 'Snap {}°: {}/{}'.format(snap_deg, count, len(tags))
            if no_host:
                msg += ' ({} no host)'.format(no_host)
            if first_err and count == 0:
                msg += ' | ' + first_err
            self._log(msg)
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def Snap45_Click(self, s, e):
        self._snap_angle(45)

    def Snap90_Click(self, s, e):
        self._snap_angle(90)

    def _leader_snap_elbow(self, angle_mode):
        """Set leader elbow to a proper geometric angle bend.

        90° mode: Creates a right-angle bend (horizontal from tag, vertical
                  to host, or vice versa) — like an L-shaped leader.
        45° mode: Creates a 45° diagonal elbow at the midpoint between
                  tag head and host center.
        0° mode:  Makes the leader straight (removes elbow).

        Each mode can be applied on top of the other — click 90° to get
        right-angle, then 45° to get diagonal, then Straight to remove."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        tags = self._sel_tags(doc, uidoc)
        if not tags:
            self._log('Select tags first')
            return
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Elbow {}deg'.format(angle_mode))
        t.Start()
        try:
            ok = 0
            no_host = 0
            first_err = None
            for tag in tags:
                try:
                    if not tag.HasLeader:
                        tag.HasLeader = True
                    h = tag.TagHeadPosition
                    c = _get_host_center(tag, view)
                    if not c:
                        no_host += 1
                        continue
                    dx = c.X - h.X
                    dy = c.Y - h.Y
                    dist = math.sqrt(dx * dx + dy * dy)
                    if dist < 0.01:
                        continue

                    if angle_mode == 0:
                        # STRAIGHT: place elbow on the line between tag and host
                        mid = XYZ(h.X + dx * 0.5, h.Y + dy * 0.5, h.Z)
                        if _set_elbow(tag, mid):
                            ok += 1
                        elif first_err is None:
                            first_err = 'SetElbow failed (straight)'
                    elif angle_mode == 90:
                        # 90° ELBOW: L-shaped bend
                        # Two options: horizontal-then-vertical or vertical-then-horizontal
                        # Pick the one that forms a cleaner L (prefer the one
                        # where the horizontal segment is at the tag end)
                        opt_a = XYZ(c.X, h.Y, h.Z)  # horizontal from tag, vertical to host
                        opt_b = XYZ(h.X, c.Y, h.Z)  # vertical from tag, horizontal to host
                        # Use whichever keeps the elbow closer to a current elbow (if any)
                        cur_elbow = _get_elbow(tag)
                        if cur_elbow:
                            da = math.sqrt((cur_elbow.X - opt_a.X) ** 2 +
                                           (cur_elbow.Y - opt_a.Y) ** 2)
                            db = math.sqrt((cur_elbow.X - opt_b.X) ** 2 +
                                           (cur_elbow.Y - opt_b.Y) ** 2)
                            elbow_pt = opt_a if da <= db else opt_b
                        else:
                            # Default: horizontal from tag (most common in drawings)
                            elbow_pt = opt_a
                        if _set_elbow(tag, elbow_pt):
                            ok += 1
                        elif first_err is None:
                            first_err = 'SetElbow failed (90°)'
                    elif angle_mode == 45:
                        # 45° ELBOW: elbow on diagonal midpoint offset
                        # Place elbow so the leader makes two equal-angle segments
                        angle = math.atan2(dy, dx)
                        # Snap to nearest 45° direction
                        snap = math.pi / 4
                        snapped_a = round(angle / snap) * snap
                        # Elbow at 60% of distance from tag toward host
                        # along the snapped angle direction
                        r = dist * 0.6
                        elbow_pt = XYZ(
                            h.X + r * math.cos(snapped_a),
                            h.Y + r * math.sin(snapped_a),
                            h.Z)
                        if _set_elbow(tag, elbow_pt):
                            ok += 1
                        elif first_err is None:
                            first_err = 'SetElbow failed (45°)'
                except Exception as ex2:
                    if first_err is None:
                        first_err = str(ex2)
            t.Commit()
            if angle_mode == 0:
                label = 'Straight'
            else:
                label = '{}° elbow'.format(angle_mode)
            msg = '{}: {}/{}'.format(label, ok, len(tags))
            if no_host:
                msg += ' ({} no host)'.format(no_host)
            if first_err and ok == 0:
                msg += ' | ' + first_err
            self._log(msg)
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Elbow error: ' + str(ex))

    def LeaderSnapElbow45_Click(self, s, e):
        self._leader_snap_elbow(45)

    def LeaderSnapElbow90_Click(self, s, e):
        self._leader_snap_elbow(90)

    def LeaderElbowStraight_Click(self, s, e):
        """Make leaders straight (remove elbow bend)."""
        self._leader_snap_elbow(0)

    # ─── LENGTH ───────────────────────────────────────────────────────────────

    def _set_leader_len(self, length):
        """Move tag head to a fixed distance from host element center,
        preserving the current direction."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Leader Length')
        t.Start()
        try:
            count = 0
            for tag in tags:
                try:
                    if not tag.HasLeader:
                        tag.HasLeader = True
                    h = tag.TagHeadPosition
                    c = _get_host_center(tag, view)
                    if not c:
                        continue
                    dx = h.X - c.X
                    dy = h.Y - c.Y
                    dist = math.sqrt(dx * dx + dy * dy)
                    if dist < 0.001:
                        # Tag is on host, push right
                        dx = 1.0
                        dy = 0.0
                        dist = 1.0
                    scale = length / dist
                    tag.TagHeadPosition = XYZ(
                        c.X + dx * scale,
                        c.Y + dy * scale,
                        h.Z)
                    count += 1
                except Exception:
                    pass
            t.Commit()
            self._log('Leader {:.2f}ft: {}/{}'.format(length, count, len(tags)))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def LeaderShort_Click(self, s, e):
        self._set_leader_len(0.25)

    def LeaderMedium_Click(self, s, e):
        self._set_leader_len(0.5)

    def LeaderLong_Click(self, s, e):
        self._set_leader_len(1.0)

    def MinimizeLeaders_Click(self, s, e):
        self._set_leader_len(self._spacing() * 0.8)

    def EqualiseLeaders_Click(self, s, e):
        """Make all selected leaders the same length (average of current lengths)."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = [tg for tg in self._sel_tags(doc, uidoc)]
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        # Calculate average leader length
        lengths = []
        for tag in tags:
            try:
                h = tag.TagHeadPosition
                c = _get_host_center(tag, view)
                if c:
                    d = math.sqrt((h.X - c.X) ** 2 + (h.Y - c.Y) ** 2)
                    lengths.append(d)
            except Exception:
                pass
        if not lengths:
            self._log('No valid leaders found'); return
        avg_len = sum(lengths) / len(lengths)
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Equalise Leaders')
        t.Start()
        try:
            count = 0
            for tag in tags:
                try:
                    if not tag.HasLeader:
                        tag.HasLeader = True
                    h = tag.TagHeadPosition
                    c = _get_host_center(tag, view)
                    if not c:
                        continue
                    dx = h.X - c.X
                    dy = h.Y - c.Y
                    dist = math.sqrt(dx * dx + dy * dy)
                    if dist < 0.001:
                        dx = 1.0
                        dy = 0.0
                        dist = 1.0
                    scale = avg_len / dist
                    tag.TagHeadPosition = XYZ(
                        c.X + dx * scale,
                        c.Y + dy * scale,
                        h.Z)
                    count += 1
                except Exception:
                    pass
            t.Commit()
            self._log('Equalised: {}/{} to {:.2f}ft'.format(count, len(tags), avg_len))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    # ─── AI / AUTO ────────────────────────────────────────────────────────────

    def SmartLeader_Click(self, s, e):
        """AI: place each tag in the least-congested 45-degree direction
        from its host element, with optimal leader length and elbow."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        # Get all existing tag positions in view for density check
        all_pts = []
        for vt in self._view_tags(doc, view):
            try:
                p = vt.TagHeadPosition
                all_pts.append((p.X, p.Y))
            except Exception:
                pass
        sp = self._spacing()
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Smart Leader')
        t.Start()
        try:
            placed = 0
            for tag in tags:
                try:
                    c = _get_host_center(tag, view)
                    if not c:
                        continue
                    hz = tag.TagHeadPosition.Z
                    best_pos = None
                    best_density = 999999
                    # Test 8 compass directions at spacing distance
                    for deg in range(0, 360, 45):
                        rad = math.radians(deg)
                        nx = c.X + sp * math.cos(rad)
                        ny = c.Y + sp * math.sin(rad)
                        # Count how many tags are near this candidate position
                        density = 0
                        for ax, ay in all_pts:
                            d = math.sqrt((ax - nx) ** 2 + (ay - ny) ** 2)
                            if d < sp * 1.5:
                                density += 1
                        if density < best_density:
                            best_density = density
                            best_pos = XYZ(nx, ny, hz)
                    if best_pos:
                        tag.HasLeader = True
                        tag.TagHeadPosition = best_pos
                        # Update density map
                        all_pts.append((best_pos.X, best_pos.Y))
                        # Create a clean elbow
                        _create_elbow_at_midpoint(tag, view, 0.2)
                        placed += 1
                except Exception:
                    pass
            t.Commit()
            self._log('Smart: {}/{} placed optimally'.format(placed, len(tags)))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def UncrossLeaders_Click(self, s, e):
        """Swap tag head positions to minimize leader line crossings."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = [tg for tg in self._sel_tags(doc, uidoc) if tg.HasLeader]
        if len(tags) < 2:
            self._log('Select 2+ tags with leaders'); return
        view = doc.ActiveView
        # Build data: tag position + host position
        data = []
        for tag in tags:
            try:
                c = _get_host_center(tag, view)
                if c:
                    h = tag.TagHeadPosition
                    data.append({
                        'tag': tag,
                        'hx': h.X, 'hy': h.Y, 'hz': h.Z,
                        'ex': c.X, 'ey': c.Y
                    })
            except Exception:
                pass
        if len(data) < 2:
            self._log('Need 2+ tags with resolvable hosts'); return

        def _cross(ax, ay, bx, by, cx, cy, dx, dy):
            """Test if line segment AB intersects line segment CD."""
            def _side(ox, oy, px, py, qx, qy):
                return (px - ox) * (qy - oy) - (py - oy) * (qx - ox)
            d1 = _side(cx, cy, dx, dy, ax, ay)
            d2 = _side(cx, cy, dx, dy, bx, by)
            d3 = _side(ax, ay, bx, by, cx, cy)
            d4 = _side(ax, ay, bx, by, dx, dy)
            if ((d1 > 0 and d2 < 0) or (d1 < 0 and d2 > 0)):
                if ((d3 > 0 and d4 < 0) or (d3 < 0 and d4 > 0)):
                    return True
            return False

        # Iteratively swap tag positions to reduce crossings
        swaps = 0
        for _round in range(len(data) * 5):
            improved = False
            for i in range(len(data)):
                for j in range(i + 1, len(data)):
                    a = data[i]
                    b = data[j]
                    # Check if current assignment crosses
                    if _cross(a['hx'], a['hy'], a['ex'], a['ey'],
                              b['hx'], b['hy'], b['ex'], b['ey']):
                        # Swap tag head positions
                        a['hx'], b['hx'] = b['hx'], a['hx']
                        a['hy'], b['hy'] = b['hy'], a['hy']
                        a['hz'], b['hz'] = b['hz'], a['hz']
                        swaps += 1
                        improved = True
            if not improved:
                break

        self._push_undo([d['tag'] for d in data])
        t = Transaction(doc, 'STINGTags Uncross Leaders')
        t.Start()
        try:
            for d in data:
                try:
                    d['tag'].TagHeadPosition = XYZ(d['hx'], d['hy'], d['hz'])
                except Exception:
                    pass
            t.Commit()
            self._log('Uncrossed: {} swaps across {} tags'.format(swaps, len(data)))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    def TidyLeaders_Click(self, s, e):
        """One-click tidy: straighten leaders, equalise lengths, then uncross.
        Runs three operations in sequence within a single transaction."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        view = doc.ActiveView
        self._push_undo(tags)
        t = Transaction(doc, 'STINGTags Tidy Leaders')
        t.Start()
        try:
            # Step 1: Enable leaders on all tags
            for tag in tags:
                try:
                    if not tag.HasLeader:
                        tag.HasLeader = True
                except Exception:
                    pass

            # Step 2: Straighten all elbows (remove kinks)
            straightened = 0
            for tag in tags:
                try:
                    h = tag.TagHeadPosition
                    end_pt = _get_leader_end(tag)
                    if end_pt is None:
                        end_pt = _get_host_center(tag, view)
                    if end_pt is None:
                        continue
                    frac = 1.0 / 3.0
                    sp = XYZ(h.X + (end_pt.X - h.X) * frac,
                             h.Y + (end_pt.Y - h.Y) * frac, h.Z)
                    if _set_elbow(tag, sp):
                        straightened += 1
                except Exception:
                    pass

            # Step 3: Equalise lengths
            lengths = []
            for tag in tags:
                try:
                    h = tag.TagHeadPosition
                    c = _get_host_center(tag, view)
                    if c:
                        lengths.append(math.sqrt(
                            (h.X - c.X) ** 2 + (h.Y - c.Y) ** 2))
                except Exception:
                    pass
            equalised = 0
            if lengths:
                avg_len = sum(lengths) / len(lengths)
                for tag in tags:
                    try:
                        h = tag.TagHeadPosition
                        c = _get_host_center(tag, view)
                        if not c:
                            continue
                        dx = h.X - c.X
                        dy = h.Y - c.Y
                        dist = math.sqrt(dx * dx + dy * dy)
                        if dist < 0.001:
                            dx = 1.0
                            dy = 0.0
                            dist = 1.0
                        scale = avg_len / dist
                        tag.TagHeadPosition = XYZ(
                            c.X + dx * scale,
                            c.Y + dy * scale, h.Z)
                        equalised += 1
                    except Exception:
                        pass

            # Step 4: Uncross (swap positions to remove crossings)
            leader_tags = [tg for tg in tags if tg.HasLeader]
            data = []
            for tag in leader_tags:
                try:
                    c = _get_host_center(tag, view)
                    if c:
                        h = tag.TagHeadPosition
                        data.append({
                            'tag': tag,
                            'hx': h.X, 'hy': h.Y, 'hz': h.Z,
                            'ex': c.X, 'ey': c.Y
                        })
                except Exception:
                    pass

            swaps = 0
            if len(data) >= 2:
                def _cross_check(a, b):
                    def _s(ox, oy, px, py, qx, qy):
                        return (px - ox) * (qy - oy) - (py - oy) * (qx - ox)
                    d1 = _s(a['ex'], a['ey'], b['ex'], b['ey'], a['hx'], a['hy'])
                    d2 = _s(a['ex'], a['ey'], b['ex'], b['ey'], b['hx'], b['hy'])
                    d3 = _s(a['hx'], a['hy'], b['hx'], b['hy'], a['ex'], a['ey'])
                    d4 = _s(a['hx'], a['hy'], b['hx'], b['hy'], b['ex'], b['ey'])
                    return (((d1 > 0 and d2 < 0) or (d1 < 0 and d2 > 0)) and
                            ((d3 > 0 and d4 < 0) or (d3 < 0 and d4 > 0)))

                for _ in range(len(data) * 3):
                    improved = False
                    for i in range(len(data)):
                        for j in range(i + 1, len(data)):
                            if _cross_check(data[i], data[j]):
                                data[i]['hx'], data[j]['hx'] = data[j]['hx'], data[i]['hx']
                                data[i]['hy'], data[j]['hy'] = data[j]['hy'], data[i]['hy']
                                data[i]['hz'], data[j]['hz'] = data[j]['hz'], data[i]['hz']
                                swaps += 1
                                improved = True
                    if not improved:
                        break

                for d in data:
                    try:
                        d['tag'].TagHeadPosition = XYZ(d['hx'], d['hy'], d['hz'])
                    except Exception:
                        pass

            t.Commit()
            self._log('Tidy: {} straightened, {} equalised ({:.2f}ft), {} uncross swaps'.format(
                straightened, equalised,
                avg_len if lengths else 0, swaps))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Error: ' + str(ex))

    # ── Analyse ───────────────────────────────────────────────────────────────
    def LayoutScore_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView; tags = self._view_tags(doc, view)
        if len(tags) < 2: self._log('Need 2+ tags'); return
        sp = self._spacing()
        hp = [Vec2(t.TagHeadPosition.X, t.TagHeadPosition.Y) for t in tags]
        overlaps = sum(1 for i in range(len(hp)) for j in range(i+1, len(hp))
                       if hp[i].dist(hp[j]) < sp)
        overlap_score = max(0, 100-overlaps*5)
        leader_tags = [t for t in tags if t.HasLeader]; ldr_score = 100
        if leader_tags:
            ldr_total = 0
            for tag in leader_tags:
                try:
                    hosts = list(tag.GetTaggedLocalElements())
                    c = self._bb_center(hosts[0], view) if hosts else None
                    if c:
                        h = tag.TagHeadPosition
                        ldr_total += math.sqrt((h.X-c.X)**2+(h.Y-c.Y)**2)
                except Exception: pass
            avg_ldr = ldr_total/len(leader_tags)
            ldr_score = max(0, 100-max(0, avg_ldr-sp)*20)
        composite = int((overlap_score+ldr_score)/2)
        self._log('LAYOUT SCORE: {}/100\nOverlap: {}/100  Leader: {}/100\n'
                  'Tags: {}  Clashes: {}  Leaders: {}'.format(
                      composite, int(overlap_score), int(ldr_score),
                      len(tags), overlaps, len(leader_tags)), '[Chart]')

    def CheckClashes_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            view = doc.ActiveView; tags = self._view_tags(doc, view); sp = self._spacing()
            if not tags: self._log('No tags in view'); return
            clashes = sum(1 for i in range(len(tags)) for j in range(i+1, len(tags))
                          if math.sqrt((tags[j].TagHeadPosition.X-tags[i].TagHeadPosition.X)**2
                                      +(tags[j].TagHeadPosition.Y-tags[i].TagHeadPosition.Y)**2) < sp)
            self._log('Clashes: {} pairs (d < {:.2f}ft)\nTotal: {} tags'.format(
                clashes, sp, len(tags)))
        except Exception as ex:
            self._log('Clash check error: ' + str(ex))

    def LeaderClash_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        leader_tags = [t for t in self._view_tags(doc, view) if t.HasLeader]
        self._log('Leaders in view: {}\nUse Uncross to resolve crossings'.format(len(leader_tags)))

    def DensityMap_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            view = doc.ActiveView; tags = self._view_tags(doc, view)
            if not tags: self._log('No tags in view'); return
            hp = [t.TagHeadPosition for t in tags]
            cx = sum(p.X for p in hp)/len(hp); cy = sum(p.Y for p in hp)/len(hp)
            q = [0,0,0,0]
            for p in hp:
                if   p.X<cx and p.Y>=cy: q[0]+=1
                elif p.X>=cx and p.Y>=cy: q[1]+=1
                elif p.X<cx:              q[2]+=1
                else:                     q[3]+=1
            self._log('DENSITY MAP  —  {} tags\nNW:{:>4}  NE:{:>4}\nSW:{:>4}  SE:{:>4}'.format(
                len(tags), q[0], q[1], q[2], q[3]), '*')
        except Exception as ex:
            self._log('Density map error: ' + str(ex))

    def ShowClusters_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView; tags = self._view_tags(doc, view)
        if len(tags) < 2: self._log('Need 2+ tags'); return
        positions = [Vec2(t.TagHeadPosition.X, t.TagHeadPosition.Y) for t in tags]
        try:
            k = min(5, len(positions)//2)
            centroids, clusters = KMeans(positions).run(k)
            lines = ['K-MEANS (k={})\n'.format(k)]
            for i, c in enumerate(clusters):
                if c:
                    lines.append('Cluster {}: {} tags  ({:.2f}, {:.2f})'.format(
                        i+1, len(c), centroids[i].x, centroids[i].y))
            self._log('\n'.join(lines), '*')
        except Exception as ex:
            self._log('Cluster error: ' + str(ex))

    # ── Pattern ───────────────────────────────────────────────────────────────
    def LearnPattern_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if len(tags) < 2: self._log('Select 2+ tags to learn from'); return
        view = doc.ActiveView; data = self._tag_data(tags, view)
        try: self._log('LEARNED: ' + str(self._pattern.learn(data)), '*')
        except Exception as ex: self._log('Learn error: ' + str(ex))

    def ApplyPattern_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        if not self._pattern: self._log('No pattern learned yet'); return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select target tags first'); return
        view = doc.ActiveView; data = self._tag_data(tags, view)
        self._push_undo(tags)
        try:
            t = Transaction(doc, 'STINGTags Apply Pattern'); t.Start()
            msg = self._pattern.apply(data)
            for d in data:
                try: d['tag'].TagHeadPosition = XYZ(d['pos'].x, d['pos'].y, d['z'])
                except Exception: pass
            t.Commit()
            self._log('APPLIED: ' + str(msg), '✨')
        except Exception as ex:
            self._log('Apply error: ' + str(ex))

    # ── Enhancement 8: batch sheet processing ─────────────────────────────────
    def BatchSheets_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        if not forms.alert(
            'Run Smart Organise (pass 1) and ISO completeness check on EVERY '
            'sheet viewport?\n\nThis cannot be undone. Save the project first.',
            title='Batch View Processing', ok=True, cancel=True):
            return
        sheets = list(FilteredElementCollector(doc).OfClass(ViewSheet).ToElements())
        if not sheets: self._log('No sheets found in project'); return
        sp = self._spacing()
        results = []; errors = []; views_done = 0
        for sheet in sheets:
            try:
                vp_ids = sheet.GetAllViewports()
                for vp_id in vp_ids:
                    vp = doc.GetElement(vp_id)
                    if not vp: continue
                    view = doc.GetElement(vp.ViewId)
                    if not view: continue
                    tags = self._view_tags(doc, view)
                    if len(tags) < 2: continue
                    # Smart Organise pass
                    try:
                        org = SmartOrganizer(doc, view, sp, 50)
                        org.load(tags)
                        t = Transaction(doc, 'STINGTags Batch — {}'.format(view.Name))
                        t.Start()
                        org.run_pass(); org.apply()
                        t.Commit()
                    except Exception as ex_org:
                        try: t.RollBack()
                        except Exception: pass
                        errors.append('{}: {}'.format(view.Name, str(ex_org)[:40]))
                        continue
                    # ISO completeness check (no transaction needed)
                    total_p = len(self._ISO_PARAMS)
                    if self._ISO_LIBS:
                        elements = []
                        for bic in self._MEP_BICS:
                            try:
                                elements.extend(
                                    FilteredElementCollector(doc, view.Id)
                                    .OfCategory(bic)
                                    .WhereElementIsNotElementType().ToElements())
                            except Exception:
                                pass
                        if elements:
                            filled = sum(1 for el in elements
                                         for p in self._ISO_PARAMS
                                         if self._iso_get(el, p))
                            pct = int(100*filled/(len(elements)*total_p)) if elements else 0
                            results.append('{}: {} tags  ISO {}%'.format(
                                view.Name, len(tags), pct))
                        else:
                            results.append('{}: {} tags  (no ISO elements)'.format(
                                view.Name, len(tags)))
                    else:
                        results.append('{}: {} tags'.format(view.Name, len(tags)))
                    views_done += 1
                    def _progress(n=views_done, sn=sheet.SheetNumber):
                        self._log('Batch: sheet {} — {} views done'.format(sn, n), '▶▶')
                    try: self._dispatch_ui(_progress)
                    except Exception: pass
            except Exception as ex_sheet:
                errors.append('Sheet {}: {}'.format(sheet.SheetNumber, str(ex_sheet)[:40]))
        summary = 'BATCH COMPLETE\n{} sheets  |  {} views processed\n'.format(
            len(sheets), views_done)
        summary += '\n'.join(results[:15])
        if len(results) > 15:
            summary += '\n… and {} more views'.format(len(results)-15)
        if errors:
            summary += '\n\nERRORS:\n' + '\n'.join(errors[:5])
        self._log(summary, '*')

    # ─────────────────────────────────────────────────────────────────────────
    # CREATE TAB  (ISO 19650)
    # ─────────────────────────────────────────────────────────────────────────

    def _iso_update_ui(self):
        try:
            scope_lbl = {'view':'View','selection':'Sel','project':'Proj'}[self._iso_scope]
            self.IsoScopeBtn.Content     = 'Scope: ' + scope_lbl
            self.IsoOverwriteBtn.Content = 'Overwrite: ' + ('Yes' if self._iso_overwrite else 'No')
            self.IsoStatus.Text = 'Scope: {}  |  Overwrite: {}'.format(
                scope_lbl, 'Yes' if self._iso_overwrite else 'No')
        except Exception: pass

    def IsoToggleScope_Click(self, s, e):
        cycle = ['view', 'selection', 'project']
        try:
            idx = cycle.index(self._iso_scope)
        except ValueError:
            idx = 0   # recover if _iso_scope ever holds an unexpected value
        self._iso_scope = cycle[(idx + 1) % len(cycle)]
        self._iso_update_ui()

    def IsoToggleOverwrite_Click(self, s, e):
        self._iso_overwrite = not self._iso_overwrite
        self._iso_update_ui()

    def _iso_collect(self, doc, uidoc):
        view = doc.ActiveView
        if self._ISO_LIBS:
            disc_map = getattr(self._TC, 'DISC_MAP', {})
            def _cat_name(el):
                try: return el.Category.Name if el.Category else ''
                except Exception: return ''
            if self._iso_scope == 'project':
                return [(el, _cat_name(el)) for el in self._TL.iter_taggable(doc, self._TC)]
            coll = (uidoc.Selection.GetElementIds() if self._iso_scope == 'selection'
                    else FilteredElementCollector(doc, view.Id)
                         .WhereElementIsNotElementType().ToElementIds())
            return [(doc.GetElement(eid), _cat_name(doc.GetElement(eid)))
                    for eid in coll
                    if doc.GetElement(eid) and
                    _cat_name(doc.GetElement(eid)) in disc_map]
        result = []
        for bic in self._MEP_BICS:
            try:
                coll = (FilteredElementCollector(doc) if self._iso_scope == 'project'
                        else FilteredElementCollector(doc, view.Id))
                for el in coll.OfCategory(bic).WhereElementIsNotElementType().ToElements():
                    cat = el.Category
                    result.append((el, cat.Name if cat else ''))
            except Exception: pass
        if self._iso_scope == 'selection':
            sel_ids = set(eid.IntegerValue for eid in uidoc.Selection.GetElementIds())
            result = [(el,c) for el,c in result if el.Id.IntegerValue in sel_ids]
        return result

    def IsoLoadParams_Click(self, s, e):
        """Bind all 13 ISO 19650 shared parameters to the Revit project.

        Workflow:
        1. Locate a shared parameter .txt file in lib/ or prompt the user.
        2. Set it as the active shared parameter file for the application.
        3. For each missing ISO param, create a project parameter binding
           scoped to ALL model categories (instance parameter, text type).
        """
        doc, uidoc = self._fd()
        if not doc: return
        try:
            app = uidoc.Application.Application

            # ── Step 1: find the shared parameter file ──────────────────────
            sp_path = None
            # Priority 1: data/MR_PARAMETERS.txt (shipped with extension)
            for candidate_name in ['MR_PARAMETERS.txt', 'ISO19650_SharedParams.txt']:
                candidate = os.path.join(_data, candidate_name)
                if os.path.exists(candidate):
                    sp_path = candidate
                    break
            # Priority 2: lib/ folder
            if not sp_path:
                candidate = os.path.join(_lib, 'ISO19650_SharedParams.txt')
                if os.path.exists(candidate):
                    sp_path = candidate
            # Priority 3: app's current shared param file
            if not sp_path:
                try:
                    cur = app.SharedParametersFilename
                    if cur and os.path.exists(cur):
                        sp_path = cur
                except Exception:
                    pass
            # Priority 4: ask user only as last resort
            if not sp_path:
                try:
                    sp_path = forms.pick_file(
                        file_ext='txt',
                        title='Select ISO 19650 Shared Parameter File (.txt)')
                except Exception:
                    sp_path = None
                if not sp_path:
                    self._iso_log('Cancelled — no shared parameter file selected'); return

            # ── Step 2: set the shared parameter file ───────────────────────
            prev_path = ''
            try:
                prev_path = app.SharedParametersFilename or ''
                app.SharedParametersFilename = sp_path
            except Exception as ex:
                self._iso_log('Cannot set shared parameter file: ' + str(ex)); return

            try:
                sp_file = app.OpenSharedParameterFile()
            except Exception as ex:
                self._iso_log('Cannot open shared parameter file: ' + str(ex)); return
            if not sp_file:
                self._iso_log('Shared parameter file is empty or unreadable'); return

            # ── Step 3: collect existing project parameter names ────────────
            existing = set()
            try:
                bm = doc.ParameterBindings
                it = bm.ForwardIterator()
                while it.MoveNext():
                    existing.add(it.Key.Name)
            except Exception:
                pass

            # ── Step 4: bind missing params ─────────────────────────────────
            # Build category set: all model categories that support instance params
            cat_set = CategorySet()
            try:
                for cat in doc.Settings.Categories:
                    try:
                        if cat.AllowsBoundParameters:
                            cat_set.Insert(cat)
                    except Exception:
                        pass
            except Exception:
                pass

            # (module-level import)
            binding = doc.Application.Create.NewInstanceBinding(cat_set)

            bound  = []
            errors = []
            all_params = self._ISO_PARAMS + [self._ISO_TAG_FIELD]

            t = Transaction(doc, 'STINGTags Bind ISO 19650 Params'); t.Start()
            try:
                for pname in all_params:
                    if pname in existing:
                        continue
                    # Search all groups in the shared param file
                    ext_def = None
                    for group in sp_file.Groups:
                        try:
                            dfn = group.Definitions.get_Item(pname)
                            if dfn:
                                ext_def = dfn
                                break
                        except Exception:
                            pass

                    if not ext_def:
                        # Create the definition in a STINGTags group
                        try:
                            grp = sp_file.Groups.get_Item('STINGTags_ISO19650')
                            if not grp:
                                grp = sp_file.Groups.Create('STINGTags_ISO19650')
                            if _HAS_PARAMETER_TYPE:
                                opts = ExternalDefinitionCreationOptions(
                                    pname, ParameterType.Text)
                            else:
                                opts = ExternalDefinitionCreationOptions(
                                    pname, SpecTypeId.String.Text)
                            opts.Visible = True
                            ext_def = grp.Definitions.Create(opts)
                        except Exception as ex:
                            errors.append('{}: {}'.format(pname, str(ex)[:40]))
                            continue

                    try:
                        if _HAS_PARAMETER_TYPE:
                            doc.ParameterBindings.Insert(
                                ext_def, binding,
                                BuiltInParameterGroup.PG_IDENTITY_DATA)
                        else:
                            doc.ParameterBindings.Insert(
                                ext_def, binding,
                                GroupTypeId.IdentityData)
                        bound.append(pname)
                    except Exception as ex:
                        errors.append('{}: {}'.format(pname, str(ex)[:40]))

                t.Commit()
            except Exception as ex:
                t.RollBack()
                self._iso_log('Binding transaction failed: ' + str(ex)); return

            # Restore previous shared param file path if we changed it
            if prev_path and prev_path != sp_path:
                try: app.SharedParametersFilename = prev_path
                except Exception: pass

            # ── Report ───────────────────────────────────────────────────────
            already = len(all_params) - len(bound) - len(errors)
            msg = 'ISO 19650 params bound.\n'
            if bound:   msg += '  New:     {}\n'.format(', '.join(bound))
            if already: msg += '  Already: {} params existed\n'.format(already)
            if errors:  msg += '  Errors:  {}\n'.format('; '.join(errors[:3]))
            self._iso_log(msg.strip(), '📎')

        except Exception as ex:
            self._iso_log('IsoLoadParams error: ' + str(ex))

    def IsoProjectConfig_Click(self, s, e):
        if not self._ISO_LIBS: self._iso_log('ISO libs not loaded'); return
        config_path = os.path.join(_cfg, 'project_config.json')

        # Load existing config
        existing = {}
        if os.path.exists(config_path):
            try:
                with io.open(config_path, encoding='utf-8') as f:
                    existing = json.load(f)
            except Exception:
                pass

        # Define editable fields with defaults
        fields = [
            ('PROJECT',   'ASS_PROJECT_COD_TXT',    existing.get('PROJECT', '')),
            ('ORIG',      'ASS_ORIGINATOR_COD_TXT', existing.get('ORIG', '')),
            ('VOL',       'ASS_VOLUME_COD_TXT',     existing.get('VOL', '')),
            ('LOC_CODES', '(comma-separated)',       ', '.join(existing.get('LOC_CODES', ['BLD1', 'EXT', 'XX']))),
            ('ZONE_CODES','(comma-separated)',       ', '.join(existing.get('ZONE_CODES', ['Z01', 'Z02', 'ZZ']))),
            ('DEF_STATUS','Default status code',     existing.get('DEF_STATUS', 'S0')),
            ('DEF_REV',   'Default revision',        existing.get('DEF_REV', 'P01')),
        ]

        # Build summary for display
        summary = 'ISO 19650 PROJECT CONFIGURATION\n'
        summary += '=' * 44 + '\n\n'
        for label, desc, val in fields:
            summary += '  {:12s}  {:20s}  {}\n'.format(label, desc, val or '(empty)')
        summary += '\nClick OK to edit values, Cancel to close.'

        try:
            proceed = forms.alert(summary, title='Project Config',
                                  ok=True, cancel=True)
        except Exception:
            proceed = False
        if not proceed:
            return

        # Ask for each value
        changed = False
        for label, desc, cur_val in fields:
            try:
                new_val = forms.ask_for_string(
                    prompt='{} [current: {}]'.format(label, cur_val or '(empty)'),
                    title='Set ' + label,
                    default=cur_val or '')
                if new_val is None:
                    continue  # user cancelled this field
                if label in ('LOC_CODES', 'ZONE_CODES'):
                    existing[label] = [c.strip() for c in new_val.split(',') if c.strip()]
                else:
                    existing[label] = new_val.strip()
                changed = True
            except Exception:
                pass

        # Merge in default maps from tag_config if not already present
        if 'DISC_MAP' not in existing:
            existing['DISC_MAP'] = getattr(self._TC, 'DISC_MAP', {})
        if 'SYS_MAP' not in existing:
            existing['SYS_MAP'] = getattr(self._TC, 'SYS_MAP', {})
        if 'PROD_MAP' not in existing:
            existing['PROD_MAP'] = getattr(self._TC, 'PROD_MAP', {})
        if 'FUNC_MAP' not in existing:
            existing['FUNC_MAP'] = getattr(self._TC, 'FUNC_MAP', {})

        # Save
        if changed:
            try:
                if not os.path.exists(_cfg):
                    os.makedirs(_cfg)
                with io.open(config_path, 'w', encoding='utf-8') as f:
                    json.dump(existing, f, indent=2, ensure_ascii=False)
                self._iso_log('Project config saved:\n' + config_path, '*')
            except Exception as ex:
                self._iso_log('Save error: ' + str(ex))
        else:
            self._iso_log('No changes made')

    def IsoAutoPopulate_Click(self, s, e):
        """Enhancement 4: populate ALL 13 ISO tokens + live progress."""
        doc, uidoc = self._fd()
        if not doc: return
        if not self._ISO_LIBS:
            self._iso_log('ISO libs not loaded (tag_config.py / tag_logic.py missing)'); return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No taggable elements in scope'); return

        # Load project constants from config
        config_path = os.path.join(_cfg, 'project_config.json')
        proj_cfg = {}
        if os.path.exists(config_path):
            try:
                with io.open(config_path, encoding='utf-8') as f:
                    proj_cfg = json.load(f)
            except Exception:
                pass

        proj_code = proj_cfg.get('PROJECT', '')
        orig_code = proj_cfg.get('ORIG', '')
        vol_code  = proj_cfg.get('VOL', '')
        def_status = proj_cfg.get('DEF_STATUS', 'S0')
        def_rev    = proj_cfg.get('DEF_REV', 'P01')

        counts = dict(proj=0, orig=0, vol=0, disc=0, lvl=0, loc=0,
                      zone=0, sys=0, func=0, prod=0, seq=0, status=0, rev=0)
        total = len(elements)
        t = Transaction(doc, 'STINGTags ISO AutoPopulate'); t.Start()
        try:
            for i, (el, cat_name) in enumerate(elements):
                # Token 1: PROJECT (from config)
                if proj_code and self._iso_set(el, 'ASS_PROJECT_COD_TXT', proj_code, self._iso_overwrite):
                    counts['proj'] += 1
                # Token 2: ORIGINATOR (from config)
                if orig_code and self._iso_set(el, 'ASS_ORIGINATOR_COD_TXT', orig_code, self._iso_overwrite):
                    counts['orig'] += 1
                # Token 3: VOLUME (from config)
                if vol_code and self._iso_set(el, 'ASS_VOLUME_COD_TXT', vol_code, self._iso_overwrite):
                    counts['vol'] += 1
                # Token 4: LEVEL
                lvl_c = ''
                if hasattr(self._TL, 'get_level_code'):
                    lvl_c = self._TL.get_level_code(doc, el)
                if not lvl_c:
                    try:
                        lid = el.LevelId
                        if lid and lid != ElementId.InvalidElementId:
                            lv = doc.GetElement(lid)
                            if lv:
                                nm = lv.Name.strip()
                                digits = ''.join(c for c in nm if c.isdigit())
                                lvl_c = ('L' + digits.zfill(2) if digits
                                         else nm.upper().replace(' ', '')[:4])
                    except Exception:
                        pass
                if lvl_c and self._iso_set(el, 'ASS_LVL_COD_TXT', lvl_c, self._iso_overwrite):
                    counts['lvl'] += 1
                # Token 5: DISCIPLINE
                disc = getattr(self._TC, 'DISC_MAP', {}).get(cat_name, '')
                if disc and self._iso_set(el, 'ASS_DISCIPLINE_COD_TXT', disc, self._iso_overwrite):
                    counts['disc'] += 1
                # Token 6: LOCATION (spatial inference from Room)
                if not self._iso_get(el, 'ASS_LOC_TXT') or self._iso_overwrite:
                    loc_code = self._infer_location(doc, el, proj_cfg.get('LOC_CODES', []))
                    if loc_code and self._iso_set(el, 'ASS_LOC_TXT', loc_code, self._iso_overwrite):
                        counts['loc'] += 1
                # Token 7: ZONE (spatial inference from Room)
                if not self._iso_get(el, 'ASS_ZONE_TXT') or self._iso_overwrite:
                    zone_code = self._infer_zone(doc, el, proj_cfg.get('ZONE_CODES', []))
                    if zone_code and self._iso_set(el, 'ASS_ZONE_TXT', zone_code, self._iso_overwrite):
                        counts['zone'] += 1
                # Token 8: SYSTEM
                sys_c = ''
                if hasattr(self._TL, 'get_sys_code'):
                    sys_c = self._TL.get_sys_code(cat_name, self._TC.SYS_MAP)
                if not sys_c:
                    sys_c = self._infer_system(el)
                if sys_c and self._iso_set(el, 'ASS_SYSTEM_TYPE_TXT', sys_c, self._iso_overwrite):
                    counts['sys'] += 1
                # Token 9: FUNCTION
                func_c = ''
                if hasattr(self._TL, 'get_func_code'):
                    func_c = self._TL.get_func_code(sys_c, self._TC.FUNC_MAP)
                if not func_c:
                    func_c = self._infer_function(el, sys_c)
                if func_c and self._iso_set(el, 'ASS_FUNC_TXT', func_c, self._iso_overwrite):
                    counts['func'] += 1
                # Token 10: PRODUCT
                prod_c = getattr(self._TC, 'PROD_MAP', {}).get(cat_name, '')
                if not prod_c:
                    prod_c = self._infer_product(el, cat_name)
                if prod_c and self._iso_set(el, 'ASS_PRODCT_COD_TXT', prod_c, self._iso_overwrite):
                    counts['prod'] += 1
                # Token 12: STATUS (default)
                if def_status and self._iso_set(el, 'ASS_STATUS_COD_TXT', def_status, self._iso_overwrite):
                    counts['status'] += 1
                # Token 13: REV (default)
                if def_rev and self._iso_set(el, 'ASS_REV_COD_TXT', def_rev, self._iso_overwrite):
                    counts['rev'] += 1
                # Progress
                if i % 20 == 0:
                    msg = 'Populating… {}/{}'.format(i + 1, total)
                    def _upd(m=msg): self._log(m, '▶')
                    try: self._dispatch_ui(_upd)
                    except Exception: pass
            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('AutoPopulate error: ' + str(ex)); return
        self._iso_log(
            'AUTO POPULATE — {} elements\n'
            'PROJ:{proj}  ORIG:{orig}  VOL:{vol}  LVL:{lvl}\n'
            'DISC:{disc}  LOC:{loc}  ZONE:{zone}  SYS:{sys}\n'
            'FUNC:{func}  PROD:{prod}  STATUS:{status}  REV:{rev}'.format(
                total, **counts), '▶')
        # Auto-refresh dashboard
        try: self._refresh_dashboard(doc, uidoc)
        except Exception: pass

    def _infer_location(self, doc, el, loc_codes):
        """Infer LOC from element's room or first LOC code in config."""
        try:
            # Try to get room at element location
            pt = None
            try:
                bb = el.get_BoundingBox(None)
                if bb:
                    pt = XYZ((bb.Min.X + bb.Max.X) / 2,
                             (bb.Min.Y + bb.Max.Y) / 2,
                             (bb.Min.Z + bb.Max.Z) / 2)
            except Exception:
                try:
                    loc = el.Location
                    if hasattr(loc, 'Point'):
                        pt = loc.Point
                except Exception:
                    pass
            if pt:
                try:
                    room = doc.GetRoomAtPoint(pt)
                    if room and room.Name:
                        # Extract location prefix from room name
                        rn = room.Name.upper()
                        for code in loc_codes:
                            if code.upper() in rn:
                                return code
                except Exception:
                    pass
            # Fallback: first LOC code
            if loc_codes:
                return loc_codes[0]
        except Exception:
            pass
        return ''

    def _infer_zone(self, doc, el, zone_codes):
        """Infer ZONE from element's room or first zone code."""
        try:
            pt = None
            try:
                bb = el.get_BoundingBox(None)
                if bb:
                    pt = XYZ((bb.Min.X + bb.Max.X) / 2,
                             (bb.Min.Y + bb.Max.Y) / 2,
                             (bb.Min.Z + bb.Max.Z) / 2)
            except Exception:
                try:
                    loc = el.Location
                    if hasattr(loc, 'Point'):
                        pt = loc.Point
                except Exception:
                    pass
            if pt:
                try:
                    room = doc.GetRoomAtPoint(pt)
                    if room:
                        # Try room number for zone
                        rnum = room.Number or ''
                        for code in zone_codes:
                            if code.upper() in rnum.upper():
                                return code
                        # Use room number prefix
                        if rnum and rnum[0].isdigit():
                            return 'Z' + rnum[:2].zfill(2)
                except Exception:
                    pass
        except Exception:
            pass
        return ''

    def _infer_system(self, el):
        """Infer SYS from Revit system parameters."""
        try:
            # Try MEP system classification
            for bip_name in ['RBS_SYSTEM_CLASSIFICATION_PARAM', 'RBS_DUCT_SYSTEM_TYPE_PARAM',
                             'RBS_PIPING_SYSTEM_TYPE_PARAM']:
                try:
                    bip = getattr(BuiltInParameter, bip_name, None)
                    if bip:
                        p = el.get_Parameter(bip)
                        if p:
                            val = p.AsString() or p.AsValueString() or ''
                            if val:
                                return val.split('-')[0].split(' ')[0][:6].upper()
                except Exception:
                    pass
            # Try System Name parameter
            p = el.LookupParameter('System Name')
            if p:
                val = p.AsString() or ''
                if val:
                    return val.split('-')[0].split(' ')[0][:6].upper()
        except Exception:
            pass
        return ''

    def _infer_function(self, el, sys_code):
        """Infer FUNC from system type and element properties."""
        try:
            # Try system classification for function
            for bip_name in ['RBS_SYSTEM_CLASSIFICATION_PARAM']:
                try:
                    bip = getattr(BuiltInParameter, bip_name, None)
                    if bip:
                        p = el.get_Parameter(bip)
                        if p:
                            val = (p.AsString() or p.AsValueString() or '').upper()
                            if 'SUPPLY' in val or 'SUP' in val: return 'SUP'
                            if 'RETURN' in val or 'RET' in val: return 'RET'
                            if 'EXHAUST' in val or 'EXH' in val: return 'EXH'
                            if 'DOMESTIC' in val: return 'DOM'
                            if 'POWER' in val: return 'PWR'
                            if 'LIGHT' in val or 'LTG' in val: return 'LTG'
                            if 'FIRE' in val or 'SPRINKLER' in val: return 'FP'
                            if 'DRAIN' in val: return 'DRN'
                            if 'VENT' in val: return 'VNT'
                except Exception:
                    pass
            # Infer from system code
            if sys_code:
                sc = sys_code.upper()
                if 'SA' in sc or 'SUPPLY' in sc: return 'SUP'
                if 'RA' in sc or 'RETURN' in sc: return 'RET'
                if 'EA' in sc or 'EXHAUST' in sc: return 'EXH'
        except Exception:
            pass
        return ''

    def _infer_product(self, el, cat_name):
        """Infer PROD from family name or type name."""
        try:
            # Get family name
            fam_name = ''
            try:
                etype = doc.GetElement(el.GetTypeId()) if hasattr(el, 'GetTypeId') else None
                if etype:
                    fam_name = etype.FamilyName or ''
            except Exception:
                pass
            if not fam_name:
                try:
                    fam_name = el.Symbol.FamilyName if hasattr(el, 'Symbol') else ''
                except Exception:
                    pass
            fn = fam_name.upper()
            # Common pattern matching
            mappings = [
                ('AHU', 'AHU'), ('FCU', 'FCU'), ('CHILLER', 'CHL'), ('BOILER', 'BLR'),
                ('PUMP', 'PMP'), ('FAN', 'FAN'), ('VAV', 'VAV'), ('GRILLE', 'GRL'),
                ('DIFFUSER', 'DIF'), ('DAMPER', 'DMP'), ('PANEL', 'PNL'),
                ('DOWNLIGHT', 'DWN'), ('LUMINAIRE', 'LUM'), ('SWITCH', 'SW'),
                ('SOCKET', 'SKT'), ('SPRINKLER', 'SPR'), ('DETECTOR', 'DET'),
                ('CALL POINT', 'MCP'), ('DISTRIBUTION', 'DB'), ('TRANSFORMER', 'XFMR'),
                ('SWITCHBOARD', 'SWB'), ('MOTOR', 'MTR'), ('SENSOR', 'SNS'),
            ]
            for pattern, code in mappings:
                if pattern in fn:
                    return code
            # Fallback: first 3 chars of family name
            if fam_name and len(fam_name) >= 3:
                return fam_name[:3].upper()
        except Exception:
            pass
        return ''

    def IsoAssignNumbers_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        if not self._ISO_LIBS: self._iso_log('ISO libs not loaded'); return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        groups = {}
        for el, cat_name in elements:
            zone = self._iso_get(el, 'ASS_ZONE_TXT') or 'ZZ'
            func = self._iso_get(el, 'ASS_FUNC_TXT') or 'XX'
            groups.setdefault((cat_name, zone, func), []).append(el)
        for key in groups:
            groups[key].sort(key=lambda e: e.Id.IntegerValue)
        pad = getattr(self._TC, 'NUM_PAD', 4); written = skipped = 0
        t = Transaction(doc, 'STINGTags ISO Assign Numbers'); t.Start()
        try:
            for key, elems in groups.items():
                for i, el in enumerate(elems, 1):
                    if self._iso_set(el, 'ASS_SEQ_NUM_TXT', str(i).zfill(pad), self._iso_overwrite):
                        written += 1
                    else: skipped += 1
            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('AssignNumbers error: ' + str(ex)); return
        self._iso_log('ASSIGN NUMBERS\n{} groups  Written: {}  Skipped: {}'.format(
            len(groups), written, skipped), '②')

    def IsoSmartTokens_Click(self, s, e):
        """Smart inference: fill empty tokens using heuristics from element properties."""
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        inferred = 0
        t = Transaction(doc, 'STINGTags ISO Smart Tokens'); t.Start()
        try:
            for el, cat_name in elements:
                # LEVEL - from element's Level property
                if not self._iso_get(el, 'ASS_LVL_COD_TXT'):
                    lvl = ''
                    if self._ISO_LIBS and hasattr(self._TL, 'get_level_code'):
                        lvl = self._TL.get_level_code(doc, el)
                    if not lvl:
                        try:
                            lid = el.LevelId
                            if lid and lid != ElementId.InvalidElementId:
                                lv = doc.GetElement(lid)
                                if lv:
                                    nm = lv.Name.strip()
                                    digits = ''.join(c for c in nm if c.isdigit())
                                    lvl = ('L' + digits.zfill(2) if digits
                                           else nm.upper().replace(' ', '')[:4])
                        except Exception:
                            pass
                    if lvl and self._iso_set(el, 'ASS_LVL_COD_TXT', lvl, self._iso_overwrite):
                        inferred += 1
                # DISCIPLINE - from category map
                if not self._iso_get(el, 'ASS_DISCIPLINE_COD_TXT'):
                    disc = getattr(self._TC, 'DISC_MAP', {}).get(cat_name, '') if self._ISO_LIBS else ''
                    if disc and self._iso_set(el, 'ASS_DISCIPLINE_COD_TXT', disc, self._iso_overwrite):
                        inferred += 1
                # SYSTEM - from Revit system params or System Name
                if not self._iso_get(el, 'ASS_SYSTEM_TYPE_TXT'):
                    sys_c = self._infer_system(el)
                    if sys_c and self._iso_set(el, 'ASS_SYSTEM_TYPE_TXT', sys_c, self._iso_overwrite):
                        inferred += 1
                # FUNCTION - from system classification
                if not self._iso_get(el, 'ASS_FUNC_TXT'):
                    sys_c = self._iso_get(el, 'ASS_SYSTEM_TYPE_TXT')
                    func_c = self._infer_function(el, sys_c)
                    if func_c and self._iso_set(el, 'ASS_FUNC_TXT', func_c, self._iso_overwrite):
                        inferred += 1
                # PRODUCT - from family name pattern matching
                if not self._iso_get(el, 'ASS_PRODCT_COD_TXT'):
                    prod_c = self._infer_product(el, cat_name)
                    if prod_c and self._iso_set(el, 'ASS_PRODCT_COD_TXT', prod_c, self._iso_overwrite):
                        inferred += 1
                # DESCRIPTION - from family name + type name (for TAG_5)
                if not self._iso_get(el, 'ASS_DESCRIPTION_TXT'):
                    desc = ''
                    try:
                        etype = doc.GetElement(el.GetTypeId()) if hasattr(el, 'GetTypeId') else None
                        if etype:
                            fam = etype.FamilyName or ''
                            tn = etype.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME)
                            tname = (tn.AsString() if tn else '') or ''
                            if fam and tname and fam != tname:
                                desc = '{} {}'.format(fam, tname)
                            elif fam:
                                desc = fam
                            elif tname:
                                desc = tname
                    except Exception:
                        pass
                    if desc and self._iso_set(el, 'ASS_DESCRIPTION_TXT', desc[:80], False):
                        inferred += 1
                # MOUNTING HEIGHT - from element elevation offset (for TAG_3)
                if not self._iso_get(el, 'MNT_HGT_MM'):
                    try:
                        off_param = el.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)
                        if not off_param:
                            off_param = el.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM)
                        if off_param and off_param.HasValue:
                            off_ft = off_param.AsDouble()
                            off_mm = int(round(off_ft * 304.8))
                            if off_mm > 0:
                                self._iso_set(el, 'MNT_HGT_MM', str(off_mm), False)
                                inferred += 1
                    except Exception:
                        pass
            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('Smart Tokens error: ' + str(ex)); return
        self._iso_log('SMART TOKENS — {} values inferred  |  {} elements'.format(
            inferred, len(elements)), '[Brain]')
        # Auto-refresh dashboard
        try: self._refresh_dashboard(doc, uidoc)
        except Exception: pass

    def IsoBuildTags_Click(self, s, e):
        """Assemble ASS_TAG_1 through TAG_6 from individual tokens."""
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        sep = getattr(self._TC, 'SEPARATOR', '-') if self._ISO_LIBS else '-'
        built = dict(tag1=0, tag2=0, tag3=0, tag4=0, tag5=0, tag6=0)
        t = Transaction(doc, 'STINGTags Build Tags'); t.Start()
        try:
            for el, cat_name in elements:
                disc = self._iso_get(el, 'ASS_DISCIPLINE_COD_TXT')
                loc  = self._iso_get(el, 'ASS_LOC_TXT')
                zone = self._iso_get(el, 'ASS_ZONE_TXT')
                lvl  = self._iso_get(el, 'ASS_LVL_COD_TXT')
                sys_c = self._iso_get(el, 'ASS_SYSTEM_TYPE_TXT')
                func = self._iso_get(el, 'ASS_FUNC_TXT')
                prod = self._iso_get(el, 'ASS_PRODCT_COD_TXT')
                seq  = self._iso_get(el, 'ASS_SEQ_NUM_TXT')

                # TAG_1: DISC-LOC-ZONE-LVL-SYS-FUNC-PROD-NUM (full identity)
                tokens = [disc, loc, zone, lvl, sys_c, func, prod, seq]
                tag1_val = sep.join(t for t in tokens if t)
                if tag1_val and self._iso_set(el, 'ASS_TAG_1_TXT', tag1_val, True):
                    built['tag1'] += 1

                # TAG_2: SYS-FUNC-PROD-NUM (system identity)
                tag2_tokens = [sys_c, func, prod, seq]
                tag2_val = sep.join(t for t in tag2_tokens if t)
                if tag2_val:
                    self._iso_set(el, 'ASS_TAG_2_TXT', tag2_val, True)
                    built['tag2'] += 1

                # TAG_3: SYS-FUNC-PROD-NUM + installation data
                mnt_hgt = self._iso_get(el, 'MNT_HGT_MM')
                mnt_type = self._iso_get(el, 'MNT_TYPE_TXT')
                inst_dtl = self._iso_get(el, 'ASS_INST_DETAIL_NUM_TXT')
                tag3_parts = [tag2_val]
                if mnt_hgt: tag3_parts.append('COE:FFL+{}mm'.format(mnt_hgt))
                if mnt_type: tag3_parts.append(mnt_type)
                if inst_dtl: tag3_parts.append(inst_dtl)
                tag3_val = ' '.join(tag3_parts)
                if len(tag3_parts) > 1:
                    self._iso_set(el, 'ASS_TAG_3_TXT', tag3_val, True)
                    built['tag3'] += 1

                # TAG_4: PROD-NUM (short label)
                tag4_tokens = [prod, seq]
                tag4_val = sep.join(t for t in tag4_tokens if t)
                if tag4_val:
                    self._iso_set(el, 'ASS_TAG_4_TXT', tag4_val, True)
                    built['tag4'] += 1

                # TAG_5: PROD-NUM + description
                desc = self._iso_get(el, 'ASS_DESCRIPTION_TXT')
                if tag4_val:
                    tag5_val = (tag4_val + ' ' + desc).strip() if desc else tag4_val
                    self._iso_set(el, 'ASS_TAG_5_TXT', tag5_val, True)
                    built['tag5'] += 1

                # TAG_6: PROD-NUM + status + comments
                status = self._iso_get(el, 'ASS_STATUS_TXT') or self._iso_get(el, 'ASS_STATUS_COD_TXT')
                comments = self._iso_get(el, 'PRJ_COMMENTS_TXT')
                if tag4_val:
                    tag6_parts = [tag4_val]
                    if status: tag6_parts.append(status)
                    if comments: tag6_parts.append(comments)
                    self._iso_set(el, 'ASS_TAG_6_TXT', ' '.join(tag6_parts), True)
                    built['tag6'] += 1

            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('Build Tags error: ' + str(ex)); return
        self._iso_log(
            'BUILD TAGS — {} elements\n'
            'TAG_1:{tag1}  TAG_2:{tag2}  TAG_3:{tag3}\n'
            'TAG_4:{tag4}  TAG_5:{tag5}  TAG_6:{tag6}'.format(
                len(elements), **built), '*')
        # Auto-refresh dashboard
        try: self._refresh_dashboard(doc, uidoc)
        except Exception: pass

    def IsoBuildT3Tags_Click(self, s, e):
        """Assemble all 15 Tier-3 discipline-specific tags from source parameters."""
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        sep = getattr(self._TC, 'SEPARATOR', '-') if self._ISO_LIBS else '-'
        counts = {}
        t = Transaction(doc, 'STINGTags Build T3 Tags'); t.Start()
        try:
            for el, cat_name in elements:
                sys_c = self._iso_get(el, 'ASS_SYSTEM_TYPE_TXT') or ''
                func = self._iso_get(el, 'ASS_FUNC_TXT') or ''
                prod = self._iso_get(el, 'ASS_PRODCT_COD_TXT') or ''
                seq  = self._iso_get(el, 'ASS_SEQ_NUM_TXT') or ''
                base = sep.join(t_ for t_ in [sys_c, func, prod, seq] if t_)
                if not base: continue

                # T3-H HVAC Equipment (Mechanical Equipment)
                if cat_name == 'Mechanical Equipment':
                    # HVC_EQP_TAG_01: base / capacity + refrigerant
                    cap_kw = self._iso_get(el, 'HVC_CAP_KW') or ''
                    cap_tr = self._iso_get(el, 'HVC_CAP_TR') or ''
                    refrig = self._iso_get(el, 'HVC_REFRIGERANT_TXT') or ''
                    parts = [base, '/']
                    if cap_kw: parts.append('{}kW'.format(cap_kw))
                    if cap_tr: parts.append('{}TR'.format(cap_tr))
                    if refrig: parts.append(refrig)
                    if len(parts) > 2:
                        self._iso_set(el, 'HVC_EQP_TAG_01_TXT', ' '.join(parts), True)
                        counts['HVC01'] = counts.get('HVC01', 0) + 1
                    # HVC_EQP_TAG_02: base / power + voltage + efficiency
                    pwr = self._iso_get(el, 'HVC_PWR_KW') or ''
                    vlt = self._iso_get(el, 'HVC_VLT_V') or ''
                    eff = self._iso_get(el, 'HVC_EFF_RATIO_NR') or ''
                    parts2 = [base, '/']
                    if pwr: parts2.append('{}kW'.format(pwr))
                    if vlt: parts2.append('{}V'.format(vlt))
                    if eff: parts2.append('EFF:{}'.format(eff))
                    if len(parts2) > 2:
                        self._iso_set(el, 'HVC_EQP_TAG_02_TXT', ' '.join(parts2), True)
                        counts['HVC02'] = counts.get('HVC02', 0) + 1
                    # HVC_EQP_TAG_03: base / controls + height + detail
                    ctrl = self._iso_get(el, 'HVC_CONTROL_TYPE_TXT') or ''
                    mhgt = self._iso_get(el, 'MNT_HGT_MM') or ''
                    idtl = self._iso_get(el, 'ASS_INST_DETAIL_NUM_TXT') or ''
                    parts3 = [base, '/']
                    if ctrl: parts3.append('BMS:{}'.format(ctrl))
                    if mhgt: parts3.append('COE:FFL+{}mm'.format(mhgt))
                    if idtl: parts3.append('Dtl:{}'.format(idtl))
                    if len(parts3) > 2:
                        self._iso_set(el, 'HVC_EQP_TAG_03_TXT', ' '.join(parts3), True)
                        counts['HVC03'] = counts.get('HVC03', 0) + 1

                # T3-D Ducts / Duct Fittings / Flex Ducts / Air Terminals
                if cat_name in ('Ducts', 'Duct Fittings', 'Flex Ducts', 'Air Terminals'):
                    dcls = self._iso_get(el, 'HVC_DUCT_CLASS_TXT') or ''
                    dflw = self._iso_get(el, 'HVC_DUCT_FLOWRATE_M3H') or ''
                    parts_d1 = [base, '/']
                    if dcls: parts_d1.append('Cl.{}'.format(dcls))
                    if dflw: parts_d1.append('{}m3/h'.format(dflw))
                    if len(parts_d1) > 2:
                        self._iso_set(el, 'HVC_DCT_TAG_01_TXT', ' '.join(parts_d1), True)
                        counts['DCT01'] = counts.get('DCT01', 0) + 1
                    dmat = self._iso_get(el, 'HVC_DCT_MAT_TXT') or ''
                    dlin = self._iso_get(el, 'HVC_DUCT_LINING_TXT') or ''
                    idtl = self._iso_get(el, 'ASS_INST_DETAIL_NUM_TXT') or ''
                    parts_d2 = [base, '/']
                    if dmat: parts_d2.append(dmat)
                    if dlin: parts_d2.append(dlin)
                    if idtl: parts_d2.append('Dtl:{}'.format(idtl))
                    if len(parts_d2) > 2:
                        self._iso_set(el, 'HVC_DCT_TAG_02_TXT', ' '.join(parts_d2), True)
                        counts['DCT02'] = counts.get('DCT02', 0) + 1
                    # Air terminal specific tag
                    if cat_name == 'Air Terminals':
                        ttype = self._iso_get(el, 'HVC_DCT_TERMINAL_TYPE_SD_RG_EG_VAV_TXT') or ''
                        tsz = self._iso_get(el, 'HVC_DCT_TERMINAL_SZ_TXT') or ''
                        tsnd = self._iso_get(el, 'HVC_DCT_SOUNDLVL_DB') or ''
                        parts_d3 = [base, '/']
                        if ttype: parts_d3.append(ttype)
                        if tsz: parts_d3.append(tsz)
                        if tsnd: parts_d3.append('{}dB'.format(tsnd))
                        if len(parts_d3) > 2:
                            self._iso_set(el, 'HVC_DCT_TAG_03_TXT', ' '.join(parts_d3), True)
                            counts['DCT03'] = counts.get('DCT03', 0) + 1

                # T3-E Electrical Equipment
                if cat_name == 'Electrical Equipment':
                    evlt = self._iso_get(el, 'ELC_PNL_VLT_V') or ''
                    ephs = self._iso_get(el, 'ELC_PNL_PHS_COUNT_NR') or ''
                    ebrk = self._iso_get(el, 'ELC_PNL_MAIN_BRK_A') or ''
                    esc  = self._iso_get(el, 'ELC_PNL_SHORT_CIRCUIT_RATING_KA') or ''
                    parts_e1 = [base, '/']
                    if evlt: parts_e1.append('{}V'.format(evlt))
                    if ephs: parts_e1.append('{}Ph'.format(ephs))
                    if ebrk: parts_e1.append('{}A'.format(ebrk))
                    if esc: parts_e1.append('{}kA'.format(esc))
                    if len(parts_e1) > 2:
                        self._iso_set(el, 'ELC_EQP_TAG_01_TXT', ' '.join(parts_e1), True)
                        counts['ELC01'] = counts.get('ELC01', 0) + 1
                    eload = self._iso_get(el, 'ELC_PNL_CONNECTED_LOAD_KW') or ''
                    eip = self._iso_get(el, 'ELC_IP_RATING_TXT') or ''
                    efed = self._iso_get(el, 'ELC_PNL_FED_FROM_PNL_TXT') or ''
                    parts_e2 = [base, '/']
                    if eload: parts_e2.append('{}kW'.format(eload))
                    if eip: parts_e2.append('IP{}'.format(eip))
                    if efed: parts_e2.append('Fed:{}'.format(efed))
                    if len(parts_e2) > 2:
                        self._iso_set(el, 'ELC_EQP_TAG_02_TXT', ' '.join(parts_e2), True)
                        counts['ELC02'] = counts.get('ELC02', 0) + 1

                # T3-F Electrical Fixtures + Lighting Fixtures (circuit traceability)
                if cat_name in ('Electrical Fixtures', 'Lighting Fixtures'):
                    eqp_tag = self._iso_get(el, 'ASS_EQUIPMENT_TAG_TXT') or ''
                    ckt_nr = self._iso_get(el, 'ELC_CKT_NR') or ''
                    if eqp_tag or ckt_nr:
                        fix_parts = [p for p in [eqp_tag, ckt_nr, prod, seq] if p]
                        fix_val = sep.join(fix_parts)
                        self._iso_set(el, 'ELE_FIX_TAG_1_TXT', fix_val, True)
                        counts['FIX1'] = counts.get('FIX1', 0) + 1
                        # With installation data
                        mhgt = self._iso_get(el, 'MNT_HGT_MM') or ''
                        mtyp = self._iso_get(el, 'MNT_TYPE_TXT') or ''
                        idtl = self._iso_get(el, 'ASS_INST_DETAIL_NUM_TXT') or ''
                        fix2_parts = [fix_val]
                        if mhgt: fix2_parts.append('COE:FFL+{}mm'.format(mhgt))
                        if mtyp: fix2_parts.append(mtyp)
                        if idtl: fix2_parts.append('Dtl:{}'.format(idtl))
                        if len(fix2_parts) > 1:
                            self._iso_set(el, 'ELE_FIX_TAG_2_TXT', ' '.join(fix2_parts), True)
                            counts['FIX2'] = counts.get('FIX2', 0) + 1

                # T3-L Lighting-specific tags
                if cat_name == 'Lighting Fixtures':
                    lwatt = self._iso_get(el, 'LTG_WATTAGE_W') or ''
                    lcct = self._iso_get(el, 'LTG_CLR_TEMP_K') or ''
                    lcri = self._iso_get(el, 'LTG_CRI') or ''
                    parts_l1 = [base, '/']
                    if lwatt: parts_l1.append('{}W'.format(lwatt))
                    if lcct: parts_l1.append('{}K'.format(lcct))
                    if lcri: parts_l1.append('CRI:{}'.format(lcri))
                    if len(parts_l1) > 2:
                        self._iso_set(el, 'LTG_FIX_TAG_01_TXT', ' '.join(parts_l1), True)
                        counts['LTG01'] = counts.get('LTG01', 0) + 1
                    lmnt = self._iso_get(el, 'MNT_TYPE_TXT') or ''
                    lemrg = self._iso_get(el, 'LTG_EMRG_TYPE') or ''
                    lctrl = self._iso_get(el, 'LTG_CTRL_TYPE') or ''
                    parts_l2 = [base, '/']
                    if lmnt: parts_l2.append(lmnt)
                    if lemrg: parts_l2.append(lemrg)
                    if lctrl: parts_l2.append(lctrl)
                    if len(parts_l2) > 2:
                        self._iso_set(el, 'LTG_FIX_TAG_02_TXT', ' '.join(parts_l2), True)
                        counts['LTG02'] = counts.get('LTG02', 0) + 1

                # T3-P Plumbing / Pipes
                if cat_name in ('Pipes', 'Pipe Fittings', 'Pipe Accessories',
                                'Plumbing Fixtures', 'Flex Pipes'):
                    psz = self._iso_get(el, 'PLM_PIPE_SZ_MM') or ''
                    pmat = self._iso_get(el, 'PLM_PPE_MAT') or ''
                    pprs = self._iso_get(el, 'PLM_PRESSURE_BAR') or ''
                    parts_p1 = [base, '/']
                    if psz: parts_p1.append('DN{}mm'.format(psz))
                    if pmat: parts_p1.append(pmat)
                    if pprs: parts_p1.append('{}bar'.format(pprs))
                    if len(parts_p1) > 2:
                        self._iso_set(el, 'PLM_EQP_TAG_01_TXT', ' '.join(parts_p1), True)
                        counts['PLM01'] = counts.get('PLM01', 0) + 1
                    pflw = self._iso_get(el, 'PLM_FLOWRATE_LPS') or ''
                    pvel = self._iso_get(el, 'PLM_VELOCITY_MPS') or ''
                    pins = self._iso_get(el, 'PLM_INS_TYPE') or ''
                    parts_p2 = [base, '/']
                    if pflw: parts_p2.append('{}L/s'.format(pflw))
                    if pvel: parts_p2.append('{}m/s'.format(pvel))
                    if pins: parts_p2.append(pins)
                    if len(parts_p2) > 2:
                        self._iso_set(el, 'PLM_EQP_TAG_02_TXT', ' '.join(parts_p2), True)
                        counts['PLM02'] = counts.get('PLM02', 0) + 1

                # T3-S Fire / Sprinklers
                if cat_name in ('Sprinklers', 'Fire Alarm Devices'):
                    dloop = self._iso_get(el, 'FLS_DEV_LOOP') or ''
                    daddr = self._iso_get(el, 'FLS_DEV_ADDRESS') or ''
                    dzone = self._iso_get(el, 'FLS_DEV_ZONE') or ''
                    parts_s1 = [base, '/']
                    if dloop: parts_s1.append('L:{}'.format(dloop))
                    if daddr: parts_s1.append('A:{}'.format(daddr))
                    if dzone: parts_s1.append('Z:{}'.format(dzone))
                    if len(parts_s1) > 2:
                        self._iso_set(el, 'FLS_DEV_TAG_01_TXT', ' '.join(parts_s1), True)
                        counts['FLS01'] = counts.get('FLS01', 0) + 1
                    if cat_name == 'Sprinklers':
                        stemp = self._iso_get(el, 'FLS_TEMP_RATING_C') or ''
                        sflow = self._iso_get(el, 'FLS_FLOW_LPM') or ''
                        shead = self._iso_get(el, 'FLS_HEAD_TYPE') or ''
                        parts_s2 = [base, '/']
                        if stemp: parts_s2.append('{}C'.format(stemp))
                        if sflow: parts_s2.append('{}LPM'.format(sflow))
                        if shead: parts_s2.append(shead)
                        if len(parts_s2) > 2:
                            self._iso_set(el, 'FLS_DEV_TAG_02_TXT', ' '.join(parts_s2), True)
                            counts['FLS02'] = counts.get('FLS02', 0) + 1

            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('Build T3 Tags error: ' + str(ex)); return
        total_built = sum(counts.values())
        detail = '  '.join('{}:{}'.format(k, v) for k, v in sorted(counts.items()))
        self._iso_log(
            'BUILD T3 TAGS: {} values across {} elements\n{}'.format(
                total_built, len(elements), detail or '(no T3 source data found)'), '*')
        try: self._refresh_dashboard(doc, uidoc)
        except Exception: pass

    def IsoBuildMatTags_Click(self, s, e):
        """Assemble all 21 material tags from MAT_/BLE_/PROP_/PER_ source parameters."""
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        counts = {}
        t = Transaction(doc, 'STINGTags Build Material Tags'); t.Start()
        try:
            for el, cat_name in elements:
                mat_code = self._iso_get(el, 'MAT_CODE') or ''
                mat_name = self._iso_get(el, 'MAT_NAME') or ''
                mat_cat = self._iso_get(el, 'MAT_CATEGORY') or ''
                mat_disc = self._iso_get(el, 'MAT_DISCIPLINE') or ''
                mat_thick = self._iso_get(el, 'MAT_THICKNESS_MM') or ''
                mat_std = self._iso_get(el, 'MAT_STANDARD') or ''
                mat_mfr = self._iso_get(el, 'MAT_MANUFACTURER') or ''
                mat_spec = self._iso_get(el, 'MAT_SPECIFICATIONS') or ''
                mat_cost = self._iso_get(el, 'MAT_COST_UGX') or ''
                mat_dur = self._iso_get(el, 'MAT_DURABILITY') or ''

                # MAT_TAG_1: MAT_CODE MAT_CATEGORY MAT_DISCIPLINE
                if mat_code:
                    parts = [p for p in [mat_code, mat_cat, mat_disc] if p]
                    self._iso_set(el, 'MAT_TAG_1_TXT', '  '.join(parts), True)
                    counts['MT1'] = counts.get('MT1', 0) + 1

                # MAT_TAG_2: MAT_NAME thickness std
                if mat_name:
                    t2 = mat_name
                    if mat_thick: t2 += '  {}mm'.format(mat_thick)
                    if mat_std: t2 += '  std:{}'.format(mat_std)
                    self._iso_set(el, 'MAT_TAG_2_TXT', t2, True)
                    counts['MT2'] = counts.get('MT2', 0) + 1

                # MAT_TAG_3: MAT_NAME mfr spec
                if mat_name and (mat_mfr or mat_spec):
                    t3 = mat_name
                    if mat_mfr: t3 += '  mfr:{}'.format(mat_mfr)
                    if mat_spec: t3 += '  spec:{}'.format(mat_spec)
                    self._iso_set(el, 'MAT_TAG_3_TXT', t3, True)
                    counts['MT3'] = counts.get('MT3', 0) + 1

                # MAT_TAG_4: MAT_CODE cost durability
                if mat_code and (mat_cost or mat_dur):
                    t4 = mat_code
                    if mat_cost: t4 += '  UGX:{}/m2'.format(mat_cost)
                    if mat_dur: t4 += '  {}'.format(mat_dur)
                    self._iso_set(el, 'MAT_TAG_4_TXT', t4, True)
                    counts['MT4'] = counts.get('MT4', 0) + 1

                # MAT_TAG_5: ISO 23386 property dict GUID
                dict_guid = self._iso_get(el, 'MAT_PROP_DICT_GUID_TXT') or ''
                iso_id = self._iso_get(el, 'MAT_ISO_19650_ID') or ''
                if dict_guid or iso_id:
                    t5_parts = []
                    if dict_guid: t5_parts.append('GUID:{}'.format(dict_guid))
                    if mat_name: t5_parts.append(mat_name)
                    if mat_std: t5_parts.append('dict:{}'.format(mat_std))
                    if iso_id: t5_parts.append('ISO-ID:{}'.format(iso_id))
                    if t5_parts:
                        self._iso_set(el, 'MAT_TAG_5_TXT', '  '.join(t5_parts), True)
                        counts['MT5'] = counts.get('MT5', 0) + 1

                # MAT_TAG_6: ISO 22057 EPD environmental data
                epd_ref = self._iso_get(el, 'MAT_EPD_REF_TXT') or ''
                co2 = self._iso_get(el, 'PER_SUST_CARBON_FOOTPRINT_KG') or ''
                rcy = self._iso_get(el, 'PER_SUST_RECYCLED_CONTENT_PCT') or ''
                if epd_ref or co2:
                    t6_parts = []
                    if epd_ref: t6_parts.append('EPD:{}'.format(epd_ref))
                    if mat_name: t6_parts.append(mat_name)
                    if co2: t6_parts.append('GWP:{}kgCO2e/m2'.format(co2))
                    if rcy: t6_parts.append('rcy:{}%'.format(rcy))
                    if t6_parts:
                        self._iso_set(el, 'MAT_TAG_6_TXT', '  '.join(t6_parts), True)
                        counts['MT6'] = counts.get('MT6', 0) + 1

                # MAT_PERF_TAG_1: thermal + fire
                u_val = self._iso_get(el, 'PER_THERM_U_VALUE_W_M2K_NR') or ''
                r_val = self._iso_get(el, 'PER_THERM_R_VALUE_M2K_W') or ''
                fire = self._iso_get(el, 'PROP_FIRE_RATING') or ''
                if u_val or r_val or fire:
                    tp1 = mat_name or cat_name
                    if u_val: tp1 += '  U:{}W/m2K'.format(u_val)
                    if r_val: tp1 += '  R:{}m2K/W'.format(r_val)
                    if fire: tp1 += '  {}'.format(fire)
                    self._iso_set(el, 'MAT_PERF_TAG_1_TXT', tp1, True)
                    counts['MP1'] = counts.get('MP1', 0) + 1

                # MAT_PERF_TAG_2: acoustic
                stc = self._iso_get(el, 'PER_ACOUSTICS_STC_RATING_TXT') or ''
                iic = self._iso_get(el, 'PER_ACOUSTICS_IIC_RATING_TXT') or ''
                nrc = self._iso_get(el, 'PROP_ACOUSTIC_ABS') or ''
                if stc or iic or nrc:
                    tp2 = mat_name or cat_name
                    if stc: tp2 += '  STC:{}'.format(stc)
                    if iic: tp2 += '  IIC:{}'.format(iic)
                    if nrc: tp2 += '  NRC:{}'.format(nrc)
                    self._iso_set(el, 'MAT_PERF_TAG_2_TXT', tp2, True)
                    counts['MP2'] = counts.get('MP2', 0) + 1

                # MAT_PERF_TAG_3: layer build-up
                layer_count_str = self._iso_get(el, 'MAT_LAYER_COUNT') or ''
                if layer_count_str:
                    try:
                        lc = int(float(layer_count_str))
                    except (ValueError, TypeError):
                        lc = 0
                    if lc > 0:
                        lparts = ['{} layers:'.format(lc)]
                        for ln in range(1, min(lc + 1, 6)):
                            lfunc = self._iso_get(el, 'MAT_LAYER_{}_FUNCTION'.format(ln)) or ''
                            lmat = self._iso_get(el, 'MAT_LAYER_{}_MATERIAL'.format(ln)) or ''
                            lthk = self._iso_get(el, 'MAT_LAYER_{}_THICKNESS_MM'.format(ln)) or ''
                            if lfunc or lmat:
                                lstr = 'L{}:{}'.format(ln, ' '.join(
                                    p for p in [lfunc, lmat, '{}mm'.format(lthk) if lthk else ''] if p))
                                lparts.append(lstr)
                        if len(lparts) > 1:
                            self._iso_set(el, 'MAT_PERF_TAG_3_TXT', '  '.join(lparts), True)
                            counts['MP3'] = counts.get('MP3', 0) + 1

                # T3-F Finishes (Walls, Floors, Ceilings)
                if cat_name == 'Walls':
                    int_fin = self._iso_get(el, 'BLE_WALL_INTERIOR_FINISH_TXT') or ''
                    ext_fin = self._iso_get(el, 'BLE_WALL_EXTERIOR_FINISH_TXT') or ''
                    coats = self._iso_get(el, 'BLE_FINISH_PAINT_COATS_NR') or ''
                    ftype = self._iso_get(el, 'BLE_FINISH_TYPE_TXT') or ''
                    if int_fin or ext_fin:
                        fw = []
                        if int_fin: fw.append('Int:{}'.format(int_fin))
                        if ext_fin: fw.append('Ext:{}'.format(ext_fin))
                        if coats: fw.append('{} coats'.format(coats))
                        if ftype: fw.append(ftype)
                        self._iso_set(el, 'FIN_WALL_TAG_TXT', '  '.join(fw), True)
                        counts['FW'] = counts.get('FW', 0) + 1
                    # Facade tag for exterior walls
                    fac_mat = self._iso_get(el, 'BLE_FACADE_MAT_TXT') or ''
                    if fac_mat:
                        fac_fin = self._iso_get(el, 'BLE_FACADE_FINISH_TXT') or ''
                        fac_pnl = self._iso_get(el, 'BLE_FACADE_PNL_SZ_TXT') or ''
                        fac_fix = self._iso_get(el, 'BLE_FACADE_FIX_SYSTEM_TXT') or ''
                        ef = [fac_mat]
                        if fac_fin: ef.append(fac_fin)
                        if fac_pnl: ef.append('pnl:{}'.format(fac_pnl))
                        if fac_fix: ef.append('fix:{}'.format(fac_fix))
                        self._iso_set(el, 'ENV_FAC_TAG_TXT', '  '.join(ef), True)
                        counts['EF'] = counts.get('EF', 0) + 1

                if cat_name == 'Floors':
                    flr_fin = self._iso_get(el, 'BLE_FLR_FINISH_TXT') or ''
                    flr_thk = self._iso_get(el, 'BLE_FLR_FINISH_THICKNESS_MM') or ''
                    flr_slip = self._iso_get(el, 'BLE_FLR_SLIP_RESISTANCE_RATING_NR') or ''
                    flr_mat = self._iso_get(el, 'BLE_FLR_FINISH_MAT_TXT') or ''
                    if flr_fin:
                        ff = [flr_fin]
                        if flr_thk: ff.append('{}mm'.format(flr_thk))
                        if flr_slip: ff.append('slip:{}'.format(flr_slip))
                        if flr_mat: ff.append(flr_mat)
                        self._iso_set(el, 'FIN_FLR_TAG_TXT', '  '.join(ff), True)
                        counts['FF'] = counts.get('FF', 0) + 1

                if cat_name == 'Ceilings':
                    clg_fin = self._iso_get(el, 'BLE_CEILING_FINISH_TXT') or ''
                    clg_sys = self._iso_get(el, 'BLE_CEILING_SYSTEM_TXT') or ''
                    clg_nrc = self._iso_get(el, 'BLE_CEILING_NOISE_REDCTION_COEFFICIENT_NRC_NR') or ''
                    clg_grd = self._iso_get(el, 'BLE_CEILING_GRID_SZ_MM') or ''
                    if clg_fin or clg_sys:
                        fc = []
                        if clg_fin: fc.append(clg_fin)
                        if clg_sys: fc.append(clg_sys)
                        if clg_nrc: fc.append('NRC:{}'.format(clg_nrc))
                        if clg_grd: fc.append('grid:{}mm'.format(clg_grd))
                        self._iso_set(el, 'FIN_CEIL_TAG_TXT', '  '.join(fc), True)
                        counts['FC'] = counts.get('FC', 0) + 1

                if cat_name == 'Roofs':
                    rcov = self._iso_get(el, 'BLE_ROOF_COVERING_MAT_TXT') or ''
                    rwp = self._iso_get(el, 'BLE_ROOF_WATERPROOFING_SYSTEM_TXT') or ''
                    rins = self._iso_get(el, 'BLE_ROOF_INS_R_VALUE_M_2K_W') or ''
                    rslp = self._iso_get(el, 'BLE_ROOF_SLOPE_DEG') or ''
                    if rcov or rwp:
                        er = []
                        if rcov: er.append(rcov)
                        if rwp: er.append(rwp)
                        if rins: er.append('R:{}'.format(rins))
                        if rslp: er.append('slope:{}deg'.format(rslp))
                        self._iso_set(el, 'ENV_ROOF_TAG_TXT', '  '.join(er), True)
                        counts['ER'] = counts.get('ER', 0) + 1

                if cat_name == 'Windows':
                    wfrm = self._iso_get(el, 'BLE_WINDOW_FRAME_MAT_TXT') or ''
                    wglz = self._iso_get(el, 'BLE_WINDOW_GLAZING_TYPE_SINGLE_DOUBLE_TRIPLE_TXT') or ''
                    wu = self._iso_get(el, 'BLE_WINDOW_U_VALUE_W_M_2K_NR') or ''
                    wshgc = self._iso_get(el, 'BLE_WINDOW_SOLAR_HEAT_GAIN_COEFFICIENT_NR') or ''
                    wvlt = self._iso_get(el, 'BLE_WINDOW_VISIBLE_LIGHT_TRANSMITTANCE_PCT') or ''
                    if wfrm or wglz:
                        ew = []
                        if wfrm: ew.append(wfrm)
                        if wglz: ew.append(wglz)
                        if wu: ew.append('U:{}W/m2K'.format(wu))
                        if wshgc: ew.append('SHGC:{}'.format(wshgc))
                        if wvlt: ew.append('VLT:{}%'.format(wvlt))
                        self._iso_set(el, 'ENV_WIN_TAG_TXT', '  '.join(ew), True)
                        counts['EW'] = counts.get('EW', 0) + 1

                # Structural tags
                if cat_name in ('Structural Columns', 'Structural Framing', 'Floors'):
                    conc = self._iso_get(el, 'BLE_STRUCT_CONCRETE_GRADE_TXT') or ''
                    if conc:
                        sreo = self._iso_get(el, 'BLE_STRUCT_REINFORCEMENT_TXT') or ''
                        sfck = self._iso_get(el, 'PROP_COMP_STRENGTH_MPA') or ''
                        sthk = self._iso_get(el, 'BLE_STRUCT_SLAB_THICKNESS_MM') or ''
                        sc = [conc]
                        if sreo: sc.append('reo:{}'.format(sreo))
                        if sfck: sc.append('fck:{}MPa'.format(sfck))
                        if sthk: sc.append('{}mm'.format(sthk))
                        self._iso_set(el, 'STR_CONC_TAG_TXT', '  '.join(sc), True)
                        counts['SC'] = counts.get('SC', 0) + 1
                    steel = self._iso_get(el, 'BLE_STRUCT_STEEL_GRADE_TXT') or ''
                    if steel:
                        ssz = self._iso_get(el, 'BLE_STRUCT_BEAM_SZ_TXT') or ''
                        sfr = self._iso_get(el, 'PROP_FIRE_RATING') or ''
                        smat = self._iso_get(el, 'BLE_STRUCT_MAT_TXT') or ''
                        ss = [steel]
                        if ssz: ss.append(ssz)
                        if sfr: ss.append('Pfire:{}'.format(sfr))
                        if smat: ss.append(smat)
                        self._iso_set(el, 'STR_STEEL_TAG_TXT', '  '.join(ss), True)
                        counts['SS'] = counts.get('SS', 0) + 1

                # Sustainability tags (all categories)
                rcy_pct = self._iso_get(el, 'PER_SUST_RECYCLED_CONTENT_PCT') or ''
                co2_val = self._iso_get(el, 'PER_SUST_CARBON_FOOTPRINT_KG') or ''
                recyclable = self._iso_get(el, 'PER_SUST_RECYCLABLE_BOOL') or ''
                if rcy_pct or co2_val:
                    sg1 = [mat_name or cat_name]
                    if rcy_pct: sg1.append('rcy:{}%'.format(rcy_pct))
                    if co2_val: sg1.append('CO2:{}kg'.format(co2_val))
                    if recyclable: sg1.append('recyl:{}'.format(recyclable))
                    self._iso_set(el, 'SUST_MAT_TAG_1_TXT', '  '.join(sg1), True)
                    counts['SG1'] = counts.get('SG1', 0) + 1

                leed = self._iso_get(el, 'PER_SUST_LEED_CONTRIB_TXT') or ''
                edge = self._iso_get(el, 'PER_SUST_EDGE_CONTRIB_TXT') or ''
                nrg = self._iso_get(el, 'PER_SUST_ENERGY_RATING_TXT') or ''
                if leed or edge or nrg:
                    sg2 = [mat_name or cat_name]
                    if leed: sg2.append('LEED:{}'.format(leed))
                    if edge: sg2.append('EDGE:{}'.format(edge))
                    if nrg: sg2.append(nrg)
                    self._iso_set(el, 'SUST_MAT_TAG_2_TXT', '  '.join(sg2), True)
                    counts['SG2'] = counts.get('SG2', 0) + 1

                # Compliance tags (all categories)
                qa_cert = self._iso_get(el, 'RGL_QA_CERTIFICATION_TXT') or ''
                unbs = self._iso_get(el, 'RGL_UNBS_STD_TXT') or ''
                if mat_code and (mat_std or qa_cert or unbs):
                    cc1 = [mat_code]
                    if mat_std: cc1.append('std:{}'.format(mat_std))
                    if qa_cert: cc1.append('QA:{}'.format(qa_cert))
                    if unbs: cc1.append('UNBS:{}'.format(unbs))
                    self._iso_set(el, 'COMP_MAT_TAG_1_TXT', '  '.join(cc1), True)
                    counts['CC1'] = counts.get('CC1', 0) + 1

                apvd = self._iso_get(el, 'RGL_QA_APVD_VENDOR_TXT') or ''
                if mat_name and (iso_id or mat_dur or apvd):
                    cc2 = [mat_name]
                    if iso_id: cc2.append('ISO:{}'.format(iso_id))
                    if mat_dur: cc2.append(mat_dur)
                    if apvd: cc2.append('vendor:{}'.format(apvd))
                    self._iso_set(el, 'COMP_MAT_TAG_2_TXT', '  '.join(cc2), True)
                    counts['CC2'] = counts.get('CC2', 0) + 1

            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('Build Mat Tags error: ' + str(ex)); return
        total_built = sum(counts.values())
        detail = '  '.join('{}:{}'.format(k, v) for k, v in sorted(counts.items()))
        self._iso_log(
            'BUILD MATERIAL TAGS: {} values across {} elements\n{}'.format(
                total_built, len(elements), detail or '(no material source data found)'), '*')
        try: self._refresh_dashboard(doc, uidoc)
        except Exception: pass

    def IsoBuildAllTags_Click(self, s, e):
        """Build all tags: T1+T2 (BuildTags), T3 discipline, and material tags."""
        self.IsoBuildTags_Click(s, e)
        self.IsoBuildT3Tags_Click(s, e)
        self.IsoBuildMatTags_Click(s, e)

    def _refresh_dashboard(self, doc, uidoc):
        """Auto-refresh the completeness dashboard after populate/build operations."""
        try:
            elements = self._iso_collect(doc, uidoc)
            if not elements: return
            total = len(elements)
            items = []
            # Token completeness
            for pname in self._ISO_PARAMS:
                filled = sum(1 for el, _ in elements if self._iso_get(el, pname))
                pct = int(100 * filled / total) if total else 0
                short = self._ISO_SHORT.get(pname, pname.replace('ASS_', '').replace('_TXT', ''))
                items.append((short, pct))
            # Assembled tag
            filled_tag = sum(1 for el, _ in elements if self._iso_get(el, self._ISO_TAG_FIELD))
            pct_tag = int(100 * filled_tag / total) if total else 0
            items.append(('TAG_1', pct_tag))
            # Update status bar with summary
            total_pct = sum(p for _, p in items) // max(len(items), 1)
            self._log('[Scope: {}]  ISO completeness: {}%  ({} elements)'.format(
                self._iso_scope_label(), total_pct, total), '▶')
        except Exception:
            pass

    def _iso_scope_label(self):
        """Return human-readable scope label."""
        scope = getattr(self, '_iso_scope', 'view')
        return {'view': 'View', 'project': 'Proj', 'selection': 'Sel'}.get(scope, scope)

    def _iso_manual_set(self, param_name, label):
        doc, uidoc = self._fd()
        if not doc: return
        val = forms.ask_for_string(prompt=label+' code:', title='Set '+label)
        if not val: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        t = Transaction(doc, 'STINGTags Set '+label); t.Start()
        count = 0
        try:
            for el, _ in elements:
                if self._iso_set(el, param_name, val, self._iso_overwrite): count += 1
            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('Error: '+str(ex)); return
        self._iso_log('{} = "{}": {} elements updated'.format(label, val, count))

    # All 13 ISO 19650 token setters
    def IsoSetProject_Click(self, s, e): self._iso_manual_set('ASS_PROJECT_COD_TXT',    'PROJECT')
    def IsoSetOrig_Click(self, s, e):    self._iso_manual_set('ASS_ORIGINATOR_COD_TXT', 'ORIG')
    def IsoSetVol_Click(self, s, e):     self._iso_manual_set('ASS_VOLUME_COD_TXT',     'VOL')
    def IsoSetDisc_Click(self, s, e):    self._iso_manual_set('ASS_DISCIPLINE_COD_TXT', 'DISC')
    def IsoSetLvl_Click(self, s, e):     self._iso_manual_set('ASS_LVL_COD_TXT',        'LVL')
    def IsoSetZone_Click(self, s, e):    self._iso_manual_set('ASS_ZONE_TXT',            'ZONE')
    def IsoSetLoc_Click(self, s, e):     self._iso_manual_set('ASS_LOC_TXT',             'LOC')
    def IsoSetSys_Click(self, s, e):     self._iso_manual_set('ASS_SYSTEM_TYPE_TXT',     'SYS')
    def IsoSetFunc_Click(self, s, e):    self._iso_manual_set('ASS_FUNC_TXT',            'FUNC')
    def IsoSetProd_Click(self, s, e):    self._iso_manual_set('ASS_PRODCT_COD_TXT',      'PROD')
    def IsoSetSeq_Click(self, s, e):     self._iso_manual_set('ASS_SEQ_NUM_TXT',         'SEQ')
    def IsoSetStatus_Click(self, s, e):  self._iso_manual_set('ASS_STATUS_COD_TXT',      'STATUS')
    def IsoSetRev_Click(self, s, e):     self._iso_manual_set('ASS_REV_COD_TXT',         'REV')
    # New in v9.3 — tokens 1, 2, 3
    def IsoValidate_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return

        # Load allowed code lists from config
        config_path = os.path.join(_cfg, 'project_config.json')
        proj_cfg = {}
        if os.path.exists(config_path):
            try:
                with io.open(config_path, encoding='utf-8') as f:
                    proj_cfg = json.load(f)
            except Exception:
                pass

        # Standard allowed values for ISO tokens (merge config + defaults)
        default_disc = set(['H', 'M', 'E', 'P', 'FP', 'A', 'S', 'C', 'L', 'F', 'ELV', 'LV', 'G'])
        default_status = set(['S0', 'S1', 'S2', 'S3', 'S4', 'S5', 'S6', 'S7'])
        allowed_disc = set(proj_cfg.get('ALLOWED_DISC', [])) or default_disc
        allowed_status = set(proj_cfg.get('ALLOWED_STATUS', [])) or default_status
        loc_codes = set(proj_cfg.get('LOC_CODES', []))
        zone_codes = set(proj_cfg.get('ZONE_CODES', []))

        missing = wrong_tokens = invalid_codes = 0
        dup_map = {}; bad_ids = []; code_issues = []
        for el, _ in elements:
            empty_params = [p for p in self._ISO_PARAMS if not self._iso_get(el, p)]
            if empty_params: missing += 1; bad_ids.append(el.Id)
            tag_val = self._iso_get(el, self._ISO_TAG_FIELD)
            if tag_val:
                if len(tag_val.split('-')) != len(self._ISO_PARAMS): wrong_tokens += 1
                dup_map.setdefault(tag_val, []).append(el.Id)
            # Code-list validation
            disc = self._iso_get(el, 'ASS_DISCIPLINE_COD_TXT')
            if disc and disc.upper() not in allowed_disc:
                invalid_codes += 1
                code_issues.append('DISC={}'.format(disc))
            status = self._iso_get(el, 'ASS_STATUS_COD_TXT')
            if status and status.upper() not in allowed_status:
                invalid_codes += 1
                code_issues.append('STATUS={}'.format(status))
            if loc_codes:
                loc = self._iso_get(el, 'ASS_LOC_TXT')
                if loc and loc not in loc_codes:
                    invalid_codes += 1
            if zone_codes:
                zone = self._iso_get(el, 'ASS_ZONE_TXT')
                if zone and zone not in zone_codes:
                    invalid_codes += 1

        dups = sum(1 for v in dup_map.values() if len(v) > 1)
        has_issues = missing or wrong_tokens or dups or invalid_codes
        status = 'PASS' if not has_issues else 'ISSUES'
        msg = ('VALIDATE — {}\n{} elements\n'
               'Incomplete tokens: {}\nWrong token count: {}\n'
               'Duplicate tags: {}\nInvalid codes: {}').format(
                   status, len(elements), missing, wrong_tokens, dups, invalid_codes)
        if code_issues:
            unique_issues = list(set(code_issues))[:5]
            msg += '\nExamples: ' + ', '.join(unique_issues)
        self._iso_log(msg, '*')
        if bad_ids:
            try:
                proceed = forms.alert(msg + '\n\nSelect offending elements?',
                                      ok=True, cancel=True)
                if proceed:
                    uidoc.Selection.SetElementIds(List[ElementId](bad_ids))
            except Exception:
                pass

    def IsoHighlight_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags ISO Highlight'); t.Start()
        counts = dict(green=0, yellow=0, red=0)
        try:
            total = len(self._ISO_PARAMS)
            for el, _ in elements:
                filled = sum(1 for p in self._ISO_PARAMS if self._iso_get(el, p))
                ogs = OverrideGraphicSettings()
                if filled == total:       col = Color(30, 180, 30);  counts['green']  += 1
                elif filled >= total // 2: col = Color(240, 180, 0); counts['yellow'] += 1
                else:                     col = Color(220, 30, 30);  counts['red']    += 1
                ogs.SetProjectionLineColor(col); ogs.SetCutLineColor(col)
                try: view.SetElementOverrides(el.Id, ogs)
                except Exception: pass
            t.Commit()
        except Exception as ex:
            t.RollBack(); self._iso_log('Highlight error: ' + str(ex)); return
        self._iso_log('HIGHLIGHT\nGreen (complete): {green}\n'
                      'Yellow (partial): {yellow}\nRed (incomplete): {red}'.format(
                          **counts), '*')

    def IsoClearHighlight_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Clear Overrides'); t.Start()
        for el, _ in elements:
            try: view.SetElementOverrides(el.Id, OverrideGraphicSettings())
            except Exception: pass
        t.Commit()
        self._iso_log('Overrides cleared: {} elements'.format(len(elements)))

    def IsoCompleteness_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        total = len(elements)
        lines = ['COMPLETENESS — {} elements\n'.format(total)]
        for pname in self._ISO_PARAMS:
            filled = sum(1 for el, _ in elements if self._iso_get(el, pname))
            pct = int(100 * filled / total)
            bar   = '█' * (pct // 10) + '░' * (10 - pct // 10)
            short = self._ISO_SHORT.get(pname, pname.replace('ASS_','').replace('_TXT',''))
            lines.append('{:<8} {} {:>3}%'.format(short, bar, pct))
        # Include assembled tag field
        filled_tag = sum(1 for el, _ in elements if self._iso_get(el, self._ISO_TAG_FIELD))
        pct_tag = int(100 * filled_tag / total)
        lines.append('\n{:<8} {}  {:>3}% (assembled)'.format(
            'TAG_1', '█' * (pct_tag // 10) + '░' * (10 - pct_tag // 10), pct_tag))
        self._iso_log('\n'.join(lines), '[Chart]')

    def IsoDupFinder_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: return
        dup_map = {}
        for el, _ in elements:
            v = self._iso_get(el, self._ISO_TAG_FIELD)
            if v: dup_map.setdefault(v, []).append(el.Id)
        dups = {k: v for k, v in dup_map.items() if len(v) > 1}
        if not dups: self._iso_log('No duplicate assembled tags found'); return
        lines = ['DUPLICATES: {} tag values\n'.format(len(dups))]
        for tv, ids in list(dups.items())[:10]:
            lines.append('  "{}" ×{}'.format(tv[:40], len(ids)))
        self._iso_log('\n'.join(lines))
        all_dup_ids = [eid for ids in dups.values() for eid in ids]
        if forms.alert('{} duplicates found.\nSelect offending elements?'.format(len(dups)),
                       ok=True, cancel=True):
            uidoc.Selection.SetElementIds(List[ElementId](all_dup_ids))

    def IsoExport_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        elements = self._iso_collect(doc, uidoc)
        if not elements: self._iso_log('No elements in scope'); return
        default_name = 'STINGTags_Register_{}.csv'.format(
            doc.Title.replace(' ', '_').replace('\\', '_'))
        try:
            csv_path = forms.save_file(
                file_ext='csv',
                default_name=default_name,
                title='Save ISO Tag Register')
        except Exception:
            csv_path = None
        if not csv_path: return  # user cancelled
        all_params = self._ISO_PARAMS + [self._ISO_TAG_FIELD]
        headers = ['ElementId', 'Category'] + all_params
        try:
            with io.open(csv_path, 'w', newline='', encoding='utf-8-sig') as f:
                w = _csv.writer(f)
                w.writerow(headers)
                for el, cat_name in elements:
                    row = [str(el.Id.IntegerValue), cat_name]
                    for p in all_params:
                        row.append(self._iso_get(el, p))
                    w.writerow(row)
            self._iso_log('EXPORTED — {} elements  ({} params)\n{}'.format(
                len(elements), len(all_params), csv_path), '④')
            try:
                self._open_file_safe(csv_path)
            except Exception: pass
        except Exception as ex:
            self._iso_log('Export error: ' + str(ex))

    def IsoNaviateConfig_Click(self, s, e):
        """Show ISO 19650 / Naviate GUID configuration.

        Auto-fills GUIDs from param_db.json (data/ folder).
        Saves editable copy to lib/naviate_guids.json.
        """
        _GUID_KEYS = [
            ('ASS_PROJECT_COD_TXT',    'PROJECT'),
            ('ASS_ORIGINATOR_COD_TXT', 'ORIG'),
            ('ASS_VOLUME_COD_TXT',     'VOL'),
            ('ASS_LVL_COD_TXT',        'LVL'),
            ('ASS_DISCIPLINE_COD_TXT', 'DISC'),
            ('ASS_LOC_TXT',            'LOC'),
            ('ASS_ZONE_TXT',           'ZONE'),
            ('ASS_SYSTEM_TYPE_TXT',    'SYS'),
            ('ASS_FUNC_TXT',           'FUNC'),
            ('ASS_PRODCT_COD_TXT',     'PROD'),
            ('ASS_SEQ_NUM_TXT',        'SEQ'),
            ('ASS_STATUS_COD_TXT',     'STATUS'),
            ('ASS_REV_COD_TXT',        'REV'),
            ('ASS_TAG_1_TXT',          'TAG_FIELD'),
        ]

        guid_path = os.path.join(_lib, 'naviate_guids.json')

        # Load existing saved GUIDs
        existing = {}
        if os.path.exists(guid_path):
            try:
                with io.open(guid_path, encoding='utf-8') as f:
                    existing = json.load(f)
            except Exception:
                pass

        # Auto-fill from param_db.json if any key is missing
        param_db = {}
        db_path = os.path.join(_data, 'param_db.json')
        if os.path.exists(db_path):
            try:
                with io.open(db_path, encoding='utf-8') as f:
                    param_db = json.load(f)
            except Exception:
                pass

        # Also try MASTER_PARAMETERS.csv for GUIDs not in param_db
        master_guids = {}
        master_path = os.path.join(_data, 'MASTER_PARAMETERS.csv')
        if os.path.exists(master_path):
            try:
                with io.open(master_path, encoding='utf-8') as f:
                    reader = _csv.DictReader(f)
                    for row in reader:
                        pname = row.get('Parameter_Name', '')
                        guid = row.get('Parameter_GUID', '')
                        if pname and guid and guid.strip():
                            master_guids[pname] = guid.strip()
            except Exception:
                pass

        # Also try MR_PARAMETERS.txt (shared param file format: PARAM\tGUID\tNAME\t...)
        mr_path = os.path.join(_data, 'MR_PARAMETERS.txt')
        if os.path.exists(mr_path):
            try:
                with io.open(mr_path, encoding='utf-8') as f:
                    for line in f:
                        parts = line.strip().split('\t')
                        if len(parts) >= 3 and parts[0] == 'PARAM':
                            guid_val = parts[1].strip()
                            pname_val = parts[2].strip()
                            if pname_val and guid_val:
                                master_guids[pname_val] = guid_val
            except Exception:
                pass

        auto_filled = 0
        for pname, label in _GUID_KEYS:
            if pname not in existing or not existing[pname]:
                # Try param_db first
                entry = param_db.get(pname, {})
                guid_val = entry.get('g', '') if isinstance(entry, dict) else ''
                # Fallback to MASTER_PARAMETERS.csv
                if not guid_val:
                    guid_val = master_guids.get(pname, '')
                if guid_val:
                    existing[pname] = guid_val
                    auto_filled += 1

        # Build summary display
        summary_lines = ['ISO 19650 / Naviate GUID Configuration', '=' * 48, '']
        for pname, label in _GUID_KEYS:
            guid_val = existing.get(pname, '(not set)')
            summary_lines.append('  {:12s}  {}'.format(label, guid_val))
        summary_lines += [
            '',
            'Example result:  PROJ-STING-B01-L02-M-Z1-ZA-MH-HVA-FAN-001-S2-P1',
        ]
        if auto_filled:
            summary_lines.append('')
            summary_lines.append('{} GUIDs auto-filled from param_db.json'.format(auto_filled))
        # Count missing
        not_set = sum(1 for pname, _ in _GUID_KEYS
                      if not existing.get(pname))
        if not_set:
            summary_lines.append('')
            summary_lines.append('{} params not yet in your shared parameter file.'.format(not_set))
            summary_lines.append('Use "Load Shared Params" to create them first.')
        summary_lines += [
            '',
            'Click OK to edit individual GUIDs, or Cancel to close.',
        ]
        summary_text = '\n'.join(summary_lines)

        try:
            proceed = forms.alert(summary_text, title='Naviate Config',
                                  ok=True, cancel=True)
        except Exception:
            proceed = True

        if not proceed:
            # Still save if we auto-filled
            if auto_filled:
                self._save_naviate_guids(guid_path, existing, auto_filled)
            return

        # Edit loop
        updated = dict(existing)
        changed = 0
        for pname, label in _GUID_KEYS:
            current_val = updated.get(pname, '')
            prompt = 'GUID for {}\n  ({})\n\nLeave blank to keep current.'.format(
                pname, label)
            try:
                new_val = forms.ask_for_string(
                    prompt=prompt,
                    default=current_val,
                    title='Naviate GUID: ' + label)
            except Exception:
                new_val = None
            if new_val is None:
                self._iso_log('Naviate config: edit cancelled'); return
            new_val = new_val.strip()
            if new_val and new_val != current_val:
                updated[pname] = new_val
                changed += 1

        total_changes = changed + auto_filled
        if total_changes == 0:
            self._iso_log('Naviate config: no changes made'); return
        self._save_naviate_guids(guid_path, updated, total_changes)

    def _save_naviate_guids(self, guid_path, data, count):
        """Write naviate_guids.json."""
        try:
            with io.open(guid_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
            self._iso_log(
                'Naviate config saved: {} GUIDs\n{}'.format(count, guid_path), '*')
        except Exception as ex:
            self._iso_log('Naviate config save error: ' + str(ex))
    def IsoInspect_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        cur = list(uidoc.Selection.GetElementIds())
        if not cur: self._iso_log('Select an element first'); return
        el = doc.GetElement(cur[0])
        if not el: return
        cat_name = el.Category.Name if el.Category else 'unknown'
        name = getattr(el, 'Name', '') or ''
        lines = ['INSPECT: {} "{}" (ID {})'.format(cat_name, name, el.Id.IntegerValue), '']
        for p in self._ISO_PARAMS:
            v = self._iso_get(el, p)
            short = self._ISO_SHORT.get(p, p.replace('ASS_','').replace('_TXT',''))
            status = '✔' if v else '·'
            lines.append('  {} {:<8} {}'.format(status, short + ':', v or '[empty]'))
        assembled = self._iso_get(el, self._ISO_TAG_FIELD)
        lines.append('\n  ► ASS_TAG_1_TXT:\n    {}'.format(assembled or '[empty]'))
        result = '\n'.join(lines)
        try: self.IsoInspect.Text = result
        except Exception: pass
        self._iso_log(result, '[Find]')

    # ─────────────────────────────────────────────────────────────────────────
    # TAG TEXT ALIGNMENT  (Revit 2022 +)
    # ─────────────────────────────────────────────────────────────────────────

    def _tag_text_align(self, alignment_name):
        """Set text alignment on selected tags.
        Tries three methods: TagTextHorizontalAlignment property (2022+),
        built-in parameter, and family parameter."""
        doc, uidoc = self._fd()
        if not doc: return
        tags = self._sel_tags(doc, uidoc)
        if not tags: self._log('Select tags first'); return
        t = Transaction(doc, 'STINGTags Text Align ' + alignment_name.capitalize())
        t.Start()
        try:
            count = 0
            first_err = None
            for tag in tags:
                ok = False
                # Method 1: TagTextHorizontalAlignment (Revit 2022+)
                if TTHA is not None:
                    try:
                        align_map = {
                            'left':   TTHA.Left,
                            'center': TTHA.Center,
                            'right':  TTHA.Right,
                        }
                        tag.TagTextHorizontalAlignment = align_map[alignment_name]
                        ok = True
                    except Exception as ex1:
                        if first_err is None:
                            first_err = 'TTHA: ' + str(ex1)
                # Method 2: Built-in parameter TAG_TEXT_ALIGNMENT
                if not ok:
                    try:
                        p = tag.LookupParameter('Tag Text Alignment')
                        if p and not p.IsReadOnly:
                            val_map = {'left': 0, 'center': 1, 'right': 2}
                            p.Set(val_map.get(alignment_name, 1))
                            ok = True
                    except Exception:
                        pass
                # Method 3: Try built-in parameter by enum
                if not ok:
                    try:
                        if hasattr(BIP, 'TAG_TEXT_ALIGNMENT'):
                            p = tag.get_Parameter(BIP.TAG_TEXT_ALIGNMENT)
                            if p and not p.IsReadOnly:
                                val_map = {'left': 0, 'center': 1, 'right': 2}
                                p.Set(val_map.get(alignment_name, 1))
                                ok = True
                    except Exception as ex3:
                        if first_err is None:
                            first_err = 'BIP: ' + str(ex3)
                if ok:
                    count += 1
            t.Commit()
            if count > 0:
                self._log('Text {}: {} tags'.format(alignment_name.upper(), count))
            else:
                self._log('Text align: 0/{} | {} | '
                          'Text alignment is a tag family property, '
                          'edit in Family Editor'.format(len(tags), first_err or '?'))
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Text alignment error: ' + str(ex))

    def TagTextAlignLeft_Click(self, s, e):   self._tag_text_align('left')
    def TagTextAlignCenter_Click(self, s, e): self._tag_text_align('center')
    def TagTextAlignRight_Click(self, s, e):  self._tag_text_align('right')

    # ─────────────────────────────────────────────────────────────────────────
    # EXTENDED SELECTION  (Naviate-parity + extras)
    # ─────────────────────────────────────────────────────────────────────────

    def SelByWorkset_Click(self, s, e):
        """Select all elements on a user-chosen workset."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            # (module-level import)
            wsets = [w for w in FilteredWorksetCollector(doc).ToWorksets()
                     if w.Kind == WorksetKind.UserWorkset]
            if not wsets: self._log('No user worksets found'); return
            names = [w.Name for w in wsets]
            chosen = forms.SelectFromList.show(names, title='Select Workset',
                                               multiselect=False)
            if not chosen: return
            ws = next((w for w in wsets if w.Name == chosen), None)
            if not ws: return
            # (module-level import)
            wf  = WorksetFilter(ws.Id)
            ids = list(FilteredElementCollector(doc)
                       .WherePasses(wf).WhereElementIsNotElementType()
                       .ToElementIds())
            uidoc.Selection.SetElementIds(List[ElementId](ids))
            self._log('Workset "{}": {} elements selected'.format(chosen, len(ids)))
        except Exception as ex:
            self._log('Workset select error: ' + str(ex))

    def SelByPhase_Click(self, s, e):
        """Select all elements whose Phase Created matches a chosen phase."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            phases = list(FilteredElementCollector(doc)
                          .OfClass(__import__('Autodesk.Revit.DB', fromlist=['Phase']).Phase)
                          .ToElements())
            if not phases: self._log('No phases found'); return
            names = [p.Name for p in phases]
            chosen = forms.SelectFromList.show(names, title='Select Phase',
                                               multiselect=False)
            if not chosen: return
            phase = next((p for p in phases if p.Name == chosen), None)
            if not phase: return
            matched = [el for el in
                       FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements()
                       if self._elem_phase_created(el, doc) == phase.Id.IntegerValue]
            uidoc.Selection.SetElementIds(
                List[ElementId]([el.Id for el in matched]))
            self._log('Phase "{}": {} elements'.format(chosen, len(matched)))
        except Exception as ex:
            self._log('Phase select error: ' + str(ex))

    def SelByDesignOption_Click(self, s, e):
        """Select all elements in a chosen Design Option."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            # (module-level import)
            opts = list(FilteredElementCollector(doc).OfClass(DesignOption).ToElements())
            if not opts: self._log('No design options found'); return
            names = [o.Name for o in opts]
            chosen = forms.SelectFromList.show(names, title='Select Design Option',
                                               multiselect=False)
            if not chosen: return
            opt = next((o for o in opts if o.Name == chosen), None)
            if not opt: return
            matched = [el for el in
                       FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements()
                       if self._elem_design_option(el) == opt.Id.IntegerValue]
            uidoc.Selection.SetElementIds(
                List[ElementId]([el.Id for el in matched]))
            self._log('Design Option "{}": {} elements'.format(chosen, len(matched)))
        except Exception as ex:
            self._log('Design Option select error: ' + str(ex))

    def SelByGroup_Click(self, s, e):
        """Select all members of groups that contain any selected element."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            # (module-level import)
            cur_ids = set(eid.IntegerValue for eid in uidoc.Selection.GetElementIds())
            groups = list(FilteredElementCollector(doc).OfClass(Group).ToElements())
            hit_groups = [g for g in groups
                          if any(mid.IntegerValue in cur_ids
                                 for mid in g.GetMemberIds())]
            if not hit_groups:
                self._log('No groups contain the current selection'); return
            all_member_ids = []
            for g in hit_groups:
                all_member_ids.extend(list(g.GetMemberIds()))
            uidoc.Selection.SetElementIds(List[ElementId](all_member_ids))
            self._log('Groups ({}): {} members selected'.format(
                len(hit_groups), len(all_member_ids)))
        except Exception as ex:
            self._log('Group select error: ' + str(ex))

    def SelByAssembly_Click(self, s, e):
        """Select all members of assemblies that contain any selected element."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            # (module-level import)
            cur_ids = set(eid.IntegerValue for eid in uidoc.Selection.GetElementIds())
            assemblies = list(FilteredElementCollector(doc)
                              .OfClass(AssemblyInstance).ToElements())
            hit = [a for a in assemblies
                   if any(mid.IntegerValue in cur_ids for mid in a.GetMemberIds())]
            if not hit:
                self._log('No assemblies contain the current selection'); return
            member_ids = []
            for a in hit:
                member_ids.extend(list(a.GetMemberIds()))
            uidoc.Selection.SetElementIds(List[ElementId](member_ids))
            self._log('Assemblies ({}): {} members'.format(len(hit), len(member_ids)))
        except Exception as ex:
            self._log('Assembly select error: ' + str(ex))

    def SelByConnected_Click(self, s, e):
        """Select MEP elements directly connected to the current selection."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            # (module-level import)
            # (module-level import)
            # (module-level import)
            cur = [doc.GetElement(eid) for eid in uidoc.Selection.GetElementIds()
                   if doc.GetElement(eid)]
            if not cur: self._log('Select MEP elements first'); return
            connected_ids = set()
            for el in cur:
                try:
                    mgr = None
                    if hasattr(el, 'ConnectorManager'):
                        mgr = el.ConnectorManager
                    elif hasattr(el, 'MEPModel') and el.MEPModel:
                        mgr = el.MEPModel.ConnectorManager
                    if mgr:
                        for conn in mgr.Connectors:
                            try:
                                for ref_conn in conn.AllRefs:
                                    ref_el = ref_conn.Owner
                                    if ref_el and ref_el.Id != el.Id:
                                        connected_ids.add(ref_el.Id)
                            except Exception:
                                pass
                except Exception:
                    pass
            if not connected_ids:
                self._log('No connected MEP elements found'); return
            uidoc.Selection.SetElementIds(List[ElementId](list(connected_ids)))
            self._log('Connected MEP elements: {}'.format(len(connected_ids)))
        except Exception as ex:
            self._log('Connected select error: ' + str(ex))

    def SelByBBox_Click(self, s, e):
        """Select all view-elements whose bounding box overlaps any selected element."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            # (module-level import)
            cur = [doc.GetElement(eid) for eid in uidoc.Selection.GetElementIds()
                   if doc.GetElement(eid)]
            if not cur: self._log('Select seed elements first'); return
            view = doc.ActiveView
            # Union bounding box of selection
            xs, ys, zs, xe, ye, ze = [], [], [], [], [], []
            for el in cur:
                bb = el.get_BoundingBox(view)
                if bb:
                    xs.append(bb.Min.X); ys.append(bb.Min.Y); zs.append(bb.Min.Z)
                    xe.append(bb.Max.X); ye.append(bb.Max.Y); ze.append(bb.Max.Z)
            if not xs: self._log('No bounding boxes found'); return
            outline = Outline(XYZ(min(xs)-0.01, min(ys)-0.01, min(zs)-0.01),
                              XYZ(max(xe)+0.01, max(ye)+0.01, max(ze)+0.01))
            flt = BoundingBoxIntersectsFilter(outline)
            matched = list(FilteredElementCollector(doc, view.Id)
                           .WherePasses(flt).WhereElementIsNotElementType().ToElementIds())
            uidoc.Selection.SetElementIds(List[ElementId](matched))
            self._log('Bounding box overlap: {} elements'.format(len(matched)))
        except Exception as ex:
            self._log('BBox select error: ' + str(ex))

    def SelPinned_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        pinned = [el for el in FilteredElementCollector(doc, view.Id)
                  .WhereElementIsNotElementType().ToElements()
                  if getattr(el, 'Pinned', False)]
        uidoc.Selection.SetElementIds(List[ElementId]([el.Id for el in pinned]))
        self._log('Pinned elements: {} selected'.format(len(pinned)))

    def SelUnpinned_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        unpinned = [el for el in FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType().ToElements()
                    if not getattr(el, 'Pinned', True)]
        uidoc.Selection.SetElementIds(List[ElementId]([el.Id for el in unpinned]))
        self._log('Unpinned elements: {} selected'.format(len(unpinned)))

    # ─────────────────────────────────────────────────────────────────────────
    # VIEW CONTROLS  — temporary hide / isolate / permanent hide / overrides
    # ─────────────────────────────────────────────────────────────────────────

    def SelIsolate_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        try:
            t = Transaction(doc, 'STINGTags Temp Isolate'); t.Start()
            doc.ActiveView.IsolateElementsTemporary(List[ElementId](ids))
            t.Commit()
            self._log('Isolated {} elements temporarily'.format(len(ids)), '[Find]')
        except Exception as ex:
            try: t.RollBack()
            except Exception: pass
            self._log('Isolate error: ' + str(ex))

    def SelHide_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        try:
            t = Transaction(doc, 'STINGTags Temp Hide'); t.Start()
            doc.ActiveView.HideElementsTemporary(List[ElementId](ids))
            t.Commit()
            self._log('Temporarily hidden: {} elements'.format(len(ids)), '*')
        except Exception as ex:
            try: t.RollBack()
            except Exception: pass
            self._log('Hide error: ' + str(ex))

    def SelRevealHidden_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            view = doc.ActiveView
            t = Transaction(doc, 'STINGTags Reveal Hidden'); t.Start()
            try:
                # Toggle reveal hidden mode
                if hasattr(view, 'IsInTemporaryViewMode'):
                    try:
                        from Autodesk.Revit.DB import TemporaryViewMode
                        in_reveal = view.IsInTemporaryViewMode(TemporaryViewMode.RevealHiddenElements)
                    except Exception:
                        in_reveal = False
                else:
                    in_reveal = False
                if in_reveal:
                    try:
                        from Autodesk.Revit.DB import TemporaryViewMode
                        view.DisableTemporaryViewMode(TemporaryViewMode.RevealHiddenElements)
                    except Exception:
                        view.DisableTemporaryViewMode(
                            view.TemporaryViewMode if hasattr(view, 'TemporaryViewMode') else 0)
                    self._log('Reveal Hidden mode OFF', '*')
                else:
                    view.EnableRevealHiddenMode()
                    self._log('Reveal Hidden mode ON (magenta elements are hidden)', '*')
            except Exception as ex2:
                # Fallback for older Revit: just try EnableRevealHiddenMode
                try:
                    view.EnableRevealHiddenMode()
                    self._log('Reveal Hidden mode toggled', '*')
                except Exception:
                    raise ex2
            t.Commit()
        except Exception as ex:
            try: t.RollBack()
            except Exception: pass
            self._log('Reveal hidden error: ' + str(ex))

    def SelResetTemp_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            t = Transaction(doc, 'STINGTags Reset Temp View'); t.Start()
            try:
                from Autodesk.Revit.DB import TemporaryViewMode
                doc.ActiveView.DisableTemporaryViewMode(
                    TemporaryViewMode.TemporaryHideIsolate)
            except ImportError:
                # Fallback: use integer value directly
                doc.ActiveView.DisableTemporaryViewMode(1)
            t.Commit()
            self._log('Temporary hide/isolate reset', '↩')
        except Exception as ex:
            try: t.RollBack()
            except Exception: pass
            self._log('Reset temp error: ' + str(ex))

    def ViewPermHide_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        t = Transaction(doc, 'STINGTags Permanent Hide'); t.Start()
        try:
            doc.ActiveView.HideElements(List[ElementId](ids))
            t.Commit()
            self._log('Permanently hidden: {} elements'.format(len(ids)))
        except Exception as ex:
            t.RollBack(); self._log('Perm hide error: ' + str(ex))

    def ViewPermUnhide_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Unhide All'); t.Start()
        try:
            unhidden = 0
            # Method 1: collect hidden elements in view
            try:
                hidden = []
                for el in FilteredElementCollector(doc, view.Id)\
                        .WhereElementIsNotElementType().ToElements():
                    try:
                        if el.IsHidden(view): hidden.append(el.Id)
                    except Exception: pass
                if hidden:
                    view.UnhideElements(List[ElementId](hidden))
                    unhidden += len(hidden)
            except Exception: pass

            # Method 2: also unhide hidden categories
            try:
                for cat in doc.Settings.Categories:
                    try:
                        if view.GetCategoryHidden(cat.Id):
                            view.SetCategoryHidden(cat.Id, False)
                            unhidden += 1
                    except Exception: pass
            except Exception: pass

            # Method 3: also reset temporary hide/isolate
            try:
                from Autodesk.Revit.DB import TemporaryViewMode
                view.DisableTemporaryViewMode(
                    TemporaryViewMode.TemporaryHideIsolate)
            except Exception:
                try: view.DisableTemporaryViewMode(1)
                except Exception: pass

            t.Commit()
            self._log('Restored: {} elements/categories unhidden, temp mode reset'.format(unhidden))
        except Exception as ex:
            try: t.RollBack()
            except Exception: pass
            self._log('Unhide error: ' + str(ex))

    def ViewUnhideCategory_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        cur = [doc.GetElement(eid) for eid in uidoc.Selection.GetElementIds()
               if doc.GetElement(eid)]
        if not cur: self._log('Select an element to identify its category'); return
        view = doc.ActiveView
        cats = set()
        for el in cur:
            if el.Category: cats.add(el.Category.Id)
        if not cats: self._log('No categories found'); return
        t = Transaction(doc, 'STINGTags Unhide Category'); t.Start()
        try:
            for cat_id in cats:
                try:
                    view.SetCategoryHidden(cat_id, False)
                except Exception:
                    pass
            t.Commit()
            self._log('Unhid {} category/categories'.format(len(cats)))
        except Exception as ex:
            t.RollBack(); self._log('Unhide cat error: ' + str(ex))

    def ViewHalftone_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Halftone ON'); t.Start()
        ogs = OverrideGraphicSettings()
        ogs.SetHalftone(True)
        for eid in ids:
            try: view.SetElementOverrides(eid, ogs)
            except Exception: pass
        t.Commit()
        self._log('Halftone ON: {} elements'.format(len(ids)))

    def ViewHalftoneOff_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Halftone OFF'); t.Start()
        ogs = OverrideGraphicSettings()
        ogs.SetHalftone(False)
        for eid in ids:
            try: view.SetElementOverrides(eid, ogs)
            except Exception: pass
        t.Commit()
        self._log('Halftone OFF: {} elements'.format(len(ids)))

    def ViewResetOverride_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Reset Overrides'); t.Start()
        for eid in ids:
            try: view.SetElementOverrides(eid, OverrideGraphicSettings())
            except Exception: pass
        t.Commit()
        self._log('Graphic overrides reset: {} elements'.format(len(ids)))

    # ─────────────────────────────────────────────────────────────────────────
    # COLOURISER
    # ─────────────────────────────────────────────────────────────────────────

    def _hex_to_color(self, hex_str):
        """Parse '#RRGGBB' → Revit Color. Returns None on bad input."""
        try:
            h = hex_str.lstrip('#')
            if len(h) != 6:
                return None
            r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
            return Color(r, g, b)
        except Exception:
            return None

    def _hex_to_media_brush(self, hex_str):
        """Parse '#RRGGBB' → WPF SolidColorBrush for UI preview updates."""
        try:
            h = hex_str.lstrip('#')
            if len(h) != 6:
                return None
            r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
            if MColor:
                return SolidColorBrush(MColor.FromRgb(r, g, b))
        except Exception:
            pass
        return None

    def _extract_swatch_hex(self, e):
        """Extract '#RRGGBB' from a routed swatch button click event.

        Tries e.Source first (the Button that raised the event), then walks
        the visual tree from e.OriginalSource upward. This handles WPF
        ControlTemplates where OriginalSource is a Border/ContentPresenter
        inside the template and .Parent does not traverse correctly."""
        # Method 1: e.Source is normally the Button itself
        try:
            tag_val = str(e.Source.Tag)
            if tag_val and tag_val.startswith('#') and len(tag_val) == 7:
                return tag_val
        except Exception:
            pass
        # Method 2: Walk visual tree from OriginalSource
        try:
            src = e.OriginalSource
            for _ in range(12):
                try:
                    tag_val = str(src.Tag)
                    if tag_val and tag_val.startswith('#') and len(tag_val) == 7:
                        return tag_val
                except Exception:
                    pass
                # Try WPF visual tree parent (works inside ControlTemplates)
                try:
                    from System.Windows.Media import VisualTreeHelper
                    src = VisualTreeHelper.GetParent(src)
                    if src is None:
                        break
                except Exception:
                    # Fallback to logical tree
                    try:
                        src = src.Parent
                        if src is None:
                            break
                    except Exception:
                        break
        except Exception:
            pass
        return None

    def _get_solid_fill_id(self, doc):
        """Find the solid fill FillPatternElement Id (cached per call)."""
        try:
            for fp in FilteredElementCollector(doc) \
                    .OfClass(FillPatternElement).ToElements():
                try:
                    if fp.GetFillPattern().IsSolidFill:
                        return fp.Id
                except Exception:
                    pass
        except Exception:
            pass
        return None

    def _apply_fill_to_ogs(self, ogs, fill_col, solid_fp_id):
        """Set surface foreground fill on an existing OGS (Revit 2019+/legacy)."""
        try:
            ogs.SetSurfaceForegroundPatternColor(fill_col)
            ogs.SetSurfaceForegroundPatternVisible(True)
            if solid_fp_id:
                ogs.SetSurfaceForegroundPatternId(solid_fp_id)
            return True
        except AttributeError:
            try:
                ogs.SetProjectionFillColor(fill_col)
                ogs.SetProjectionFillPatternVisible(True)
                if solid_fp_id:
                    ogs.SetProjectionFillPatternId(solid_fp_id)
                return True
            except Exception:
                return False

    def _apply_outline_to_ogs(self, ogs, outline_col):
        """Set projection + cut line colour on an existing OGS."""
        try:
            ogs.SetProjectionLineColor(outline_col)
        except Exception:
            pass
        try:
            ogs.SetCutLineColor(outline_col)
        except Exception:
            pass

    def _colour_apply(self, doc, uidoc, fill_hex, outline_hex=None):
        """Apply fill and/or outline colour overrides to selection.

        IMPORTANT: Gets existing OGS per element to PRESERVE any previously
        applied outline, transparency, or pattern overrides."""
        ids = list(uidoc.Selection.GetElementIds())
        if not ids:
            self._log('Select elements first')
            return 0
        view = doc.ActiveView
        fill_col = self._hex_to_color(fill_hex) if fill_hex else None
        outline_col = self._hex_to_color(outline_hex) if outline_hex else None
        solid_fp_id = self._get_solid_fill_id(doc) if fill_col else None
        t = Transaction(doc, 'STINGTags Colour Override')
        t.Start()
        count = 0
        first_err = None
        for eid in ids:
            try:
                # Get EXISTING overrides so outline/transparency are preserved
                ogs = view.GetElementOverrides(eid)
                if fill_col:
                    self._apply_fill_to_ogs(ogs, fill_col, solid_fp_id)
                if outline_col:
                    self._apply_outline_to_ogs(ogs, outline_col)
                view.SetElementOverrides(eid, ogs)
                count += 1
            except Exception as ex:
                if first_err is None:
                    first_err = str(ex)
        t.Commit()
        if count == 0 and first_err:
            self._log('Colour failed: ' + first_err)
        return count

    def _update_colour_previews(self):
        """Update all colour preview swatches in the VIEW tab."""
        try:
            fb = self._hex_to_media_brush(self._active_fill_hex)
            if fb:
                self.FillPreview.Background = fb
        except Exception:
            pass
        try:
            ob = self._hex_to_media_brush(self._active_outline_hex)
            if ob:
                self.OutlinePreview.Background = ob
        except Exception:
            pass
        # Update gradient preview buttons
        try:
            gfb = self._hex_to_media_brush(self._grad_from_hex)
            if gfb:
                self.GradFromBtn.Background = gfb
        except Exception:
            pass
        try:
            gtb = self._hex_to_media_brush(self._grad_to_hex)
            if gtb:
                self.GradToBtn.Background = gtb
        except Exception:
            pass

    def ColourSwatch_Click(self, s, e):
        """Fill colour swatch clicked.

        When gradient pick mode is active (_grad_picking = 'from' or 'to'),
        captures the chosen colour into _grad_from_hex / _grad_to_hex without
        applying any fill override. Otherwise applies the colour immediately.
        """
        hex_col = self._extract_swatch_hex(e)
        if not hex_col:
            self._log('Swatch: could not read colour from button')
            return

        # ── Gradient pick-mode intercept ─────────────────────────────────────
        if getattr(self, '_grad_picking', None) == 'from':
            self._grad_from_hex = hex_col
            self._grad_picking = None
            self._update_colour_previews()
            self._log('Gradient FROM: {}'.format(hex_col), '➵')
            return
        if getattr(self, '_grad_picking', None) == 'to':
            self._grad_to_hex = hex_col
            self._grad_picking = None
            self._update_colour_previews()
            self._log('Gradient TO: {}'.format(hex_col), '➶')
            return

        # ── Normal fill apply ─────────────────────────────────────────────────
        doc, uidoc = self._fd()
        if not doc: return
        self._active_fill_hex = hex_col
        self._update_colour_previews()
        outline = self._active_outline_hex if self._outline_enabled else None
        n = self._colour_apply(doc, uidoc, hex_col, outline)
        self._log('Fill {}: {} elements'.format(hex_col, n), '*')

    def OutlineSwatch_Click(self, s, e):
        """Outline colour swatch clicked — store + apply outline to selection."""
        hex_col = self._extract_swatch_hex(e)
        if not hex_col:
            self._log('Outline swatch: could not read colour')
            return
        self._active_outline_hex = hex_col
        self._update_colour_previews()
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids:
            self._log('Outline saved: {} (select elements then apply)'.format(hex_col))
            return
        view = doc.ActiveView
        col = self._hex_to_color(hex_col)
        if not col: return
        t = Transaction(doc, 'STINGTags Outline Override')
        t.Start()
        try:
            count = 0
            for eid in ids:
                try:
                    # Preserve existing fill/transparency
                    ogs = view.GetElementOverrides(eid)
                    self._apply_outline_to_ogs(ogs, col)
                    view.SetElementOverrides(eid, ogs)
                    count += 1
                except Exception:
                    pass
            t.Commit()
            self._log('Outline {}: {} elements'.format(hex_col, count), '*')
        except Exception as ex:
            if t.HasStarted():
                t.RollBack()
            self._log('Outline error: ' + str(ex))

    def ColourToggleOutline_Click(self, s, e):
        """Toggle whether a separate outline colour is applied alongside fill."""
        self._outline_enabled = not self._outline_enabled
        label = 'Custom: ON' if self._outline_enabled else 'Custom: OFF'
        try:
            self.ColourOutlineToggleBtn.Content = label
            opacity = 1.0 if self._outline_enabled else 0.4
            self.OutlineSwatchPanel.IsEnabled = self._outline_enabled
            self.OutlineSwatchPanel.Opacity = opacity
        except Exception:
            pass
        self._log('Outline colour: {}'.format(
            'ON — click outline swatch to set' if self._outline_enabled else 'OFF'))

    def ColourResetFill_Click(self, s, e):
        """Remove fill override from selection (preserves outline)."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Reset Fill')
        t.Start()
        count = 0
        for eid in ids:
            try:
                ogs = view.GetElementOverrides(eid)
                # Reset fill pattern and colour
                try:
                    ogs.SetSurfaceForegroundPatternId(ElementId.InvalidElementId)
                    ogs.SetSurfaceForegroundPatternVisible(False)
                except AttributeError:
                    try:
                        ogs.SetProjectionFillPatternId(ElementId.InvalidElementId)
                        ogs.SetProjectionFillPatternVisible(False)
                    except Exception:
                        pass
                view.SetElementOverrides(eid, ogs)
                count += 1
            except Exception:
                pass
        t.Commit()
        self._log('Fill reset: {} elements'.format(count))

    def ColourResetOutline_Click(self, s, e):
        """Remove projection/cut line colour override from selection."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Reset Outline')
        t.Start()
        count = 0
        for eid in ids:
            try:
                ogs = view.GetElementOverrides(eid)
                # Reset line colour to invalid (= use default)
                try:
                    inv = Color.InvalidColorValue
                    ogs.SetProjectionLineColor(inv)
                    ogs.SetCutLineColor(inv)
                except Exception:
                    # Fallback: try with explicit invalid colour
                    try:
                        ogs.SetProjectionLineColor(Color(0, 0, 0))
                        ogs.SetCutLineColor(Color(0, 0, 0))
                    except Exception:
                        pass
                view.SetElementOverrides(eid, ogs)
                count += 1
            except Exception:
                pass
        t.Commit()
        self._log('Outline reset: {} elements'.format(count))

    def ColourClear_Click(self, s, e):
        """Remove ALL graphic overrides from every element in the active view."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        all_ids = list(FilteredElementCollector(doc, view.Id)
                       .WhereElementIsNotElementType().ToElementIds())
        t = Transaction(doc, 'STINGTags Clear All Overrides'); t.Start()
        cleared = 0
        for eid in all_ids:
            try:
                view.SetElementOverrides(eid, OverrideGraphicSettings())
                cleared += 1
            except Exception:
                pass
        t.Commit()
        self._log('All colour overrides cleared: {} elements'.format(cleared), '*')

    def ColourByAuto_Click(self, s, e):
        """Auto-colour all elements in view by the chosen grouping scheme."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            item = self.ColourByCombo.SelectedItem
            scheme = str(item.Content) if item else 'By Category'
        except Exception:
            scheme = 'By Category'

        # ── By Selection: apply fill to current selection only ──────────────
        if scheme == 'By Selection':
            self._colour_by_selection(doc, uidoc)
            return

        view  = doc.ActiveView
        elems = list(FilteredElementCollector(doc, view.Id)
                     .WhereElementIsNotElementType().ToElements())
        # Group elements
        groups = {}
        _param_cache = {}   # store per-element key
        range_min = range_max = pname_range = None

        if 'Value Range' in scheme:
            pname_range = forms.ask_for_string('Parameter name for range colouring:')
            if not pname_range: return
            try:
                range_min = float(forms.ask_for_string('Min value:') or '0')
                range_max = float(forms.ask_for_string('Max value:') or '100')
            except Exception:
                range_min, range_max = 0.0, 100.0

        for el in elems:
            try:
                if scheme == 'By Category':
                    key = el.Category.Name if el.Category else 'Unknown'
                elif scheme == 'By Level':
                    lp = el.LookupParameter('Level') or el.LookupParameter('Reference Level')
                    key = lp.AsValueString() if lp else 'No Level'
                elif scheme == 'By Phase':
                    key = self._elem_phase_name(el, doc)
                elif scheme == 'By Workset':
                    wp = el.LookupParameter('Workset')
                    key = wp.AsValueString() if wp else 'Unknown'
                elif scheme == 'By System':
                    sp = el.LookupParameter('System Name') or el.LookupParameter('System Type')
                    key = sp.AsValueString() if sp else 'No System'
                elif 'Value Range' in scheme and pname_range:
                    p = el.LookupParameter(pname_range)
                    try:
                        val = p.AsDouble() if p else 0
                    except Exception:
                        val = 0
                    # Normalise 0-1 for gradient
                    rng = range_max - range_min if range_max != range_min else 1
                    t_  = max(0.0, min(1.0, (val - range_min) / rng))
                    _param_cache[el.Id.IntegerValue] = t_
                    key = str(round(t_ * 10) / 10)  # bucket 0.0 … 1.0
                elif 'Parameter' in scheme:
                    pname = forms.ask_for_string('Parameter name to colour by:')
                    if not pname: return
                    p = el.LookupParameter(pname)
                    key = p.AsValueString() if p else '[empty]'
                    scheme = 'By ' + pname
                else:
                    key = 'Unknown'
                groups.setdefault(key, []).append(el)
            except Exception:
                pass

        # Assign colours
        PALETTE = [
            '#F44336','#E91E63','#9C27B0','#3F51B5','#2196F3',
            '#00BCD4','#4CAF50','#8BC34A','#FF9800','#FF5722',
            '#795548','#607D8B','#FFEB3B','#009688','#673AB7',
        ]
        # (module-level import)
        solid_id = None
        for fp in FilteredElementCollector(doc).OfClass(FillPatternElement).ToElements():
            try:
                if fp.GetFillPattern().IsSolidFill: solid_id = fp.Id; break
            except Exception: pass

        t = Transaction(doc, 'STINGTags Auto Colour'); t.Start()

        if 'Value Range' in scheme and _param_cache:
            # Gradient green→red for normalised range
            from_col = self._hex_to_color('#4CAF50')
            to_col   = self._hex_to_color('#F44336')
            def _lerp(c1, c2, t_):
                return Color(int(c1.Red+(c2.Red-c1.Red)*t_),
                             int(c1.Green+(c2.Green-c1.Green)*t_),
                             int(c1.Blue+(c2.Blue-c1.Blue)*t_))
            for el in elems:
                t_ = _param_cache.get(el.Id.IntegerValue, 0.0)
                col = _lerp(from_col, to_col, t_)
                try:
                    ogs = OverrideGraphicSettings()
                    if solid_id:
                        ogs.SetSurfaceForegroundPatternColor(col)
                        ogs.SetSurfaceForegroundPatternVisible(True)
                        ogs.SetSurfaceForegroundPatternId(solid_id)
                    view.SetElementOverrides(el.Id, ogs)
                except Exception: pass
        else:
            for idx, (key, members) in enumerate(sorted(groups.items())):
                hex_col = PALETTE[idx % len(PALETTE)]
                col = self._hex_to_color(hex_col)
                for el in members:
                    try:
                        ogs = OverrideGraphicSettings()
                        if solid_id:
                            ogs.SetSurfaceForegroundPatternColor(col)
                            ogs.SetSurfaceForegroundPatternVisible(True)
                            ogs.SetSurfaceForegroundPatternId(solid_id)
                        ogs.SetProjectionLineColor(col)
                        view.SetElementOverrides(el.Id, ogs)
                    except Exception: pass
        t.Commit()
        self._log('Auto colour ({}) — {} groups, {} elements'.format(
            scheme, len(groups), len(elems)), '*')


    # ─────────────────────────────────────────────────────────────────────────
    # Help ──────────────────────────────────────────────────────────────────
    def Help_Click(self, s, e):
        self._log(
            'STINGTAGS v9.3  —  Smart Tag Intelligence Engine\n\n'
            'MODELESS — Revit stays active.\n'
            '[Pin] Pin = always-on-top toggle.\n'
            'Esc: 1st deselects; 2nd (empty) offers close.\n'
            '↑↓←→ nudge tags when panel has focus.\n'
            'Sel badge polls live every 500 ms.\n\n'
            'SELECT\n'
            '  AI: Predict / Similar / Chain / Cluster / Pattern / Boundary\n'
            '  Category: 14 MEP/arch shortcuts + ALL + View-cats pickers\n'
            '  Spatial: Near / Room / Level / Quad / Edge / Grid / BBox\n'
            '  State: Untagged / Tagged / Empty Mark / Visible / Pinned / Unpinned\n'
            '  Project: Workset / Phase / Design Option / Group / Assembly / Connected\n'
            '  Inline param+operator+value filter\n'
            '  Isolate / Hide / Reveal Hidden / Reset temporary\n'
            '  M1–M3 memory + Boolean ops\n\n'
            'ORGANISE\n'
            '  Orientation H/V + FlipH/V + SmartOrient\n'
            '  TAG TEXT ALIGNMENT: ◀L / ≡C / R▶ (Revit 2022+)\n'
            '  Nudge / Offset Close/Far / SmartOffset\n'
            '  Align + Distribute + grid/circle/stack/radial layouts\n'
            '  Leaders: add/remove/snap/uncross/short/med/long\n'
            '  Batch Sheets: all viewports, one tx per view\n\n'
            'CREATE  (ISO 19650 — 13 tokens + assembled tag)\n'
            '  PROJECT/ORIG/VOL/LVL/DISC/LOC/ZONE/SYS/FUNC/PROD/SEQ/STATUS/REV\n'
            '  Naviate Config: full 14-param Combine Param Values formula\n'
            '  Validate / Completeness / Duplicates / Export CSV (15 columns)\n\n'
            'VIEW\n'
            '  Colouriser: 17 fill swatches + optional separate outline colour\n'
            '  Auto-colour: Category / Level / Phase / Workset / System / Param\n'
            '  Temp isolate / hide / reveal / reset\n'
            '  Perm hide / unhide all / unhide category\n'
            '  Halftone ON/OFF / Reset graphic overrides', '*')


    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — SMART RENUMBER (reading order)
    # ─────────────────────────────────────────────────────────────────────────
    def SmartRenumber_Click(self, s, e):
        """Sequential Mark renumber sorted top→bottom, left→right (reading order)."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        elems = [doc.GetElement(eid) for eid in uidoc.Selection.GetElementIds()]
        if not elems:
            elems = list(FilteredElementCollector(doc, view.Id)
                         .WhereElementIsNotElementType().ToElements())
        if not elems: self._log('No elements to renumber'); return
        # Sort: primary Y descending (top first), secondary X ascending (left first)
        def _sort_key(el):
            c = self._bb_center(el, view)
            return (round(-c.Y, 2), round(c.X, 2)) if c else (0, 0)
        sorted_els = sorted(elems, key=_sort_key)
        start = forms.ask_for_string('Starting number:', default='1')
        if start is None: return
        try: n = int(start)
        except ValueError: self._log('Invalid starting number'); return
        t = Transaction(doc, 'STINGTags Smart Renumber'); t.Start()
        count = 0
        for el in sorted_els:
            try:
                mp = el.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) or el.LookupParameter('Mark')
                if mp and not mp.IsReadOnly:
                    mp.Set(str(n)); n += 1; count += 1
            except Exception: pass
        t.Commit()
        self._log('Renumbered {} elements (reading order, start {})'.format(count, start), '*')

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — TAG CLONE
    # ─────────────────────────────────────────────────────────────────────────
    def TagClone_Click(self, s, e):
        """Copy family, leader state, orientation, text-align from first selected tag
        to all other selected tags of the same category."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        tags = [doc.GetElement(eid) for eid in ids
                if isinstance(doc.GetElement(eid), IndependentTag)]
        if len(tags) < 2: self._log('Select 2+ tags — first is the source'); return
        src = tags[0]
        try:
            src_sym_id = src.GetTypeId()
            src_leader = src.HasLeader
            src_orient = src.TagOrientation
            try:
                src_align = src.TagTextHorizontalAlignment
                has_align = True
            except Exception:
                has_align = False
        except Exception as ex:
            self._log('Cannot read source tag: ' + str(ex)); return
        t = Transaction(doc, 'STINGTags Clone Tag Settings'); t.Start()
        count = 0
        for tag in tags[1:]:
            try:
                if src_sym_id and src_sym_id != ElementId.InvalidElementId:
                    tag.ChangeTypeId(src_sym_id)
                tag.HasLeader   = src_leader
                tag.TagOrientation = src_orient
                if has_align:
                    try: tag.TagTextHorizontalAlignment = src_align
                    except Exception: pass
                count += 1
            except Exception: pass
        t.Commit()
        self._log('Cloned settings to {} tags'.format(count), '[List]')

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — MULTI-VIEW AUDIT CSV
    # ─────────────────────────────────────────────────────────────────────────
    def TagAuditMultiView_Click(self, s, e):
        """Write tag audit CSV spanning every view on every sheet (ViewName column added)."""
        doc, uidoc = self._fd()
        if not doc: return
        sheets = list(FilteredElementCollector(doc).OfClass(ViewSheet).ToElements())
        if not sheets: self._log('No sheets in project'); return
        rows = []
        for sheet in sheets:
            for vp_id in sheet.GetAllViewports():
                vp = doc.GetElement(vp_id)
                if not vp: continue
                view = doc.GetElement(vp.ViewId)
                if not view: continue
                try:
                    tags = list(FilteredElementCollector(doc, view.Id)
                                .OfClass(IndependentTag).ToElements())
                    pts  = [self._bb_center(t, view) for t in tags]
                    for i, tag in enumerate(tags):
                        pt = pts[i]
                        nn_dist = min(
                            (pts[j].DistanceTo(pt) for j in range(len(pts)) if j != i),
                            default=0
                        ) if len(pts) > 1 else 0
                        rows.append({
                            'Sheet':    sheet.SheetNumber + ' ' + sheet.Name,
                            'View':     view.Name,
                            'Id':       tag.Id.IntegerValue,
                            'Category': str(tag.Category.Name) if tag.Category else '',
                            'HasLeader':str(tag.HasLeader),
                            'Orientation': str(tag.TagOrientation),
                            'X':        round(pt.X, 4) if pt else '',
                            'Y':        round(pt.Y, 4) if pt else '',
                            'NN_dist_ft': round(nn_dist, 4),
                        })
                except Exception: pass
        if not rows: self._log('No tags found on sheets'); return
        fname = 'STINGTags_MultiViewAudit_{}.csv'.format(doc.Title.replace(' ', '_'))
        path  = os.path.join(tempfile.gettempdir(), fname)
        fields = ['Sheet','View','Id','Category','HasLeader','Orientation','X','Y','NN_dist_ft']
        try:
            with io.open(path, 'w', newline='') as f:
                w = _csv.DictWriter(f, fieldnames=fields)
                w.writeheader(); w.writerows(rows)
            try:
                self._open_folder(path)
            except Exception: pass
            self._log('Multi-view audit: {} tags, {} views\n{}'.format(
                len(rows), len(set(r['View'] for r in rows)), path), '[Chart]')
        except Exception as ex:
            self._log('Audit export error: ' + str(ex))

    # ─────────────────────────────────────────────────────────────────────────
    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 FIX — LIVE PROJECT-PARAMETER DROPDOWN
    # ─────────────────────────────────────────────────────────────────────────
    def RefreshLiveParams_Click(self, s, e):
        """Collect every parameter name present in the active view, populate LiveParamCombo."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        # (module-level import)
        seen = {}  # pname -> dtype string
        view = doc.ActiveView
        for el in FilteredElementCollector(doc, view.Id)\
                  .WhereElementIsNotElementType().ToElements():
            try:
                for p in el.Parameters:
                    try:
                        pname = p.Definition.Name
                        if pname in seen: continue
                        st = p.StorageType
                        dtype = {
                            StorageType.String:    'TEXT',
                            StorageType.Double:    'NUMBER',
                            StorageType.Integer:   'INTEGER',
                            StorageType.ElementId: 'ELEMENTID',
                        }.get(st, 'TEXT')
                        seen[pname] = dtype
                    except Exception: pass
            except Exception: pass
        self.LiveParamCombo.Items.Clear()
        ph = CBI(); ph.Content = '— {} params in view —'.format(len(seen))
        self.LiveParamCombo.Items.Add(ph)
        for pname in sorted(seen.keys()):
            ci = CBI()
            ci.Content = '{} [{}]'.format(pname, seen[pname])
            ci.Tag     = pname
            self.LiveParamCombo.Items.Add(ci)
        self.LiveParamCombo.SelectedIndex = 0
        self._log('Live params: {} found in view'.format(len(seen)), '↻')

    def LiveParamCombo_SelectionChanged(self, s, e):
        """Auto-fill Cond1Param (and show db info) when user picks a live param."""
        try:
            item = self.LiveParamCombo.SelectedItem
            if item is None: return
            pname = item.Tag if hasattr(item, 'Tag') and item.Tag else None
            if not pname: return
            # Fill first empty condition param box
            for cbox in (self.Cond1Param, self.Cond2Param, self.Cond3Param):
                try:
                    if not cbox.Text.strip():
                        cbox.Text = pname; break
                except Exception: pass
            else:
                self.Cond1Param.Text = pname
            # Also show info from param_db if present
            db = self._load_param_db()
            v  = db.get(pname, {})
            if v:
                self.ParamInfoType.Text  = 'Type: {}'.format(v.get('t','TEXT'))
                self.ParamInfoGroup.Text = 'Group: {}'.format(v.get('gr',''))
                g = v.get('g','')
                self.ParamInfoGuid.Text  = 'GUID: {}'.format(g[:8]+'…' if len(g)>8 else g or '—')
                self.ParamInfoDesc.Text  = v.get('d','') or v.get('dn','')
                cats = v.get('c',[])
                self.ParamInfoCats.Text  = 'Cats: ' + ', '.join(cats[:4]) + ('…' if len(cats)>4 else '')
        except Exception: pass

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 FIX — DESELECT (remove picked category from selection)
    # ─────────────────────────────────────────────────────────────────────────
    def Deselect_Click(self, s, e):
        """Remove elements of a chosen category from the current selection,
        or if nothing is selected, clear the whole selection."""
        doc, uidoc = self._fd()
        if not doc: return
        cur_ids = list(uidoc.Selection.GetElementIds())
        if not cur_ids:
            self._log('Nothing selected to deselect from', '⊘')
            return
        # Build category name → ids map
        cat_map = {}
        for eid in cur_ids:
            try:
                el  = doc.GetElement(eid)
                cat = el.Category.Name if el and el.Category else 'Unknown'
                cat_map.setdefault(cat, []).append(eid)
            except Exception:
                cat_map.setdefault('Unknown', []).append(eid)
        options = ['⊘ ALL ({})'.format(len(cur_ids))] + \
                  ['{} ({})'.format(c, len(v)) for c, v in sorted(cat_map.items())]
        choice = forms.SelectFromList.show(options, title='Deselect which category?',
                                            multiselect=False)
        if not choice: return
        if choice.startswith('⊘ ALL'):
            uidoc.Selection.SetElementIds(List[ElementId]())
            self._log('Deselected all ({})'.format(len(cur_ids)), '⊘')
            return
        cat_name = choice.split(' (')[0].strip()
        remove   = set(cat_map.get(cat_name, []))
        keep     = [eid for eid in cur_ids if eid not in remove]
        self._set_ids(uidoc, [doc.GetElement(eid) for eid in keep])
        self._log('Deselected {} "{}" — {} remain'.format(
            len(remove), cat_name, len(keep)), '⊘')

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 FIX — COLOURISER BY SELECTION
    # ─────────────────────────────────────────────────────────────────────────
    def _colour_by_selection(self, doc, uidoc):
        """Apply current fill colour to the current Revit selection only."""
        ids = list(uidoc.Selection.GetElementIds())
        if not ids:
            self._log('No elements selected — make a selection first', '⚠')
            return
        col = self._hex_to_color(self._active_fill_hex)
        outline = self._active_outline_hex if self._outline_enabled else None
        out_col = self._hex_to_color(outline) if outline else None
        solid_id = self._get_solid_fill_id(doc)
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Colour By Selection')
        t.Start()
        count = 0
        for eid in ids:
            try:
                ogs = view.GetElementOverrides(eid)
                if col:
                    self._apply_fill_to_ogs(ogs, col, solid_id)
                if out_col:
                    self._apply_outline_to_ogs(ogs, out_col)
                view.SetElementOverrides(eid, ogs)
                count += 1
            except Exception:
                pass
        t.Commit()
        self._log('By Selection: {} → {} elements'.format(
            self._active_fill_hex, count), '*')


    # ─────────────────────────────────────────────────────────────────────────

    _param_db     = None   # loaded lazily
    _param_search_results = []
    _grad_from_hex = '#2196F3'
    _grad_to_hex   = '#F44336'
    _cond_rows_visible = 1  # 1-3

    def _load_param_db(self):
        if self._param_db is not None:
            return self._param_db
        try:
            db_path = os.path.join(_data, 'param_db.json')
            with io.open(db_path, encoding='utf-8') as f:
                self._param_db = json.load(f)
        except Exception:
            self._param_db = {}
        return self._param_db

    def _populate_param_cat_filter(self):
        """Fill ParamCatFilter with all unique categories from param_db."""
        try:
            db = self._load_param_db()
            cats = set()
            for v in db.values():
                for c in v.get('c', []):
                    cats.add(c)
            self.ParamCatFilter.Items.Clear()
            # (module-level import)
            all_item = CBI(); all_item.Content = 'All Categories'
            self.ParamCatFilter.Items.Add(all_item)
            for cat in sorted(cats):
                ci = CBI(); ci.Content = cat
                self.ParamCatFilter.Items.Add(ci)
            self.ParamCatFilter.SelectedIndex = 0
        except Exception: pass

    def _param_search_run(self):
        """Full-text search across param_db; populate result list."""
        try:
            db  = self._load_param_db()
            try:
                query = (self.ParamSearchBox.Text or '').strip().lower()
            except Exception:
                query = ''
            try:
                cat_item = self.ParamCatFilter.SelectedItem
                cat_filter = cat_item.Content if cat_item else 'All Categories'
            except Exception:
                cat_filter = 'All Categories'
            results = []
            for pname, v in db.items():
                # Category filter
                if cat_filter != 'All Categories':
                    if cat_filter not in v.get('c', []):
                        continue
                # Text filter
                if query:
                    search_blob = (pname + ' ' + v.get('d','') + ' ' +
                                   v.get('dn','') + ' ' + v.get('gr','')).lower()
                    if query not in search_blob:
                        continue
                results.append(pname)
            results.sort()
            self._param_search_results = results
            self.ParamResultsList.Items.Clear()
            for r in results[:200]:
                self.ParamResultsList.Items.Add(r)
            try:
                self.ParamMatchCount.Text = '{} matches'.format(len(results))
            except Exception: pass
        except Exception: pass

    def ParamSearch_TextChanged(self, s, e):
        self._param_search_run()

    def ParamCatFilter_SelectionChanged(self, s, e):
        if not hasattr(self, '_param_db'):
            self._load_param_db()
        self._param_search_run()

    def ParamResult_SelectionChanged(self, s, e):
        """Populate detail panel and auto-fill Cond1Param when a param is selected."""
        try:
            pname = self.ParamResultsList.SelectedItem
            if not pname: return
            db = self._load_param_db()
            v  = db.get(pname, {})
            cats = v.get('c', [])
            self.ParamInfoType.Text  = 'Type: {}'.format(v.get('t','TEXT'))
            self.ParamInfoGroup.Text = 'Group: {}'.format(v.get('gr',''))
            g = v.get('g','')
            self.ParamInfoGuid.Text  = 'GUID: {}'.format(g[:8]+'…' if len(g)>8 else g)
            self.ParamInfoDesc.Text  = v.get('d','') or v.get('dn','')
            self.ParamInfoCats.Text  = 'Cats: ' + ', '.join(cats[:4]) + \
                                       ('…' if len(cats)>4 else '')
            # Auto-fill first empty condition param box
            for cbox in (self.Cond1Param, self.Cond2Param, self.Cond3Param):
                try:
                    if not cbox.Text.strip():
                        cbox.Text = pname; break
                except Exception: pass
            else:
                self.Cond1Param.Text = pname
        except Exception: pass

    def ParamConditionAdd_Click(self, s, e):
        """Show next condition row (up to 3)."""
        try:
            if self._cond_rows_visible < 2:
                self.CondRow2.Visibility = \
                    __import__('System.Windows', fromlist=['Visibility']).Visibility.Visible
                self._cond_rows_visible = 2
            elif self._cond_rows_visible < 3:
                self.CondRow3.Visibility = \
                    __import__('System.Windows', fromlist=['Visibility']).Visibility.Visible
                self._cond_rows_visible = 3
        except Exception: pass

    def ParamConditionRemove_Click(self, s, e):
        """Hide last visible condition row."""
        try:
            c = System.Windows.Visibility.Collapsed
            if self._cond_rows_visible == 3:
                self.CondRow3.Visibility = c; self._cond_rows_visible = 2
            elif self._cond_rows_visible == 2:
                self.CondRow2.Visibility = c; self._cond_rows_visible = 1
        except Exception: pass

    def ParamConditionClear_Click(self, s, e):
        """Clear all condition fields and hide extra rows."""
        try:
            c = System.Windows.Visibility.Collapsed
            for box in (self.Cond1Param, self.Cond1Val,
                        self.Cond2Param, self.Cond2Val,
                        self.Cond3Param, self.Cond3Val):
                try: box.Text = ''
                except Exception: pass
            self.CondRow2.Visibility = c
            self.CondRow3.Visibility = c
            self._cond_rows_visible  = 1
            self.ParamSearchBox.Text = ''
            self.ParamResultsList.Items.Clear()
            self.ParamMatchCount.Text = '0 matches'
            for lbl in (self.ParamInfoType, self.ParamInfoGroup,
                        self.ParamInfoGuid, self.ParamInfoDesc, self.ParamInfoCats):
                try: lbl.Text = ''
                except Exception: pass
        except Exception: pass

    def _read_conditions(self):
        """Return list of (pname, op, val, logic) tuples for active condition rows."""
        conds = []
        rows = [
            (self.Cond1Param, self.Cond1Op, self.Cond1Val, 'AND'),
        ]
        if self._cond_rows_visible >= 2:
            rows.append((self.Cond2Param, self.Cond2Op, self.Cond2Val,
                         self.Cond2Logic.SelectedItem.Content
                         if self.Cond2Logic.SelectedItem else 'AND'))
        if self._cond_rows_visible >= 3:
            rows.append((self.Cond3Param, self.Cond3Op, self.Cond3Val,
                         self.Cond3Logic.SelectedItem.Content
                         if self.Cond3Logic.SelectedItem else 'AND'))
        for pbox, opbox, vbox, logic in rows:
            try:
                pname = pbox.Text.strip()
                op    = opbox.SelectedItem.Content if opbox.SelectedItem else 'contains'
                val   = vbox.Text.strip()
                if pname:
                    conds.append((pname, str(op), val, str(logic)))
            except Exception: pass
        return conds

    def _eval_condition(self, el, pname, op, val):
        """Evaluate a single condition against an element. Returns bool."""
        # (module-level import)
        try:
            p = el.LookupParameter(pname)
            if p is None:
                # Try built-in by name
                p = el.get_Parameter(getattr(BuiltInParameter,
                    'INVALID', BuiltInParameter.INVALID)) if False else None
            if p is None:
                return op in ('is empty',)
            st = p.StorageType
            if st == StorageType.String:
                v = p.AsString() or ''
            elif st == StorageType.Double:
                v = p.AsDouble()
                raw_v = v
                v_str = str(v)
            elif st == StorageType.Integer:
                v = p.AsInteger()
                raw_v = v
                v_str = str(v)
            elif st == StorageType.ElementId:
                eid = p.AsElementId()
                v = str(eid.IntegerValue)
            else:
                v = ''

            op = op.strip()
            if op == 'is empty':
                return not v or str(v).strip() == ''
            if op == 'has value':
                return bool(v) and str(v).strip() != ''
            # String ops
            vs = str(v).lower()
            vl = val.lower()
            if op == 'contains':    return vl in vs
            if op == 'equals':      return vs == vl
            if op == 'starts with': return vs.startswith(vl)
            if op == 'ends with':   return vs.endswith(vl)
            # Numeric ops
            try:
                fv = float(str(v))
                fval = float(val) if val else 0
            except Exception:
                return False
            if op == '=':  return fv == fval
            if op == '!=': return fv != fval
            if op == '>':  return fv > fval
            if op == '<':  return fv < fval
            if op == '>=': return fv >= fval
            if op == '<=': return fv <= fval
            if op == 'in range':
                parts = val.split('|')
                if len(parts) == 2:
                    return float(parts[0]) <= fv <= float(parts[1])
        except Exception:
            pass
        return False

    def _run_param_filter(self, select_results=True):
        """Run all active conditions against elements in the active view. Returns count."""
        doc, uidoc = self._fd()
        if not doc: return 0
        conds = self._read_conditions()
        if not conds:
            self._log('Add at least one condition first')
            return 0
        view    = doc.ActiveView
        matched = []
        for el in FilteredElementCollector(doc, view.Id)\
                  .WhereElementIsNotElementType().ToElements():
            try:
                result = None
                for i, (pname, op, val, logic) in enumerate(conds):
                    hit = self._eval_condition(el, pname, op, val)
                    if i == 0:
                        result = hit
                    elif logic == 'AND':
                        result = result and hit
                    else:
                        result = result or hit
                if result:
                    matched.append(el)
            except Exception:
                pass
        if select_results:
            self._set_ids(uidoc, matched)
        return len(matched)

    def ParamFilterPreview_Click(self, s, e):
        """Count matches without changing the selection."""
        n = self._run_param_filter(select_results=False)
        try: self.ParamMatchCount.Text = '{} matches'.format(n)
        except Exception: pass
        self._log('[Preview] {} elements match conditions'.format(n), '[Find]')

    def ApplyParamFilter_Click(self, s, e):
        """Apply all conditions — select matching elements."""
        n = self._run_param_filter(select_results=True)
        try: self.ParamMatchCount.Text = '{} matches'.format(n)
        except Exception: pass
        self._log('[Filter] {} elements selected'.format(n), '▶')

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — ISO COMPLETENESS DASHBOARD
    # ─────────────────────────────────────────────────────────────────────────

    _dashboard_rows = []   # list of dicts for filtering

    class _DashRow:
        """Simple bindable row for the ISO dashboard DataGrid."""
        def __init__(self, eid, cat, pct, rag, missing):
            self.Id      = str(eid)
            self.Cat     = cat
            self.Pct     = str(pct) + '%'
            self.Rag     = rag
            self.Missing = missing

    def _load_dashboard(self):
        """Collect elements in scope, compute per-element ISO completeness."""
        doc, uidoc = self._fd()
        if not doc: return []
        elements = self._iso_collect(doc, uidoc)
        rows = []
        for el, cat_name in elements:
            filled  = sum(1 for p in self._ISO_PARAMS if self._iso_get(el, p))
            total   = len(self._ISO_PARAMS)
            pct     = int(filled * 100 / total) if total else 0
            missing = ', '.join(
                self._ISO_SHORT.get(p, p.replace('ASS_','').replace('_TXT',''))
                for p in self._ISO_PARAMS if not self._iso_get(el, p)
            )[:60]
            if   pct == 100: rag = '?'
            elif pct >= 50:  rag = '?'
            else:            rag = '?'
            rows.append({
                'el_id': el.Id.IntegerValue,
                'row':   self._DashRow(el.Id.IntegerValue, cat_name, pct, rag, missing or '—'),
                'pct':   pct,
                'rag':   rag,
            })
        return rows

    def _apply_dashboard_filter(self):
        """Repopulate DataGrid according to slider/RAG filter."""
        try:
            min_pct = int(self.DashboardFilterSlider.Value)
            rag_item = self.DashboardRAGFilter.SelectedItem
            rag_filter = rag_item.Content if rag_item else 'All'
        except Exception:
            min_pct, rag_filter = 0, 'All'

        visible = []
        for r in self._dashboard_rows:
            if r['pct'] < min_pct: continue
            if rag_filter == '? Red'   and r['rag'] != '?': continue
            if rag_filter == '? Amber' and r['rag'] != '?': continue
            if rag_filter == '? Green' and r['rag'] != '?': continue
            visible.append(r['row'])

        try:
            self.IsoDashboardGrid.ItemsSource = visible
            self.DashboardCountLabel.Text = '{} rows'.format(len(visible))
        except Exception: pass

    def IsoDashboardLoad_Click(self, s, e):
        """Load ISO completeness data into the DataGrid."""
        self._dashboard_rows = self._load_dashboard()
        self._apply_dashboard_filter()
        self._iso_log('Dashboard: {} elements loaded'.format(len(self._dashboard_rows)), '[Chart]')

    def IsoDashboardExport_Click(self, s, e):
        """Export visible dashboard rows to CSV."""
        if not self._dashboard_rows:
            self._iso_log('Load dashboard first'); return
        path = os.path.join(tempfile.gettempdir(),
                            'STINGTags_ISODashboard.csv')
        try:
            with io.open(path, 'w', newline='') as f:
                w = _csv.writer(f)
                w.writerow(['ElementId','Category','Completeness_%','RAG','Missing_Tokens'])
                for r in self._dashboard_rows:
                    row = r['row']
                    w.writerow([row.Id, row.Cat, row.Pct, row.Rag, row.Missing])
            try:
                self._open_folder(path)
            except Exception: pass
            self._iso_log('Dashboard exported: {} rows\n{}'.format(
                len(self._dashboard_rows), path), '④')
        except Exception as ex:
            self._iso_log('Export error: ' + str(ex))

    def IsoDashboardGrid_SelectionChanged(self, s, e):
        """Select the clicked element in Revit."""
        try:
            row = self.IsoDashboardGrid.SelectedItem
            if row is None: return
            doc, uidoc = self._fd()
            if not doc: return
            eid = ElementId(int(row.Id))
            uidoc.Selection.SetElementIds(List[ElementId]([eid]))
        except Exception: pass

    def DashboardFilterSlider_ValueChanged(self, s, e):
        try:
            v = int(self.DashboardFilterSlider.Value)
            self.DashboardFilterLabel.Text = '{}%'.format(v)
        except Exception: pass
        self._apply_dashboard_filter()

    def DashboardRAGFilter_SelectionChanged(self, s, e):
        self._apply_dashboard_filter()

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — VIEWPORT SYNC (DispatcherTimer-based, 750ms)
    # ─────────────────────────────────────────────────────────────────────────

    _last_view_id = -1

    def _refresh_undo_combo(self):
        """Rebuild UndoHistoryCombo for the current view's undo stack."""
        try:
            _, uidoc = self._fd()
            if not uidoc: return
            vid = uidoc.ActiveView.Id.IntegerValue
            stack = self._undo_stack.get(vid, [])
            self.UndoHistoryCombo.Items.Clear()
            for i, entry in enumerate(reversed(stack)):
                label = entry.get('label', 'Snapshot {}'.format(i+1))
                # (module-level import)
                ci = CBI(); ci.Content = label; ci.Tag = i
                self.UndoHistoryCombo.Items.Add(ci)
            depth = len(stack)
            self.UndoDepthBadge.Text = '{}/5'.format(depth)
        except Exception:
            pass

    def UndoHistoryCombo_SelectionChanged(self, s, e):
        """Jump to a specific undo snapshot selected from the combo."""
        try:
            item = self.UndoHistoryCombo.SelectedItem
            if item is None: return
            idx = item.Tag  # index from top of stack
            doc, uidoc = self._fd()
            if not doc: return
            vid = uidoc.ActiveView.Id.IntegerValue
            stack = self._undo_stack.get(vid, [])
            snapshot_idx = len(stack) - 1 - int(idx)
            if 0 <= snapshot_idx < len(stack):
                snapshot = stack[snapshot_idx]
                t = Transaction(doc, 'STINGTags Undo Jump')
                t.Start()
                count = 0
                for tag_id, data in snapshot.get('tags', {}).items():
                    try:
                        tag = doc.GetElement(ElementId(int(tag_id)))
                        if tag:
                            tag.TagHeadPosition = data['pos']
                            tag.HasLeader       = data['ldr']
                            count += 1
                    except Exception:
                        pass
                t.Commit()
                self._log('Restored snapshot: {} ({}  tags)'.format(
                    snapshot.get('label', '*'), count), '⌫')
        except Exception as ex:
            self._log('Undo jump error: ' + str(ex))

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — COLOUR SCHEME PERSISTENCE (project_config.json)
    # ─────────────────────────────────────────────────────────────────────────

    def _config_path(self):
        return os.path.join(_cfg, 'project_config.json')

    def _load_config(self):
        try:
            p = self._config_path()
            if os.path.exists(p):
                with io.open(p, encoding='utf-8') as f:
                    return json.load(f)
        except Exception:
            pass
        return {}

    def _save_config(self, cfg):
        try:
            p = self._config_path()
            try:
                os.makedirs(os.path.dirname(p))
            except OSError:
                pass
            with io.open(p, 'w', encoding='utf-8') as f:
                json.dump(cfg, f, indent=2)
        except Exception as ex:
            self._log('Config save error: ' + str(ex))

    def _refresh_scheme_combo(self):
        try:
            cfg     = self._load_config()
            schemes = cfg.get('colour_schemes', {})
            self.ColourSchemeCombo.Items.Clear()
            # (module-level import)
            ph = CBI(); ph.Content = '— saved schemes —'
            self.ColourSchemeCombo.Items.Add(ph)
            for name in sorted(schemes.keys()):
                ci = CBI(); ci.Content = name
                self.ColourSchemeCombo.Items.Add(ci)
            self.ColourSchemeCombo.SelectedIndex = 0
        except Exception: pass

    def SaveColourScheme_Click(self, s, e):
        name = forms.ask_for_string('Scheme name (e.g. "MEP Discipline"):')
        if not name: return
        cfg = self._load_config()
        cfg.setdefault('colour_schemes', {})[name] = {
            'fill':    self._active_fill_hex,
            'outline': self._active_outline_hex,
            'outline_enabled': self._outline_enabled,
            'palette': 'Material 500',
        }
        self._save_config(cfg)
        self._refresh_scheme_combo()
        self._log('Colour scheme "{}" saved'.format(name), '[Save]')

    def LoadColourScheme_Click(self, s, e):
        try:
            item = self.ColourSchemeCombo.SelectedItem
            if item is None or item.Content == '— saved schemes —': return
            name = item.Content
            cfg  = self._load_config()
            sc   = cfg.get('colour_schemes', {}).get(name)
            if not sc: return
            self._active_fill_hex    = sc.get('fill',    self._active_fill_hex)
            self._active_outline_hex = sc.get('outline', self._active_outline_hex)
            self._outline_enabled    = sc.get('outline_enabled', False)
            self._update_colour_previews()
            self._log('Scheme "{}" loaded — fill {}, outline {}'.format(
                name, self._active_fill_hex, self._active_outline_hex), '*')
        except Exception as ex:
            self._log('Load scheme error: ' + str(ex))

    def DeleteColourScheme_Click(self, s, e):
        try:
            item = self.ColourSchemeCombo.SelectedItem
            if item is None or item.Content == '— saved schemes —': return
            name = item.Content
            if not forms.alert('Delete scheme "{}"?'.format(name), ok=True, cancel=True):
                return
            cfg = self._load_config()
            cfg.get('colour_schemes', {}).pop(name, None)
            self._save_config(cfg)
            self._refresh_scheme_combo()
            self._log('Scheme "{}" deleted'.format(name), '✕')
        except Exception as ex:
            self._log('Delete scheme error: ' + str(ex))

    def ColourSchemeCombo_SelectionChanged(self, s, e):
        pass  # Auto-load on explicit Load click only

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — PALETTE SWITCHER (10 palettes)
    # ─────────────────────────────────────────────────────────────────────────

    _PALETTES = {
        'Material 500': [
            '#F44336','#FF7043','#FF9800','#FFC107','#FFEB3B','#8BC34A',
            '#4CAF50','#009688','#00BCD4','#03A9F4','#2196F3','#3F51B5',
            '#673AB7','#9C27B0','#E91E63','#795548','#9E9E9E','#607D8B',
            '#FFFFFF','#212121',
        ],
        'Material 700': [
            '#D32F2F','#E64A19','#F57C00','#FFA000','#F9A825','#689F38',
            '#388E3C','#00796B','#0097A7','#0288D1','#1976D2','#303F9F',
            '#512DA8','#7B1FA2','#C2185B','#5D4037','#616161','#455A64',
            '#F5F5F5','#212121',
        ],
        'Material A200': [
            '#FF5252','#FF6D00','#FFAB40','#FFD740','#FFFF00','#B2FF59',
            '#69F0AE','#64FFDA','#18FFFF','#40C4FF','#448AFF','#536DFE',
            '#7C4DFF','#E040FB','#FF4081','#BCAAA4','#EEEEEE','#B0BEC5',
            '#FFFFFF','#424242',
        ],
        'Pastel': [
            '#FFCDD2','#FFCCBC','#FFE0B2','#FFF9C4','#F0F4C3','#DCEDC8',
            '#C8E6C9','#B2DFDB','#B2EBF2','#B3E5FC','#BBDEFB','#C5CAE9',
            '#D1C4E9','#E1BEE7','#F8BBD0','#D7CCC8','#F5F5F5','#CFD8DC',
            '#FFFFFF','#607D8B',
        ],
        'Muted Dark': [
            '#C62828','#BF360C','#E65100','#F57F17','#9E9D24','#33691E',
            '#1B5E20','#004D40','#006064','#01579B','#0D47A1','#1A237E',
            '#311B92','#4A148C','#880E4F','#3E2723','#424242','#263238',
            '#ECEFF1','#000000',
        ],
        'RAG Traffic': [
            '#F44336','#E57373','#EF9A9A','#FFCCBC','#FFEB3B','#FFF176',
            '#F9A825','#F57F17','#C8E6C9','#A5D6A7','#4CAF50','#2E7D32',
            '#B3E5FC','#29B6F6','#0288D1','#01579B','#9E9E9E','#607D8B',
            '#FFFFFF','#212121',
        ],
        'Greyscale': [
            '#FFFFFF','#F5F5F5','#EEEEEE','#E0E0E0','#BDBDBD','#9E9E9E',
            '#757575','#616161','#424242','#212121','#000000','#B0BEC5',
            '#90A4AE','#78909C','#607D8B','#546E7A','#455A64','#37474F',
            '#263238','#ECEFF1',
        ],
        'MEP Discipline': [
            '#F44336','#2196F3','#4CAF50','#FF9800','#9C27B0','#00BCD4',
            '#FF5722','#607D8B','#FFEB3B','#8BC34A','#3F51B5','#E91E63',
            '#009688','#FFC107','#795548','#CDDC39','#03A9F4','#FF4081',
            '#69F0AE','#212121',
        ],
        'Warm Tones': [
            '#FF1744','#F44336','#FF5722','#FF7043','#FF9800','#FFA726',
            '#FFC107','#FFD54F','#FFEE58','#FFF9C4','#FFCCBC','#FFAB91',
            '#FF8A65','#FF7043','#BF360C','#7F0000','#795548','#4E342E',
            '#FFFFFF','#212121',
        ],
        'Cool Tones': [
            '#E3F2FD','#BBDEFB','#90CAF9','#64B5F6','#42A5F5','#2196F3',
            '#1E88E5','#1976D2','#1565C0','#0D47A1','#B3E5FC','#81D4FA',
            '#4FC3F7','#29B6F6','#03A9F4','#0288D1','#B2EBF2','#80DEEA',
            '#26C6DA','#006064',
        ],
        'Earth Tones': [
            '#8D6E63','#A1887F','#795548','#6D4C41','#5D4037','#4E342E',
            '#D7CCC8','#BCAAA4','#EFEBE9','#3E2723','#BF360C','#E64A19',
            '#FF5722','#FF8A65','#FFAB91','#FFE0B2','#FFF3E0','#FBE9E7',
            '#FFFFFF','#212121',
        ],
        'Neon Vivid': [
            '#FF1744','#FF9100','#FFEA00','#76FF03','#00E676','#1DE9B6',
            '#00E5FF','#2979FF','#651FFF','#D500F9','#F50057','#FF3D00',
            '#FFC400','#AEEA00','#00BFA5','#00B0FF','#6200EA','#AA00FF',
            '#C51162','#304FFE',
        ],
        'Ocean Depths': [
            '#E0F7FA','#B2EBF2','#80DEEA','#4DD0E1','#26C6DA','#00BCD4',
            '#00ACC1','#0097A7','#00838F','#006064','#84FFFF','#18FFFF',
            '#00E5FF','#00B8D4','#B3E5FC','#81D4FA','#4FC3F7','#29B6F6',
            '#039BE5','#01579B',
        ],
        'Forest Canopy': [
            '#E8F5E9','#C8E6C9','#A5D6A7','#81C784','#66BB6A','#4CAF50',
            '#43A047','#388E3C','#2E7D32','#1B5E20','#DCEDC8','#C5E1A5',
            '#AED581','#9CCC65','#8BC34A','#7CB342','#689F38','#558B2F',
            '#33691E','#827717',
        ],
        'Sunset Gradient': [
            '#311B92','#4527A0','#512DA8','#5E35B1','#7B1FA2','#9C27B0',
            '#C2185B','#D81B60','#E91E63','#F44336','#FF5722','#FF7043',
            '#FF8A65','#FF9800','#FFA726','#FFB74D','#FFC107','#FFD54F',
            '#FFEB3B','#FFF176',
        ],
        'ISO Status': [
            '#F44336','#E53935','#C62828','#FF9800','#FB8C00','#EF6C00',
            '#FFEB3B','#FDD835','#F9A825','#4CAF50','#43A047','#2E7D32',
            '#2196F3','#1E88E5','#1565C0','#9E9E9E','#757575','#424242',
            '#FFFFFF','#212121',
        ],
        'Accessibility': [
            '#0072B2','#E69F00','#009E73','#F0E442','#56B4E9','#D55E00',
            '#CC79A7','#000000','#FFFFFF','#999999','#332288','#88CCEE',
            '#44AA99','#117733','#DDCC77','#CC6677','#AA4499','#882255',
            '#661100','#6699CC',
        ],
        'High Contrast': [
            '#FF0000','#00FF00','#0000FF','#FFFF00','#FF00FF','#00FFFF',
            '#FF8000','#8000FF','#0080FF','#FF0080','#000000','#FFFFFF',
            '#808080','#FF4444','#44FF44','#4444FF','#FFAA00','#00AAFF',
            '#AA00FF','#333333',
        ],
    }

    def PaletteSelector_SelectionChanged(self, s, e):
        """Rebuild fill swatch rows for the selected palette."""
        try:
            item = self.PaletteSelector.SelectedItem
            if item is None: return
            palette_name = str(item.Content)
            colours = self._PALETTES.get(palette_name, self._PALETTES['Material 500'])
            self._rebuild_fill_swatches(colours)
        except Exception: pass

    def _rebuild_fill_swatches(self, colours):
        """Replace swatch button colours in FillSwatchRow1/Row2."""
        try:
            def parse(h):
                h = h.lstrip('#')
                r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
                if MColor:
                    return SolidColorBrush(MColor.FromRgb(r, g, b))
                return None

            row1_btns = list(self.FillSwatchRow1.Children)
            row2_btns = list(self.FillSwatchRow2.Children)
            all_btns = row1_btns + row2_btns
            for i, btn in enumerate(all_btns):
                if i >= len(colours): break
                try:
                    c = colours[i]
                    # Always update Tag (this is what the click handler reads)
                    btn.Tag = c
                    btn.ToolTip = c
                    # Update visual only if MColor is available
                    brush = parse(c)
                    if brush:
                        btn.Background = brush
                    # Border slightly darker
                    h = c.lstrip('#')
                    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
                    dr = max(0, r - 40); dg = max(0, g - 40); db = max(0, b - 40)
                    dark = '#{:02X}{:02X}{:02X}'.format(dr, dg, db)
                    dark_brush = parse(dark)
                    if dark_brush:
                        btn.BorderBrush = dark_brush
                except Exception: pass
            self._log('Palette: {} swatches updated'.format(min(len(colours), len(all_btns))))
        except Exception as ex:
            self._log('Palette error: ' + str(ex))

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — CUSTOM HEX, TRANSPARENCY, GRADIENT, PATTERN, LINE WEIGHT
    # ─────────────────────────────────────────────────────────────────────────

    def CustomHexInput_GotFocus(self, s, e):
        try:
            if self.CustomHexInput.Text in ('RRGGBB', ''):
                self.CustomHexInput.Text = ''
                # (module-level import)
                self.CustomHexInput.Foreground = SolidColorBrush(Colors.Black)
        except Exception: pass

    def CustomHexInput_KeyDown(self, s, e):
        """Live preview swatch + Enter to apply."""
        try:
            hex_str = '#' + self.CustomHexInput.Text.strip().lstrip('#')
            h = hex_str.lstrip('#')
            if len(h) == 6:
                brush = self._hex_to_media_brush(hex_str)
                if brush:
                    self.CustomHexPreview.Background = brush
                    self.CustomHexPreview.IsEnabled = True
            if e.Key == Key.Return:
                self.ColourCustomHex_Click(s, e)
        except Exception:
            pass

    def ColourCustomHex_Click(self, s, e):
        """Apply custom hex fill colour from the text field."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            hex_str = '#' + self.CustomHexInput.Text.strip().lstrip('#')
            if len(hex_str) != 7:
                self._log('Enter 6-digit hex (e.g. A3C4F1)')
                return
            col = self._hex_to_color(hex_str)
            if col is None:
                self._log('Invalid hex colour')
                return
        except Exception:
            return
        self._active_fill_hex = hex_str
        self._update_colour_previews()
        outline = self._active_outline_hex if self._outline_enabled else None
        n = self._colour_apply(doc, uidoc, hex_str, outline)
        self._log('Fill {}: {} elements'.format(hex_str, n), '*')

    def TransparencySlider_ValueChanged(self, s, e):
        try:
            v = int(self.TransparencySlider.Value)
            self.TransparencyLabel.Text = '{}%'.format(v)
        except Exception: pass

    def ColourApplyTransparency_Click(self, s, e):
        """Apply transparency override to selection (preserves fill/outline)."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids:
            self._log('Select elements first')
            return
        try:
            pct = int(self.TransparencySlider.Value)
        except Exception:
            pct = 50
        pct = max(0, min(90, pct))
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Transparency')
        t.Start()
        ok = 0
        first_err = None
        for eid in ids:
            try:
                ogs = view.GetElementOverrides(eid)
                applied = False
                try:
                    ogs.SetSurfaceTransparency(pct)
                    applied = True
                except (AttributeError, Exception) as ex1:
                    if first_err is None:
                        first_err = 'SetSurfaceTransparency: ' + str(ex1)
                if not applied:
                    try:
                        ogs.SetHalftone(pct > 50)
                        applied = True
                    except Exception:
                        pass
                view.SetElementOverrides(eid, ogs)
                if applied:
                    ok += 1
            except Exception as ex:
                if first_err is None:
                    first_err = str(ex)
        t.Commit()
        if ok > 0:
            self._log('Transparency {}%: {}/{}'.format(pct, ok, len(ids)), '*')
        else:
            self._log('Transparency failed: {} | {}'.format(
                len(ids), first_err or 'unknown'))

    def GradFrom_Click(self, s, e):
        """Set gradient FROM colour — user clicks any fill swatch next."""
        self._grad_picking = 'from'
        self._log('Click any fill swatch to set gradient FROM colour', '⟵')

    def GradTo_Click(self, s, e):
        """Set gradient TO colour — user clicks any fill swatch next."""
        self._grad_picking = 'to'
        self._log('Click any fill swatch to set gradient TO colour', '⟶')

    def ColourApplyGradient_Click(self, s, e):
        """Linearly interpolate fill colour from->to across selection, sorted by X."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids:
            self._log('Select elements first')
            return
        from_col = self._hex_to_color(self._grad_from_hex)
        to_col = self._hex_to_color(self._grad_to_hex)
        if not from_col or not to_col:
            self._log('Set gradient FROM and TO first (click button, then swatch)')
            return
        view = doc.ActiveView
        elems = []
        for eid in ids:
            el = doc.GetElement(eid)
            if el:
                elems.append((el, eid))
        def _x_pos(item):
            c = _bb_center(item[0], view)
            return c.X if c else 0
        elems.sort(key=_x_pos)
        n = len(elems)
        if n == 0:
            self._log('No valid elements')
            return
        def _interp(c1, c2, t_):
            return Color(
                max(0, min(255, int(c1.Red + (c2.Red - c1.Red) * t_))),
                max(0, min(255, int(c1.Green + (c2.Green - c1.Green) * t_))),
                max(0, min(255, int(c1.Blue + (c2.Blue - c1.Blue) * t_))),
            )
        solid_id = self._get_solid_fill_id(doc)
        outline = self._active_outline_hex if self._outline_enabled else None
        outline_col = self._hex_to_color(outline) if outline else None
        tr = Transaction(doc, 'STINGTags Gradient')
        tr.Start()
        count = 0
        for i, (el, eid) in enumerate(elems):
            t_ = float(i) / max(1, n - 1)
            col = _interp(from_col, to_col, t_)
            try:
                ogs = view.GetElementOverrides(eid)
                self._apply_fill_to_ogs(ogs, col, solid_id)
                if outline_col:
                    self._apply_outline_to_ogs(ogs, outline_col)
                view.SetElementOverrides(eid, ogs)
                count += 1
            except Exception:
                pass
        tr.Commit()
        self._log('Gradient {} -> {}: {}/{}'.format(
            self._grad_from_hex, self._grad_to_hex, count, n), '*')

    def ColourApplyPattern_Click(self, s, e):
        """Apply selected fill pattern with current fill colour to selection."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids:
            self._log('Select elements first')
            return
        try:
            pat_item = self.FillPatternCombo.SelectedItem
            pat_name = str(pat_item.Content) if pat_item else 'Solid'
        except Exception:
            pat_name = 'Solid'
        col = self._hex_to_color(self._active_fill_hex)
        fp_id = None
        for fp in FilteredElementCollector(doc).OfClass(FillPatternElement).ToElements():
            try:
                fp_pat = fp.GetFillPattern()
                if pat_name == 'Solid' and fp_pat.IsSolidFill:
                    fp_id = fp.Id
                    break
                elif fp_pat.Name:
                    if pat_name.lower().replace(' ', '') in \
                            fp_pat.Name.lower().replace(' ', ''):
                        fp_id = fp.Id
                        break
            except Exception:
                pass
        if fp_id is None and pat_name != 'Solid':
            self._log('Pattern "{}" not found'.format(pat_name))
            return
        view = doc.ActiveView
        outline = self._active_outline_hex if self._outline_enabled else None
        outline_col = self._hex_to_color(outline) if outline else None
        t = Transaction(doc, 'STINGTags Fill Pattern')
        t.Start()
        count = 0
        for eid in ids:
            try:
                ogs = view.GetElementOverrides(eid)
                try:
                    if col:
                        ogs.SetSurfaceForegroundPatternColor(col)
                    ogs.SetSurfaceForegroundPatternVisible(True)
                    if fp_id:
                        ogs.SetSurfaceForegroundPatternId(fp_id)
                except AttributeError:
                    try:
                        if col:
                            ogs.SetProjectionFillColor(col)
                        ogs.SetProjectionFillPatternVisible(True)
                        if fp_id:
                            ogs.SetProjectionFillPatternId(fp_id)
                    except Exception:
                        pass
                if outline_col:
                    self._apply_outline_to_ogs(ogs, outline_col)
                view.SetElementOverrides(eid, ogs)
                count += 1
            except Exception:
                pass
        t.Commit()
        self._log('Pattern "{}": {}/{}'.format(pat_name, count, len(ids)), '*')

    def ColourApplyLineWeight_Click(self, s, e):
        """Apply projection line weight override to selection."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids:
            self._log('Select elements first')
            return
        try:
            lw_item = self.LineWeightCombo.SelectedItem
            lw_str = str(lw_item.Content) if lw_item else 'Default'
        except Exception:
            lw_str = 'Default'
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Line Weight')
        t.Start()
        count = 0
        for eid in ids:
            try:
                ogs = view.GetElementOverrides(eid)
                if lw_str == 'Default':
                    ogs.SetProjectionLineWeight(-1)
                else:
                    ogs.SetProjectionLineWeight(int(lw_str))
                view.SetElementOverrides(eid, ogs)
                count += 1
            except Exception:
                pass
        t.Commit()
        self._log('Line weight {}: {}/{}'.format(lw_str, count, len(ids)), '*')

    # ─────────────────────────────────────────────────────────────────────────
    # v9.4 — UNDO HISTORY HELPERS (label-aware _push_undo override)
    # ─────────────────────────────────────────────────────────────────────────

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── BULK PARAMETER WRITE
    # ═════════════════════════════════════════════════════════════════════════

    _bulk_param_map = {}   # pname → dtype, populated by BulkParamRefresh_Click

    def BulkParamRefresh_Click(self, s, e):
        """Populate BulkParamCombo from every parameter on selected (or in-view) elements."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        # (module-level import)
        ids  = list(uidoc.Selection.GetElementIds())
        elems = ([doc.GetElement(eid) for eid in ids] if ids else
                 list(FilteredElementCollector(doc, doc.ActiveView.Id)
                      .WhereElementIsNotElementType().ToElements()))
        seen = {}
        for el in elems:
            try:
                for p in el.Parameters:
                    try:
                        pn = p.Definition.Name
                        if pn in seen or p.IsReadOnly: continue
                        st = p.StorageType
                        seen[pn] = {StorageType.String:'TEXT',
                                    StorageType.Double:'NUMBER',
                                    StorageType.Integer:'INTEGER',
                                    StorageType.ElementId:'ELEMENTID'}.get(st,'TEXT')
                    except Exception: pass
            except Exception: pass
        self._bulk_param_map = seen
        self.BulkParamCombo.Items.Clear()
        ph = CBI(); ph.Content = '— {} writable params —'.format(len(seen))
        self.BulkParamCombo.Items.Add(ph)
        for pn in sorted(seen):
            ci = CBI(); ci.Content = '{} [{}]'.format(pn, seen[pn])
            ci.Tag = pn; self.BulkParamCombo.Items.Add(ci)
        self.BulkParamCombo.SelectedIndex = 0
        self.BulkParamStatus.Text = '{} writable params found'.format(len(seen))

    def BulkParamSuggest_Click(self, s, e):
        """Pick the most relevant parameter for the current selection category."""
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        cats = set()
        for eid in ids:
            try:
                el = doc.GetElement(eid)
                if el and el.Category: cats.add(el.Category.Name)
            except Exception: pass
        db = self._load_param_db()
        candidates = []
        for pn, v in db.items():
            overlap = sum(1 for c in v.get('c',[]) if c in cats)
            if overlap: candidates.append((overlap, pn, v.get('t','TEXT')))
        candidates.sort(reverse=True)
        if not candidates: self._log('No param_db matches for selection category'); return
        top = candidates[:10]
        options = ['{} [{}]'.format(pn, t) for _, pn, t in top]
        choice = forms.SelectFromList.show(options, title='Suggested parameters')
        if not choice: return
        pn = choice.split(' [')[0]
        # (module-level import)
        # Add to combo if not present and select it
        for i in range(self.BulkParamCombo.Items.Count):
            item = self.BulkParamCombo.Items[i]
            if hasattr(item,'Tag') and item.Tag == pn:
                self.BulkParamCombo.SelectedIndex = i; return
        ci = CBI(); ci.Content = choice; ci.Tag = pn
        self.BulkParamCombo.Items.Add(ci)
        self.BulkParamCombo.SelectedItem = ci

    def BulkParamValue_KeyDown(self, s, e):
        if e.Key == Key.Return: self.BulkParamWrite_Click(s, e)

    def _bulk_get_pname(self):
        try:
            item = self.BulkParamCombo.SelectedItem
            if item and hasattr(item,'Tag') and item.Tag:
                return str(item.Tag)
        except Exception: pass
        return None

    def BulkParamPreview_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        pname = self._bulk_get_pname()
        if not pname: self._log('Pick a parameter from the dropdown'); return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        count = sum(1 for eid in ids
                    if doc.GetElement(eid) and
                    doc.GetElement(eid).LookupParameter(pname))
        self.BulkParamStatus.Text = '{}/{} elements have "{}"'.format(count, len(ids), pname)
        self._log('[Bulk] {} has param "{}": {}/{}'.format(
            'Selection', pname, count, len(ids)), '✏')

    def BulkParamWrite_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        pname = self._bulk_get_pname()
        if not pname: self._log('Pick a parameter first'); return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        try: val = self.BulkParamValue.Text.strip()
        except Exception: val = ''
        # (module-level import)
        t = Transaction(doc, 'STINGTags Bulk Write: ' + pname); t.Start()
        ok = fail = 0
        for eid in ids:
            try:
                el = doc.GetElement(eid)
                if not el: continue
                p = el.LookupParameter(pname)
                if not p or p.IsReadOnly: continue
                st = p.StorageType
                if   st == StorageType.String:  p.Set(val); ok += 1
                elif st == StorageType.Double:
                    try:
                        raw_val = float(val)
                        # Convert from display units → internal units (Revit 2022+)
                        try:
                            # (module-level import)
                            uid = p.GetUnitTypeId()
                            internal = UnitUtils.ConvertToInternalUnits(raw_val, uid)
                        except Exception:
                            internal = raw_val  # older Revit or dimensionless param
                        p.Set(internal); ok += 1
                    except Exception: fail += 1
                elif st == StorageType.Integer:
                    p.Set(int(round(float(val)))); ok += 1
                else: fail += 1
            except Exception: fail += 1
        t.Commit()
        msg = 'Wrote "{}" = "{}" → {} ok, {} failed'.format(pname, val, ok, fail)
        self.BulkParamStatus.Text = msg
        self._log('[Bulk Write] ' + msg, '✏')

    def BulkParamClear_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        pname = self._bulk_get_pname()
        if not pname: self._log('Pick a parameter first'); return
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        # (module-level import)
        t = Transaction(doc, 'STINGTags Bulk Clear: ' + pname); t.Start()
        ok = 0
        for eid in ids:
            try:
                p = doc.GetElement(eid).LookupParameter(pname)
                if p and not p.IsReadOnly and p.StorageType == StorageType.String:
                    p.Set(''); ok += 1
            except Exception: pass
        t.Commit()
        msg = 'Cleared "{}" on {} elements'.format(pname, ok)
        self.BulkParamStatus.Text = msg
        self._log('[Bulk Clear] ' + msg, '✏')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── SCHEDULE COLUMN WIDTH SYNC
    # ═════════════════════════════════════════════════════════════════════════

    def _get_selected_schedules(self, uidoc, doc):
        """Return list of ViewSchedule elements from current selection (or sheet viewports)."""
        # (module-level import)
        ids = list(uidoc.Selection.GetElementIds())
        scheds = []
        for eid in ids:
            el = doc.GetElement(eid)
            if isinstance(el, ScheduleSheetInstance):
                v = doc.GetElement(el.ScheduleId)
                if isinstance(v, ViewSchedule): scheds.append(v)
            elif isinstance(el, ViewSchedule):
                scheds.append(el)
        return scheds

    def _sched_set_col_width(self, sched, col_width_ft, col_idx=None):
        """Set column widths on a schedule's body section. col_idx=None → all columns."""
        # (module-level import)
        td = sched.GetTableData()
        sd = td.GetSectionData(SectionType.Body)
        count = sd.NumberOfColumns
        t_range = range(count) if col_idx is None else [col_idx]
        for ci in t_range:
            try: sd.SetColumnWidth(ci, col_width_ft)
            except Exception: pass

    def SchedSetWidth_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        scheds = self._get_selected_schedules(uidoc, doc)
        if not scheds: self._log('Select schedule(s) or schedule viewports on a sheet'); return
        try: mm = float(self.SchedWidthInput.Text.strip())
        except Exception: mm = 25.0
        ft = mm / 304.8   # mm → feet
        t = Transaction(doc, 'STINGTags Schedule Column Width'); t.Start()
        for sch in scheds:
            try: self._sched_set_col_width(sch, ft)
            except Exception: pass
        t.Commit()
        self._log('Set all columns to {} mm: {} schedules'.format(mm, len(scheds)), '[Align]')

    def SchedMatchWidest_Click(self, s, e):
        """Find max column width across all selected schedules and apply to all."""
        doc, uidoc = self._fd()
        if not doc: return
        scheds = self._get_selected_schedules(uidoc, doc)
        if not scheds: self._log('Select schedule viewports'); return
        # (module-level import)
        max_w = 0
        for sch in scheds:
            try:
                td = sch.GetTableData()
                sd = td.GetSectionData(SectionType.Body)
                for ci in range(sd.NumberOfColumns):
                    w = sd.GetColumnWidth(ci)
                    if w > max_w: max_w = w
            except Exception: pass
        if max_w == 0: self._log('Could not read column widths'); return
        t = Transaction(doc, 'STINGTags Schedule Match Widest'); t.Start()
        for sch in scheds:
            try: self._sched_set_col_width(sch, max_w)
            except Exception: pass
        t.Commit()
        self._log('Matched widest ({:.1f} mm): {} schedules'.format(
            max_w * 304.8, len(scheds)), '[Align]')

    def SchedEqualise_Click(self, s, e):
        """Make all columns equal within each selected schedule (total ÷ count)."""
        doc, uidoc = self._fd()
        if not doc: return
        scheds = self._get_selected_schedules(uidoc, doc)
        if not scheds: self._log('Select schedule viewports'); return
        # (module-level import)
        t = Transaction(doc, 'STINGTags Schedule Equalise Columns'); t.Start()
        for sch in scheds:
            try:
                td = sch.GetTableData()
                sd = td.GetSectionData(SectionType.Body)
                count = sd.NumberOfColumns
                total = sum(sd.GetColumnWidth(ci) for ci in range(count))
                if count and total:
                    equal = total / count
                    self._sched_set_col_width(sch, equal)
            except Exception: pass
        t.Commit()
        self._log('Equalised columns: {} schedules'.format(len(scheds)), '[Align]')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── ROOM TAG POSITION SYNC
    # ═════════════════════════════════════════════════════════════════════════

    def _get_room_tags(self, doc, uidoc):
        # (module-level import)
        ids = list(uidoc.Selection.GetElementIds())
        tags = [doc.GetElement(eid) for eid in ids
                if isinstance(doc.GetElement(eid), SpatialElementTag)]
        if not tags:
            # Try rooms — find their tags
            rooms = [doc.GetElement(eid) for eid in ids
                     if isinstance(doc.GetElement(eid), SpatialElement)]
            view = doc.ActiveView
            all_tags = list(FilteredElementCollector(doc, view.Id)
                            .OfClass(SpatialElementTag).ToElements())
            room_ids = {r.Id for r in rooms}
            tags = [t for t in all_tags
                    if t.TaggedLocalElementId in room_ids]
        return tags

    def _move_room_tags(self, doc, uidoc, anchor):
        tags = self._get_room_tags(doc, uidoc)
        if not tags: self._log('Select room tags or rooms'); return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Room Tag Sync'); t.Start()
        count = 0
        for tag in tags:
            try:
                room = tag.GetTaggedLocalElement()
                if not room: continue
                bb = room.get_BoundingBox(view)
                if not bb: continue
                cx = (bb.Min.X + bb.Max.X) / 2
                cy = (bb.Min.Y + bb.Max.Y) / 2
                if anchor == 'centroid':
                    pt = XYZ(cx, cy, bb.Min.Z)
                elif anchor == 'top_left':
                    pad = 0.25  # ft offset from corner
                    pt = XYZ(bb.Min.X + pad, bb.Max.Y - pad, bb.Min.Z)
                elif anchor == 'top_centre':
                    pad = 0.5
                    pt = XYZ(cx, bb.Max.Y - pad, bb.Min.Z)
                else:
                    pt = XYZ(cx, cy, bb.Min.Z)
                tag.TagHeadPosition = pt
                count += 1
            except Exception: pass
        t.Commit()
        self._log('Room tag sync ({}): {} tags'.format(anchor, count), '*')

    def RoomTagCentroid_Click(self, s, e):
        d, u = self._fd(); self._move_room_tags(d, u, 'centroid')
    def RoomTagTopLeft_Click(self, s, e):
        d, u = self._fd(); self._move_room_tags(d, u, 'top_left')
    def RoomTagTopCentre_Click(self, s, e):
        d, u = self._fd(); self._move_room_tags(d, u, 'top_centre')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── LEADER ENDPOINT LOCK / FREE
    # ═════════════════════════════════════════════════════════════════════════

    _locked_leader_ends = {}   # tag.Id.IntegerValue → XYZ

    def LeaderEndLock_Click(self, s, e):
        """Freeze leader arrowhead to Free endpoint at current position."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        ids = list(uidoc.Selection.GetElementIds())
        tags = [doc.GetElement(eid) for eid in ids
                if isinstance(doc.GetElement(eid), IndependentTag)]
        if not tags: self._log('Select tags with leaders'); return
        t = Transaction(doc, 'STINGTags Lock Leader End'); t.Start()
        count = 0
        for tag in tags:
            try:
                if not tag.HasLeader: continue
                # Store current end position then set to Free
                end_pt = tag.LeaderEnd
                self._locked_leader_ends[tag.Id.IntegerValue] = end_pt
                tag.LeaderEndCondition = LeaderEndCondition.Free
                tag.LeaderEnd = end_pt   # explicit fix
                count += 1
            except Exception: pass
        t.Commit()
        self._log('Leader end locked (Free + stored): {} tags'.format(count), '*')

    def LeaderEndFree_Click(self, s, e):
        """Release leader to Attached (snaps back to host element)."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        ids = list(uidoc.Selection.GetElementIds())
        tags = [doc.GetElement(eid) for eid in ids
                if isinstance(doc.GetElement(eid), IndependentTag)]
        t = Transaction(doc, 'STINGTags Free Leader End'); t.Start()
        count = 0
        for tag in tags:
            try:
                if not tag.HasLeader: continue
                tag.LeaderEndCondition = LeaderEndCondition.Attached
                self._locked_leader_ends.pop(tag.Id.IntegerValue, None)
                count += 1
            except Exception: pass
        t.Commit()
        self._log('Leader end freed (Attached): {} tags'.format(count), '*')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── LINKED MODEL ELEMENTS
    # ═════════════════════════════════════════════════════════════════════════

    def LinksList_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        links = list(FilteredElementCollector(doc)
                     .OfClass(RevitLinkInstance).ToElements())
        if not links: self._log('No Revit links in project'); return
        lines = []
        for lnk in links:
            try:
                name = lnk.Name
                status = str(lnk.GetLinkDocument()) != 'None'
                lines.append('{} — {}'.format(name, '✓ loaded' if status else '✗ unloaded'))
            except Exception:
                lines.append(str(lnk.Name))
        self._log('Links ({}):\n'.format(len(links)) + '\n'.join(lines), '*')

    def SelInLink_Click(self, s, e):
        """Pick a loaded link then select elements by category within it."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        links = list(FilteredElementCollector(doc)
                     .OfClass(RevitLinkInstance).ToElements())
        loaded = [lnk for lnk in links
                  if lnk.GetLinkDocument() is not None]
        if not loaded: self._log('No loaded links found'); return
        choice = forms.SelectFromList.show(
            [lnk.Name for lnk in loaded], title='Select link')
        if not choice: return
        lnk = next(l for l in loaded if l.Name == choice)
        link_doc = lnk.GetLinkDocument()
        # Pick category
        cats = set()
        for el in FilteredElementCollector(link_doc)\
                  .WhereElementIsNotElementType().ToElements():
            try:
                if el.Category: cats.add(el.Category.Name)
            except Exception: pass
        cat_choice = forms.SelectFromList.show(sorted(cats), title='Category in link')
        if not cat_choice: return
        matched = [el for el in FilteredElementCollector(link_doc)
                   .WhereElementIsNotElementType().ToElements()
                   if el.Category and el.Category.Name == cat_choice]
        self._log('Link "{}": {} "{}" elements (cannot select linked elements directly — '
                  'count reported)'.format(choice, len(matched), cat_choice), '*')

    def TagLinked_Click(self, s, e):
        """Place tags on visible linked elements using Revit's PickObject."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        # (module-level import)
        try:
            ref = uidoc.Selection.PickObject(
                ObjectType.LinkedElement, 'Pick a linked element to tag')
            if ref is None: return
            view = doc.ActiveView
            t = Transaction(doc, 'STINGTags Tag Linked Element'); t.Start()
            tag = IndependentTag.Create(
                doc, view.Id, ref, False,
                TagMode.TM_ADDBY_CATEGORY,
                TagOrientation.Horizontal,
                ref.GlobalPoint if ref.GlobalPoint else XYZ.Zero)
            t.Commit()
            self._log('Tagged linked element', '*')
        except Exception as ex:
            if 'cancelled' not in str(ex).lower():
                self._log('Tag linked: ' + str(ex))

    def LinksAudit_Click(self, s, e):
        """Export Revit link status to CSV."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        links = list(FilteredElementCollector(doc)
                     .OfClass(RevitLinkInstance).ToElements())
        path = os.path.join(tempfile.gettempdir(), 'STINGTags_LinksAudit.csv')
        with io.open(path, 'w', newline='') as f:
            w = _csv.writer(f)
            w.writerow(['Id','Name','Loaded','Path'])
            for lnk in links:
                try:
                    lnk_doc = lnk.GetLinkDocument()
                    loaded  = lnk_doc is not None
                    path_str = lnk_doc.PathName if loaded else 'unloaded'
                    w.writerow([lnk.Id.IntegerValue, lnk.Name, loaded, path_str])
                except Exception:
                    w.writerow([lnk.Id.IntegerValue, lnk.Name, False, ''])
        try:
            self._open_folder(path)
        except Exception: pass
        self._log('Links audit: {} links → {}'.format(len(links), path), '*')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── PDF EXPORT
    # ═════════════════════════════════════════════════════════════════════════

    def _get_pdf_size(self):
        try:
            item = self.PDFSizeCombo.SelectedItem
            return str(item.Content) if item else 'A3'
        except Exception:
            return 'A3'

    def _paper_size_name(self, size_str):
        """Map friendly name to Revit paper size name string."""
        return {'A1':'A1','A2':'A2','A3':'A3','A4':'A4'}.get(size_str, 'A3')

    def PDFExportSheets_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        ids = list(uidoc.Selection.GetElementIds())
        sheets = [doc.GetElement(eid) for eid in ids
                  if isinstance(doc.GetElement(eid), ViewSheet)]
        if not sheets: self._log('Select sheet(s) first'); return
        folder = tempfile.gettempdir()
        try:
            from Autodesk.Revit.DB import PDFExportOptions, PDFPaperSize, PDFColorDepthType
            opts = PDFExportOptions()
            opts.Combine = True
            fname = doc.Title + '_sheets'
            sheet_ids = List[ElementId]([sh.Id for sh in sheets])
            doc.Export(folder, fname, sheet_ids, opts)
            path = os.path.join(folder, fname + '.pdf')
            try:
                self._open_folder(path)
            except Exception: pass
            self._log('PDF exported: {} sheets → {}'.format(len(sheets), path), '*')
        except Exception as ex:
            # Revit < 2022 fallback message
            self._log('PDF export requires Revit 2022+. Error: ' + str(ex), '*')

    def PDFExportActive_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        folder = tempfile.gettempdir()
        try:
            from Autodesk.Revit.DB import PDFExportOptions
            opts = PDFExportOptions()
            opts.Combine = True
            view = doc.ActiveView
            fname = doc.Title + '_' + view.Name.replace(' ','_')
            doc.Export(folder, fname, List[ElementId]([view.Id]), opts)
            path = os.path.join(folder, fname + '.pdf')
            try:
                self._open_folder(path)
            except Exception: pass
            self._log('PDF exported: {} → {}'.format(view.Name, path), '*')
        except Exception as ex:
            self._log('PDF export error (Revit 2022+): ' + str(ex), '*')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── SHEET INDEX
    # ═════════════════════════════════════════════════════════════════════════

    def SheetIndexGenerate_Click(self, s, e):
        """Create a new ViewSchedule listing all sheets, optionally filtered by prefix."""
        doc, uidoc = self._fd()
        if not doc: return
        prefix = forms.ask_for_string(
            'Sheet number prefix filter (leave blank for all sheets):',
            default='')
        try:
            from Autodesk.Revit.DB import ScheduleDefinition, ScheduleFieldType
            from Autodesk.Revit.DB import ViewSchedule, SchedulableField
            from Autodesk.Revit.DB import BuiltInParameter as BIP
            t = Transaction(doc, 'STINGTags Sheet Index'); t.Start()
            sched = ViewSchedule.CreateSheetList(doc)
            sched.Name = 'STINGTags Sheet Index' + (' — ' + prefix if prefix else '')
            # Add filter if prefix given
            if prefix:
                defn = sched.Definition
                from Autodesk.Revit.DB import ScheduleFilter, ScheduleFilterType
                for i in range(defn.GetFieldCount()):
                    fld = defn.GetField(i)
                    try:
                        if 'Sheet Number' in fld.ColumnHeading:
                            sf = ScheduleFilter(fld.FieldId,
                                                ScheduleFilterType.BeginsWith,
                                                prefix)
                            defn.AddFilter(sf)
                            break
                    except Exception: pass
            t.Commit()
            self._log('Sheet index created: "{}"'.format(sched.Name), '[List]')
        except Exception as ex:
            self._log('Sheet index error: ' + str(ex))

    def SheetIndexExport_Click(self, s, e):
        """Export all sheet metadata to CSV."""
        doc, uidoc = self._fd()
        if not doc: return
        sheets = list(FilteredElementCollector(doc)
                      .OfClass(ViewSheet).ToElements())
        from Autodesk.Revit.DB import BuiltInParameter as BIP
        path = os.path.join(tempfile.gettempdir(), 'STINGTags_SheetIndex.csv')
        with io.open(path, 'w', newline='') as f:
            w = _csv.writer(f)
            w.writerow(['SheetNumber','SheetName','Discipline','RevisionDate',
                        'RevisionDescription','IsPlaceholder'])
            for sh in sheets:
                try:
                    disc = sh.LookupParameter('Discipline') or sh.LookupParameter('Sheet Discipline')
                    disc_val = disc.AsString() if disc else ''
                    rev_date = sh.get_Parameter(BIP.SHEET_CURRENT_REVISION_DATE)
                    rev_desc = sh.get_Parameter(BIP.SHEET_CURRENT_REVISION_DESCRIPTION)
                    w.writerow([
                        sh.SheetNumber,
                        sh.Name,
                        disc_val,
                        rev_date.AsString() if rev_date else '',
                        rev_desc.AsString() if rev_desc else '',
                        sh.IsPlaceholder,
                    ])
                except Exception: pass
        try:
            self._open_folder(path)
        except Exception: pass
        self._log('Sheet index CSV: {} sheets → {}'.format(len(sheets), path), '[List]')

    def SheetIndexFilter_Click(self, s, e):
        """Filter current selection to sheets matching a number prefix."""
        doc, uidoc = self._fd()
        if not doc: return
        prefix = forms.ask_for_string('Sheet number prefix (e.g. "E-"):')
        if prefix is None: return
        sheets = list(FilteredElementCollector(doc)
                      .OfClass(ViewSheet).ToElements())
        matched = [sh for sh in sheets
                   if sh.SheetNumber.startswith(prefix)]
        self._set_ids(uidoc, matched)
        self._log('Sheets matching "{}": {}'.format(prefix, len(matched)), '[List]')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── CROP REGION RESIZE
    # ═════════════════════════════════════════════════════════════════════════

    def CropRegionSet_Click(self, s, e):
        """Resize active view crop box to typed W×H (mm) at typed scale."""
        doc, uidoc = self._fd()
        if not doc: return
        try:
            w_mm   = float(self.CropWidthInput.Text.strip())
            h_mm   = float(self.CropHeightInput.Text.strip())
            scale  = float(self.CropScaleInput.Text.strip()) or 100.0
        except Exception:
            self._log('Enter numeric W / H / Scale values'); return
        view = doc.ActiveView
        if not view.CropBoxActive:
            t_on = Transaction(doc, 'Enable Crop'); t_on.Start()
            view.CropBoxActive = True; t_on.Commit()
        # Convert mm at print scale → model feet
        # model_size_ft = (print_mm / 1000) * scale / 0.3048
        w_ft = (w_mm / 1000.0) * scale / 0.3048
        h_ft = (h_mm / 1000.0) * scale / 0.3048
        crop = view.CropBox
        centre = (crop.Min + crop.Max) * 0.5
        # (module-level import)
        new_box = BoundingBoxXYZ()
        new_box.Min = XYZ(centre.X - w_ft / 2, centre.Y - h_ft / 2, crop.Min.Z)
        new_box.Max = XYZ(centre.X + w_ft / 2, centre.Y + h_ft / 2, crop.Max.Z)
        t = Transaction(doc, 'STINGTags Crop Region'); t.Start()
        view.CropBox = new_box; t.Commit()
        self._log('Crop set: {}×{} mm @1:{} — {}×{:.0f} ft model'.format(
            w_mm, h_mm, int(scale), round(w_ft,2), round(h_ft,2)), '[Align]')

    def CropRegionRead_Click(self, s, e):
        """Read current crop size and fill the W/H fields."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        try:
            if not view.CropBoxActive:
                self._log('Crop box is not active on this view'); return
            crop = view.CropBox
            scale = view.Scale or 100
            w_ft = crop.Max.X - crop.Min.X
            h_ft = crop.Max.Y - crop.Min.Y
            w_mm = int(round(w_ft * 0.3048 * 1000 / scale))
            h_mm = int(round(h_ft * 0.3048 * 1000 / scale))
            self.CropWidthInput.Text  = str(w_mm)
            self.CropHeightInput.Text = str(h_mm)
            self.CropScaleInput.Text  = str(scale)
            self._log('Crop read: {}×{} mm @1:{}'.format(w_mm, h_mm, scale), '[List]')
        except Exception as ex:
            self._log('Crop read error: ' + str(ex))

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── VIEW FILTER TOGGLES
    # ═════════════════════════════════════════════════════════════════════════

    _filter_toggle_refs = []   # list of (filter_id, checkbox_widget)

    def FilterToggleRefresh_Click(self, s, e):
        """Populate FilterTogglePanel with checkboxes for each filter on active view."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        # CheckBox, StackPanel imported at module level
        # (module-level import)
        view = doc.ActiveView
        try:
            filter_ids = list(view.GetFilters())
        except Exception:
            self._log('Active view has no filters or does not support filters'); return
        self.FilterTogglePanel.Children.Clear()
        self._filter_toggle_refs = []
        if not filter_ids:
            self.FilterToggleStatus.Text = 'No filters on active view'
            return
        for fid in filter_ids:
            try:
                filt = doc.GetElement(fid)
                if not filt: continue
                enabled = view.GetFilterVisibility(fid)
                cb = CheckBox()
                cb.Content    = filt.Name
                cb.IsChecked  = enabled
                cb.FontSize   = 8
                cb.Margin     = Thickness(3, 2, 0, 2)
                cb.Tag        = fid.IntegerValue
                cb.Checked   += self._filter_toggle_change
                cb.Unchecked += self._filter_toggle_change
                self.FilterTogglePanel.Children.Add(cb)
                self._filter_toggle_refs.append((fid, cb))
            except Exception: pass
        self.FilterToggleStatus.Text = '{} filters'.format(len(self._filter_toggle_refs))
        self._log('Filter toggles loaded: {} filters'.format(
            len(self._filter_toggle_refs)), '*')

    def _filter_toggle_change(self, s, e):
        """Checkbox changed — enable or disable that filter."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        if not isinstance(s, CheckBox): return
        fid_int  = s.Tag
        enabled  = bool(s.IsChecked)
        view     = doc.ActiveView
        try:
            fid = ElementId(int(fid_int))
            t = Transaction(doc, 'STINGTags Filter Toggle'); t.Start()
            view.SetFilterVisibility(fid, enabled)
            t.Commit()
        except Exception as ex:
            self._log('Filter toggle error: ' + str(ex))

    def FilterToggleAllOn_Click(self, s, e):
        self._filter_toggle_all(True)

    def FilterToggleAllOff_Click(self, s, e):
        self._filter_toggle_all(False)

    def _filter_toggle_all(self, state):
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        t = Transaction(doc, 'STINGTags Filters All {}'.format('ON' if state else 'OFF'))
        t.Start()
        for fid, cb in self._filter_toggle_refs:
            try:
                view.SetFilterVisibility(fid, state)
                cb.IsChecked = state
            except Exception: pass
        t.Commit()
        self._log('All filters: {}'.format('ON' if state else 'OFF'), '*')

    # ═════════════════════════════════════════════════════════════════════════
    # v9.5 ── COLOUR BY ISO COMPLETENESS GRADIENT
    # ═════════════════════════════════════════════════════════════════════════

    def ColourByISOCompleteness_Click(self, s, e):
        """Apply green→amber→red fill gradient to all in-scope elements
        based on their ISO 19650 completeness score."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        elements = self._iso_collect(doc, uidoc)
        if not elements:
            self._log('No elements in scope for ISO completeness colour'); return
        # (module-level import)
        solid_id = None
        for fp in FilteredElementCollector(doc).OfClass(FillPatternElement).ToElements():
            try:
                if fp.GetFillPattern().IsSolidFill: solid_id = fp.Id; break
            except Exception: pass
        # Colours: green (100%) → amber (50%) → red (0%)
        def _rag_color(pct):
            """Linearly interpolate: 0=red, 50=amber, 100=green."""
            if pct >= 100: return Color(76, 175, 80)    # #4CAF50 green
            if pct >= 50:
                # amber at 50, green at 100 → t goes 0→1
                t_ = (pct - 50) / 50.0
                return Color(
                    int(255 + (76 - 255) * t_),
                    int(152 + (175 - 152) * t_),
                    int(0   + (80 - 0)   * t_))
            else:
                # red at 0, amber at 50 → t goes 0→1
                t_ = pct / 50.0
                return Color(
                    int(244 + (255 - 244) * t_),
                    int(67  + (152 - 67)  * t_),
                    int(54  + (0   - 54)  * t_))
        t = Transaction(doc, 'STINGTags ISO Completeness Colour'); t.Start()
        green = amber = red = 0
        for el, _cat in elements:
            try:
                filled = sum(1 for p in self._ISO_PARAMS if self._iso_get(el, p))
                pct    = int(filled * 100 / len(self._ISO_PARAMS)) if self._ISO_PARAMS else 0
                col    = _rag_color(pct)
                if pct == 100: green += 1
                elif pct >= 50: amber += 1
                else: red += 1
                ogs = OverrideGraphicSettings()
                ogs.SetSurfaceForegroundPatternColor(col)
                ogs.SetSurfaceForegroundPatternVisible(True)
                if solid_id: ogs.SetSurfaceForegroundPatternId(solid_id)
                view.SetElementOverrides(el.Id, ogs)
            except Exception: pass
        t.Commit()
        self._log('ISO completeness colours: ? {} ? {} ? {} elements'.format(
            green, amber, red), '*')

    def _push_undo_labelled(self, doc, view, label):
        """Push a snapshot with a readable label for the history combo."""
        try:
            vid = view.Id.IntegerValue
            tags = list(FilteredElementCollector(doc, view.Id)
                        .OfClass(IndependentTag).ToElements())
            snapshot = {'label': label, 'tags': {}}
            for tag in tags:
                try:
                    snapshot['tags'][str(tag.Id.IntegerValue)] = {
                        'pos': tag.TagHeadPosition,
                        'ldr': tag.HasLeader,
                    }
                except Exception: pass
            if vid not in self._undo_stack:
                self._undo_stack[vid] = []
            self._undo_stack[vid].append(snapshot)
            if len(self._undo_stack[vid]) > 5:
                self._undo_stack[vid] = self._undo_stack[vid][-5:]
            self._refresh_undo_combo()
        except Exception: pass

    # ═══════════════════════════════════════════════════════════════════════════
    # v9.6 ── PROJECT HEALTH SCORE
    # Multi-metric project quality index shown as 0–100 badge
    # ═══════════════════════════════════════════════════════════════════════════

    _health_metrics = []   # list of (metric_name, score_0_100, weight, detail)

    def _compute_health(self, doc, uidoc, scope):
        """
        Compute 6-metric weighted health score.
        Returns (overall_0_100, list_of_metric_dicts)
        """
        metrics = []

        # ── METRIC 1: ISO 19650 completeness (weight 25)
        try:
            elements = self._iso_collect(doc, uidoc)
            if elements:
                pcts = []
                for el, _c in elements:
                    filled = sum(1 for p in self._ISO_PARAMS if self._iso_get(el, p))
                    pcts.append(int(filled * 100 / len(self._ISO_PARAMS)) if self._ISO_PARAMS else 100)
                iso_score = int(sum(pcts) / len(pcts))
                metrics.append({
                    'name': 'ISO 19650 Completeness',
                    'score': iso_score,
                    'weight': 25,
                    'detail': 'Avg {}% across {} elements'.format(iso_score, len(elements)),
                    'rag': 'G' if iso_score >= 80 else ('A' if iso_score >= 50 else 'R'),
                })
            else:
                metrics.append({'name':'ISO 19650 Completeness','score':0,'weight':25,
                                 'detail':'No tagged elements found','rag':'R'})
        except Exception as ex:
            metrics.append({'name':'ISO 19650 Completeness','score':50,'weight':25,
                             'detail':'Error: '+str(ex)[:50],'rag':'A'})

        # ── METRIC 2: Tag coverage (weight 20)
        try:
            view = doc.ActiveView
            all_el = list(FilteredElementCollector(doc, view.Id)
                          .WhereElementIsNotElementType().ToElements())
            taggable = [e for e in all_el if e.Category and
                        e.Category.HasMaterialQuantities and not
                        isinstance(e, (IndependentTag, SpatialElementTag))]
            tags = list(FilteredElementCollector(doc, view.Id)
                        .OfClass(IndependentTag).ToElements())
            tagged_ids = set()
            for t in tags:
                try: tagged_ids.add(t.TaggedLocalElementId)
                except Exception: pass
            coverage = int(len(tagged_ids) * 100 / len(taggable)) if taggable else 100
            coverage = min(coverage, 100)
            metrics.append({
                'name': 'Tag Coverage',
                'score': coverage,
                'weight': 20,
                'detail': '{}/{} elements tagged in active view'.format(len(tagged_ids), len(taggable)),
                'rag': 'G' if coverage >= 80 else ('A' if coverage >= 50 else 'R'),
            })
        except Exception as ex:
            metrics.append({'name':'Tag Coverage','score':50,'weight':20,
                             'detail':'Error: '+str(ex)[:50],'rag':'A'})

        # ── METRIC 3: Leader consistency (weight 15)
        try:
            view = doc.ActiveView
            tags_all = list(FilteredElementCollector(doc, view.Id)
                            .OfClass(IndependentTag).ToElements())
            if tags_all:
                with_leader = sum(1 for t in tags_all if t.HasLeader)
                ratio = with_leader * 100 // len(tags_all)
                # Ideal: 40-70% with leaders (some context)
                if 40 <= ratio <= 70:
                    ldr_score = 100
                elif ratio < 40:
                    ldr_score = int(ratio * 100 / 40)
                else:
                    ldr_score = int((100 - ratio) * 100 / 30)
                metrics.append({
                    'name': 'Leader Consistency',
                    'score': max(ldr_score, 0),
                    'weight': 15,
                    'detail': '{}% of {} tags have leaders'.format(ratio, len(tags_all)),
                    'rag': 'G' if ldr_score >= 80 else ('A' if ldr_score >= 50 else 'R'),
                })
            else:
                metrics.append({'name':'Leader Consistency','score':100,'weight':15,
                                 'detail':'No tags in view','rag':'G'})
        except Exception as ex:
            metrics.append({'name':'Leader Consistency','score':50,'weight':15,
                             'detail':'Error: '+str(ex)[:50],'rag':'A'})

        # ── METRIC 4: View naming compliance (weight 15)
        try:
            views = list(FilteredElementCollector(doc).OfClass(View).ToElements())
            good = 0
            total = 0
            # Naming convention: discipline prefix + hyphen + descriptor
            for v in views:
                try:
                    if v.IsTemplate or not v.CanBePrinted: continue
                    total += 1
                    name = v.Name
                    # Check: has underscore or hyphen separator, not default names
                    if any(sep in name for sep in ['-', '_']) and \
                       name not in ('Level 1', 'Elevation', 'Section', 'Detail', 'Plan'):
                        good += 1
                except Exception: pass
            naming_score = int(good * 100 / total) if total else 100
            metrics.append({
                'name': 'View Naming Compliance',
                'score': naming_score,
                'weight': 15,
                'detail': '{}/{} views use consistent naming'.format(good, total),
                'rag': 'G' if naming_score >= 80 else ('A' if naming_score >= 50 else 'R'),
            })
        except Exception as ex:
            metrics.append({'name':'View Naming Compliance','score':50,'weight':15,
                             'detail':'Error: '+str(ex)[:50],'rag':'A'})

        # ── METRIC 5: Parameter fill rate (weight 15)
        try:
            db = self._load_param_db()
            view = doc.ActiveView
            els = list(FilteredElementCollector(doc, view.Id)
                        .WhereElementIsNotElementType().ToElements())[:200]
            if els:
                total_p = fill_p = 0
                for el in els:
                    try:
                        for p in el.Parameters:
                            try:
                                if p.Definition.Name in db:
                                    total_p += 1
                                    v = p.AsString() or p.AsValueString()
                                    if v and v.strip(): fill_p += 1
                            except Exception: pass
                    except Exception: pass
                fill_rate = int(fill_p * 100 / total_p) if total_p else 100
                metrics.append({
                    'name': 'Parameter Fill Rate',
                    'score': fill_rate,
                    'weight': 15,
                    'detail': '{}/{} DB params filled (sample 200 el)'.format(fill_p, total_p),
                    'rag': 'G' if fill_rate >= 70 else ('A' if fill_rate >= 40 else 'R'),
                })
            else:
                metrics.append({'name':'Parameter Fill Rate','score':100,'weight':15,
                                 'detail':'No elements sampled','rag':'G'})
        except Exception as ex:
            metrics.append({'name':'Parameter Fill Rate','score':50,'weight':15,
                             'detail':'Error: '+str(ex)[:50],'rag':'A'})

        # ── METRIC 6: Annotation density balance (weight 10)
        try:
            view = doc.ActiveView
            tags_v = list(FilteredElementCollector(doc, view.Id)
                          .OfClass(IndependentTag).ToElements())
            if len(tags_v) > 2:
                bb = view.CropBox
                w = abs(bb.Max.X - bb.Min.X)
                h = abs(bb.Max.Y - bb.Min.Y)
                if w > 0 and h > 0:
                    density = len(tags_v) / (w * h)
                    # 0-0.5 annotations/sq-ft → ideal
                    ideal_max = 0.5
                    d_score = int(max(0, 1 - max(0, density - ideal_max) / ideal_max) * 100)
                else:
                    d_score = 80
            else:
                d_score = 90
            metrics.append({
                'name': 'Annotation Density',
                'score': d_score,
                'weight': 10,
                'detail': '{} tags in active view'.format(len(tags_v)),
                'rag': 'G' if d_score >= 70 else ('A' if d_score >= 40 else 'R'),
            })
        except Exception as ex:
            metrics.append({'name':'Annotation Density','score':50,'weight':10,
                             'detail':'Error: '+str(ex)[:50],'rag':'A'})

        # Weighted overall
        total_w = sum(m['weight'] for m in metrics)
        overall  = int(sum(m['score'] * m['weight'] for m in metrics) / total_w) if total_w else 0
        return overall, metrics

    def _health_rag_color(self, rag):
        if rag == 'G': return '#4CAF50'
        if rag == 'A': return '#FF9800'
        return '#F44336'

    def HealthScore_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        try:
            scope_item = self.HealthScopeCombo.SelectedItem
            scope = str(scope_item.Content) if scope_item else 'Active View'
        except Exception:
            scope = 'Active View'
        overall, metrics = self._compute_health(doc, uidoc, scope)
        self._health_metrics = metrics
        self.HealthScoreBadge.Text = '{}/100'.format(overall)
        # Set badge colour
        try:
            # (module-level import)
            # MColor imported at module level
            if overall >= 80:
                hex_c = '4CAF50'
            elif overall >= 60:
                hex_c = 'FF9800'
            else:
                hex_c = 'F44336'
            r, g, b = int(hex_c[0:2],16), int(hex_c[2:4],16), int(hex_c[4:6],16)
            brush = SolidColorBrush(MColor.FromArgb(255, r, g, b))
            self.HealthScoreBadge.Parent.Background = brush
        except Exception: pass
        # Populate breakdown panel
        self.HealthBreakdownPanel.Children.Clear()
        # TextBlock imported at module level
        # (module-level import)
        # (module-level import)
        # MColor imported at module level
        for m in metrics:
            tb = TextBlock()
            rag_sym = {'G': '?', 'A': '?', 'R': '?'}.get(m['rag'], '•')
            tb.Text = '{} {} — {}%  |  {}'.format(rag_sym, m['name'], m['score'], m['detail'])
            tb.FontSize = 7
            tb.TextWrapping = 2  # Wrap
            tb.Margin = Thickness(0, 1, 0, 1)
            self.HealthBreakdownPanel.Children.Add(tb)
        self._log('Health score: {}/100  ({})'.format(overall, scope), '*')

    def HealthReport_Click(self, s, e):
        doc, uidoc = self._fd()
        if not doc: return
        if not self._health_metrics:
            self.HealthScore_Click(s, e)
        if not self._health_metrics:
            self._log('No health data — run Health Score first'); return
        try:
            path = forms.save_file(
                file_ext='csv',
                default_name='STINGTags_HealthReport.csv',
                title='Save Health Report')
        except Exception:
            path = None
        if not path: return
        try:
            with io.open(path, 'w', newline='', encoding='utf-8-sig') as f:
                w = _csv.writer(f)
                w.writerow(['Metric', 'Score', 'Weight', 'RAG', 'Detail'])
                for m in self._health_metrics:
                    w.writerow([m.get('name',''), m.get('score',''),
                                 m.get('weight',''), m.get('rag',''), m.get('detail','')])
            try:
                self._open_file_safe(path)
            except Exception: pass
            self._log('Health report saved: {} metrics → {}'.format(
                len(self._health_metrics), path), '📊')
        except Exception as ex:
            self._log('Health report error: ' + str(ex))

    def HealthFixAll_Click(self, s, e):
        """AI auto-fix: address any Red/Amber metrics where safe automated fixes exist."""
        doc, uidoc = self._fd()
        if not doc: return
        if not self._health_metrics:
            self.HealthScore_Click(s, e)
        fixes_applied = []
        for m in self._health_metrics:
            if m['rag'] == 'G': continue
            name = m['name']
            try:
                if name == 'Leader Consistency' and m['score'] < 50:
                    # Auto-add leaders to tags without them
                    view = doc.ActiveView
                    tags_no_ldr = [t for t in
                                   FilteredElementCollector(doc, view.Id)
                                   .OfClass(IndependentTag).ToElements()
                                   if not t.HasLeader]
                    if tags_no_ldr:
                        tx = Transaction(doc, 'STINGTags Fix Leader Consistency'); tx.Start()
                        for tg in tags_no_ldr[:50]:  # cap at 50
                            try: tg.HasLeader = True
                            except Exception: pass
                        tx.Commit()
                        fixes_applied.append('Added leaders to {} tags'.format(min(len(tags_no_ldr), 50)))
                elif name == 'View Naming Compliance' and m['score'] < 50:
                    fixes_applied.append('View naming: manual renaming required — use ORGANISE tab')
                elif name == 'ISO 19650 Completeness' and m['score'] < 50:
                    fixes_applied.append('ISO completeness: run ISO tab auto-populate tools')
            except Exception as ex:
                fixes_applied.append('Error fixing {}: {}'.format(name, str(ex)[:40]))
        if fixes_applied:
            self._log('HealthFixAll applied:\n' + '\n'.join(fixes_applied), '[Fix]')
            self.HealthScore_Click(s, e)  # re-score
        else:
            self._log('HealthFixAll: no automated fixes applicable at this score level', '[Fix]')

    # ═══════════════════════════════════════════════════════════════════════════
    # v9.6 ── PARAMETER ANOMALY DETECTION
    # Statistical outlier analysis: >2σ, empties, duplicates, type/instance mismatches
    # ═══════════════════════════════════════════════════════════════════════════

    _anomaly_results = []

    def AnomalyParamRefresh_Click(self, s, e):
        """Populate AnomalyParamCombo from writable params on current selection."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        ids = list(uidoc.Selection.GetElementIds())
        elems = ([doc.GetElement(eid) for eid in ids] if ids else
                 list(FilteredElementCollector(doc, doc.ActiveView.Id)
                      .WhereElementIsNotElementType().ToElements())[:100])
        seen = {}
        for el in elems:
            try:
                for p in el.Parameters:
                    try:
                        # (module-level import)
                        if p.StorageType in (StorageType.Double, StorageType.Integer):
                            seen[p.Definition.Name] = p.StorageType
                    except Exception: pass
            except Exception: pass
        self.AnomalyParamCombo.Items.Clear()
        hdr = CBI(); hdr.Content = '— All numeric params ({}) —'.format(len(seen)); self.AnomalyParamCombo.Items.Add(hdr)
        for pn in sorted(seen):
            ci = CBI(); ci.Content = pn; ci.Tag = pn; self.AnomalyParamCombo.Items.Add(ci)
        self.AnomalyParamCombo.SelectedIndex = 0

    def AnomalyDetect_Click(self, s, e):
        """Run statistical anomaly detection on selected elements."""
        doc, uidoc = self._fd()
        if not doc: return
        # (module-level import)
        ids = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements first'); return
        elems = [doc.GetElement(eid) for eid in ids if doc.GetElement(eid)]

        # Which params to check?
        try:
            item = self.AnomalyParamCombo.SelectedItem
            target_pname = (str(item.Tag) if item and hasattr(item,'Tag') and item.Tag else None)
        except Exception:
            target_pname = None

        # Collect values per param
        # (module-level import)
        param_vals = defaultdict(list)  # pname → [(elem, value)]
        for el in elems:
            try:
                for p in el.Parameters:
                    try:
                        pn = p.Definition.Name
                        if target_pname and pn != target_pname: continue
                        if p.StorageType == StorageType.Double:
                            param_vals[pn].append((el, p.AsDouble()))
                        elif p.StorageType == StorageType.Integer:
                            param_vals[pn].append((el, float(p.AsInteger())))
                        elif p.StorageType == StorageType.String:
                            v = p.AsString()
                            if v: param_vals[pn + '_str'].append((el, v))
                    except Exception: pass
            except Exception: pass

        anomalies = []

        # Statistical outliers for numeric params
        for pn, pairs in param_vals.items():
            if pn.endswith('_str'):
                # Duplicate detection for string params
                # (module-level import)
                vals_only = [v for _, v in pairs]
                counts = Counter(vals_only)
                dups = {v: c for v, c in counts.items() if c > 1 and v}
                for v, c in dups.items():
                    anomalies.append({
                        'type': 'DUPLICATE',
                        'param': pn[:-4],
                        'value': str(v),
                        'count': c,
                        'ids': [el.Id.IntegerValue for el, vv in pairs if vv == v],
                        'severity': 'WARN',
                    })
                continue
            if len(pairs) < 3: continue
            vals = [v for _, v in pairs]
            mean = sum(vals) / len(vals)
            variance = sum((x - mean) ** 2 for x in vals) / len(vals)
            std = variance ** 0.5
            if std < 1e-9: continue
            for el, v in pairs:
                z = abs(v - mean) / std
                if z > 2.5:
                    anomalies.append({
                        'type': 'OUTLIER',
                        'param': pn,
                        'value': '{:.3f} (z={:.1f})'.format(v, z),
                        'count': 1,
                        'ids': [el.Id.IntegerValue],
                        'severity': 'HIGH' if z > 3.5 else 'WARN',
                    })

        # Empty mandatory params check (params in param_db marked as mandatory group)
        try:
            db = self._load_param_db()
            mandatory = [pn for pn, v in db.items() if v.get('gr','') in
                         ('ASS_MNG','ISO_ID','PRJ_ID')]
            for el in elems:
                try:
                    for pn in mandatory:
                        p = el.LookupParameter(pn)
                        if p:
                            v = p.AsString() or p.AsValueString()
                            if not v or not v.strip():
                                anomalies.append({
                                    'type': 'EMPTY_MANDATORY',
                                    'param': pn,
                                    'value': '(empty)',
                                    'count': 1,
                                    'ids': [el.Id.IntegerValue],
                                    'severity': 'HIGH',
                                })
                except Exception: pass
        except Exception: pass

        self._anomaly_results = anomalies
        self._anomaly_elements = elems  # store for export

        # Display summary
        if not anomalies:
            self.AnomalyResults.Text = '✓ No anomalies found across {} elements'.format(len(elems))
        else:
            high = sum(1 for a in anomalies if a.get('severity') == 'HIGH')
            warn = len(anomalies) - high
            lines = ['Found {} anomalies ({} HIGH, {} WARN) in {} elements:'.format(
                len(anomalies), high, warn, len(elems))]
            for a in anomalies[:15]:
                sym = '?' if a['severity'] == 'HIGH' else '?'
                lines.append('{} [{}] {} = {} (elem {})'.format(
                    sym, a['type'], a['param'], a['value'],
                    a['ids'][0] if a['ids'] else '?'))
            if len(anomalies) > 15:
                lines.append('... and {} more — click Export for full list'.format(len(anomalies)-15))
            self.AnomalyResults.Text = '\n'.join(lines)
        self._log('Anomaly scan: {} found in {} elements'.format(len(anomalies), len(elems)), '[Scan]')

        # Auto-select HIGH severity elements
        if anomalies:
            high_ids = set()
            for a in anomalies:
                if a.get('severity') == 'HIGH':
                    high_ids.update(a.get('ids', []))
            if high_ids:
                self._set_ids(uidoc, [doc.GetElement(ElementId(int(i)))
                                  for i in high_ids if doc.GetElement(ElementId(int(i)))])

    def AnomalyExport_Click(self, s, e):
        if not self._anomaly_results:
            self._log('Run anomaly detection first'); return
        try:
            path = forms.save_file(
                file_ext='csv',
                default_name='STINGTags_AnomalyReport.csv',
                title='Save Anomaly Report')
        except Exception:
            path = None
        if not path: return
        try:
            with io.open(path, 'w', newline='', encoding='utf-8-sig') as f:
                w = _csv.writer(f)
                w.writerow(['Severity','Type','Parameter','Value','Count','ElementIds'])
                for a in self._anomaly_results:
                    w.writerow([a.get('severity',''), a.get('type',''), a.get('param',''),
                                 a.get('value',''), a.get('count',''),
                                 ';'.join(str(i) for i in a.get('ids',[]))])
            try:
                self._open_file_safe(path)
            except Exception: pass
            self._log('Anomaly report: {} rows saved'.format(len(self._anomaly_results)), '🔍')
        except Exception as ex:
            self._log('Anomaly export error: ' + str(ex))

    # ═══════════════════════════════════════════════════════════════════════════
    # v9.6 ── AI CONTEXT-AWARE TAG PLACEMENT
    # Density-aware optimal positioning using bounding box analysis
    # ═══════════════════════════════════════════════════════════════════════════

    _ai_place_snapshot  = {}   # elem_id → old TagHeadPosition
    _ai_place_options   = {'clearance_ft': 0.5, 'max_leader_ft': 3.0, 'preferred_quadrant': 'auto'}

    def _ai_density_grid(self, doc, view, grid_divisions=10):
        """Divide view bounding box into a density grid. Returns dict cell→count."""
        try:
            bb = view.CropBox
            if not bb: return {}, None, None
        except Exception:
            return {}, None, None
        w = bb.Max.X - bb.Min.X
        h = bb.Max.Y - bb.Min.Y
        if w <= 0 or h <= 0: return {}, None, None
        cell_w = w / grid_divisions
        cell_h = h / grid_divisions
        grid = {}
        for el in FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType():
            try:
                ebb = el.get_BoundingBox(view)
                if not ebb: continue
                cx = (ebb.Min.X + ebb.Max.X) / 2
                cy = (ebb.Min.Y + ebb.Max.Y) / 2
                col = int((cx - bb.Min.X) / cell_w)
                row = int((cy - bb.Min.Y) / cell_h)
                col = max(0, min(col, grid_divisions - 1))
                row = max(0, min(row, grid_divisions - 1))
                grid[(col, row)] = grid.get((col, row), 0) + 1
            except Exception: pass
        return grid, bb, (cell_w, cell_h)

    def _ai_find_best_position(self, host_pt, grid, bb, cell_dims, clearance_ft, max_leader_ft):
        """Find lowest-density position near host within leader range."""
        if not grid or not bb or not cell_dims: return None
        cell_w, cell_h = cell_dims
        # Sample candidate offsets in quadrants
        offsets = [
            XYZ( 1.5, 1.5, 0), XYZ(-1.5, 1.5, 0),
            XYZ( 1.5,-1.5, 0), XYZ(-1.5,-1.5, 0),
            XYZ( 2.0, 0.0, 0), XYZ(-2.0, 0.0, 0),
            XYZ( 0.0, 2.0, 0), XYZ( 0.0,-2.0, 0),
        ]
        best_pt = None
        best_density = float('inf')
        for off in offsets:
            try:
                candidate = XYZ(host_pt.X + off.X, host_pt.Y + off.Y, host_pt.Z)
                # Check within view bounds
                if not (bb.Min.X <= candidate.X <= bb.Max.X and
                        bb.Min.Y <= candidate.Y <= bb.Max.Y): continue
                # Check distance within leader limit
                dist = ((off.X**2 + off.Y**2) ** 0.5)
                if dist > max_leader_ft: continue
                # Get grid density at candidate
                col = int((candidate.X - bb.Min.X) / cell_w)
                row = int((candidate.Y - bb.Min.Y) / cell_h)
                density = grid.get((col, row), 0)
                if density < best_density:
                    best_density = density
                    best_pt = candidate
            except Exception: pass
        return best_pt

    def AISmartPlace_Click(self, s, e):
        """AI-powered optimal tag placement for selected elements."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        ids  = list(uidoc.Selection.GetElementIds())
        if not ids: self._log('Select elements to place tags on'); return

        # Build density grid
        grid, bb, cell_dims = self._ai_density_grid(doc, view)
        opts = self._ai_place_options
        clearance  = opts.get('clearance_ft', 0.5)
        max_leader = opts.get('max_leader_ft', 3.0)

        # Find existing tags for selected elements
        all_tags = list(FilteredElementCollector(doc, view.Id)
                        .OfClass(IndependentTag).ToElements())
        sel_id_set = set(eid.IntegerValue for eid in ids)
        target_tags = [t for t in all_tags
                       if t.TaggedLocalElementId.IntegerValue in sel_id_set]

        if not target_tags:
            self._log('No existing tags for selected elements — place tags first'); return

        # Save snapshot for undo
        self._ai_place_snapshot = {}
        for tag in target_tags:
            try:
                self._ai_place_snapshot[tag.Id.IntegerValue] = tag.TagHeadPosition
            except Exception: pass

        placed = 0
        tx = Transaction(doc, 'STINGTags AI Smart Place'); tx.Start()
        for tag in target_tags:
            try:
                host_pt = tag.TagHeadPosition
                best = self._ai_find_best_position(host_pt, grid, bb, cell_dims,
                                                    clearance, max_leader)
                if best:
                    tag.TagHeadPosition = best
                    # Update density grid
                    if cell_dims:
                        cw, ch = cell_dims
                        col = int((best.X - bb.Min.X) / cw)
                        row = int((best.Y - bb.Min.Y) / ch)
                        grid[(col, row)] = grid.get((col, row), 0) + 1
                    placed += 1
            except Exception: pass
        tx.Commit()

        # Update confidence badge
        conf = int(placed * 100 / len(target_tags)) if target_tags else 0
        self.AIPlaceConfidence.Text = '{}% conf'.format(conf)
        self._log('AI Smart Place: {}/{} tags repositioned'.format(placed, len(target_tags)), '[Bot]')

    def AIDensityMap_Click(self, s, e):
        """Colour-code view by annotation density (green = sparse, red = crowded)."""
        doc, uidoc = self._fd()
        if not doc: return
        view = doc.ActiveView
        grid, bb, cell_dims = self._ai_density_grid(doc, view)
        if not grid or not bb or not cell_dims:
            self._log('Cannot build density map for this view type'); return
        max_density = max(grid.values()) if grid else 1
        if max_density == 0: max_density = 1

        # (module-level import)
        solid_id = None
        for fp in FilteredElementCollector(doc).OfClass(FillPatternElement).ToElements():
            try:
                if fp.GetFillPattern().IsSolidFill: solid_id = fp.Id; break
            except Exception: pass

        elements = list(FilteredElementCollector(doc, view.Id)
                        .WhereElementIsNotElementType().ToElements())
        cw, ch = cell_dims

        tx = Transaction(doc, 'STINGTags Density Map'); tx.Start()
        count = 0
        for el in elements:
            try:
                ebb = el.get_BoundingBox(view)
                if not ebb: continue
                cx = (ebb.Min.X + ebb.Max.X) / 2
                cy = (ebb.Min.Y + ebb.Max.Y) / 2
                col = int((cx - bb.Min.X) / cw)
                row = int((cy - bb.Min.Y) / ch)
                density = grid.get((col, row), 0)
                t_norm = density / max_density   # 0=empty, 1=max
                # green (low) → amber → red (high)
                if t_norm < 0.5:
                    r = int(t_norm * 2 * 255)
                    g = 200
                else:
                    r = 220
                    g = int((1 - t_norm) * 2 * 200)
                col_c = Color(r, g, 60)
                ogs = OverrideGraphicSettings()
                ogs.SetSurfaceForegroundPatternColor(col_c)
                ogs.SetSurfaceForegroundPatternVisible(True)
                if solid_id: ogs.SetSurfaceForegroundPatternId(solid_id)
                ogs.SetProjectionLineColor(col_c)
                view.SetElementOverrides(el.Id, ogs)
                count += 1
            except Exception: pass
        tx.Commit()
        self._log('Density map applied: {} elements coloured'.format(count), '[Place]')

    def AIPlaceUndo_Click(self, s, e):
        """Restore tag positions from snapshot taken before AI placement."""
        doc, uidoc = self._fd()
        if not doc: return
        if not self._ai_place_snapshot:
            self._log('No AI placement snapshot to restore'); return
        tx = Transaction(doc, 'STINGTags AI Place Undo'); tx.Start()
        restored = 0
        for tag_id_int, pos in self._ai_place_snapshot.items():
            try:
                tag = doc.GetElement(ElementId(int(tag_id_int)))
                if tag:
                    tag.TagHeadPosition = pos
                    restored += 1
            except Exception: pass
        tx.Commit()
        self._ai_place_snapshot = {}
        self.AIPlaceConfidence.Text = '-- --'
        self._log('AI placement undone: {} tags restored'.format(restored), '↺')

    def AIPlaceOptions_Click(self, s, e):
        """Configure AI placement parameters."""
        opts = self._ai_place_options
        clearance = forms.ask_for_string(
            'Clearance radius (ft) between tags [{} current]:'.format(opts['clearance_ft']),
            default=str(opts['clearance_ft']))
        if clearance is None: return
        max_leader = forms.ask_for_string(
            'Max leader length (ft) [{} current]:'.format(opts['max_leader_ft']),
            default=str(opts['max_leader_ft']))
        if max_leader is None: return
        try:
            self._ai_place_options['clearance_ft']  = float(clearance)
            self._ai_place_options['max_leader_ft'] = float(max_leader)
        except ValueError:
            pass
        self._log('AI options: clearance={}ft, max_leader={}ft'.format(
            self._ai_place_options['clearance_ft'],
            self._ai_place_options['max_leader_ft']), '⚙')


# ─────────────────────────────────────────────────────────────────────────────
# ENTRY POINT
# __persistentengine__ keeps the IronPython engine alive after .Show() returns.
# Module-level _panel_instance prevents GC of the Python object.
# Re-clicking the ribbon button re-executes this block: if the panel is still
# open, bring it to front; otherwise create a fresh one.
# ─────────────────────────────────────────────────────────────────────────────
try:
    _uidoc = __revit__.ActiveUIDocument
    _doc   = _uidoc.Document if _uidoc else None
except Exception:
    _uidoc, _doc = None, None

if not _doc:
    try:
        # pyrevit.revit imported at module level as _pyrevit_revit
        _doc, _uidoc = _pyrevit_revit.doc, _pyrevit_revit.uidoc
    except Exception:
        pass

if _doc:
    _need_new = True
    try:
        if _panel_instance is not None and _panel_instance.IsVisible:
            _panel_instance.Activate()
            _need_new = False
    except NameError:
        pass   # first run — _panel_instance not yet defined
    except Exception:
        pass

    if _need_new:
        try:
            _panel_instance = STINGTagsPanel(_doc, _uidoc)
            _panel_instance.Show()          # modeless — Revit stays active
        except Exception as _ex:
            forms.alert('STINGTags failed to open:\n\n' + str(_ex))
else:
    forms.alert('STINGTags: no active Revit document found.')
