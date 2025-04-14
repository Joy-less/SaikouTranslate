using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Godot;
using GTranslate;
using GTranslate.Translators;
using ZiggyCreatures.Caching.Fusion;

[SupportedOSPlatform("Windows")]
public partial class WindowTranslator : Node {
    [Export] public OptionButton SourceLanguageModelDropdown { get; set; }
    [Export] public SpinBox CaptureIntervalSlider { get; set; }
    [Export] public OptionButton TranslationServiceDropdown { get; set; }
    [Export] public OptionButton SourceLanguageDropdown { get; set; }
    [Export] public OptionButton TargetLanguageDropdown { get; set; }
    [Export] public Button CustomFontButton { get; set; }
    [Export] public Button ToggleButton { get; set; }
    [Export] public Button ResetButton { get; set; }
    [Export] public RichTextLabel InformationLabel { get; set; }
    [Export] public FileDialog CustomFontFileDialog { get; set; }
    [Export] public Theme MainTheme { get; set; }

    public readonly Dictionary<string, ITranslator?> Translators = new() {
        ["Disabled"] = null,
        ["Any"] = new AggregateTranslator(),
        ["Google"] = new GoogleTranslator(),
        ["Bing"] = new BingTranslator(),
        ["Yandex"] = new YandexTranslator(),
        ["Microsoft"] = new MicrosoftTranslator(),
    };

    private readonly OCRUtility OCRUtility = new();
    private readonly FusionCache TranslationCache = new(new FusionCacheOptions());
    private ITranslator? Translator;
    private Window? OverlayWindow;
    private bool Started;
    private double TimeUntilCapture;
    private bool Capturing;
    private long RecognisedCount;
    private long TranslatedCount;

    public override void _Ready() {
        // Setup controls
        FillSourceLanguageModelDropdown();
        FillTranslationServiceDropdown();
        FillSourceLanguageDropdown();
        FillTargetLanguageDropdown();
        SetTranslationService();

        // Connect events
        TranslationServiceDropdown.ItemSelected += _ => SetTranslationService();
        CustomFontButton.Pressed += PromptSelectCustomFont;
        ToggleButton.Pressed += Toggle;
        ResetButton.Pressed += Reset;
        CustomFontFileDialog.FileSelected += SetCustomFont;
    }
    public override void _Process(double Delta) {
        // Log translator information
        InformationLabel.Text = $"""
            Recognised: {RecognisedCount}
            Translated: {TranslatedCount}
            """;
        
        // Capture screen every interval
        if (Started && !Capturing) {
            TimeUntilCapture -= Delta;
            if (TimeUntilCapture <= 0) {
                TimeUntilCapture = CaptureIntervalSlider.Value;
                _ = CaptureAsync();
            }
        }
    }
    public void SpawnOverlayWindow() {
        // Create overlay window
        OverlayWindow = new Window() {
            AlwaysOnTop = true,
            Transparent = true,
            TransparentBg = true,
            Borderless = true,
            MousePassthrough = true,
            Unresizable = true,
            Unfocusable = true,
            ContentScaleSize = Vector2I.Zero,
            Size = DisplayServer.ScreenGetSize(),
        };
        // Spawn overlay window
        GetTree().Root.AddChild(OverlayWindow);
        // Make overlay window unclickable
        WindowPassthroughUtility.SetWindowPassthrough(OverlayWindow, false);
    }
    public void DestroyOverlayWindow() {
        OverlayWindow?.QueueFree();
        OverlayWindow = null;
    }

    private static void FillDropdown(OptionButton Dropdown, IEnumerable<string> Items, string? Default = null) {
        Dropdown.Clear();

        foreach (string Item in Items) {
            Dropdown.AddItem(Item);

            if (Default == Item) {
                Dropdown.Select(Dropdown.ItemCount - 1);
            }
        }
    }
    private void FillSourceLanguageModelDropdown() {
        FillDropdown(SourceLanguageModelDropdown, OCRUtility.GetAvailableLanguages());
    }
    private void FillSourceLanguageDropdown() {
        FillDropdown(SourceLanguageDropdown, [.. Language.LanguageDictionary.Values.Select(Language => Language.Name)], "Japanese");
    }
    private void FillTargetLanguageDropdown() {
        FillDropdown(TargetLanguageDropdown, [.. Language.LanguageDictionary.Values.Select(Language => Language.Name)], "English");
    }
    private void FillTranslationServiceDropdown() {
        // Fill dropdown with translation services
        TranslationServiceDropdown.Clear();
        foreach (string TranslationService in Translators.Keys) {
            TranslationServiceDropdown.AddItem(TranslationService);
        }
    }
    private void SetTranslationService() {
        // Get chosen translation service
        string TranslationService = TranslationServiceDropdown.GetItemText(TranslationServiceDropdown.Selected);
        // Set translator
        Translator = Translators[TranslationService];
        // Enable source/target language dropdowns when translation enabled
        SourceLanguageDropdown.Disabled = Translator is null;
        TargetLanguageDropdown.Disabled = Translator is null;
    }
    private void PromptSelectCustomFont() {
        CustomFontFileDialog.Show();
    }
    private void Toggle() {
        // Toggle started flag
        Started = !Started;
        // Change toggle button text
        ToggleButton.Text = Started ? "Stop" : "Start";
        // Disable configurations while capturing
        SourceLanguageModelDropdown.Disabled = Started;
        CaptureIntervalSlider.Editable = !Started;
        SourceLanguageDropdown.Disabled = Started || Translator is null;
        TargetLanguageDropdown.Disabled = Started || Translator is null;
        TranslationServiceDropdown.Disabled = Started;
        CustomFontButton.Disabled = Started;
        // Destroy existing overlay window
        DestroyOverlayWindow();
        // Spawn overlay window
        if (Started) {
            SpawnOverlayWindow();
        }
    }
    private void Reset() {
        // Destroy overlay window
        DestroyOverlayWindow();
        // Reload scene
        GetTree().ReloadCurrentScene();
    }
    private void SetCustomFont(string FilePath) {
        // Show path
        CustomFontButton.Text = FilePath;
        // Load font from path
        FontFile Font = new();
        Font.LoadDynamicFont(FilePath);
        // Set default font
        MainTheme.DefaultFont = Font;
    }
    private void ClearOverlayWindow() {
        // Prevent current capture from overlaying
        Capturing = false;
        // Clear overlays
        if (OverlayWindow is not null) {
            foreach (Node Overlay in OverlayWindow.GetChildren()) {
                Overlay.QueueFree();
            }
        }
    }
    private async Task CaptureAsync() {
        if (Capturing) return;
        Capturing = true;

        try {
            // Get handle of foreground window
            nint ForegroundWindowHandle = CaptureUtility.GetForegroundWindowHandle();
            // Get chosen source language model for OCR
            string SourceLanguageModel = SourceLanguageModelDropdown.GetItemText(SourceLanguageModelDropdown.Selected);
            // Recognise page from foreground window
            using TesseractOCR.Page Page = await Task.Run(() => OCRUtility.Recognise(ForegroundWindowHandle, SourceLanguageModel));
            // Recognise paragraphs from page
            IEnumerable<TesseractOCR.Layout.Paragraph> Paragraphs = await Task.Run(() => Page.Layout.SelectMany(Block => Block.Paragraphs));

            // Get rect of foreground window
            Rect2I ForegroundWindowRect = CaptureUtility.GetWindowRect2I(ForegroundWindowHandle);

            // Create labels for each recognition
            List<Label> Overlays = [];
            await Task.Run(() => {
                foreach (TesseractOCR.Layout.Paragraph Paragraph in Paragraphs) {
                    // Ensure bounds available
                    if (Paragraph.BoundingBox is not TesseractOCR.Rect BlockRect) {
                        continue;
                    }
                    // Create overlay label
                    Label Overlay = new() {
                        Text = Paragraph.Text,
                        Position = new Vector2(BlockRect.X1, BlockRect.Y1) + ForegroundWindowRect.Position,
                        Size = new Vector2(BlockRect.Width, BlockRect.Height),
                    };
                    RecognisedCount += Overlay.Text.Length;
                    // Style overlay label
                    Overlay.AddThemeColorOverride(StringNames.FontOutlineColor, new Color(0, 0, 0));
                    Overlay.AddThemeConstantOverride(StringNames.OutlineSize, 10);
                    Overlay.AddThemeFontSizeOverride(StringNames.FontSize, Paragraph.FontProperties.PointSize);
                    // Add overlay label
                    Overlays.Add(Overlay);

                    // Ensure this is still the latest capture
                    if (!Capturing) return;
                }
            });

            // Get chosen source and target languages
            string SourceLanguage = SourceLanguageDropdown.GetItemText(SourceLanguageDropdown.Selected);
            string TargetLanguage = TargetLanguageDropdown.GetItemText(TargetLanguageDropdown.Selected);
            // Translate each label
            if (Translator is not null) {
                List<Task> TranslateTasks = [];
                foreach (Label Overlay in Overlays) {
                    // Translate label
                    async Task TranslateLabelAsync() {
                        // Try get translation from cache
                        string Translation = await TranslationCache.GetOrSetAsync(Overlay.Text, async CancelToken => {
                            // Otherwise, request translation
                            TranslatedCount += Overlay.Text.Length;
                            return (await Translator.TranslateAsync(Overlay.Text, TargetLanguage, SourceLanguage)).Translation;
                        });
                        // Set text to translation
                        Overlay.Text = Translation;
                    }
                    TranslateTasks.Add(TranslateLabelAsync());
                }
                // Wait for all translations to complete
                await Task.WhenAll(TranslateTasks);
            }

            // Ensure this is still the latest capture
            if (!Capturing) return;

            // Clear existing overlays
            ClearOverlayWindow();
            // Overlay each label
            foreach (Label Overlay in Overlays) {
                OverlayWindow?.AddChild(Overlay);
            }
        }
        catch (Exception Ex) {
            GD.PushError(Ex);
        }
        finally {
            Capturing = false;
        }
    }
}