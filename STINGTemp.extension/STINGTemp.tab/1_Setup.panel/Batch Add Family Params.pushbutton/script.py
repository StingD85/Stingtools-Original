# -*- coding: utf-8 -*-
"""
Batch Add Shared Parameters to Families (STINGTemp External-Data Edition)

Adapted from proven v3.4 self-contained script. All data now loaded from
the extension's data/ directory at run time.

973 parameters | 47 categories | 10,659 bindings | 197 formulas
Revit 2025-2027 compatible (GroupTypeId + BuiltInParameterGroup)

Author: Author
Version: 6.0.0
"""

__title__ = "Batch Add\nFamily Params"
__author__ = "Author"
__doc__ = "Add shared parameters and formulas to families from external data files"

import clr
import os
import sys
import codecs
import tempfile
import shutil
from datetime import datetime
from collections import defaultdict

clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import BuiltInCategory, SaveAsOptions
from Autodesk.Revit.UI import *
from pyrevit import revit, DB, forms, script

# -- resolve lib path
_p = os.path.dirname(os.path.abspath(__file__))
while _p and _p != os.path.dirname(_p):
    if os.path.basename(_p).endswith('.extension'):
        break
    _p = os.path.dirname(_p)
_lib = os.path.join(_p, 'lib')
if _lib not in sys.path:
    sys.path.insert(0, _lib)

import data_loader
import revit_compat

output = script.get_output()
output.close_others()

# -- Version detection (delegate to revit_compat)
REVIT_VERSION = revit_compat.REVIT_VERSION
HAS_GROUP_TYPE_ID = revit_compat.HAS_GROUP_TYPE_ID
GROUP_TYPE_ID_MAP = revit_compat.GROUP_TYPE_ID_MAP
HAS_BUILTIN_PARAM_GROUP = revit_compat.HAS_BUILTIN_PARAM_GROUP
BUILTIN_PARAM_GROUP_MAP = revit_compat.BUILTIN_PARAM_GROUP_MAP
GROUP_CODE_TO_KEY = revit_compat.GROUP_CODE_TO_KEY

def get_parameter_group(group_name):
    return revit_compat.get_parameter_group(group_name)

# -- Load external data
EMBEDDED_SP_CONTENT = data_loader.read_shared_parameter_file()
EMBEDDED_CATEGORY_PARAMS = data_loader.build_category_params_dict()
EMBEDDED_FORMULAS = data_loader.build_formulas_list()
CATEGORY_FORMULAS = data_loader.build_discipline_category_formulas(EMBEDDED_FORMULAS)

# -- Category map (from revit_compat)
CATEGORY_MAP = revit_compat.CATEGORY_MAP
NAME_TO_CATEGORY = revit_compat.NAME_TO_BUILTIN

class FamilyParameterProcessor:
    """Handles batch parameter addition to families using embedded data."""
    
    def __init__(self, app):
        self.app = app
        self.definitions = {}
        self.category_params = EMBEDDED_CATEGORY_PARAMS
        self.results = []
        self.temp_sp_file = None
    
    def setup_shared_parameters(self):
        """Create temp shared parameter file from embedded content."""
        try:
            import tempfile
            fd, self.temp_sp_file = tempfile.mkstemp(suffix='.txt', prefix='MR_PARAMS_')
            os.close(fd)
            
            # Write embedded content using codecs (IronPython compatible)
            with codecs.open(self.temp_sp_file, 'w', 'utf-16-le') as f:
                f.write(u'\ufeff')  # BOM
                f.write(EMBEDDED_SP_CONTENT)
            
            # Load into Revit
            self.app.SharedParametersFilename = self.temp_sp_file
            sp_file = self.app.OpenSharedParameterFile()
            
            if sp_file is None:
                return False, "Could not open shared parameter file"
            
            for group in sp_file.Groups:
                for defn in group.Definitions:
                    self.definitions[defn.Name] = defn
            
            return True, "{} definitions loaded".format(len(self.definitions))
        
        except Exception as e:
            import traceback
            return False, str(e) + "\n" + traceback.format_exc()
    
    def cleanup(self):
        """Remove temp shared parameter file."""
        if self.temp_sp_file and os.path.exists(self.temp_sp_file):
            try:
                os.remove(self.temp_sp_file)
            except:
                pass
    
    def get_family_category(self, family_doc):
        """Get CSV category name for a family document."""
        try:
            family_cat = family_doc.OwnerFamily.FamilyCategory
            if family_cat is None:
                return None
            
            try:
                bic = family_cat.BuiltInCategory
                if bic in CATEGORY_MAP:
                    return CATEGORY_MAP[bic]
            except:
                pass
            
            cat_name = family_cat.Name
            if cat_name in self.category_params:
                return cat_name
            
            variations = {
                'Specialty Equipment': 'Specialty Equipment',
                'Speciality Equipment': 'Specialty Equipment',
                'Air Terminal': 'Air Terminals',
                'Air Terminals': 'Air Terminals',
                'Duct Terminal': 'Air Terminals',
            }
            if cat_name in variations:
                return variations[cat_name]
            
            return None
        except:
            return None
    
    def get_existing_params(self, family_doc):
        """Get existing parameter names in family."""
        existing = set()
        try:
            fm = family_doc.FamilyManager
            for param in fm.Parameters:
                existing.add(param.Definition.Name)
        except:
            pass
        return existing
    
    def get_param_by_name(self, family_doc, param_name):
        """Get family parameter by name."""
        try:
            fm = family_doc.FamilyManager
            for param in fm.Parameters:
                if param.Definition.Name == param_name:
                    return param
        except:
            pass
        return None
    
    def add_parameters(self, family_doc, params_to_add, dry_run=False):
        """Add parameters to family."""
        fm = family_doc.FamilyManager
        existing = self.get_existing_params(family_doc)
        
        stats = {'added': [], 'skipped': [], 'failed': []}
        
        for param in params_to_add:
            name = param['name']
            
            if name in existing:
                stats['skipped'].append(name)
                continue
            
            if name not in self.definitions:
                stats['failed'].append((name, "Not in shared parameter file"))
                continue
            
            if dry_run:
                stats['added'].append(name)
                continue
            
            try:
                defn = self.definitions[name]
                is_instance = param['binding_type'].lower() == 'instance'
                param_group = get_parameter_group(param['group'])
                
                # Add parameter with appropriate API
                if param_group is not None:
                    fm.AddParameter(defn, param_group, is_instance)
                else:
                    # This shouldn't happen, but fallback to adding without group
                    stats['failed'].append((name, "Could not determine parameter group"))
                    continue
                
                stats['added'].append(name)
            except Exception as e:
                stats['failed'].append((name, str(e)))
        
        return stats
    
    def apply_formulas(self, family_doc, cat_name, dry_run=False):
        """Apply formulas to calculated parameters in family."""
        if cat_name not in CATEGORY_FORMULAS:
            return {'applied': [], 'skipped': [], 'failed': []}
        
        fm = family_doc.FamilyManager
        existing = self.get_existing_params(family_doc)
        
        stats = {'applied': [], 'skipped': [], 'failed': []}
        
        formulas = CATEGORY_FORMULAS[cat_name]
        
        for formula_info in formulas:
            param_name = formula_info['parameter']
            formula_str = formula_info['formula']
            
            if param_name not in existing:
                stats['skipped'].append((param_name, "Parameter not in family"))
                continue
            
            inputs_str = formula_info.get('inputs', '')
            if inputs_str:
                input_params = [p.strip() for p in inputs_str.split(',')]
                missing_inputs = [p for p in input_params if p and p not in existing]
                if missing_inputs:
                    stats['skipped'].append((param_name, "Missing inputs: {}".format(', '.join(missing_inputs[:3]))))
                    continue
            
            if dry_run:
                stats['applied'].append(param_name)
                continue
            
            try:
                param = self.get_param_by_name(family_doc, param_name)
                if param is None:
                    stats['failed'].append((param_name, "Could not find parameter"))
                    continue
                
                fm.SetFormula(param, formula_str)
                stats['applied'].append(param_name)
                
            except Exception as e:
                stats['failed'].append((param_name, str(e)))
        
        return stats
    
    def backup_family(self, family_path, backup_dir):
        """Create backup of family file."""
        try:
            if not os.path.exists(backup_dir):
                os.makedirs(backup_dir)
            
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            base = os.path.splitext(os.path.basename(family_path))[0]
            backup_path = os.path.join(backup_dir, "{}_{}.rfa".format(base, timestamp))
            
            shutil.copy2(family_path, backup_path)
            return backup_path
        except:
            return None
    
    def process_family(self, family_path, dry_run=False, create_backup=True):
        """Process a single family file."""
        family_name = os.path.basename(family_path)
        
        result = {
            'family': family_name,
            'path': family_path,
            'category': None,
            'status': 'unknown',
            'added': [],
            'skipped': [],
            'failed': [],
            'formulas_applied': [],
            'formulas_skipped': [],
            'formulas_failed': [],
            'backup': None,
            'params_available': 0,
            'formulas_available': 0
        }
        
        family_doc = None
        
        try:
            family_doc = self.app.OpenDocumentFile(family_path)
            if family_doc is None:
                result['status'] = 'error'
                result['failed'].append(("", "Could not open family"))
                return result
            
            cat_name = self.get_family_category(family_doc)
            result['category'] = cat_name
            
            if cat_name is None or cat_name not in self.category_params:
                result['status'] = 'skipped'
                result['failed'].append(("", "No parameter mapping for category"))
                family_doc.Close(False)
                return result
            
            params_for_cat = self.category_params[cat_name]
            result['params_available'] = len(params_for_cat)
            result['formulas_available'] = len(CATEGORY_FORMULAS.get(cat_name, []))
            
            # Add parameters
            with revit.Transaction("Add Parameters", doc=family_doc):
                stats = self.add_parameters(family_doc, params_for_cat, dry_run)
            
            result['added'] = stats['added']
            result['skipped'] = stats['skipped']
            result['failed'].extend(stats['failed'])
            
            # Apply formulas (in separate transaction)
            if not dry_run and cat_name in CATEGORY_FORMULAS:
                with revit.Transaction("Apply Formulas", doc=family_doc):
                    formula_stats = self.apply_formulas(family_doc, cat_name, dry_run)
                result['formulas_applied'] = formula_stats['applied']
                result['formulas_skipped'] = formula_stats['skipped']
                result['formulas_failed'] = formula_stats['failed']
            elif dry_run:
                formula_stats = self.apply_formulas(family_doc, cat_name, dry_run=True)
                result['formulas_applied'] = formula_stats['applied']
                result['formulas_skipped'] = formula_stats['skipped']
            
            # Save if changes made
            if not dry_run and (stats['added'] or result['formulas_applied']):
                if create_backup:
                    backup_dir = os.path.join(os.path.dirname(family_path), '_param_backups')
                    result['backup'] = self.backup_family(family_path, backup_dir)
                
                save_opts = SaveAsOptions()
                save_opts.OverwriteExistingFile = True
                family_doc.SaveAs(family_path, save_opts)
            
            if dry_run:
                result['status'] = 'preview'
            elif stats['added'] or result['formulas_applied']:
                result['status'] = 'success'
            else:
                result['status'] = 'no_changes'
            
            family_doc.Close(False)
            
        except Exception as e:
            result['status'] = 'error'
            result['failed'].append(("", str(e)))
            if family_doc is not None:
                try:
                    family_doc.Close(False)
                except:
                    pass
        
        return result
    
    def process_families(self, family_paths, dry_run=False, create_backup=True):
        """Process multiple families."""
        self.results = []
        total = len(family_paths)
        
        for idx, path in enumerate(family_paths):
            output.print_md("---")
            output.print_md("**[{}/{}]** {}".format(
                idx + 1, total, os.path.basename(path)))
            
            result = self.process_family(path, dry_run, create_backup)
            self.results.append(result)
            
            if result['category']:
                output.print_md("Category: {} ({} params, {} formulas available)".format(
                    result['category'], result['params_available'], result['formulas_available']))
            
            if result['added']:
                output.print_md("Params added: **{}**".format(len(result['added'])))
            
            if result['skipped']:
                output.print_md("Params skipped (existing): {}".format(len(result['skipped'])))
            
            if result['formulas_applied']:
                output.print_md("Formulas applied: **{}**".format(len(result['formulas_applied'])))
            
            if result['status'] == 'skipped':
                output.print_md("*Skipped - no mapping for category*")
            elif result['status'] == 'error':
                errors = [f[1] for f in result['failed'] if isinstance(f, tuple) and f[1]]
                if errors:
                    output.print_md("**ERROR**: {}".format(errors[0][:100]))
        
        return self.results


def select_families():
    """Let user select family files."""
    options = ['Select Individual Families', 'Select Folder (recursive)']
    choice = forms.CommandSwitchWindow.show(options, message='Select families:')
    
    if choice == 'Select Individual Families':
        files = forms.pick_file(file_ext='rfa', multi_file=True, 
                                title='Select Family Files (.rfa)')
        return list(files) if files else []
    
    elif choice == 'Select Folder (recursive)':
        folder = forms.pick_folder(title='Select Folder Containing Families')
        if folder:
            families = []
            for root, dirs, files in os.walk(folder):
                dirs[:] = [d for d in dirs if not d.startswith('_')]
                for f in files:
                    if f.lower().endswith('.rfa') and not f.startswith('~'):
                        families.append(os.path.join(root, f))
            return families
    
    return []


def print_summary(results, dry_run=False):
    """Print processing summary."""
    output.print_md("---")
    output.print_md("# {}".format("Preview Results" if dry_run else "Processing Complete"))
    
    total = len(results)
    success = sum(1 for r in results if r['status'] == 'success')
    no_changes = sum(1 for r in results if r['status'] == 'no_changes')
    skipped = sum(1 for r in results if r['status'] == 'skipped')
    errors = sum(1 for r in results if r['status'] == 'error')
    
    total_added = sum(len(r['added']) for r in results)
    total_skipped = sum(len(r['skipped']) for r in results)
    total_failed = sum(len(r['failed']) for r in results)
    total_formulas = sum(len(r.get('formulas_applied', [])) for r in results)
    
    output.print_md("## Families")
    output.print_md("| Status | Count |")
    output.print_md("|--------|-------|")
    output.print_md("| Total processed | {} |".format(total))
    
    if dry_run:
        output.print_md("| Would be modified | {} |".format(
            sum(1 for r in results if len(r['added']) > 0 or len(r.get('formulas_applied', [])) > 0)))
    else:
        output.print_md("| Successfully modified | {} |".format(success))
        output.print_md("| No changes needed | {} |".format(no_changes))
    
    output.print_md("| Skipped (no mapping) | {} |".format(skipped))
    output.print_md("| Errors | {} |".format(errors))
    
    output.print_md("## Parameters")
    output.print_md("| Status | Count |")
    output.print_md("|--------|-------|")
    output.print_md("| {} | **{}** |".format(
        "Would add" if dry_run else "Added", total_added))
    output.print_md("| Already existed | {} |".format(total_skipped))
    output.print_md("| Failed | {} |".format(total_failed))
    
    output.print_md("## Formulas")
    output.print_md("| Status | Count |")
    output.print_md("|--------|-------|")
    output.print_md("| {} | **{}** |".format(
        "Would apply" if dry_run else "Applied", total_formulas))
    
    by_cat = defaultdict(lambda: {'count': 0, 'added': 0, 'formulas': 0, 'available': 0})
    for r in results:
        if r['category']:
            by_cat[r['category']]['count'] += 1
            by_cat[r['category']]['added'] += len(r['added'])
            by_cat[r['category']]['formulas'] += len(r.get('formulas_applied', []))
            by_cat[r['category']]['available'] = r['params_available']
    
    if by_cat:
        output.print_md("## By Category")
        output.print_md("| Category | Families | Params | Formulas | Available |")
        output.print_md("|----------|----------|--------|----------|-----------|")
        for cat in sorted(by_cat.keys()):
            data = by_cat[cat]
            output.print_md("| {} | {} | {} | {} | {} |".format(
                cat, data['count'], data['added'], data['formulas'], data['available']))
    
    all_errors = []
    for r in results:
        for f in r['failed']:
            if isinstance(f, tuple) and f[1]:
                all_errors.append("{}: {} - {}".format(r['family'], f[0], f[1]))
    
    if all_errors:
        output.print_md("## Errors")
        for err in all_errors[:20]:
            output.print_md("- {}".format(err))
        if len(all_errors) > 20:
            output.print_md("*...and {} more*".format(len(all_errors) - 20))


def main():
    """Main entry point."""
    app = __revit__.Application
    
    output.print_md("# Batch Add Parameters to Families")
    output.print_md("*Self-contained version with formulas - Revit {} detected*".format(REVIT_VERSION))
    output.print_md("- **810 shared parameters**")
    output.print_md("- **53 categories**")
    output.print_md("- **7,072 parameter-category bindings**")
    output.print_md("- **52 calculated formulas**")
    
    # Show API info
    if HAS_GROUP_TYPE_ID:
        output.print_md("- *Using GroupTypeId API (Revit 2024+)*")
        output.print_md("- *Available groups: {}*".format(', '.join(sorted(GROUP_TYPE_ID_MAP.keys()))))
    elif HAS_BUILTIN_PARAM_GROUP:
        output.print_md("- *Using BuiltInParameterGroup API (Revit 2020-2023)*")
    else:
        output.print_md("- **WARNING: No parameter group API available**")
    
    output.print_md("---")
    
    processor = FamilyParameterProcessor(app)
    
    try:
        output.print_md("## Loading Embedded Data")
        success, msg = processor.setup_shared_parameters()
        if not success:
            forms.alert("Failed to load shared parameters:\n{}".format(msg))
            return
        output.print_md("Shared parameters: **{}**".format(msg))
        output.print_md("Categories: **{}** with parameter mappings".format(
            len(processor.category_params)))
        output.print_md("Formulas: **{}** calculated parameters".format(
            len(EMBEDDED_FORMULAS)))
        
        output.print_md("### Categories with Parameters")
        cats = sorted(processor.category_params.keys())
        for cat in cats[:10]:
            formula_count = len(CATEGORY_FORMULAS.get(cat, []))
            output.print_md("- {}: {} params, {} formulas".format(
                cat, len(processor.category_params[cat]), formula_count))
        if len(cats) > 10:
            output.print_md("- *...and {} more categories*".format(len(cats) - 10))
        
        output.print_md("---")
        output.print_md("## Select Families")
        family_paths = select_families()
        if not family_paths:
            forms.alert("No families selected")
            return
        
        output.print_md("Selected **{}** families".format(len(family_paths)))
        
        output.print_md("---")
        output.print_md("## Select Mode")
        mode = forms.CommandSwitchWindow.show(
            ['Preview (no changes)', 'Execute (modify families)'],
            message='Select mode:'
        )
        
        if mode is None:
            return
        
        dry_run = mode == 'Preview (no changes)'
        output.print_md("Mode: **{}**".format("Preview" if dry_run else "Execute"))
        
        if not dry_run:
            if not forms.alert(
                "Ready to modify {} families.\n\n"
                "- Add parameters based on category\n"
                "- Apply calculated formulas\n"
                "- Backups created in '_param_backups' folder\n\n"
                "Proceed?".format(len(family_paths)),
                yes=True, no=True
            ):
                return
        
        output.print_md("---")
        output.print_md("## Processing Families")
        
        results = processor.process_families(
            family_paths,
            dry_run=dry_run,
            create_backup=True
        )
        
        print_summary(results, dry_run)
        
        if dry_run:
            output.print_md("---")
            output.print_md("*This was a preview. Run again and select 'Execute' to apply changes.*")
    
    finally:
        processor.cleanup()


if __name__ == '__main__':
    main()
