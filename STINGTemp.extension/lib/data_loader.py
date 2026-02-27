# -*- coding: utf-8 -*-
"""
STINGTemp data loader (lib/data_loader.py)

Central module that reads every CSV, JSON and TXT file in the extension's
data/ directory.  All button scripts import from here; none embed data.

Design goals
  - Single point of truth: edit a CSV, reload pyRevit, every button sees the
    change immediately.
  - Version/hash tracking: each file's header version and SHA-256 hash are
    available for audit and change detection.
  - Revit-version-agnostic: pure CPython / IronPython; nothing here touches
    the Revit API.

Version: 6.0.0
"""

import os
import sys
import csv
import json
import codecs
import hashlib
from collections import defaultdict

# ---------------------------------------------------------------------------
# Path resolution
# ---------------------------------------------------------------------------

def _find_extension_root():
    """Walk up from this file to the *.extension directory."""
    p = os.path.dirname(os.path.abspath(__file__))
    while p and p != os.path.dirname(p):
        if os.path.basename(p).endswith('.extension'):
            return p
        p = os.path.dirname(p)
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


EXTENSION_ROOT = _find_extension_root()
DATA_DIR = os.path.join(EXTENSION_ROOT, 'data')


def data_path(filename):
    """Absolute path to a file inside data/."""
    return os.path.join(DATA_DIR, filename)


# ---------------------------------------------------------------------------
# Low-level readers
# ---------------------------------------------------------------------------

def file_hash(path):
    """SHA-256 of *path* (first 12 hex chars)."""
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(65536), b''):
            h.update(chunk)
    return h.hexdigest()[:12]


def read_csv_version(filename):
    """Return the ``# v...`` comment on line 1 of a data CSV, or None."""
    p = data_path(filename) if not os.path.isabs(filename) else filename
    if not os.path.exists(p):
        return None
    with open(p, 'r') as fh:
        first = fh.readline().strip()
        if first.startswith('#'):
            return first.lstrip('# ').strip()
    return None


def read_csv(filename, skip_comments=True):
    """Read a CSV from data/ and return a list of ``dict`` rows.

    Lines beginning with ``#`` are dropped when *skip_comments* is True.
    """
    p = data_path(filename)
    rows = []
    with open(p, 'r') as fh:
        lines = fh.readlines()
    clean = [ln for ln in lines if not (skip_comments and ln.strip().startswith('#'))]
    if not clean:
        return []
    for row in csv.DictReader(clean):
        rows.append(row)
    return rows


def read_json(filename):
    """Read a JSON file from data/."""
    with open(data_path(filename), 'r') as fh:
        return json.load(fh)


def read_shared_parameter_file(filename='MR_PARAMETERS.txt'):
    """Return the text content of the Revit shared-parameter file."""
    p = data_path(filename)
    with codecs.open(p, 'r', 'utf-8') as fh:
        return fh.read()


# ---------------------------------------------------------------------------
# File inventory & change tracking
# ---------------------------------------------------------------------------

def data_file_inventory():
    """Return ``{filename: {version, hash, size, mtime}}`` for every file
    in the data/ directory.  Useful for the dockable panel and audit logs.
    """
    inv = {}
    if not os.path.isdir(DATA_DIR):
        return inv
    for fname in sorted(os.listdir(DATA_DIR)):
        fp = os.path.join(DATA_DIR, fname)
        if not os.path.isfile(fp):
            continue
        entry = {
            'size': os.path.getsize(fp),
            'mtime': os.path.getmtime(fp),
            'hash': file_hash(fp),
            'version': None,
        }
        if fname.endswith('.csv'):
            entry['version'] = read_csv_version(fname)
        inv[fname] = entry
    return inv


# ---------------------------------------------------------------------------
# Domain loaders (thin wrappers)
# ---------------------------------------------------------------------------

def load_mr_parameters():
    return read_csv('MR_PARAMETERS.csv')

def load_category_bindings():
    """CATEGORY_BINDINGS.csv -> ``{Revit_Category: [row_dict, ...]}``."""
    rows = read_csv('CATEGORY_BINDINGS.csv')
    d = defaultdict(list)
    for r in rows:
        cat = r.get('Revit_Category', '').strip()
        if cat:
            d[cat].append(r)
    return dict(d)

def load_parameter_categories():
    return read_csv('PARAMETER__CATEGORIES.csv')

def load_binding_coverage_matrix():
    return read_csv('BINDING_COVERAGE_MATRIX.csv')

def load_family_parameter_bindings():
    return read_csv('FAMILY_PARAMETER_BINDINGS.csv')

def load_formulas():
    rows = read_csv('FORMULAS_WITH_DEPENDENCIES.csv')
    for r in rows:
        try:
            r['Dependency_Level'] = int(r.get('Dependency_Level', 0))
        except (ValueError, TypeError):
            r['Dependency_Level'] = 0
    return sorted(rows, key=lambda r: r['Dependency_Level'])

def load_schedule_field_remap():
    return read_csv('SCHEDULE_FIELD_REMAP.csv')

def load_mr_schedules():
    return read_csv('MR_SCHEDULES.csv')

def load_ble_materials():
    return read_csv('BLE_MATERIALS.csv')

def load_mep_materials():
    return read_csv('MEP_MATERIALS.csv')

def load_material_schema():
    return read_json('MATERIAL_SCHEMA.json')


# ---------------------------------------------------------------------------
# Derived structures (replicate what the old embedded script built)
# ---------------------------------------------------------------------------

def build_category_params_dict():
    """Build ``{category: [param_dict, ...]}`` from CATEGORY_BINDINGS +
    MR_PARAMETERS.  This is the authoritative replacement for the old
    ``EMBEDDED_CATEGORY_PARAMS`` dict.

    Each param_dict has keys: name, data_type, binding_type, group, guid,
    description, has_formula, user_modifiable, hide_when_no_value.
    """
    # Index parameters by name for O(1) lookup
    param_rows = load_mr_parameters()
    param_index = {}
    for r in param_rows:
        pn = r.get('Parameter_Name', '').strip()
        if pn and pn not in param_index:
            param_index[pn] = r

    # Walk bindings
    binding_rows = read_csv('CATEGORY_BINDINGS.csv')
    cat_params = defaultdict(list)
    seen = defaultdict(set)  # avoid duplicates per category

    for b in binding_rows:
        cat = b.get('Revit_Category', '').strip()
        pname = b.get('Parameter_Name', '').strip()
        if not cat or not pname:
            continue
        if pname in seen[cat]:
            continue
        seen[cat].add(pname)

        pinfo = param_index.get(pname, {})
        cat_params[cat].append({
            'name': pname,
            'data_type': pinfo.get('Data_Type', 'TEXT').strip(),
            'binding_type': b.get('Binding_Type', pinfo.get('Binding_Type', 'Type')).strip(),
            'group': pinfo.get('Group_Name', 'ASS_MNG').strip(),
            'guid': pinfo.get('Parameter_GUID', '').strip(),
            'description': pinfo.get('Description', '').strip(),
            'has_formula': pinfo.get('Has_Formula', 'False').strip(),
            'user_modifiable': pinfo.get('User_Modifiable', '1').strip(),
            'hide_when_no_value': pinfo.get('Hide_When_No_Value', '0').strip(),
        })

    return dict(cat_params)


def build_formulas_list():
    """Build formula dicts from FORMULAS_WITH_DEPENDENCIES.csv.

    Each entry: discipline, parameter, data_type, formula, description,
    inputs, dependency_level, uses_builtin_geometry, builtin_inputs.
    """
    rows = load_formulas()
    out = []
    for r in rows:
        out.append({
            'discipline': r.get('Discipline', '').strip(),
            'parameter': r.get('Parameter_Name', '').strip(),
            'data_type': r.get('Data_Type', '').strip(),
            'formula': r.get('Revit_Formula', '').strip(),
            'description': r.get('Description', '').strip(),
            'inputs': r.get('Input_Parameters', '').strip(),
            'dependency_level': r.get('Dependency_Level', 0),
            'uses_builtin_geometry': r.get('Uses_Builtin_Geometry', 'False').strip().lower() == 'true',
            'builtin_inputs': r.get('Builtin_Inputs', '').strip(),
        })
    return out


# Discipline -> Revit category mapping (unchanged from original)
DISCIPLINE_TO_CATEGORIES = {
    "ELECTRICAL": [
        "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures",
        "Lighting Devices", "Cable Trays", "Cable Tray Fittings",
        "Conduits", "Conduit Fittings", "Fire Alarm Devices",
        "Security Devices", "Communication Devices", "Data Devices",
        "Telephone Devices", "Nurse Call Devices"
    ],
    "HVAC": [
        "Mechanical Equipment", "Air Terminals", "Ducts", "Duct Fittings",
        "Duct Accessories", "Flex Ducts"
    ],
    "PLUMBING": [
        "Plumbing Fixtures", "Plumbing Equipment", "Pipes", "Pipe Fittings",
        "Pipe Accessories", "Flex Pipes", "Sprinklers"
    ],
    "CONSTRUCTION": [
        "Walls", "Floors", "Ceilings", "Roofs", "Structural Columns",
        "Structural Framing", "Structural Foundations", "Doors", "Windows",
        "Stairs", "Ramps", "Curtain Panels", "Curtain Wall Mullions",
        "Generic Models", "Casework"
    ],
    "ARCHITECTURAL": [
        "Walls", "Floors", "Ceilings", "Roofs", "Doors", "Windows",
        "Stairs", "Ramps", "Rooms", "Curtain Panels", "Curtain Wall Mullions"
    ],
    "COSTING": [
        "Air Terminals", "Cable Tray Fittings", "Cable Trays", "Casework",
        "Ceilings", "Communication Devices", "Conduit Fittings", "Conduits",
        "Curtain Panels", "Curtain Wall Mullions", "Data Devices", "Doors",
        "Duct Accessories", "Duct Fittings", "Ducts", "Electrical Equipment",
        "Electrical Fixtures", "Fire Alarm Devices", "Flex Ducts", "Flex Pipes",
        "Floors", "Furniture", "Furniture Systems", "Generic Models",
        "Lighting Devices", "Lighting Fixtures", "Mechanical Equipment",
        "Nurse Call Devices", "Parking", "Pipe Accessories", "Pipe Fittings",
        "Pipes", "Planting", "Plumbing Equipment", "Plumbing Fixtures",
        "Ramps", "Roofs", "Security Devices", "Site", "Specialty Equipment",
        "Sprinklers", "Stairs", "Structural Columns", "Structural Foundations",
        "Structural Framing", "Telephone Devices", "Walls", "Windows"
    ],
}


def build_discipline_category_formulas(formulas=None):
    """Build ``{category: [formula_dict, ...]}`` from discipline mapping.

    Sorted by dependency_level within each category.
    """
    if formulas is None:
        formulas = build_formulas_list()

    cat_formulas = defaultdict(list)
    for disc, cats in DISCIPLINE_TO_CATEGORIES.items():
        for f in formulas:
            if f['discipline'] == disc:
                for c in cats:
                    cat_formulas[c].append(f)

    for c in cat_formulas:
        cat_formulas[c] = sorted(cat_formulas[c],
                                  key=lambda x: x['dependency_level'])
    return dict(cat_formulas)


def build_remap_dict():
    """Build ``{old_field: new_field}`` from SCHEDULE_FIELD_REMAP.csv,
    including only REMAPPED entries (not REMOVED).
    """
    rows = load_schedule_field_remap()
    d = {}
    for r in rows:
        old = r.get('Old_Schedule_Field', '').strip()
        new = r.get('Consolidated_Parameter', '').strip()
        action = r.get('Action', '').strip()
        if old and new and action == 'REMAPPED':
            d[old] = new
    return d
