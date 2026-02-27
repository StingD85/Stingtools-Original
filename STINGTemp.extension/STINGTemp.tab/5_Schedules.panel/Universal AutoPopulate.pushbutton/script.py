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
__title__ = "Universal\nAutoPopulate"
__author__ = "Author"
__doc__ = "Auto-populate parameter values using category bindings and field remap data"

def main():
    doc = revit.doc
    output.print_md("# Universal AutoPopulate")
    remap = data_loader.build_remap_dict()
    cat_params = data_loader.build_category_params_dict()
    output.print_md("Remaps: **{}** | Categories: **{}**".format(
        len(remap), len(cat_params)))

    output.print_md("## Active field remaps")
    output.print_md("| Old field | New parameter |")
    output.print_md("|-----------|---------------|")
    for i, (old, new) in enumerate(sorted(remap.items())):
        if i >= 25:
            output.print_md("*... and {} more*".format(len(remap) - 25))
            break
        output.print_md("| {} | {} |".format(old, new))

    mode = forms.CommandSwitchWindow.show(
        ['Preview Only', 'Apply Remaps'], message='Mode:')
    if mode != 'Apply Remaps':
        return

    # Apply remaps: for each element, copy old param value to new param
    target_cats = forms.SelectFromList.show(
        sorted(cat_params.keys()), title='Categories', multiselect=True)
    if not target_cats:
        return

    total_updated = 0
    for cat_name in target_cats:
        bic = revit_compat.bic_from_category_name(cat_name)
        if bic is None:
            continue
        elems = FilteredElementCollector(doc).OfCategory(bic)\
            .WhereElementIsNotElementType().ToElements()
        with revit.Transaction("AutoPopulate {}".format(cat_name)):
            for elem in elems:
                for old_name, new_name in remap.items():
                    old_p = elem.LookupParameter(old_name)
                    new_p = elem.LookupParameter(new_name)
                    if old_p and new_p and old_p.HasValue and not new_p.HasValue:
                        try:
                            val = old_p.AsString() or old_p.AsValueString()
                            if val:
                                new_p.Set(val)
                                total_updated += 1
                        except Exception:
                            pass
    output.print_md("---")
    output.print_md("Updated **{}** parameter values".format(total_updated))

if __name__ == '__main__':
    main()
