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
from pyrevit import forms, script, HOST_APP
import json
output = script.get_output()
output.close_others()

__title__ = "Check pyRevit\nVersion"
__author__ = "Author"
__doc__ = "Display pyRevit version and extension data summary"

def main():
    output.print_md("# STINGTemp Extension Info")
    output.print_md("---")
    output.print_md("## Revit")
    output.print_md("- Version: **{}**".format(HOST_APP.version_name))
    output.print_md("- Build: **{}**".format(HOST_APP.build))

    output.print_md("## pyRevit")
    try:
        from pyrevit.versionmgr import get_pyrevit_version
        output.print_md("- pyRevit: **{}**".format(get_pyrevit_version()))
    except Exception:
        output.print_md("- pyRevit version: (could not detect)")

    output.print_md("## Extension")
    ext_path = os.path.join(data_loader.EXTENSION_ROOT, 'extension.json')
    if os.path.exists(ext_path):
        with open(ext_path) as f:
            ext = json.load(f)
        for k, v in sorted(ext.items()):
            output.print_md("- {}: **{}**".format(k, v))

    output.print_md("## Data file versions")
    inv = data_loader.data_file_inventory()
    for fname, info in sorted(inv.items()):
        ver = info.get('version', '')
        output.print_md("- {} [{}] {}".format(fname, info['hash'], ver or ''))

if __name__ == '__main__':
    main()
