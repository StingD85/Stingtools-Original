# -*- coding: utf-8 -*-
"""Viewport Alignment Tools"""

from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms

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
    box = viewport.GetBoxCenter()
    return box


def get_viewport_outline(viewport):
    """Get outline box of viewport"""
    outline = viewport.GetBoxOutline()
    return outline


def align_top():
    """Align selected viewports to top edge"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Find topmost point
    max_y = max([get_viewport_outline(vp).MaximumPoint.Y for vp in viewports])
    
    with revit.Transaction('Align Viewports Top'):
        for vp in viewports:
            outline = get_viewport_outline(vp)
            center = get_viewport_center(vp)
            
            current_height = outline.MaximumPoint.Y - outline.MinimumPoint.Y
            new_y = max_y - current_height / 2.0
            
            new_center = XYZ(center.X, new_y, center.Z)
            vp.SetBoxCenter(new_center)


def align_middle_y():
    """Align selected viewports to middle Y axis"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Calculate average Y
    centers = [get_viewport_center(vp) for vp in viewports]
    avg_y = sum([c.Y for c in centers]) / len(centers)
    
    with revit.Transaction('Align Viewports Middle Y'):
        for vp in viewports:
            center = get_viewport_center(vp)
            new_center = XYZ(center.X, avg_y, center.Z)
            vp.SetBoxCenter(new_center)


def align_bottom():
    """Align selected viewports to bottom edge"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Find bottommost point
    min_y = min([get_viewport_outline(vp).MinimumPoint.Y for vp in viewports])
    
    with revit.Transaction('Align Viewports Bottom'):
        for vp in viewports:
            outline = get_viewport_outline(vp)
            center = get_viewport_center(vp)
            
            current_height = outline.MaximumPoint.Y - outline.MinimumPoint.Y
            new_y = min_y + current_height / 2.0
            
            new_center = XYZ(center.X, new_y, center.Z)
            vp.SetBoxCenter(new_center)


def align_left():
    """Align selected viewports to left edge"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Find leftmost point
    min_x = min([get_viewport_outline(vp).MinimumPoint.X for vp in viewports])
    
    with revit.Transaction('Align Viewports Left'):
        for vp in viewports:
            outline = get_viewport_outline(vp)
            center = get_viewport_center(vp)
            
            current_width = outline.MaximumPoint.X - outline.MinimumPoint.X
            new_x = min_x + current_width / 2.0
            
            new_center = XYZ(new_x, center.Y, center.Z)
            vp.SetBoxCenter(new_center)


def align_middle_x():
    """Align selected viewports to middle X axis"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Calculate average X
    centers = [get_viewport_center(vp) for vp in viewports]
    avg_x = sum([c.X for c in centers]) / len(centers)
    
    with revit.Transaction('Align Viewports Middle X'):
        for vp in viewports:
            center = get_viewport_center(vp)
            new_center = XYZ(avg_x, center.Y, center.Z)
            vp.SetBoxCenter(new_center)


def align_right():
    """Align selected viewports to right edge"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Find rightmost point
    max_x = max([get_viewport_outline(vp).MaximumPoint.X for vp in viewports])
    
    with revit.Transaction('Align Viewports Right'):
        for vp in viewports:
            outline = get_viewport_outline(vp)
            center = get_viewport_center(vp)
            
            current_width = outline.MaximumPoint.X - outline.MinimumPoint.X
            new_x = max_x - current_width / 2.0
            
            new_center = XYZ(new_x, center.Y, center.Z)
            vp.SetBoxCenter(new_center)
