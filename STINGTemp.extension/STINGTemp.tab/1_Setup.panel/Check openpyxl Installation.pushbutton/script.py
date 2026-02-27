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
output = script.get_output()
output.close_others()

__title__ = "Check openpyxl\nInstallation"
__author__ = "Author"
__doc__ = "Verify openpyxl is available and list data files with hashes"

def main():
    output.print_md("# Environment Check")
    output.print_md("---")
    output.print_md("## openpyxl")
    try:
        import openpyxl
        output.print_md("openpyxl **{}** is installed.".format(openpyxl.__version__))
    except ImportError:
        output.print_md("**openpyxl is NOT installed.**")
        output.print_md("Install: `pip install openpyxl`")

    output.print_md("---")
    output.print_md("## Data files")
    inv = data_loader.data_file_inventory()
    output.print_md("| File | Size | Hash | Version |")
    output.print_md("|------|------|------|---------|")
    for fname, info in sorted(inv.items()):
        output.print_md("| {} | {:,.0f} B | `{}` | {} |".format(
            fname, info['size'], info['hash'],
            info.get('version') or ''))

if __name__ == '__main__':
    main()
