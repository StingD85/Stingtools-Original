// StingBIM.AI.UI.Windows.ChatWindow
// Interactive AI Chat Window for Revit

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace StingBIM.AI.UI.Windows
{
    public partial class ChatWindow : Window
    {
        private readonly ObservableCollection<ChatMessage> _messages;
        private readonly UIApplication _uiApp;
        private readonly Dictionary<string, Func<string, string>> _intentHandlers;

        public ChatWindow(UIApplication uiApp)
        {
            InitializeComponent();

            _uiApp = uiApp;
            _messages = new ObservableCollection<ChatMessage>();
            MessagesContainer.ItemsSource = _messages;

            // Initialize intent handlers
            _intentHandlers = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "help", _ => GetHelpResponse() },
                { "hello", _ => "Hello! I'm your StingBIM AI assistant. How can I help you with your Revit project today?" },
                { "hi", _ => "Hi there! What would you like to do with your BIM model?" },
                { "wall", ProcessWallIntent },
                { "door", ProcessDoorIntent },
                { "window", ProcessWindowIntent },
                { "room", ProcessRoomIntent },
                { "floor", ProcessFloorIntent },
                { "parameter", ProcessParameterIntent },
                { "schedule", ProcessScheduleIntent },
                { "material", ProcessMaterialIntent },
                { "element", ProcessElementIntent },
                { "select", ProcessSelectionIntent },
                { "count", ProcessCountIntent },
                { "list", ProcessListIntent },
                { "area", ProcessAreaIntent },
                { "volume", ProcessVolumeIntent },
            };

            // Add welcome message
            AddAssistantMessage(
                "Welcome to StingBIM AI Assistant!\n\n" +
                "I can help you with:\n" +
                "• Creating and modifying elements (walls, doors, windows)\n" +
                "• Querying model information\n" +
                "• Managing parameters and schedules\n" +
                "• Material information\n\n" +
                "Try asking me something or use the quick actions below!");
        }

        private void AddUserMessage(string text)
        {
            _messages.Add(new ChatMessage
            {
                Text = text,
                IsUser = true,
                Alignment = HorizontalAlignment.Right
            });
            ScrollToBottom();
        }

        private void AddAssistantMessage(string text)
        {
            _messages.Add(new ChatMessage
            {
                Text = text,
                IsUser = false,
                Alignment = HorizontalAlignment.Left
            });
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            MessagesScrollViewer.ScrollToEnd();
        }

        private void SendMessage()
        {
            var input = InputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(input))
                return;

            InputTextBox.Text = string.Empty;
            AddUserMessage(input);

            // Process the input and generate response
            var response = ProcessInput(input);
            AddAssistantMessage(response);
        }

        private string ProcessInput(string input)
        {
            try
            {
                var lowerInput = input.ToLowerInvariant();

                // Check for matching intents
                foreach (var handler in _intentHandlers)
                {
                    if (lowerInput.Contains(handler.Key))
                    {
                        return handler.Value(input);
                    }
                }

                // Default response for unrecognized input
                return GetDefaultResponse(input);
            }
            catch (Exception ex)
            {
                return $"Sorry, I encountered an error: {ex.Message}\n\nPlease try rephrasing your request.";
            }
        }

        private string GetHelpResponse()
        {
            return "Here's what I can help you with:\n\n" +
                   "📋 QUERIES:\n" +
                   "• \"Count walls\" - Count elements by category\n" +
                   "• \"List rooms\" - List elements in the model\n" +
                   "• \"Get room area\" - Get area/volume info\n" +
                   "• \"Show selected element\" - Info about selection\n\n" +
                   "🔧 PARAMETERS:\n" +
                   "• \"List parameters\" - Show available parameters\n" +
                   "• \"Parameter info\" - Get parameter details\n\n" +
                   "📊 SCHEDULES:\n" +
                   "• \"Schedule info\" - Available schedule templates\n\n" +
                   "🎨 MATERIALS:\n" +
                   "• \"Material info\" - Query materials database\n\n" +
                   "Type 'help' anytime for this list!";
        }

        private string GetDefaultResponse(string input)
        {
            return $"I understood you said: \"{input}\"\n\n" +
                   "I'm still learning! Here are some things you can try:\n" +
                   "• \"Count walls in the model\"\n" +
                   "• \"List all rooms\"\n" +
                   "• \"Show parameter info\"\n" +
                   "• \"Help\" for more options";
        }

        private string ProcessWallIntent(string input)
        {
            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return "Please open a Revit document first.";

                var walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType()
                    .ToElements();

                if (input.ToLower().Contains("count"))
                {
                    return $"Found {walls.Count} walls in the current model.\n\n" +
                           $"Wall types in use:\n" +
                           string.Join("\n", walls
                               .GroupBy(w => w.Name)
                               .Select(g => $"• {g.Key}: {g.Count()}"));
                }

                return $"There are {walls.Count} walls in the model.\n\n" +
                       "You can ask me to:\n" +
                       "• \"Count walls\" - Get wall count\n" +
                       "• \"List wall types\" - See wall types";
            }
            catch (Exception ex)
            {
                return $"Error accessing walls: {ex.Message}";
            }
        }

        private string ProcessDoorIntent(string input)
        {
            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return "Please open a Revit document first.";

                var doors = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .ToElements();

                return $"Found {doors.Count} doors in the current model.\n\n" +
                       "Door families in use:\n" +
                       string.Join("\n", doors
                           .GroupBy(d => d.Name)
                           .Take(10)
                           .Select(g => $"• {g.Key}: {g.Count()}"));
            }
            catch (Exception ex)
            {
                return $"Error accessing doors: {ex.Message}";
            }
        }

        private string ProcessWindowIntent(string input)
        {
            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return "Please open a Revit document first.";

                var windows = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .WhereElementIsNotElementType()
                    .ToElements();

                return $"Found {windows.Count} windows in the current model.\n\n" +
                       "Window types in use:\n" +
                       string.Join("\n", windows
                           .GroupBy(w => w.Name)
                           .Take(10)
                           .Select(g => $"• {g.Key}: {g.Count()}"));
            }
            catch (Exception ex)
            {
                return $"Error accessing windows: {ex.Message}";
            }
        }

        private string ProcessRoomIntent(string input)
        {
            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return "Please open a Revit document first.";

                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .ToList();

                if (rooms.Count == 0)
                    return "No rooms found in the model. Rooms need to be placed and bounded.";

                var totalArea = rooms.Sum(r =>
                {
                    var param = r.get_Parameter(BuiltInParameter.ROOM_AREA);
                    return param?.AsDouble() ?? 0;
                });

                // Convert from sq ft to sq m (Revit internal units)
                var areaSqM = totalArea * 0.092903;

                return $"Found {rooms.Count} rooms in the model.\n\n" +
                       $"Total area: {areaSqM:F2} m² ({totalArea:F2} sq ft)\n\n" +
                       "Rooms:\n" +
                       string.Join("\n", rooms
                           .Take(10)
                           .Select(r => $"• {r.Name}"));
            }
            catch (Exception ex)
            {
                return $"Error accessing rooms: {ex.Message}";
            }
        }

        private string ProcessFloorIntent(string input)
        {
            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return "Please open a Revit document first.";

                var floors = new FilteredElementCollector(doc)
                    .OfClass(typeof(Floor))
                    .WhereElementIsNotElementType()
                    .ToElements();

                return $"Found {floors.Count} floors in the current model.\n\n" +
                       "Floor types:\n" +
                       string.Join("\n", floors
                           .GroupBy(f => f.Name)
                           .Take(10)
                           .Select(g => $"• {g.Key}: {g.Count()}"));
            }
            catch (Exception ex)
            {
                return $"Error accessing floors: {ex.Message}";
            }
        }

        private string ProcessParameterIntent(string input)
        {
            return "StingBIM Parameter System:\n\n" +
                   "📊 818 ISO 19650-compliant parameters available\n\n" +
                   "Categories:\n" +
                   "• Identity Data (MR_ID_*)\n" +
                   "• Dimensions (MR_DIM_*)\n" +
                   "• Materials (MR_MAT_*)\n" +
                   "• Performance (MR_PERF_*)\n" +
                   "• Cost (MR_COST_*)\n" +
                   "• Sustainability (MR_SUST_*)\n\n" +
                   "Parameters are stored in:\n" +
                   "data/parameters/MR_PARAMETERS.txt";
        }

        private string ProcessScheduleIntent(string input)
        {
            return "StingBIM Schedule Templates:\n\n" +
                   "📋 146 professional templates available\n\n" +
                   "Categories:\n" +
                   "• ARCH_* - Architectural (doors, windows, rooms)\n" +
                   "• STR_* - Structural (columns, beams, foundations)\n" +
                   "• MEP_* - Mechanical/Electrical/Plumbing\n" +
                   "• FM_* - Facilities Management\n\n" +
                   "Templates are in: data/schedules/";
        }

        private string ProcessMaterialIntent(string input)
        {
            return "StingBIM Materials Database:\n\n" +
                   "🎨 2,450+ materials with full properties\n\n" +
                   "Data includes:\n" +
                   "• Thermal conductivity (W/m·K)\n" +
                   "• Density (kg/m³)\n" +
                   "• Cost per unit (regional pricing)\n" +
                   "• Sustainability ratings\n" +
                   "• Fire ratings\n\n" +
                   "Sources:\n" +
                   "• BLE_MATERIALS.xlsx (Building/Landscape/Electrical)\n" +
                   "• MEP_MATERIALS.xlsx (Mechanical/Plumbing)";
        }

        private string ProcessElementIntent(string input)
        {
            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return "Please open a Revit document first.";

                var selection = _uiApp.ActiveUIDocument.Selection.GetElementIds();
                if (selection.Count == 0)
                    return "No elements selected. Please select an element in Revit first.";

                var element = doc.GetElement(selection.First());
                var category = element.Category?.Name ?? "Unknown";
                var family = (element as FamilyInstance)?.Symbol?.Family?.Name ?? "N/A";

                return $"Selected Element Info:\n\n" +
                       $"• Name: {element.Name}\n" +
                       $"• Category: {category}\n" +
                       $"• Family: {family}\n" +
                       $"• ID: {element.Id.IntegerValue}\n" +
                       $"• Type: {element.GetType().Name}";
            }
            catch (Exception ex)
            {
                return $"Error getting element info: {ex.Message}";
            }
        }

        private string ProcessSelectionIntent(string input)
        {
            return ProcessElementIntent(input);
        }

        private string ProcessCountIntent(string input)
        {
            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return "Please open a Revit document first.";

                var categories = new Dictionary<string, BuiltInCategory>
                {
                    { "wall", BuiltInCategory.OST_Walls },
                    { "door", BuiltInCategory.OST_Doors },
                    { "window", BuiltInCategory.OST_Windows },
                    { "room", BuiltInCategory.OST_Rooms },
                    { "floor", BuiltInCategory.OST_Floors },
                    { "ceiling", BuiltInCategory.OST_Ceilings },
                    { "column", BuiltInCategory.OST_Columns },
                    { "beam", BuiltInCategory.OST_StructuralFraming },
                };

                var lowerInput = input.ToLowerInvariant();
                var results = new List<string>();

                foreach (var cat in categories)
                {
                    if (lowerInput.Contains(cat.Key) || lowerInput.Contains("all"))
                    {
                        var count = new FilteredElementCollector(doc)
                            .OfCategory(cat.Value)
                            .WhereElementIsNotElementType()
                            .GetElementCount();
                        results.Add($"• {cat.Key}s: {count}");
                    }
                }

                if (results.Count == 0)
                {
                    // Count all major categories
                    foreach (var cat in categories)
                    {
                        var count = new FilteredElementCollector(doc)
                            .OfCategory(cat.Value)
                            .WhereElementIsNotElementType()
                            .GetElementCount();
                        if (count > 0)
                            results.Add($"• {cat.Key}s: {count}");
                    }
                }

                return "Element Counts:\n\n" + string.Join("\n", results);
            }
            catch (Exception ex)
            {
                return $"Error counting elements: {ex.Message}";
            }
        }

        private string ProcessListIntent(string input)
        {
            return ProcessCountIntent(input);
        }

        private string ProcessAreaIntent(string input)
        {
            return ProcessRoomIntent(input);
        }

        private string ProcessVolumeIntent(string input)
        {
            return ProcessRoomIntent(input);
        }

        #region Event Handlers

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendMessage();
                e.Handled = true;
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string query)
            {
                InputTextBox.Text = query;
                SendMessage();
            }
        }

        #endregion
    }

    public class ChatMessage
    {
        public string Text { get; set; }
        public bool IsUser { get; set; }
        public HorizontalAlignment Alignment { get; set; }
    }
}
