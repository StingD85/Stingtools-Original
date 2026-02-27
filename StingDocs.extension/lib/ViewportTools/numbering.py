# -*- coding: utf-8 -*-
"""Viewport Numbering Tools"""

from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms
import re

doc = revit.doc
uidoc = revit.uidoc


def get_selected_viewports():
    """Get currently selected viewports on sheet"""
    selection = revit.get_selection()
    viewports = []
    
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        if isinstance(elem, Viewport):
            viewports.append(elem)
    
    if not viewports:
        forms.alert('Please select viewports on a sheet.', exitscript=True)
    
    return viewports


def get_viewport_center(viewport):
    """Get center point of viewport"""
    return viewport.GetBoxCenter()


def extract_number(detail_number_str):
    """Extract numeric value from detail number string"""
    # Try to find digits in the string
    match = re.search(r'\d+', str(detail_number_str))
    if match:
        return int(match.group())
    return 0


def number_by_click():
    """Number viewports by clicking them in sequence"""
    forms.alert('Number by Click feature:\nClick viewports in order to number them.\n\n'
               'This feature requires click selection which is not yet implemented in this version.')


def renumber_left_to_right():
    """Renumber viewports from left to right"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return 0
    
    # Get starting number
    start_num = forms.ask_for_string(
        prompt='Enter starting detail number:',
        default='1',
        title='Starting Number'
    )
    
    if not start_num:
        return 0
    
    try:
        start_number = int(start_num)
    except:
        forms.alert('Please enter a valid number.')
        return 0
    
    # Sort viewports by X coordinate (left to right)
    sorted_vps = sorted(viewports, key=lambda vp: get_viewport_center(vp).X)
    
    with revit.Transaction('Renumber Viewports Left to Right'):
        for i, vp in enumerate(sorted_vps):
            detail_num_param = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
            if detail_num_param and not detail_num_param.IsReadOnly:
                detail_num_param.Set(str(start_number + i))
    
    return len(viewports)


def renumber_top_to_bottom():
    """Renumber viewports from top to bottom"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return 0
    
    # Get starting number
    start_num = forms.ask_for_string(
        prompt='Enter starting detail number:',
        default='1',
        title='Starting Number'
    )
    
    if not start_num:
        return 0
    
    try:
        start_number = int(start_num)
    except:
        forms.alert('Please enter a valid number.')
        return 0
    
    # Sort viewports by Y coordinate (top to bottom - descending Y)
    sorted_vps = sorted(viewports, key=lambda vp: get_viewport_center(vp).Y, reverse=True)
    
    with revit.Transaction('Renumber Viewports Top to Bottom'):
        for i, vp in enumerate(sorted_vps):
            detail_num_param = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
            if detail_num_param and not detail_num_param.IsReadOnly:
                detail_num_param.Set(str(start_number + i))
    
    return len(viewports)


def add_to_detail_number(increment):
    """Add increment to detail numbers (can be negative)"""
    viewports = get_selected_viewports()
    
    if not viewports:
        return 0
    
    count = 0
    with revit.Transaction('Modify Detail Numbers'):
        for vp in viewports:
            detail_num_param = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
            if detail_num_param and not detail_num_param.IsReadOnly:
                current_value = detail_num_param.AsString()
                
                # Extract number from current value
                current_number = extract_number(current_value)
                
                # Add increment
                new_number = max(1, current_number + increment)
                
                # Set new value
                detail_num_param.Set(str(new_number))
                count += 1
    
    return count


def add_prefix():
    """Add prefix to detail numbers"""
    viewports = get_selected_viewports()
    
    if not viewports:
        return
    
    prefix = forms.ask_for_string(
        prompt='Enter prefix to add:',
        default='A-',
        title='Add Prefix'
    )
    
    if not prefix:
        return
    
    with revit.Transaction('Add Prefix to Detail Numbers'):
        for vp in viewports:
            detail_num_param = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
            if detail_num_param and not detail_num_param.IsReadOnly:
                current_value = detail_num_param.AsString()
                new_value = prefix + current_value
                detail_num_param.Set(new_value)


def add_suffix():
    """Add suffix to detail numbers"""
    viewports = get_selected_viewports()
    
    if not viewports:
        return
    
    suffix = forms.ask_for_string(
        prompt='Enter suffix to add:',
        default='-R1',
        title='Add Suffix'
    )
    
    if not suffix:
        return
    
    with revit.Transaction('Add Suffix to Detail Numbers'):
        for vp in viewports:
            detail_num_param = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)
            if detail_num_param and not detail_num_param.IsReadOnly:
                current_value = detail_num_param.AsString()
                new_value = current_value + suffix
                detail_num_param.Set(new_value)
