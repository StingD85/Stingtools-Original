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
__title__ = "Batch Create\nSchedules"
__author__ = "Author"
__doc__ = "Batch create schedules from MR_SCHEDULES.csv"

def main():
    doc = revit.doc
    output.print_md("# Batch Create Schedules")
    schedules = data_loader.load_mr_schedules()
    output.print_md("Loaded **{}** definitions".format(len(schedules)))
    by_disc = {}
    for s in schedules:
        d = s.get('Discipline', 'Unknown')
        by_disc.setdefault(d, []).append(s)
    selected = forms.SelectFromList.show(sorted(by_disc.keys()),
        title='Select disciplines', multiselect=True)
    if not selected:
        return
    to_create = []
    for d in selected:
        to_create.extend(by_disc[d])
    output.print_md("Will create **{}** schedules".format(len(to_create)))

if __name__ == '__main__':
    main()
