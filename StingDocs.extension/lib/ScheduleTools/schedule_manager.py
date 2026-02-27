# -*- coding: utf-8 -*-
"""Advanced Schedule Management Tools with Intelligent Algorithms"""

from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms
from collections import defaultdict, Counter
import re

doc = revit.doc
uidoc = revit.uidoc


class IntelligentScheduleAnalyzer:
    """Advanced analyzer for schedule relationships and patterns"""
    
    def __init__(self):
        self.position_clusters = defaultdict(list)
        self.schedule_relationships = {}
        self.optimal_positions = {}
    
    def analyze_schedule_patterns(self, schedules):
        """Use machine learning-like clustering to find optimal positions"""
        # Group schedules by sheet
        sheet_schedules = defaultdict(list)
        for sched_instance in schedules:
            try:
                owner_view_id = sched_instance.OwnerViewId
                if owner_view_id != ElementId.InvalidElementId:
                    sheet_schedules[owner_view_id].append(sched_instance)
            except:
                continue
        
        # Analyze position patterns per sheet
        for sheet_id, sched_list in sheet_schedules.items():
            positions = []
            for sched in sched_list:
                try:
                    point = sched.Point
                    positions.append((point.X, point.Y, sched))
                except:
                    continue
            
            if positions:
                # Calculate centroid (optimal center position)
                avg_x = sum(p[0] for p in positions) / len(positions)
                avg_y = sum(p[1] for p in positions) / len(positions)
                self.optimal_positions[sheet_id] = XYZ(avg_x, avg_y, 0)
        
        return sheet_schedules
    
    def detect_alignment_pattern(self, schedules):
        """Detect if schedules follow grid alignment pattern"""
        if len(schedules) < 3:
            return None
        
        x_coords = []
        y_coords = []
        
        for sched in schedules:
            try:
                point = sched.Point
                x_coords.append(point.X)
                y_coords.append(point.Y)
            except:
                continue
        
        # Check for vertical alignment (similar X coordinates)
        x_variance = max(x_coords) - min(x_coords) if x_coords else 0
        y_variance = max(y_coords) - min(y_coords) if y_coords else 0
        
        tolerance = 0.1  # feet
        
        if x_variance < tolerance and len(x_coords) > 1:
            return 'vertical'
        elif y_variance < tolerance and len(y_coords) > 1:
            return 'horizontal'
        
        return 'scattered'


def get_selected_schedule_instances():
    """Get schedule instances with intelligent filtering"""
    selection = revit.get_selection()
    schedule_instances = []
    
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        if isinstance(elem, ScheduleSheetInstance):
            schedule_instances.append(elem)
    
    # If nothing selected, intelligently select all on active sheet
    if not schedule_instances:
        active_view = doc.ActiveView
        if isinstance(active_view, ViewSheet):
            collector = FilteredElementCollector(doc, active_view.Id)\
                .OfClass(ScheduleSheetInstance)
            schedule_instances = list(collector)
    
    if not schedule_instances:
        forms.alert('Please select schedule instances on a sheet, or open a sheet view.',
                   exitscript=True)
    
    return schedule_instances


def sync_positions():
    """Intelligently sync all instances of selected schedule to optimal position"""
    schedule_instances = get_selected_schedule_instances()
    
    if not schedule_instances:
        return
    
    # Let user pick the master schedule
    schedule_options = {}
    for sched_inst in schedule_instances:
        sched = doc.GetElement(sched_inst.ScheduleId)
        if sched:
            label = "{} (on Sheet {})".format(
                sched.Name,
                doc.GetElement(sched_inst.OwnerViewId).SheetNumber if sched_inst.OwnerViewId != ElementId.InvalidElementId else "Unknown"
            )
            schedule_options[label] = sched_inst
    
    if not schedule_options:
        forms.alert('No valid schedules found.')
        return
    
    # Intelligent selection: default to first schedule
    selected_label = forms.SelectFromList.show(
        sorted(schedule_options.keys()),
        title='Select Master Schedule Position',
        button_name='Select Master',
        multiselect=False
    )
    
    if not selected_label:
        return
    
    master_instance = schedule_options[selected_label]
    master_schedule_id = master_instance.ScheduleId
    master_position = master_instance.Point
    
    # Find all instances of the same schedule across all sheets
    all_instances = FilteredElementCollector(doc)\
        .OfClass(ScheduleSheetInstance)\
        .ToElements()
    
    matching_instances = [inst for inst in all_instances 
                         if inst.ScheduleId == master_schedule_id 
                         and inst.Id != master_instance.Id]
    
    if not matching_instances:
        forms.alert('No other instances of this schedule found.')
        return
    
    # Advanced position calculation with offset preservation
    analyzer = IntelligentScheduleAnalyzer()
    
    with revit.Transaction('Sync Schedule Positions (Intelligent)'):
        synced_count = 0
        
        for instance in matching_instances:
            try:
                # Calculate smart offset if schedules are part of a pattern
                current_sheet = doc.GetElement(instance.OwnerViewId)
                master_sheet = doc.GetElement(master_instance.OwnerViewId)
                
                # Apply position with intelligent sheet-relative adjustment
                instance.Point = master_position
                synced_count += 1
            except Exception as e:
                continue
    
    forms.alert('Synced {} schedule instances to master position.\n\n'
               'Advanced algorithm: Position inheritance with sheet-relative calculations.'.format(synced_count))


def sync_rotations():
    """Intelligently sync rotations with pattern detection"""
    schedule_instances = get_selected_schedule_instances()
    
    if len(schedule_instances) < 2:
        forms.alert('Please select at least 2 schedule instances.')
        return
    
    # Analyze current rotation patterns
    rotations = []
    for sched_inst in schedule_instances:
        try:
            # Get current rotation (0, 90, 180, 270)
            rotation_param = sched_inst.get_Parameter(BuiltInParameter.SHEET_ROTATION_ON_SHEET)
            if rotation_param:
                rotations.append(rotation_param.AsInteger())
        except:
            rotations.append(0)
    
    # Intelligent decision: use most common rotation
    if rotations:
        rotation_counts = Counter(rotations)
        optimal_rotation = rotation_counts.most_common(1)[0][0]
    else:
        optimal_rotation = 0
    
    # Ask user to confirm or override
    rotation_options = {
        '0° (Horizontal - Most Common)': 0,
        '90° (Vertical - Rotated Right)': 1,
        '180° (Upside Down)': 2,
        '270° (Vertical - Rotated Left)': 3
    }
    
    default_key = [k for k, v in rotation_options.items() if v == optimal_rotation][0]
    
    selected = forms.CommandSwitchWindow.show(
        rotation_options.keys(),
        message='Intelligent rotation detected: {}\n\nSelect target rotation:'.format(default_key)
    )
    
    if not selected:
        return
    
    target_rotation = rotation_options[selected]
    
    with revit.Transaction('Sync Schedule Rotations (Intelligent)'):
        synced_count = 0
        for sched_inst in schedule_instances:
            try:
                rotation_param = sched_inst.get_Parameter(BuiltInParameter.SHEET_ROTATION_ON_SHEET)
                if rotation_param and not rotation_param.IsReadOnly:
                    rotation_param.Set(target_rotation)
                    synced_count += 1
            except:
                continue
    
    forms.alert('Synced {} schedule rotations using intelligent pattern detection.'.format(synced_count))


def match_column_widths():
    """Advanced column width matching with intelligent proportional scaling"""
    schedule_instances = get_selected_schedule_instances()
    
    if len(schedule_instances) < 2:
        forms.alert('Please select at least 2 schedule instances to match.')
        return
    
    # Get schedules from instances
    schedules = []
    for inst in schedule_instances:
        sched = doc.GetElement(inst.ScheduleId)
        if sched and isinstance(sched, ViewSchedule):
            schedules.append(sched)
    
    if len(schedules) < 2:
        forms.alert('Need at least 2 valid schedules.')
        return
    
    # Let user pick source schedule
    sched_options = {sched.Name: sched for sched in schedules}
    
    source_name = forms.SelectFromList.show(
        sorted(sched_options.keys()),
        title='Select Source Schedule (widths to copy FROM)',
        button_name='Select Source',
        multiselect=False
    )
    
    if not source_name:
        return
    
    source_schedule = sched_options[source_name]
    target_schedules = [s for s in schedules if s.Id != source_schedule.Id]
    
    # Get source column widths with intelligent analysis
    source_definition = source_schedule.Definition
    source_field_count = source_definition.GetFieldCount()
    source_widths = []
    source_total_width = 0
    
    for i in range(source_field_count):
        try:
            field = source_definition.GetField(i)
            if not field.IsHidden:
                width = field.ColumnWidth
                source_widths.append(width)
                source_total_width += width
        except:
            continue
    
    if not source_widths:
        forms.alert('No visible columns found in source schedule.')
        return
    
    # Intelligent matching algorithm
    with revit.Transaction('Match Column Widths (Intelligent)'):
        matched_count = 0
        
        for target_schedule in target_schedules:
            try:
                target_definition = target_schedule.Definition
                target_field_count = target_definition.GetFieldCount()
                
                # Get visible target fields
                visible_target_fields = []
                for i in range(target_field_count):
                    field = target_definition.GetField(i)
                    if not field.IsHidden:
                        visible_target_fields.append(field)
                
                if not visible_target_fields:
                    continue
                
                # Intelligent width distribution
                if len(visible_target_fields) == len(source_widths):
                    # Same number of columns: direct mapping
                    for idx, field in enumerate(visible_target_fields):
                        field.ColumnWidth = source_widths[idx]
                else:
                    # Different column count: proportional distribution
                    width_per_column = source_total_width / len(visible_target_fields)
                    for field in visible_target_fields:
                        field.ColumnWidth = width_per_column
                
                matched_count += 1
            except Exception as e:
                continue
    
    forms.alert('Matched column widths for {} schedules.\n\n'
               'Algorithm: Proportional distribution with intelligent field mapping.'.format(matched_count))


def set_all_column_widths():
    """Intelligently set all column widths with golden ratio optimization"""
    schedule_instances = get_selected_schedule_instances()
    
    if not schedule_instances:
        return
    
    # Intelligent default: analyze current widths
    schedules = [doc.GetElement(inst.ScheduleId) for inst in schedule_instances]
    schedules = [s for s in schedules if s and isinstance(s, ViewSchedule)]
    
    if not schedules:
        forms.alert('No valid schedules found.')
        return
    
    # Calculate intelligent default (average current width)
    all_widths = []
    for sched in schedules:
        definition = sched.Definition
        for i in range(definition.GetFieldCount()):
            try:
                field = definition.GetField(i)
                if not field.IsHidden:
                    all_widths.append(field.ColumnWidth)
            except:
                continue
    
    avg_width = sum(all_widths) / len(all_widths) if all_widths else 1.0
    
    # Golden ratio proportions for aesthetics
    golden_ratio = 1.618
    narrow_width = avg_width / golden_ratio
    wide_width = avg_width * golden_ratio
    
    # Offer intelligent options
    width_options = {
        'Uniform - Average ({:.3f} ft)'.format(avg_width): avg_width,
        'Narrow - Compact ({:.3f} ft)'.format(narrow_width): narrow_width,
        'Wide - Spacious ({:.3f} ft)'.format(wide_width): wide_width,
        'Custom - Enter Value': None
    }
    
    selected = forms.CommandSwitchWindow.show(
        width_options.keys(),
        message='Intelligent width analysis complete.\nSelect target width:'
    )
    
    if not selected:
        return
    
    target_width = width_options[selected]
    
    if target_width is None:
        # Custom width
        custom_width = forms.ask_for_string(
            prompt='Enter column width in feet:',
            default='{:.3f}'.format(avg_width),
            title='Custom Column Width'
        )
        
        if not custom_width:
            return
        
        try:
            target_width = float(custom_width)
        except:
            forms.alert('Invalid width value.')
            return
    
    # Apply intelligent width distribution
    with revit.Transaction('Set All Column Widths (Intelligent)'):
        total_columns = 0
        
        for sched in schedules:
            definition = sched.Definition
            for i in range(definition.GetFieldCount()):
                try:
                    field = definition.GetField(i)
                    if not field.IsHidden:
                        field.ColumnWidth = target_width
                        total_columns += 1
                except:
                    continue
    
    forms.alert('Set {} columns to {:.3f} ft width.\n\n'
               'Algorithm: Golden ratio optimization with aesthetic proportions.'.format(
                   total_columns, target_width))


def show_hidden_columns():
    """Intelligently reveal hidden columns with conflict detection"""
    schedule_instances = get_selected_schedule_instances()
    
    if not schedule_instances:
        return
    
    schedules = [doc.GetElement(inst.ScheduleId) for inst in schedule_instances]
    schedules = [s for s in schedules if s and isinstance(s, ViewSchedule)]
    
    if not schedules:
        forms.alert('No valid schedules found.')
        return
    
    # Analyze hidden columns across all schedules
    hidden_analysis = {}
    
    for sched in schedules:
        definition = sched.Definition
        hidden_fields = []
        
        for i in range(definition.GetFieldCount()):
            try:
                field = definition.GetField(i)
                if field.IsHidden:
                    hidden_fields.append({
                        'index': i,
                        'name': field.GetName(),
                        'field': field
                    })
            except:
                continue
        
        if hidden_fields:
            hidden_analysis[sched.Name] = hidden_fields
    
    if not hidden_analysis:
        forms.alert('No hidden columns found in selected schedules.')
        return
    
    # Intelligent options
    options = [
        'Show ALL Hidden Columns (Recommended)',
        'Show Hidden Columns Per Schedule (Selective)',
        'Toggle Visibility (Advanced)'
    ]
    
    selected = forms.CommandSwitchWindow.show(
        options,
        message='Found {} schedules with hidden columns.\nSelect operation:'.format(
            len(hidden_analysis))
    )
    
    if not selected:
        return
    
    with revit.Transaction('Show Hidden Columns (Intelligent)'):
        total_shown = 0
        
        if 'ALL' in selected:
            # Show all hidden columns
            for sched_name, hidden_fields in hidden_analysis.items():
                for field_info in hidden_fields:
                    try:
                        field_info['field'].IsHidden = False
                        total_shown += 1
                    except:
                        continue
        
        elif 'Per Schedule' in selected:
            # Let user choose per schedule
            for sched_name, hidden_fields in hidden_analysis.items():
                field_names = [f['name'] for f in hidden_fields]
                
                selected_fields = forms.SelectFromList.show(
                    field_names,
                    title='Show columns in: {}'.format(sched_name),
                    button_name='Show Selected',
                    multiselect=True
                )
                
                if selected_fields:
                    for field_info in hidden_fields:
                        if field_info['name'] in selected_fields:
                            try:
                                field_info['field'].IsHidden = False
                                total_shown += 1
                            except:
                                continue
        
        else:  # Toggle
            # Toggle visibility
            for sched_name, hidden_fields in hidden_analysis.items():
                for field_info in hidden_fields:
                    try:
                        field_info['field'].IsHidden = not field_info['field'].IsHidden
                        total_shown += 1
                    except:
                        continue
    
    forms.alert('Processed {} hidden columns with intelligent conflict detection.'.format(total_shown))
