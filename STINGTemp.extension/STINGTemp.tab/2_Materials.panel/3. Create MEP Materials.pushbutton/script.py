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
__title__ = "3. Create MEP\nMaterials"
__author__ = "Author"
__doc__ = "Create MEP materials from MEP_MATERIALS.csv"

def main():
    doc = revit.doc
    output.print_md("# Create MEP Materials")
    all_mats = data_loader.load_mep_materials()
    output.print_md("Loaded **{}** materials".format(len(all_mats)))

    mode = forms.CommandSwitchWindow.show(
        ['Preview Only', 'Create Materials'], message='Mode:')
    if mode is None:
        return

    if mode == 'Preview Only':
        cats = {}
        for m in all_mats:
            c = m.get('MAT_CATEGORY', 'Unknown')
            cats[c] = cats.get(c, 0) + 1
        output.print_md("| Category | Count |")
        output.print_md("|----------|-------|")
        for c in sorted(cats):
            output.print_md("| {} | {} |".format(c, cats[c]))
        return

    existing = set(m.Name for m in FilteredElementCollector(doc).OfClass(Material))
    created = skipped = failed = 0
    with revit.Transaction("Create MEP Materials"):
        for md in all_mats:
            name = md.get('MAT_NAME', '').strip()
            if not name or name in existing:
                skipped += 1
                continue
            try:
                mid = Material.Create(doc, name)
                el = doc.GetElement(mid)
                cs = md.get('BLE_APP-COLOR', '')
                if cs and cs.startswith('RGB'):
                    parts = cs.replace('RGB ', '').split('-')
                    if len(parts) == 3:
                        el.Color = Color(int(parts[0]), int(parts[1]), int(parts[2]))
                tr = md.get('BLE_APP-TRANSPARENCY', '')
                if tr:
                    try:
                        el.Transparency = int(float(tr))
                    except Exception:
                        pass
                desc = md.get('BLE_APP-DESCRIPTION', '')
                if desc:
                    try:
                        el.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION).Set(desc[:255])
                    except Exception:
                        pass
                created += 1
                existing.add(name)
            except Exception:
                failed += 1
    output.print_md("---")
    output.print_md("Created **{}** | Skipped **{}** | Failed **{}**".format(
        created, skipped, failed))

if __name__ == '__main__':
    main()
