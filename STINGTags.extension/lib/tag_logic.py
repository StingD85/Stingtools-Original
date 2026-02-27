# -*- coding: utf-8 -*-
"""
tag_logic.py
Shared helper functions for all ISO_Tagging scripts.
IronPython 2.7 compatible.
"""

from Autodesk.Revit.DB import StorageType, ElementId


# ── Parameter helpers ────────────────────────────────────────────────────────

def get_param(el, name):
    """Return the Parameter object or None."""
    return el.LookupParameter(name)


def get_str(el, name):
    """Return string value of a parameter or empty string."""
    p = el.LookupParameter(name)
    if p and p.StorageType == StorageType.String:
        v = p.AsString()
        return v if v else ''
    return ''


def set_str(el, name, value, overwrite=False):
    """Set a TEXT parameter. Skips read-only params. Skips non-empty unless overwrite=True."""
    p = el.LookupParameter(name)
    if not p or p.IsReadOnly or p.StorageType != StorageType.String:
        return False
    existing = p.AsString() or ''
    if existing and not overwrite:
        return False
    p.Set(value)
    return True


def set_if_empty(el, name, value):
    """Set only when the parameter is currently empty."""
    return set_str(el, name, value, overwrite=False)


# ── Level helpers ─────────────────────────────────────────────────────────────

def get_level_code(doc, el):
    """Return a short level code from the element's host level."""
    try:
        lvl_id = el.LevelId
        if lvl_id and lvl_id != ElementId.InvalidElementId:
            lvl = doc.GetElement(lvl_id)
            if lvl:
                name = lvl.Name  # e.g. "Level 01", "Ground Floor", "Basement 1"
                # Normalise common patterns
                name = name.strip()
                if name.lower().startswith('level '):
                    code = 'L' + name[6:].strip().zfill(2)
                elif name.lower() in ('ground', 'ground floor', 'ground level'):
                    code = 'GF'
                elif name.lower().startswith('basement') or name.lower().startswith('b'):
                    digits = ''.join(c for c in name if c.isdigit())
                    code = 'B' + (digits or '1')
                elif name.lower().startswith('roof'):
                    code = 'RF'
                else:
                    # Use first 4 chars, uppercase, no spaces
                    code = name.upper().replace(' ', '')[:4]
                return code
    except Exception:
        pass
    return 'XX'


# ── Category helpers ──────────────────────────────────────────────────────────

def get_category_name(el):
    """Return element category name or empty string."""
    try:
        cat = el.Category
        return cat.Name if cat else ''
    except Exception:
        return ''


# ── SYS / FUNC derivation ─────────────────────────────────────────────────────

def get_sys_code(cat_name, sys_map):
    """Return the SYS code for this category name, or empty string."""
    for code, cats in sys_map.items():
        if cat_name in cats:
            return code
    return ''


def get_func_code(sys_code, func_map):
    """Return the FUNC code for this SYS code, or empty string."""
    return func_map.get(sys_code, '')


# ── Validation helpers ────────────────────────────────────────────────────────

def tag_is_complete(tag_val, expected_tokens=8):
    """Return True when the tag string has the correct number of '-'-separated tokens."""
    if not tag_val:
        return False
    return len(tag_val.split('-')) == expected_tokens


def validate_disc_code(code, disc_map):
    valid = set(disc_map.values())
    return code in valid


def validate_sys_code(code, sys_map):
    return code in sys_map


# ── Element iteration helper ──────────────────────────────────────────────────

def iter_taggable(doc, tag_config):
    """
    Yield every non-element-type element whose category name appears in
    DISC_MAP — i.e. every element that should receive ISO tokens.
    """
    from Autodesk.Revit.DB import FilteredElementCollector
    collector = FilteredElementCollector(doc).WhereElementIsNotElementType()
    known_cats = set(tag_config.DISC_MAP.keys())
    for el in collector:
        cat = get_category_name(el)
        if cat and cat in known_cats:
            yield el, cat
