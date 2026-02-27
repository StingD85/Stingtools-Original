# -*- coding: utf-8 -*-
"""Measurement Tools - Measure and copy to clipboard"""

from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms
import clr

clr.AddReference('System.Windows.Forms')
from System.Windows.Forms import Clipboard

doc = revit.doc
uidoc = revit.uidoc


def get_display_units():
    """Get the current display units for length"""
    units = doc.GetUnits()
    format_options = units.GetFormatOptions(UnitType.UT_Length)
    return format_options


def format_length(length_in_feet):
    """Format length according to project units"""
    format_opts = get_display_units()
    formatted = UnitFormatUtils.Format(
        doc.GetUnits(),
        UnitType.UT_Length,
        length_in_feet,
        False,
        False
    )
    return formatted


def format_area(area_in_sqft):
    """Format area according to project units"""
    formatted = UnitFormatUtils.Format(
        doc.GetUnits(),
        UnitType.UT_Area,
        area_in_sqft,
        False,
        False
    )
    return formatted


def copy_to_clipboard(text):
    """Copy text to clipboard"""
    Clipboard.SetText(str(text))


def measure_lines():
    """Measure total length of selected lines"""
    selection = revit.get_selection()
    
    lines = []
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        # Get model lines, detail lines, etc
        if hasattr(elem, 'GeometryCurve'):
            lines.append(elem)
        elif isinstance(elem, CurveElement):
            lines.append(elem)
    
    if not lines:
        forms.alert('Please select lines (model lines, detail lines, etc.).')
        return
    
    total_length = 0
    for line_elem in lines:
        try:
            if hasattr(line_elem, 'GeometryCurve'):
                curve = line_elem.GeometryCurve
            else:
                curve = line_elem.GetCurve()
            
            if curve:
                total_length += curve.Length
        except:
            continue
    
    if total_length == 0:
        forms.alert('No valid lengths found.')
        return
    
    # Format and display
    formatted_length = format_length(total_length)
    copy_to_clipboard(formatted_length)
    
    forms.alert('Total Length: {}\n\n(Copied to clipboard)'.format(formatted_length),
               title='Measurement Result')


def measure_areas():
    """Measure total area of selected filled regions"""
    selection = revit.get_selection()
    
    regions = []
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        if isinstance(elem, FilledRegion):
            regions.append(elem)
    
    if not regions:
        forms.alert('Please select filled regions.')
        return
    
    total_area = 0
    for region in regions:
        try:
            # Get area parameter
            area_param = region.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)
            if area_param:
                total_area += area_param.AsDouble()
        except:
            continue
    
    if total_area == 0:
        forms.alert('No valid areas found.')
        return
    
    # Format and display
    formatted_area = format_area(total_area)
    copy_to_clipboard(formatted_area)
    
    forms.alert('Total Area: {}\n\n(Copied to clipboard)'.format(formatted_area),
               title='Measurement Result')


def measure_perimeters():
    """Measure total perimeter of selected filled regions"""
    selection = revit.get_selection()
    
    regions = []
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        if isinstance(elem, FilledRegion):
            regions.append(elem)
    
    if not regions:
        forms.alert('Please select filled regions.')
        return
    
    total_perimeter = 0
    for region in regions:
        try:
            # Get boundary curves
            boundaries = region.GetBoundaries()
            for curve_loop in boundaries:
                for curve in curve_loop:
                    total_perimeter += curve.Length
        except:
            continue
    
    if total_perimeter == 0:
        forms.alert('No valid perimeters found.')
        return
    
    # Format and display
    formatted_perimeter = format_length(total_perimeter)
    copy_to_clipboard(formatted_perimeter)
    
    forms.alert('Total Perimeter: {}\n\n(Copied to clipboard)'.format(formatted_perimeter),
               title='Measurement Result')


def measure_room_areas():
    """Measure total area of selected rooms"""
    selection = revit.get_selection()
    
    rooms = []
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        if isinstance(elem, SpatialElement) and elem.Area > 0:
            rooms.append(elem)
    
    if not rooms:
        forms.alert('Please select rooms or spaces.')
        return
    
    total_area = 0
    room_count = 0
    
    for room in rooms:
        try:
            area = room.Area
            if area > 0:
                total_area += area
                room_count += 1
        except:
            continue
    
    if total_area == 0:
        forms.alert('No valid room areas found.')
        return
    
    # Format and display
    formatted_area = format_area(total_area)
    copy_to_clipboard(formatted_area)
    
    message = 'Total Area: {}\nNumber of Rooms: {}\n\n(Copied to clipboard)'.format(
        formatted_area,
        room_count
    )
    
    forms.alert(message, title='Measurement Result')
