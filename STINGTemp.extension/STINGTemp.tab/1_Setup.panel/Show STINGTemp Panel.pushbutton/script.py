# -*- coding: utf-8 -*-
"""Toggle the STINGTemp dockable window visibility."""

__title__ = "Show\nSTINGTemp"
__author__ = "Author"
__doc__ = "Show or hide the STINGTemp dockable panel."

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from System import Guid
from Autodesk.Revit.UI import DockablePaneId
from pyrevit import HOST_APP

PANEL_GUID = Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")

try:
    uiapp = HOST_APP.uiapp
    pid = DockablePaneId(PANEL_GUID)
    pane = uiapp.GetDockablePane(pid)
    if pane:
        if pane.IsShown():
            pane.Hide()
        else:
            pane.Show()
except Exception as e:
    from Autodesk.Revit.UI import TaskDialog
    TaskDialog.Show(
        "STINGTemp",
        "Could not toggle panel. It may require a Revit restart "
        "after first install.\n\nError: {}".format(str(e)[:200])
    )
