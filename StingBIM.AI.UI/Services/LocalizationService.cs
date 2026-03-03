// StingBIM.AI.UI.Services.LocalizationService
// Multi-language support for UI text localization
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.UI.Services
{
    /// <summary>
    /// Service for managing UI localization and multi-language support.
    /// </summary>
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<LocalizationService> _instance =
            new Lazy<LocalizationService>(() => new LocalizationService());

        public static LocalizationService Instance => _instance.Value;

        private Dictionary<string, Dictionary<string, string>> _translations;
        private string _currentLanguage;
        private readonly string _localizationPath;

        /// <summary>
        /// Event fired when the language changes.
        /// </summary>
        public event EventHandler<string> LanguageChanged;

        /// <summary>
        /// PropertyChanged event for data binding support.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the currently active language code.
        /// </summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            private set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged();
                    LanguageChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Gets all available languages.
        /// </summary>
        public List<LanguageInfo> AvailableLanguages { get; private set; }

        private LocalizationService()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>();
            AvailableLanguages = new List<LanguageInfo>();
            _localizationPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "Localization");

            LoadDefaultTranslations();
            LoadCustomTranslations();

            // Set default language based on system culture
            var systemCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _currentLanguage = _translations.ContainsKey(systemCulture) ? systemCulture : "en";
        }

        #region Public Methods

        /// <summary>
        /// Gets a localized string by key.
        /// </summary>
        public string GetString(string key, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue ?? string.Empty;

            // Try current language
            if (_translations.TryGetValue(_currentLanguage, out var langDict))
            {
                if (langDict.TryGetValue(key, out var value))
                    return value;
            }

            // Fallback to English
            if (_currentLanguage != "en" && _translations.TryGetValue("en", out var enDict))
            {
                if (enDict.TryGetValue(key, out var value))
                    return value;
            }

            // Return default or key itself
            return defaultValue ?? key;
        }

        /// <summary>
        /// Gets a localized string with format parameters.
        /// </summary>
        public string GetFormattedString(string key, params object[] args)
        {
            var template = GetString(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        /// <summary>
        /// Indexer for easy access to localized strings.
        /// </summary>
        public string this[string key] => GetString(key);

        /// <summary>
        /// Changes the current language.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return;

            if (!_translations.ContainsKey(languageCode))
            {
                Logger.Warn($"Language '{languageCode}' not available, using English");
                languageCode = "en";
            }

            CurrentLanguage = languageCode;
            Logger.Info($"Language changed to: {languageCode}");
        }

        /// <summary>
        /// Adds or updates a translation.
        /// </summary>
        public void SetTranslation(string languageCode, string key, string value)
        {
            if (!_translations.ContainsKey(languageCode))
            {
                _translations[languageCode] = new Dictionary<string, string>();
            }
            _translations[languageCode][key] = value;
        }

        /// <summary>
        /// Loads translations from a JSON file.
        /// </summary>
        public void LoadTranslationsFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Warn($"Translation file not found: {filePath}");
                    return;
                }

                var json = File.ReadAllText(filePath);
                var translations = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);

                foreach (var lang in translations)
                {
                    if (!_translations.ContainsKey(lang.Key))
                    {
                        _translations[lang.Key] = new Dictionary<string, string>();
                    }

                    foreach (var kvp in lang.Value)
                    {
                        _translations[lang.Key][kvp.Key] = kvp.Value;
                    }
                }

                UpdateAvailableLanguages();
                Logger.Info($"Loaded translations from: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load translations from: {filePath}");
            }
        }

        /// <summary>
        /// Exports all translations to a JSON file.
        /// </summary>
        public void ExportTranslations(string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_translations, Formatting.Indented);
                File.WriteAllText(filePath, json);
                Logger.Info($"Exported translations to: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to export translations to: {filePath}");
            }
        }

        #endregion

        #region Private Methods

        private void LoadDefaultTranslations()
        {
            // English (default)
            _translations["en"] = new Dictionary<string, string>
            {
                // General
                ["app.title"] = "StingBIM AI Assistant",
                ["app.version"] = "Version {0}",

                // Chat Panel
                ["chat.placeholder"] = "Type a message or command...",
                ["chat.send"] = "Send",
                ["chat.clear"] = "Clear conversation",
                ["chat.thinking"] = "Thinking...",
                ["chat.processing"] = "Processing your request",
                ["chat.analyzing"] = "Analyzing the model",
                ["chat.generating"] = "Generating response",

                // Voice
                ["voice.start"] = "Start voice input",
                ["voice.stop"] = "Stop voice input",
                ["voice.listening"] = "Listening...",
                ["voice.processing"] = "Processing speech...",

                // Actions
                ["action.copy"] = "Copy",
                ["action.retry"] = "Retry",
                ["action.cancel"] = "Cancel",
                ["action.close"] = "Close",
                ["action.save"] = "Save",
                ["action.export"] = "Export",
                ["action.import"] = "Import",
                ["action.delete"] = "Delete",
                ["action.edit"] = "Edit",
                ["action.undo"] = "Undo",
                ["action.redo"] = "Redo",

                // Quick Actions
                ["quick.title"] = "Quick Actions",
                ["quick.create"] = "Create",
                ["quick.analyze"] = "Analyze",
                ["quick.schedule"] = "Schedule",
                ["quick.parameters"] = "Parameters",
                ["quick.compliance"] = "Compliance",
                ["quick.help"] = "Help",

                // Context Sidebar
                ["context.connection"] = "Connection Status",
                ["context.connected"] = "Connected to Revit",
                ["context.disconnected"] = "Not connected",
                ["context.activeView"] = "Active View",
                ["context.selection"] = "Selection",
                ["context.noSelection"] = "No selection",
                ["context.elements"] = "{0} element(s) selected",
                ["context.statistics"] = "Model Statistics",
                ["context.recentCommands"] = "Recent Commands",
                ["context.undoHistory"] = "Undo History",

                // Notifications
                ["notify.success"] = "Success",
                ["notify.error"] = "Error",
                ["notify.warning"] = "Warning",
                ["notify.info"] = "Information",

                // Settings
                ["settings.title"] = "Settings",
                ["settings.language"] = "Language",
                ["settings.theme"] = "Theme",
                ["settings.voice"] = "Voice Settings",
                ["settings.advanced"] = "Advanced",

                // Onboarding
                ["tour.welcome.title"] = "Welcome to StingBIM AI",
                ["tour.welcome.desc"] = "Let's take a quick tour of the interface.",
                ["tour.input.title"] = "Chat Input",
                ["tour.input.desc"] = "Type your commands and questions here.",
                ["tour.voice.title"] = "Voice Commands",
                ["tour.voice.desc"] = "Click the microphone to use voice commands.",
                ["tour.skip"] = "Skip tour",
                ["tour.next"] = "Next",
                ["tour.back"] = "Back",
                ["tour.finish"] = "Finish",

                // Favorites
                ["favorites.title"] = "Favorites",
                ["favorites.add"] = "Add to favorites",
                ["favorites.empty"] = "No favorites yet",

                // Search
                ["search.placeholder"] = "Search messages...",
                ["search.results"] = "{0} of {1} results",
                ["search.noResults"] = "No results found",

                // Export
                ["export.title"] = "Export Conversation",
                ["export.html"] = "HTML Document",
                ["export.text"] = "Plain Text",
                ["export.markdown"] = "Markdown",
                ["export.json"] = "JSON Data",

                // Errors
                ["error.connection"] = "Connection failed",
                ["error.timeout"] = "Request timed out",
                ["error.unknown"] = "An unknown error occurred",

                // Element Types
                ["element.wall"] = "Wall",
                ["element.door"] = "Door",
                ["element.window"] = "Window",
                ["element.floor"] = "Floor",
                ["element.ceiling"] = "Ceiling",
                ["element.roof"] = "Roof",
                ["element.room"] = "Room",
                ["element.stair"] = "Stair",
                ["element.column"] = "Column",
                ["element.beam"] = "Beam"
            };

            // French
            _translations["fr"] = new Dictionary<string, string>
            {
                ["app.title"] = "Assistant IA StingBIM",
                ["chat.placeholder"] = "Tapez un message ou une commande...",
                ["chat.send"] = "Envoyer",
                ["chat.clear"] = "Effacer la conversation",
                ["chat.thinking"] = "Réflexion...",
                ["voice.start"] = "Démarrer la saisie vocale",
                ["voice.stop"] = "Arrêter la saisie vocale",
                ["voice.listening"] = "Écoute...",
                ["action.copy"] = "Copier",
                ["action.retry"] = "Réessayer",
                ["action.cancel"] = "Annuler",
                ["action.close"] = "Fermer",
                ["action.save"] = "Enregistrer",
                ["quick.title"] = "Actions rapides",
                ["context.connected"] = "Connecté à Revit",
                ["context.disconnected"] = "Non connecté",
                ["settings.title"] = "Paramètres",
                ["settings.language"] = "Langue",
                ["tour.welcome.title"] = "Bienvenue dans StingBIM AI",
                ["favorites.title"] = "Favoris",
                ["search.placeholder"] = "Rechercher des messages...",
                ["element.wall"] = "Mur",
                ["element.door"] = "Porte",
                ["element.window"] = "Fenêtre",
                ["element.floor"] = "Plancher",
                ["element.room"] = "Pièce"
            };

            // German
            _translations["de"] = new Dictionary<string, string>
            {
                ["app.title"] = "StingBIM KI-Assistent",
                ["chat.placeholder"] = "Nachricht oder Befehl eingeben...",
                ["chat.send"] = "Senden",
                ["chat.clear"] = "Gespräch löschen",
                ["chat.thinking"] = "Denke nach...",
                ["voice.start"] = "Spracheingabe starten",
                ["voice.stop"] = "Spracheingabe beenden",
                ["voice.listening"] = "Höre zu...",
                ["action.copy"] = "Kopieren",
                ["action.retry"] = "Wiederholen",
                ["action.cancel"] = "Abbrechen",
                ["action.close"] = "Schließen",
                ["action.save"] = "Speichern",
                ["quick.title"] = "Schnellaktionen",
                ["context.connected"] = "Mit Revit verbunden",
                ["context.disconnected"] = "Nicht verbunden",
                ["settings.title"] = "Einstellungen",
                ["settings.language"] = "Sprache",
                ["element.wall"] = "Wand",
                ["element.door"] = "Tür",
                ["element.window"] = "Fenster",
                ["element.floor"] = "Boden",
                ["element.room"] = "Raum"
            };

            // Spanish
            _translations["es"] = new Dictionary<string, string>
            {
                ["app.title"] = "Asistente IA StingBIM",
                ["chat.placeholder"] = "Escribe un mensaje o comando...",
                ["chat.send"] = "Enviar",
                ["chat.clear"] = "Borrar conversación",
                ["chat.thinking"] = "Pensando...",
                ["voice.start"] = "Iniciar entrada de voz",
                ["voice.stop"] = "Detener entrada de voz",
                ["voice.listening"] = "Escuchando...",
                ["action.copy"] = "Copiar",
                ["action.retry"] = "Reintentar",
                ["action.cancel"] = "Cancelar",
                ["action.close"] = "Cerrar",
                ["action.save"] = "Guardar",
                ["quick.title"] = "Acciones rápidas",
                ["context.connected"] = "Conectado a Revit",
                ["context.disconnected"] = "No conectado",
                ["settings.title"] = "Configuración",
                ["settings.language"] = "Idioma",
                ["element.wall"] = "Muro",
                ["element.door"] = "Puerta",
                ["element.window"] = "Ventana",
                ["element.floor"] = "Piso",
                ["element.room"] = "Habitación"
            };

            // Swahili (for East African support)
            _translations["sw"] = new Dictionary<string, string>
            {
                ["app.title"] = "Msaidizi wa AI wa StingBIM",
                ["chat.placeholder"] = "Andika ujumbe au amri...",
                ["chat.send"] = "Tuma",
                ["chat.clear"] = "Futa mazungumzo",
                ["chat.thinking"] = "Inafikiria...",
                ["voice.start"] = "Anza kuongea",
                ["voice.stop"] = "Acha kuongea",
                ["voice.listening"] = "Inasikiliza...",
                ["action.copy"] = "Nakili",
                ["action.cancel"] = "Ghairi",
                ["action.close"] = "Funga",
                ["action.save"] = "Hifadhi",
                ["quick.title"] = "Vitendo vya haraka",
                ["context.connected"] = "Imeunganishwa na Revit",
                ["context.disconnected"] = "Haijaunganishwa",
                ["settings.title"] = "Mipangilio",
                ["settings.language"] = "Lugha",
                ["element.wall"] = "Ukuta",
                ["element.door"] = "Mlango",
                ["element.window"] = "Dirisha",
                ["element.floor"] = "Sakafu",
                ["element.room"] = "Chumba"
            };

            // Arabic
            _translations["ar"] = new Dictionary<string, string>
            {
                ["app.title"] = "مساعد StingBIM الذكي",
                ["chat.placeholder"] = "اكتب رسالة أو أمر...",
                ["chat.send"] = "إرسال",
                ["chat.clear"] = "مسح المحادثة",
                ["chat.thinking"] = "جاري التفكير...",
                ["voice.start"] = "بدء الإدخال الصوتي",
                ["voice.stop"] = "إيقاف الإدخال الصوتي",
                ["voice.listening"] = "جاري الاستماع...",
                ["action.copy"] = "نسخ",
                ["action.cancel"] = "إلغاء",
                ["action.close"] = "إغلاق",
                ["action.save"] = "حفظ",
                ["settings.title"] = "الإعدادات",
                ["settings.language"] = "اللغة",
                ["element.wall"] = "جدار",
                ["element.door"] = "باب",
                ["element.window"] = "نافذة",
                ["element.floor"] = "أرضية",
                ["element.room"] = "غرفة"
            };

            // Chinese (Simplified)
            _translations["zh"] = new Dictionary<string, string>
            {
                ["app.title"] = "StingBIM AI 助手",
                ["chat.placeholder"] = "输入消息或命令...",
                ["chat.send"] = "发送",
                ["chat.clear"] = "清除对话",
                ["chat.thinking"] = "思考中...",
                ["voice.start"] = "开始语音输入",
                ["voice.stop"] = "停止语音输入",
                ["voice.listening"] = "正在聆听...",
                ["action.copy"] = "复制",
                ["action.cancel"] = "取消",
                ["action.close"] = "关闭",
                ["action.save"] = "保存",
                ["settings.title"] = "设置",
                ["settings.language"] = "语言",
                ["element.wall"] = "墙",
                ["element.door"] = "门",
                ["element.window"] = "窗",
                ["element.floor"] = "楼板",
                ["element.room"] = "房间"
            };

            UpdateAvailableLanguages();
        }

        private void LoadCustomTranslations()
        {
            try
            {
                if (!Directory.Exists(_localizationPath))
                    return;

                var files = Directory.GetFiles(_localizationPath, "*.json");
                foreach (var file in files)
                {
                    LoadTranslationsFromFile(file);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load custom translations");
            }
        }

        private void UpdateAvailableLanguages()
        {
            AvailableLanguages = _translations.Keys.Select(code => new LanguageInfo
            {
                Code = code,
                Name = GetLanguageName(code),
                NativeName = GetLanguageNativeName(code)
            }).OrderBy(l => l.Name).ToList();
        }

        private static string GetLanguageName(string code)
        {
            return code switch
            {
                "en" => "English",
                "fr" => "French",
                "de" => "German",
                "es" => "Spanish",
                "sw" => "Swahili",
                "ar" => "Arabic",
                "zh" => "Chinese (Simplified)",
                _ => code
            };
        }

        private static string GetLanguageNativeName(string code)
        {
            return code switch
            {
                "en" => "English",
                "fr" => "Français",
                "de" => "Deutsch",
                "es" => "Español",
                "sw" => "Kiswahili",
                "ar" => "العربية",
                "zh" => "简体中文",
                _ => code
            };
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Information about an available language.
    /// </summary>
    public class LanguageInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string NativeName { get; set; }

        public string DisplayName => $"{Name} ({NativeName})";
    }

    /// <summary>
    /// Markup extension for easy localization in XAML.
    /// </summary>
    public class LocalizeExtension : System.Windows.Markup.MarkupExtension
    {
        public string Key { get; set; }
        public string Default { get; set; }

        public LocalizeExtension() { }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return LocalizationService.Instance.GetString(Key, Default);
        }
    }
}
