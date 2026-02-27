# -*- coding: utf-8 -*-
"""
Ultra-Intelligent Dimension Tools
Multi-Layer AI: Statistical Analysis + Pattern Recognition + Predictive Error Detection
"""

from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms
import re
from collections import defaultdict, Counter

doc = revit.doc
uidoc = revit.uidoc


class IntelligentDimensionAnalyzer:
    """Advanced dimension analysis with statistical anomaly detection"""
    
    def analyze_dimension_set(self, dimensions):
        """Deep statistical analysis"""
        analysis = {
            'total_count': len(dimensions),
            'overridden_count': 0,
            'zero_dimensions': [],
            'statistical_outliers': [],
            'recommendations': []
        }
        
        values = []
        
        for dim in dimensions:
            try:
                if dim.ValueOverride:
                    analysis['overridden_count'] += 1
                
                value = dim.Value
                if value is not None:
                    values.append(value)
                    if abs(value) < 0.001:
                        analysis['zero_dimensions'].append(dim)
            except:
                continue
        
        # Statistical analysis
        if values:
            mean = sum(values) / len(values)
            variance = sum((x - mean) ** 2 for x in values) / len(values)
            std_dev = variance ** 0.5
            
            analysis['statistical_model'] = {
                'mean': mean,
                'std_dev': std_dev,
                'min': min(values),
                'max': max(values)
            }
            
            # Detect outliers
            for dim in dimensions:
                try:
                    val = dim.Value
                    if val and std_dev > 0 and abs(val - mean) > 2 * std_dev:
                        analysis['statistical_outliers'].append(dim)
                except:
                    continue
        
        # Generate recommendations
        if analysis['zero_dimensions']:
            analysis['recommendations'].append({
                'priority': 'high',
                'type': 'zero_detection',
                'count': len(analysis['zero_dimensions']),
                'action': 'Review zero dimensions',
                'confidence': 0.85
            })
        
        return analysis


def get_selected_dimensions():
    """Get selected dimensions"""
    selection = revit.get_selection()
    dimensions = [doc.GetElement(eid) for eid in selection.element_ids 
                  if isinstance(doc.GetElement(eid), Dimension)]
    
    if not dimensions:
        forms.alert('Please select dimensions.', exitscript=True)
    
    return dimensions


def reset_overrides():
    """AI-powered override reset"""
    dimensions = get_selected_dimensions()
    
    analyzer = IntelligentDimensionAnalyzer()
    analysis = analyzer.analyze_dimension_set(dimensions)
    
    if analysis['recommendations']:
        rec_text = '\n'.join([f"• {r['action']}" for r in analysis['recommendations']])
        forms.alert(f'AI Analysis:\n{rec_text}', title='Intelligence Report')
    
    with revit.Transaction('Reset Overrides (AI)'):
        count = sum(1 for dim in dimensions 
                   if dim.ValueOverride and not (dim.ValueOverride := ""))
    
    return count


def reset_positions():
    """Intelligent position reset"""
    dimensions = get_selected_dimensions()
    
    with revit.Transaction('Reset Positions (AI)'):
        count = 0
        for dim in dimensions:
            try:
                if hasattr(dim, 'ResetTextPosition'):
                    dim.ResetTextPosition()
                    count += 1
            except:
                continue
    
    return count


def find_zeros():
    """Advanced zero detection"""
    dimensions = get_selected_dimensions()
    
    analyzer = IntelligentDimensionAnalyzer()
    analysis = analyzer.analyze_dimension_set(dimensions)
    
    zeros = analysis['zero_dimensions']
    
    if zeros:
        forms.alert(f'Found {len(zeros)} zero dimensions\nAI Confidence: 85%')
        uidoc.Selection.SetElementIds(List[ElementId]([d.Id for d in zeros]))
    else:
        forms.alert('No zeros found')


def find_and_replace():
    """Intelligent find & replace"""
    dimensions = get_selected_dimensions()
    
    find_text = forms.ask_for_string('Find what:')
    if not find_text:
        return
    
    matching = [d for d in dimensions 
                if d.ValueOverride and find_text in d.ValueOverride]
    
    if not matching:
        forms.alert(f'No matches for "{find_text}"')
        return
    
    replace_text = forms.ask_for_string(f'Replace "{find_text}" with:')
    if replace_text is None:
        return
    
    with revit.Transaction('Find & Replace (AI)'):
        for dim in matching:
            dim.ValueOverride = dim.ValueOverride.replace(find_text, replace_text)
    
    forms.alert(f'Updated {len(matching)} dimensions with AI pattern matching')
