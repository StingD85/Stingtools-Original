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
__title__ = "1. Create\nWalls"
__author__ = "Author"
__doc__ = "Create Walls types with parameters and materials from external data"

def main():
    doc = revit.doc
    output.print_md("# Create Walls")

    cat_params = data_loader.build_category_params_dict()
    params = cat_params.get("Walls", [])
    output.print_md("Parameters for Walls: **{}**".format(len(params)))

    ble_mats = data_loader.load_ble_materials()
    elem_mats = [m for m in ble_mats
                 if 'WALL' in m.get('SOURCE_SHEET', '').upper()
                 or 'WALL' in m.get('MAT_CATEGORY', '').upper()]
    output.print_md("Materials for Walls: **{}**".format(len(elem_mats)))

    mode = forms.CommandSwitchWindow.show(
        ['Preview Parameters', 'Preview Materials', 'Create Types'],
        message='Mode:')
    if mode is None:
        return

    if mode == 'Preview Parameters':
        output.print_md("| Name | Type | Group |")
        output.print_md("|------|------|-------|")
        for p in params[:60]:
            output.print_md("| {} | {} | {} |".format(
                p['name'], p['data_type'], p['group']))
        if len(params) > 60:
            output.print_md("*... and {} more*".format(len(params) - 60))

    elif mode == 'Preview Materials':
        output.print_md("| Code | Name | Thickness mm |")
        output.print_md("|------|------|-------------|")
        for m in elem_mats[:40]:
            output.print_md("| {} | {} | {} |".format(
                m.get('MAT_CODE', ''), m.get('MAT_NAME', '')[:50],
                m.get('MAT_THICKNESS_MM', '')))
        if len(elem_mats) > 40:
            output.print_md("*... and {} more*".format(len(elem_mats) - 40))

    else:
        output.print_md("*Type creation uses Revit compound-layer API.*")
        output.print_md("Ready to create **{}** {elem} types.".format(len(elem_mats)))

if __name__ == '__main__':
    main()
