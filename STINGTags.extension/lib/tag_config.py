# -*- coding: utf-8 -*-
"""
tag_config.py
Project-level token lookup tables. Loaded from project_config.json (written by
ProjectConfig button in CPython mode). Falls back to built-in defaults when no
JSON exists.

IronPython 2.7 compatible — no openpyxl, no f-strings.
"""

import os
import sys

# ── Constants ────────────────────────────────────────────────────────────────
NUM_PAD   = 4          # zero-pad width: 0001
SEPARATOR = '-'
LIB_DIR   = os.path.dirname(os.path.abspath(__file__))
EXT_DIR   = os.path.dirname(LIB_DIR)
CONFIG_JSON = os.path.join(EXT_DIR, 'config', 'project_config.json')
WORKBOOK_PATH = os.path.join(EXT_DIR, 'config', 'REVIT_TAG_AUTOMATION_TEMPLATE_v2.xlsx')

# ── JSON loader (IronPython compatible) ──────────────────────────────────────
def _load_json(path):
    """Minimal JSON loader that works in IronPython 2.7."""
    try:
        import json
        with open(path, 'r') as f:
            return json.load(f)
    except Exception:
        return None


def _load_from_json():
    data = _load_json(CONFIG_JSON)
    if not data:
        return None, None, None, None, None, None
    return (
        data.get('DISC_MAP', {}),
        data.get('SYS_MAP', {}),
        data.get('PROD_MAP', {}),
        data.get('FUNC_MAP', {}),
        data.get('LOC_CODES', []),
        data.get('ZONE_CODES', []),
    )


# ── Built-in defaults (mirror Sheet 02-TAG-FAMILY-CONFIG) ───────────────────
# These are used when no project_config.json has been generated yet.
# ProjectConfig button writes the JSON and overrides these.

_DEFAULT_DISC_MAP = {
    # MEP
    'Air Terminals':             'M',
    'Duct Accessories':          'M',
    'Duct Fittings':             'M',
    'Ducts':                     'M',
    'Flex Ducts':                'M',
    'Mechanical Equipment':      'M',
    'Pipes':                     'M',
    'Pipe Fittings':             'M',
    'Pipe Accessories':          'M',
    'Flex Pipes':                'M',
    'Plumbing Fixtures':         'P',
    'Sprinklers':                'FP',
    'Electrical Equipment':      'E',
    'Electrical Fixtures':       'E',
    'Lighting Fixtures':         'E',
    'Lighting Devices':          'E',
    'Conduits':                  'E',
    'Conduit Fittings':          'E',
    'Cable Trays':               'E',
    'Cable Tray Fittings':       'E',
    'Fire Alarm Devices':        'FP',
    'Communication Devices':     'LV',
    'Data Devices':              'LV',
    'Nurse Call Devices':        'LV',
    'Security Devices':          'LV',
    'Telephone Devices':         'LV',
    # Architecture
    'Doors':                     'A',
    'Windows':                   'A',
    'Walls':                     'A',
    'Floors':                    'A',
    'Ceilings':                  'A',
    'Roofs':                     'A',
    'Rooms':                     'A',
    'Furniture':                 'A',
    'Furniture Systems':         'A',
    'Casework':                  'A',
    'Railings':                  'A',
    'Stairs':                    'A',
    'Ramps':                     'A',
    # Structure
    'Structural Columns':        'S',
    'Structural Framing':        'S',
    'Structural Foundations':    'S',
    'Columns':                   'S',
    # Generic
    'Generic Models':            'G',
    'Specialty Equipment':       'G',
    'Medical Equipment':         'G',
}

_DEFAULT_SYS_MAP = {
    'HVAC': [
        'Air Terminals', 'Duct Accessories', 'Duct Fittings',
        'Ducts', 'Flex Ducts', 'Mechanical Equipment',
    ],
    'HWS':  ['Pipes', 'Pipe Fittings', 'Pipe Accessories'],
    'DHW':  ['Plumbing Fixtures', 'Flex Pipes'],
    'FP':   ['Sprinklers'],
    'LV':   [
        'Electrical Equipment', 'Electrical Fixtures',
        'Lighting Fixtures', 'Lighting Devices',
        'Conduits', 'Conduit Fittings', 'Cable Trays', 'Cable Tray Fittings',
    ],
    'FLS':  ['Fire Alarm Devices'],
    'COM':  ['Communication Devices', 'Telephone Devices'],
    'ICT':  ['Data Devices'],
    'NCL':  ['Nurse Call Devices'],
    'SEC':  ['Security Devices'],
}

_DEFAULT_PROD_MAP = {
    'Air Terminals':          'GRL',
    'Duct Accessories':       'DAC',
    'Duct Fittings':          'DFT',
    'Ducts':                  'DU',
    'Flex Ducts':             'FDU',
    'Mechanical Equipment':   'AHU',
    'Pipes':                  'PP',
    'Pipe Fittings':          'PFT',
    'Pipe Accessories':       'PAC',
    'Flex Pipes':             'FPP',
    'Plumbing Fixtures':      'FIX',
    'Sprinklers':             'SPR',
    'Electrical Equipment':   'DB',
    'Electrical Fixtures':    'SKT',
    'Lighting Fixtures':      'LUM',
    'Lighting Devices':       'LDV',
    'Conduits':               'CDT',
    'Conduit Fittings':       'CFT',
    'Cable Trays':            'CBLT',
    'Cable Tray Fittings':    'CTF',
    'Fire Alarm Devices':     'FAD',
    'Communication Devices':  'COM',
    'Data Devices':           'DAT',
    'Nurse Call Devices':     'NCL',
    'Security Devices':       'SEC',
    'Telephone Devices':      'TEL',
    'Doors':                  'DR',
    'Windows':                'WIN',
    'Walls':                  'WL',
    'Floors':                 'FL',
    'Ceilings':               'CLG',
    'Roofs':                  'RF',
    'Rooms':                  'RM',
    'Furniture':              'FUR',
    'Furniture Systems':      'FUS',
    'Casework':               'CWK',
    'Railings':               'RLG',
    'Stairs':                 'STR',
    'Ramps':                  'RMP',
    'Structural Columns':     'COL',
    'Structural Framing':     'BM',
    'Structural Foundations': 'FDN',
    'Columns':                'COL',
    'Generic Models':         'GEN',
    'Specialty Equipment':    'SPE',
    'Medical Equipment':      'MED',
}

_DEFAULT_FUNC_MAP = {
    'HVAC':  'SUP',
    'HWS':   'HTG',
    'DHW':   'SAN',
    'FP':    'FP',
    'LV':    'PWR',
    'FLS':   'FLS',
    'COM':   'COM',
    'ICT':   'ICT',
    'NCL':   'NCL',
    'SEC':   'SEC',
}

_DEFAULT_LOC_CODES = ['BLD1', 'BLD2', 'BLD3', 'EXT', 'XX']
_DEFAULT_ZONE_CODES = ['Z01', 'Z02', 'Z03', 'Z04', 'ZZ', 'XX']


# ── Public API — loaded once on import ───────────────────────────────────────
_disc, _sys, _prod, _func, _loc, _zone = _load_from_json()

DISC_MAP   = _disc  if _disc  else _DEFAULT_DISC_MAP
SYS_MAP    = _sys   if _sys   else _DEFAULT_SYS_MAP
PROD_MAP   = _prod  if _prod  else _DEFAULT_PROD_MAP
FUNC_MAP   = _func  if _func  else _DEFAULT_FUNC_MAP
LOC_CODES  = _loc   if _loc   else _DEFAULT_LOC_CODES
ZONE_CODES = _zone  if _zone  else _DEFAULT_ZONE_CODES

CONFIG_SOURCE = 'project_config.json' if _disc else 'built-in defaults'


def reload_config():
    """Re-read project_config.json. Call after ProjectConfig saves a new file."""
    global DISC_MAP, SYS_MAP, PROD_MAP, FUNC_MAP, LOC_CODES, ZONE_CODES, CONFIG_SOURCE
    _d, _s, _p, _f, _l, _z = _load_from_json()
    if _d:
        DISC_MAP, SYS_MAP, PROD_MAP = _d, _s, _p
        FUNC_MAP, LOC_CODES, ZONE_CODES = _f, _l, _z
        CONFIG_SOURCE = 'project_config.json'
    return CONFIG_SOURCE
