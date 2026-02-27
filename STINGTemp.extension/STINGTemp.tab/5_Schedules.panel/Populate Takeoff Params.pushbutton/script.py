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
__title__ = "Populate\nTakeoff Params"
__author__ = "Author"
__doc__ = "Populate calculated takeoff parameters using formulas"

def main():
    doc = revit.doc
    output.print_md("# Populate Takeoff Parameters")
    formulas = data_loader.build_formulas_list()
    cat_formulas = data_loader.build_discipline_category_formulas(formulas)
    output.print_md("**{}** formulas across **{}** categories".format(
        len(formulas), len(cat_formulas)))
    by_disc = {}
    for f in formulas:
        d = f['discipline']
        by_disc.setdefault(d, []).append(f)
    output.print_md("| Discipline | Formulas |")
    output.print_md("|-----------|----------|")
    for d in sorted(by_disc):
        output.print_md("| {} | {} |".format(d, len(by_disc[d])))
    output.print_md("---")
    for d in ['CONSTRUCTION', 'COSTING']:
        fs = by_disc.get(d, [])
        output.print_md("## {} formulas".format(d))
        for f in fs[:10]:
            output.print_md("- **{}** = `{}`".format(f['parameter'], f['formula']))
        if len(fs) > 10:
            output.print_md("*... and {} more*".format(len(fs) - 10))

if __name__ == '__main__':
    main()
