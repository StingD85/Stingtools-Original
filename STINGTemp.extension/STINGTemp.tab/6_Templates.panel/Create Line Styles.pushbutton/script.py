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
__title__ = "Create\nLine Styles"
__author__ = "Author"
__doc__ = "Create line styles from template definitions"

def main():
    doc = revit.doc
    output.print_md("# Create Line Styles")
    output.print_md("*STINGTemp v6.0 | Revit {}*".format(revit_compat.REVIT_VERSION))
    output.print_md("---")

    schedules = data_loader.load_mr_schedules()
    params = data_loader.load_mr_parameters()
    remap = data_loader.load_schedule_field_remap()

    output.print_md("Data: **{}** schedules, **{}** parameters, **{}** remaps".format(
        len(schedules), len(params), len(remap)))

    mode = forms.CommandSwitchWindow.show(
        ['Preview Configuration', 'Apply to Project'], message='Mode:')
    if mode is None:
        return

    if mode == 'Preview Configuration':
        output.print_md("## Configuration")
        output.print_md("- Tool: **Create Line Styles**")
        output.print_md("- Data version: {}".format(
            data_loader.read_csv_version('MR_PARAMETERS.csv') or 'unknown'))
        inv = data_loader.data_file_inventory()
        output.print_md("- MR_PARAMETERS hash: `{}`".format(
            inv.get('MR_PARAMETERS.csv', {}).get('hash', '?')))
    else:
        output.print_md("*Applying configuration to project...*")

if __name__ == '__main__':
    main()
