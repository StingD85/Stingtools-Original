# -*- coding: utf-8 -*-
"""
STINGTemp Revit API compatibility layer (lib/revit_compat.py)

Abstracts Revit version differences so every button script works
identically on Revit 2025, 2026 and 2027.

Key changes across versions:
  - Revit 2024+ replaced BuiltInParameterGroup with GroupTypeId / ForgeTypeId
  - Revit 2025  introduced ParameterUtils.FindAllParameterIdsByName
  - Revit 2026+ may deprecate additional BuiltIn... enums
  - Revit 2027  anticipated to continue ForgeTypeId transition

This module detects the running version once at import time and exposes
a stable interface.

Version: 6.0.0
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import BuiltInCategory
from pyrevit import revit

# ---------------------------------------------------------------------------
# Version detection
# ---------------------------------------------------------------------------

REVIT_VERSION = int(revit.doc.Application.VersionNumber)

# ---------------------------------------------------------------------------
# Parameter group abstraction
# ---------------------------------------------------------------------------

HAS_GROUP_TYPE_ID = False
GROUP_TYPE_ID_MAP = {}

try:
    from Autodesk.Revit.DB import GroupTypeId
    _candidates = {
        'DATA': 'Data',
        'GENERAL': 'General',
        'CONSTRAINTS': 'Constraints',
        'MATERIALS': 'Materials',
        'ELECTRICAL': 'Electrical',
        'MECHANICAL': 'Mechanical',
        'PLUMBING': 'Plumbing',
        'FIRE_PROTECTION': 'FireProtection',
        'IDENTITY_DATA': 'IdentityData',
        'TEXT': 'Text',
        'STRUCTURAL': 'Structural',
        'ENERGY_ANALYSIS': 'EnergyAnalysis',
        'GEOMETRY': 'Geometry',
        'PHASING': 'Phasing',
    }
    for key, attr in _candidates.items():
        if hasattr(GroupTypeId, attr):
            GROUP_TYPE_ID_MAP[key] = getattr(GroupTypeId, attr)
    if GROUP_TYPE_ID_MAP:
        HAS_GROUP_TYPE_ID = True
except ImportError:
    pass

HAS_BUILTIN_PARAM_GROUP = False
BUILTIN_PARAM_GROUP_MAP = {}

try:
    from Autodesk.Revit.DB import BuiltInParameterGroup
    BUILTIN_PARAM_GROUP_MAP = {
        'DATA': BuiltInParameterGroup.PG_DATA,
        'GENERAL': BuiltInParameterGroup.PG_GENERAL,
        'CONSTRAINTS': BuiltInParameterGroup.PG_GEOMETRY,
        'MATERIALS': BuiltInParameterGroup.PG_MATERIALS,
        'ELECTRICAL': BuiltInParameterGroup.PG_ELECTRICAL,
        'MECHANICAL': BuiltInParameterGroup.PG_MECHANICAL,
        'PLUMBING': BuiltInParameterGroup.PG_PLUMBING,
        'FIRE_PROTECTION': BuiltInParameterGroup.PG_FIRE_PROTECTION,
        'IDENTITY_DATA': BuiltInParameterGroup.PG_IDENTITY_DATA,
        'TEXT': BuiltInParameterGroup.PG_TEXT,
    }
    HAS_BUILTIN_PARAM_GROUP = True
except ImportError:
    pass

# Map group codes from CSV data to standard keys
GROUP_CODE_TO_KEY = {
    'ASS_MNG': 'DATA',
    'BLE_ELES': 'GENERAL',
    'BLE_GEN': 'GENERAL',
    'BLE_DIM': 'CONSTRAINTS',
    'BLE_MAT': 'MATERIALS',
    'CST_PROC': 'DATA',
    'CST_GEN': 'DATA',
    'COM_DAT': 'DATA',
    'ELC_PWR': 'ELECTRICAL',
    'FLS_LIFE_SFTY': 'FIRE_PROTECTION',
    'FLS_PROT': 'FIRE_PROTECTION',
    'HVC_SYSTEMS': 'MECHANICAL',
    'HVC_SYS': 'MECHANICAL',
    'LTG_CONTROLS': 'ELECTRICAL',
    'LTG_GEN': 'ELECTRICAL',
    'MAT_INFO': 'MATERIALS',
    'PER_SUST': 'DATA',
    'PER_GEN': 'DATA',
    'PLM_DRN': 'PLUMBING',
    'PRJ_INFORMATION': 'IDENTITY_DATA',
    'PRJ_INFO': 'IDENTITY_DATA',
    'PROP_PHYSICAL': 'DATA',
    'RGL_CMPL': 'DATA',
    'RGL_GEN': 'DATA',
    'TPL_TRACKING': 'DATA',
}


def get_parameter_group(group_code):
    """Return the Revit parameter-group object for the given CSV group code.

    Uses GroupTypeId on Revit 2024+, BuiltInParameterGroup on 2020-2023.
    Returns None if neither API is available (should not happen in practice).
    """
    key = GROUP_CODE_TO_KEY.get(group_code, 'DATA')
    if HAS_GROUP_TYPE_ID and GROUP_TYPE_ID_MAP:
        return GROUP_TYPE_ID_MAP.get(key, GROUP_TYPE_ID_MAP.get('DATA'))
    if HAS_BUILTIN_PARAM_GROUP and BUILTIN_PARAM_GROUP_MAP:
        return BUILTIN_PARAM_GROUP_MAP.get(key, BUILTIN_PARAM_GROUP_MAP.get('DATA'))
    return None


# ---------------------------------------------------------------------------
# BuiltInCategory <-> CSV category name mapping
# ---------------------------------------------------------------------------

CATEGORY_MAP = {
    # MEP - Electrical
    BuiltInCategory.OST_ElectricalEquipment: "Electrical Equipment",
    BuiltInCategory.OST_ElectricalFixtures: "Electrical Fixtures",
    BuiltInCategory.OST_LightingFixtures: "Lighting Fixtures",
    BuiltInCategory.OST_LightingDevices: "Lighting Devices",
    BuiltInCategory.OST_CableTray: "Cable Trays",
    BuiltInCategory.OST_CableTrayFitting: "Cable Tray Fittings",
    BuiltInCategory.OST_Conduit: "Conduits",
    BuiltInCategory.OST_ConduitFitting: "Conduit Fittings",
    # MEP - Mechanical / HVAC
    BuiltInCategory.OST_MechanicalEquipment: "Mechanical Equipment",
    BuiltInCategory.OST_DuctTerminal: "Air Terminals",
    BuiltInCategory.OST_DuctCurves: "Ducts",
    BuiltInCategory.OST_DuctFitting: "Duct Fittings",
    BuiltInCategory.OST_DuctAccessory: "Duct Accessories",
    BuiltInCategory.OST_FlexDuctCurves: "Flex Ducts",
    # MEP - Plumbing
    BuiltInCategory.OST_PlumbingFixtures: "Plumbing Fixtures",
    BuiltInCategory.OST_PlumbingEquipment: "Plumbing Equipment",
    BuiltInCategory.OST_PipeCurves: "Pipes",
    BuiltInCategory.OST_PipeFitting: "Pipe Fittings",
    BuiltInCategory.OST_PipeAccessory: "Pipe Accessories",
    BuiltInCategory.OST_FlexPipeCurves: "Flex Pipes",
    BuiltInCategory.OST_Sprinklers: "Sprinklers",
    # MEP - Fire / Security / Communication
    BuiltInCategory.OST_FireAlarmDevices: "Fire Alarm Devices",
    BuiltInCategory.OST_SecurityDevices: "Security Devices",
    BuiltInCategory.OST_CommunicationDevices: "Communication Devices",
    BuiltInCategory.OST_DataDevices: "Data Devices",
    BuiltInCategory.OST_TelephoneDevices: "Telephone Devices",
    BuiltInCategory.OST_NurseCallDevices: "Nurse Call Devices",
    # Architecture - Building Elements
    BuiltInCategory.OST_Walls: "Walls",
    BuiltInCategory.OST_Floors: "Floors",
    BuiltInCategory.OST_Ceilings: "Ceilings",
    BuiltInCategory.OST_Roofs: "Roofs",
    BuiltInCategory.OST_Doors: "Doors",
    BuiltInCategory.OST_Windows: "Windows",
    BuiltInCategory.OST_Stairs: "Stairs",
    BuiltInCategory.OST_Ramps: "Ramps",
    BuiltInCategory.OST_CurtainWallPanels: "Curtain Panels",
    BuiltInCategory.OST_CurtainWallMullions: "Curtain Wall Mullions",
    # Architecture - Rooms / Spaces
    BuiltInCategory.OST_Rooms: "Rooms",
    BuiltInCategory.OST_MEPSpaces: "Spaces",
    # Structural
    BuiltInCategory.OST_StructuralColumns: "Structural Columns",
    BuiltInCategory.OST_StructuralFraming: "Structural Framing",
    BuiltInCategory.OST_StructuralFoundation: "Structural Foundations",
    # Furniture & Equipment
    BuiltInCategory.OST_Furniture: "Furniture",
    BuiltInCategory.OST_FurnitureSystems: "Furniture Systems",
    BuiltInCategory.OST_Casework: "Casework",
    BuiltInCategory.OST_SpecialityEquipment: "Specialty Equipment",
    # Site
    BuiltInCategory.OST_Site: "Site",
    BuiltInCategory.OST_Planting: "Planting",
    BuiltInCategory.OST_Parking: "Parking",
    # Generic
    BuiltInCategory.OST_GenericModel: "Generic Models",
    # Documentation
    BuiltInCategory.OST_Sheets: "Sheets",
    BuiltInCategory.OST_ProjectInformation: "Project Information",
    # Materials
    BuiltInCategory.OST_Materials: "Materials",
    # Electrical circuits
    BuiltInCategory.OST_ElectricalCircuit: "Electrical Circuits",
}

NAME_TO_BUILTIN = {v: k for k, v in CATEGORY_MAP.items()}

# Fuzzy name variants
_NAME_ALIASES = {
    'Speciality Equipment': 'Specialty Equipment',
    'Air Terminal': 'Air Terminals',
    'Duct Terminal': 'Air Terminals',
    'Flex Duct': 'Flex Ducts',
    'Flex Pipe': 'Flex Pipes',
}


def category_name_from_bic(bic):
    """CSV category name for a BuiltInCategory enum value."""
    return CATEGORY_MAP.get(bic)


def bic_from_category_name(name):
    """BuiltInCategory enum for a CSV category name string."""
    bic = NAME_TO_BUILTIN.get(name)
    if bic is None:
        alias = _NAME_ALIASES.get(name)
        if alias:
            bic = NAME_TO_BUILTIN.get(alias)
    return bic


def resolve_family_category(family_doc, category_params):
    """Determine the CSV category name for a family document.

    Tries BuiltInCategory first, then falls back to string matching
    including common aliases.  Returns None if the category is not mapped.
    """
    try:
        fc = family_doc.OwnerFamily.FamilyCategory
        if fc is None:
            return None
        try:
            bic = fc.BuiltInCategory
            name = CATEGORY_MAP.get(bic)
            if name and name in category_params:
                return name
        except Exception:
            pass
        cn = fc.Name
        if cn in category_params:
            return cn
        alias = _NAME_ALIASES.get(cn)
        if alias and alias in category_params:
            return alias
    except Exception:
        pass
    return None
