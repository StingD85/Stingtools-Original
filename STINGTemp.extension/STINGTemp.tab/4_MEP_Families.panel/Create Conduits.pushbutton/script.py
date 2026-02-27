# -*- coding: utf-8 -*-
import os, sys
_p = os.path.dirname(os.path.abspath(__file__))
while _p and _p != os.path.dirname(_p):
    if os.path.basename(_p).endswith('.extension'):
        break
    _p = os.path.dirname(_p)
_lib = os.path.join(_p, 'lib')
if _lib not in sys.path:
    sys.path.insert(0, _lib)
import data_loader
import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')
from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms, script
import revit_compat
output = script.get_output()
output.close_others()
__title__ = "Create\nConduits"
__author__ = "Author"
__doc__ = "Create Conduits types with MEP parameters from external data"

def main():
    doc = revit.doc
    output.print_md("# Create Conduits")

    cat_params = data_loader.build_category_params_dict()
    params = cat_params.get("Conduits", [])
    output.print_md("Parameters for Conduits: **{}**".format(len(params)))

    mep_mats = data_loader.load_mep_materials()
    elem_mats = [m for m in mep_mats
                 if 'CONDUIT' in m.get('SOURCE_SHEET', '').upper()
                 or 'CONDUIT' in m.get('MAT_CATEGORY', '').upper()]
    output.print_md("Materials for Conduits: **{}**".format(len(elem_mats)))

    mode = forms.CommandSwitchWindow.show(
        ['Preview Parameters', 'Preview Materials'],
        message='Mode:')
    if mode is None:
        return

    if mode == 'Preview Parameters':
        output.print_md("| Name | Type | Group |")
        output.print_md("|------|------|-------|")
        for p in params[:60]:
            output.print_md("| {} | {} | {} |".format(
                p['name'], p['data_type'], p['group']))
    else:
        output.print_md("| Code | Name | Discipline |")
        output.print_md("|------|------|------------|")
        for m in elem_mats[:40]:
            output.print_md("| {} | {} | {} |".format(
                m.get('MAT_CODE', ''), m.get('MAT_NAME', '')[:50],
                m.get('MAT_DISCIPLINE', '')))

if __name__ == '__main__':
    main()
