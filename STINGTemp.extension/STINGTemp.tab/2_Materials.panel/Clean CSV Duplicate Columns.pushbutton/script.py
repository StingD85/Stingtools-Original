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
from pyrevit import forms, script
import csv as csv_mod
output = script.get_output()
output.close_others()

__title__ = "Clean CSV\nDuplicate Columns"
__author__ = "Author"
__doc__ = "Detect and report duplicate columns in CSV data files"

def main():
    output.print_md("# Clean CSV Duplicate Columns")
    csv_files = [f for f in os.listdir(data_loader.DATA_DIR) if f.endswith('.csv')]
    if not csv_files:
        forms.alert("No CSV files found")
        return
    selected = forms.SelectFromList.show(sorted(csv_files),
        title='Select CSV files to check', multiselect=True)
    if not selected:
        return
    for fname in selected:
        path = data_loader.data_path(fname)
        with open(path, 'r') as f:
            for line in f:
                if not line.strip().startswith('#'):
                    header = line.strip().split(',')
                    break
        seen = set()
        dupes = [c for c in header if c in seen or seen.add(c)]
        if dupes:
            output.print_md("**{}**: {} duplicate(s): {}".format(
                fname, len(dupes), ', '.join(dupes[:5])))
        else:
            output.print_md("**{}**: clean".format(fname))

if __name__ == '__main__':
    main()
