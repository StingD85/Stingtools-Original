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
__title__ = "Export Schedules\nto CSV"
__author__ = "Author"
__doc__ = "Export Revit schedules to CSV files"
import csv as csv_mod

def main():
    doc = revit.doc
    output.print_md("# Export Schedules to CSV")
    collector = FilteredElementCollector(doc).OfClass(ViewSchedule)
    schedules = [s for s in collector if not s.IsTitleblockRevisionSchedule]
    output.print_md("Found **{}** schedules".format(len(schedules)))
    if not schedules:
        return
    names = sorted(s.Name for s in schedules)
    selected = forms.SelectFromList.show(names,
        title='Select schedules', multiselect=True)
    if not selected:
        return
    folder = forms.pick_folder(title='Export folder')
    if not folder:
        return
    exported = 0
    for sched in schedules:
        if sched.Name not in selected:
            continue
        try:
            table = sched.GetTableData()
            section = table.GetSectionData(SectionType.Body)
            rows = section.NumberOfRows
            cols = section.NumberOfColumns
            fname = sched.Name.replace('/', '_').replace('\\', '_')
            csv_path = os.path.join(folder, "{}.csv".format(fname))
            with open(csv_path, 'w') as f:
                w = csv_mod.writer(f)
                for r in range(rows):
                    row = []
                    for c in range(cols):
                        try:
                            row.append(sched.GetCellText(SectionType.Body, r, c))
                        except Exception:
                            row.append('')
                    w.writerow(row)
            exported += 1
            output.print_md("Exported: **{}**".format(sched.Name))
        except Exception as e:
            output.print_md("Failed: {} - {}".format(sched.Name, str(e)[:60]))
    output.print_md("---")
    output.print_md("Exported **{}** schedules".format(exported))

if __name__ == '__main__':
    main()
