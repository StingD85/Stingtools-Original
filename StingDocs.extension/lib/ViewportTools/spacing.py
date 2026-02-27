# -*- coding: utf-8 -*-
"""Viewport Spacing Tools"""

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
    return viewport.GetBoxCenter()


def get_viewport_outline(viewport):
    """Get outline box of viewport"""
    return viewport.GetBoxOutline()


def order_horizontally():
    """Order viewports horizontally with specified gap"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Get gap distance
    gap_str = forms.ask_for_string(
        prompt='Enter horizontal gap distance (in feet):',
        default='0.5',
        title='Horizontal Gap'
    )
    
    if not gap_str:
        return
    
    try:
        gap = float(gap_str)
    except:
        forms.alert('Please enter a valid number.')
        return
    
    # Sort viewports by X coordinate
    sorted_vps = sorted(viewports, key=lambda vp: get_viewport_center(vp).X)
    
    with revit.Transaction('Order Viewports Horizontally'):
        current_x = get_viewport_center(sorted_vps[0]).X
        
        for i, vp in enumerate(sorted_vps):
            if i == 0:
                continue
            
            outline = get_viewport_outline(vp)
            center = get_viewport_center(vp)
            width = outline.MaximumPoint.X - outline.MinimumPoint.X
            
            # Calculate previous viewport's right edge
            prev_outline = get_viewport_outline(sorted_vps[i-1])
            prev_right = prev_outline.MaximumPoint.X
            
            # Calculate new X position
            new_x = prev_right + gap + width / 2.0
            
            new_center = XYZ(new_x, center.Y, center.Z)
            vp.SetBoxCenter(new_center)


def order_from_middle():
    """Order viewports horizontally from middle of view"""
    viewports = get_selected_viewports()
    
    if len(viewports) < 2:
        forms.alert('Please select at least 2 viewports.')
        return
    
    # Get gap distance
    gap_str = forms.ask_for_string(
        prompt='Enter horizontal gap distance (in feet):',
        default='0.5',
        title='Horizontal Gap'
    )
    
    if not gap_str:
        return
    
    try:
        gap = float(gap_str)
    except:
        forms.alert('Please enter a valid number.')
        return
    
    # Sort viewports by X coordinate
    sorted_vps = sorted(viewports, key=lambda vp: get_viewport_center(vp).X)
    
    # Calculate total width needed
    total_width = sum([get_viewport_outline(vp).MaximumPoint.X - 
                      get_viewport_outline(vp).MinimumPoint.X 
                      for vp in sorted_vps])
    total_width += gap * (len(sorted_vps) - 1)
    
    # Get average Y position
    avg_y = sum([get_viewport_center(vp).Y for vp in sorted_vps]) / len(sorted_vps)
    
    # Calculate starting X position (center of all viewports)
    centers = [get_viewport_center(vp) for vp in sorted_vps]
    avg_x = sum([c.X for c in centers]) / len(centers)
    start_x = avg_x - total_width / 2.0
    
    with revit.Transaction('Order Viewports from Middle'):
        current_x = start_x
        
        for vp in sorted_vps:
            outline = get_viewport_outline(vp)
            width = outline.MaximumPoint.X - outline.MinimumPoint.X
            
            new_x = current_x + width / 2.0
            new_center = XYZ(new_x, avg_y, 0)
            vp.SetBoxCenter(new_center)
            
            current_x += width + gap
