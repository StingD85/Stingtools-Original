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
__title__ = "Create\nParameters"
__author__ = "Author"
__doc__ = "Bind shared parameters to the active project from MR_PARAMETERS data"

import codecs, tempfile

def main():
    app = __revit__.Application
    doc = revit.doc
    output.print_md("# Create Project Parameters")

    params = data_loader.load_mr_parameters()
    bindings = data_loader.load_category_bindings()
    sp_content = data_loader.read_shared_parameter_file()
    inv = data_loader.data_file_inventory()

    output.print_md("Data: **{}** parameters, **{}** categories".format(
        len(params), len(bindings)))
    output.print_md("MR_PARAMETERS hash: `{}`".format(
        inv.get('MR_PARAMETERS.csv', {}).get('hash', '?')))

    mode = forms.CommandSwitchWindow.show(
        ['Preview Only', 'Bind to Project'], message='Mode:')
    if mode is None:
        return

    if mode == 'Preview Only':
        output.print_md("## Parameter groups")
        groups = {}
        for p in params:
            g = p.get('Group_Name', 'Unknown')
            groups[g] = groups.get(g, 0) + 1
        output.print_md("| Group | Count |")
        output.print_md("|-------|-------|")
        for g in sorted(groups):
            output.print_md("| {} | {} |".format(g, groups[g]))
        output.print_md("---")
        output.print_md("## Categories")
        for cat in sorted(bindings):
            output.print_md("- {}: {} bindings".format(cat, len(bindings[cat])))
        return

    # Load definitions from temp SP file
    fd, tmp = tempfile.mkstemp(suffix='.txt', prefix='MR_PARAMS_')
    os.close(fd)
    try:
        with codecs.open(tmp, 'w', 'utf-16-le') as f:
            f.write(u'\ufeff')
            f.write(sp_content)
        app.SharedParametersFilename = tmp
        sp_file = app.OpenSharedParameterFile()
        if sp_file is None:
            forms.alert("Could not open shared parameter file")
            return
        definitions = {}
        for group in sp_file.Groups:
            for defn in group.Definitions:
                definitions[defn.Name] = defn
        output.print_md("Loaded **{}** definitions".format(len(definitions)))

        added = skipped = failed = 0
        with revit.Transaction("Bind Parameters"):
            for cat_name, cat_bindings in sorted(bindings.items()):
                for b in cat_bindings:
                    pn = b.get('Parameter_Name', '').strip()
                    bt = b.get('Binding_Type', 'Type').strip()
                    if pn not in definitions:
                        skipped += 1
                        continue
                    # Check if already bound
                    existing = doc.ParameterBindings
                    it = existing.ForwardIterator()
                    already = False
                    while it.MoveNext():
                        if it.Key.Name == pn:
                            already = True
                            break
                    if already:
                        skipped += 1
                        continue
                    try:
                        cats = app.Create.NewCategorySet()
                        cat_obj = doc.Settings.Categories.get_Item(cat_name)
                        if not cat_obj:
                            skipped += 1
                            continue
                        cats.Insert(cat_obj)
                        if bt.lower() == 'instance':
                            bind = app.Create.NewInstanceBinding(cats)
                        else:
                            bind = app.Create.NewTypeBinding(cats)
                        doc.ParameterBindings.Insert(definitions[pn], bind)
                        added += 1
                    except Exception as e:
                        failed += 1

        output.print_md("---")
        output.print_md("Added: **{}** | Skipped: **{}** | Failed: **{}**".format(
            added, skipped, failed))
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)

if __name__ == '__main__':
    main()
