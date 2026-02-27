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
__title__ = "Extract\nData"
__author__ = "Author"
__doc__ = "Extract element data and parameters from the Revit project"
import csv as csv_mod

def main():
    doc = revit.doc
    output.print_md("# Extract Project Data")
    categories = sorted(revit_compat.CATEGORY_MAP.values())
    selected = forms.SelectFromList.show(categories,
        title='Select categories', multiselect=True)
    if not selected:
        return
    folder = forms.pick_folder(title='Output folder')
    if not folder:
        return
    for cat_name in selected:
        bic = revit_compat.bic_from_category_name(cat_name)
        if bic is None:
            continue
        elems = FilteredElementCollector(doc).OfCategory(bic)\
            .WhereElementIsNotElementType().ToElements()
        output.print_md("**{}**: {} elements".format(cat_name, len(elems)))
        if not elems:
            continue
        param_names = sorted(set(
            p.Definition.Name for p in elems[0].Parameters))
        csv_path = os.path.join(folder, "{}_extract.csv".format(
            cat_name.replace(' ', '_')))
        with open(csv_path, 'w') as f:
            w = csv_mod.writer(f)
            w.writerow(['Element_Id'] + param_names)
            for elem in elems:
                row = [str(elem.Id.IntegerValue)]
                for pn in param_names:
                    p = elem.LookupParameter(pn)
                    if p and p.HasValue:
                        row.append(p.AsValueString() or p.AsString() or '')
                    else:
                        row.append('')
                w.writerow(row)
        output.print_md("Saved `{}`".format(csv_path))

if __name__ == '__main__':
    main()
