# -*- coding: utf-8 -*-
"""Import Model Patterns from PAT file into Revit.

This script batch imports all patterns from a .pat file as MODEL patterns.
Use for surface patterns like brick, tile, wood, stone, etc.
"""

__title__ = "Import\nModel\nPatterns"
__author__ = "BLE"
__doc__ = "Batch import MODEL fill patterns from .pat file"

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from pyrevit import revit, forms, script
import re
import os

doc = revit.doc
output = script.get_output()

def parse_pat_file(file_path):
    """Parse a .pat file and return list of pattern definitions"""
    patterns = []
    current_pattern = None
    
    with open(file_path, 'r') as f:
        lines = f.readlines()
    
    for line in lines:
        line = line.strip()
        
        if not line or line.startswith(';'):
            continue
        
        if line.startswith('*'):
            if current_pattern and current_pattern['lines']:
                patterns.append(current_pattern)
            
            parts = line[1:].split(',')
            pattern_name = parts[0].strip()
            pattern_desc = parts[1].strip() if len(parts) > 1 else pattern_name
            
            current_pattern = {
                'name': pattern_name,
                'description': pattern_desc,
                'lines': []
            }
        
        elif current_pattern and re.match(r'^-?\d', line):
            current_pattern['lines'].append(line)
    
    if current_pattern and current_pattern['lines']:
        patterns.append(current_pattern)
    
    return patterns

def create_fill_pattern(pattern_def, target):
    """Create a FillPattern from pattern definition"""
    try:
        name = pattern_def['name']
        lines = pattern_def['lines']
        
        fill_grids = []
        
        for line in lines:
            parts = [p.strip() for p in line.split(',')]
            
            angle = float(parts[0])
            x_origin = float(parts[1])
            y_origin = float(parts[2])
            delta_x = float(parts[3]) / 12.0
            delta_y = float(parts[4]) / 12.0
            
            fill_grid = FillGrid()
            fill_grid.Angle = angle * 0.0174533
            fill_grid.Origin = UV(x_origin / 12.0, y_origin / 12.0)
            fill_grid.Shift = delta_x
            fill_grid.Offset = delta_y
            
            if len(parts) > 5:
                segments = []
                for i in range(5, len(parts)):
                    seg_val = float(parts[i]) / 12.0
                    segments.append(abs(seg_val))
                fill_grid.SetSegments(segments)
            
            fill_grids.append(fill_grid)
        
        fill_pattern = FillPattern(name, target, FillPatternHostOrientation.ToView)
        fill_pattern.SetFillGrids(fill_grids)
        
        return fill_pattern
    
    except Exception as e:
        return None

def get_existing_pattern_names(doc, target):
    """Get list of existing fill pattern names"""
    collector = FilteredElementCollector(doc).OfClass(FillPatternElement)
    existing = []
    for elem in collector:
        fp = elem.GetFillPattern()
        if fp and fp.Target == target:
            existing.append(fp.Name)
    return existing

# MAIN
pat_file = forms.pick_file(file_ext='pat', title='Select MODEL Patterns PAT File')

if pat_file:
    output.print_md("# Importing MODEL Patterns")
    output.print_md("**File:** {}".format(pat_file))
    
    patterns = parse_pat_file(pat_file)
    output.print_md("**Found {} patterns in file**".format(len(patterns)))
    
    existing_names = get_existing_pattern_names(doc, FillPatternTarget.Model)
    
    imported = 0
    skipped = 0
    errors = 0
    
    with revit.Transaction("Import Model Patterns"):
        for pattern_def in patterns:
            name = pattern_def['name']
            
            if name in existing_names:
                skipped += 1
                continue
            
            try:
                fill_pattern = create_fill_pattern(pattern_def, FillPatternTarget.Model)
                
                if fill_pattern:
                    FillPatternElement.Create(doc, fill_pattern)
                    imported += 1
                else:
                    errors += 1
            
            except Exception as e:
                errors += 1
    
    output.print_md("---")
    output.print_md("## Results")
    output.print_md("- **Imported:** {} patterns".format(imported))
    output.print_md("- **Skipped:** {} (already exist)".format(skipped))
    output.print_md("- **Errors:** {}".format(errors))
    output.print_md("---")
    output.print_md("✅ **Complete!** Check Manage > Fill Patterns > Model tab")
else:
    forms.alert("No file selected.", title="Cancelled")
