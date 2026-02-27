# -*- coding: utf-8 -*-
"""Sheet Management Tools"""

from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms

doc = revit.doc
uidoc = revit.uidoc


def get_selected_sheets():
    """Get currently selected sheets"""
    selection = revit.get_selection()
    sheets = []
    
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        if isinstance(elem, ViewSheet):
            sheets.append(elem)
    
    if not sheets:
        forms.alert('Please select sheets in the Project Browser.', exitscript=True)
    
    return sheets


def reset_title_on_sheet():
    """Reset Title on Sheet parameter to match view name"""
    sheets = get_selected_sheets()
    
    with revit.Transaction('Reset Title on Sheet'):
        count = 0
        for sheet in sheets:
            viewports = sheet.GetAllViewports()
            for vp_id in viewports:
                vp = doc.GetElement(vp_id)
                view = doc.GetElement(vp.ViewId)
                
                # Get Title on Sheet parameter
                title_param = vp.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION)
                if title_param and not title_param.IsReadOnly:
                    # Set to view name
                    title_param.Set(view.Name)
                    count += 1
    
    forms.alert('Reset {} viewport titles.'.format(count))


def add_to_sheet_number(increment):
    """Add increment to sheet numbers"""
    sheets = get_selected_sheets()
    
    # Implementation needed
    forms.alert('Sheet number increment feature coming soon!')
    return 0


def add_prefix():
    """Add prefix to sheet numbers"""
    sheets = get_selected_sheets()
    
    prefix = forms.ask_for_string(
        prompt='Enter prefix to add:',
        default='A-',
        title='Add Prefix'
    )
    
    if not prefix:
        return
    
    with revit.Transaction('Add Prefix to Sheet Numbers'):
        for sheet in sheets:
            sheet_num_param = sheet.get_Parameter(BuiltInParameter.SHEET_NUMBER)
            if sheet_num_param and not sheet_num_param.IsReadOnly:
                current_value = sheet_num_param.AsString()
                new_value = prefix + current_value
                sheet_num_param.Set(new_value)


def add_suffix():
    """Add suffix to sheet numbers"""
    sheets = get_selected_sheets()
    
    suffix = forms.ask_for_string(
        prompt='Enter suffix to add:',
        default='-R1',
        title='Add Suffix'
    )
    
    if not suffix:
        return
    
    with revit.Transaction('Add Suffix to Sheet Numbers'):
        for sheet in sheets:
            sheet_num_param = sheet.get_Parameter(BuiltInParameter.SHEET_NUMBER)
            if sheet_num_param and not sheet_num_param.IsReadOnly:
                current_value = sheet_num_param.AsString()
                new_value = current_value + suffix
                sheet_num_param.Set(new_value)


def find_and_replace():
    """Find and replace in sheet numbers"""
    sheets = get_selected_sheets()
    
    find_text = forms.ask_for_string(
        prompt='Find what:',
        title='Find and Replace'
    )
    
    if not find_text:
        return
    
    replace_text = forms.ask_for_string(
        prompt='Replace with:',
        default='',
        title='Find and Replace'
    )
    
    if replace_text is None:
        return
    
    with revit.Transaction('Find and Replace Sheet Numbers'):
        count = 0
        for sheet in sheets:
            sheet_num_param = sheet.get_Parameter(BuiltInParameter.SHEET_NUMBER)
            if sheet_num_param and not sheet_num_param.IsReadOnly:
                current_value = sheet_num_param.AsString()
                if find_text in current_value:
                    new_value = current_value.replace(find_text, replace_text)
                    sheet_num_param.Set(new_value)
                    count += 1
    
    forms.alert('Updated {} sheet numbers.'.format(count))
