using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GTranslate;
using GTranslate.Translators;
using ZiggyCreatures.Caching.Fusion;

#pragma warning disable CA1416

// Download Tesseract model from https://github.com/tesseract-ocr/tessdata

public partial class WindowTranslator : Node {
    [Export] OptionButton SourceLanguageModelDropdown;
    [Export] SpinBox CaptureIntervalSlider;
    [Export] OptionButton TranslationServiceDropdown;
    [Export] OptionButton SourceLanguageDropdown;
    [Export] OptionButton TargetLanguageDropdown;
    [Export] Button CustomFontButton;
    [Export] Button ToggleButton;
    [Export] Button ResetButton;
    [Export] RichTextLabel InformationLabel;
    [Export] FileDialog CustomFontFileDialog;
    [Export] Theme MainTheme;

    private readonly OCRUtility OCRUtility = new();
    private readonly FusionCache TranslationCache = new(new FusionCacheOptions());
    private ITranslator Translator;
    private Window OverlayWindow;
    private bool Capturing;
    private long RecognisedCharacters;
    private long TranslatedCharacters;

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

        // Start capturing when started
        _ = CaptureLoopAsync();
    }
    public override void _Process(double Delta) {
        // Log translator information
        InformationLabel.Text = $"Recognised: {RecognisedCharacters}" + "\n" + $"Translated: {TranslatedCharacters}";
    }
    public void SpawnOverlayWindow() {
        // Create overlay window
        OverlayWindow = new Window() {
            AlwaysOnTop = true,
            Transparent = true,
            Borderless = true,
            MousePassthrough = true,
            Unresizable = true,
            Unfocusable = true,
            ContentScaleSize = Vector2I.Zero,
        };
        // Spawn overlay window
        GetTree().Root.AddChild(OverlayWindow);
        // Make overlay window unclickable
        WindowPassthroughUtility.SetWindowPassthrough(OverlayWindow, false);
    }
    public async Task CaptureLoopAsync(CancellationToken CancelToken = default) {
        while (true) {
            // Wait for next capture
            await Task.Delay(TimeSpan.FromSeconds(CaptureIntervalSlider.Value), CancelToken);
            // Try capture active window
            if (Capturing) {
                try {
                    await CaptureAsync();
                }
                catch (Exception Ex) {
                    GD.PushError(Ex);
                }
            }
        }
    }

    private static void FillDropdown(OptionButton Dropdown, IList<string> Items, string Default = null) {
        Dropdown.Clear();
        foreach (string Item in Items) {
            Dropdown.AddItem(Item);
        }
        if (Default is not null) {
            Dropdown.Selected = Items.IndexOf(Default);
        }
    }
    private void FillSourceLanguageModelDropdown() {
        FillDropdown(SourceLanguageModelDropdown, OCRUtility.GetAvailableLanguages());
    }
    private void FillSourceLanguageDropdown() {
        FillDropdown(SourceLanguageDropdown, Language.LanguageDictionary.Values.Select(Language => Language.Name).ToArray(), "Japanese");
    }
    private void FillTargetLanguageDropdown() {
        FillDropdown(TargetLanguageDropdown, Language.LanguageDictionary.Values.Select(Language => Language.Name).ToArray(), "English");
    }
    private void FillTranslationServiceDropdown() {
        // Get translation services
        List<string> TranslationServices = ["Disabled", "Any"];
        foreach (TranslationServices TranslationService in Enum.GetValues<TranslationServices>()) {
            TranslationServices.Add(TranslationService.ToString());
        }
        // Fill dropdown with translation services
        TranslationServiceDropdown.Clear();
        foreach (string TranslationService in TranslationServices) {
            TranslationServiceDropdown.AddItem(TranslationService);
        }
    }
    private void SetTranslationService() {
        // Get chosen translation service
        string TranslationService = TranslationServiceDropdown.GetItemText(TranslationServiceDropdown.Selected);
        // Set translator
        Translator = TranslationService switch {
            "Any" => new AggregateTranslator(),
            "Google" => new GoogleTranslator(),
            "Bing" => new BingTranslator(),
            "Yandex" => new YandexTranslator(),
            "Microsoft" => new MicrosoftTranslator(),
            _ => null
        };
        // Enable source/target language dropdowns when translation enabled
        SourceLanguageDropdown.Disabled = Translator is null;
        TargetLanguageDropdown.Disabled = Translator is null;
    }
    private void PromptSelectCustomFont() {
        CustomFontFileDialog.Show();
    }
    private void Toggle() {
        // Toggle capturing flag
        Capturing = !Capturing;
        // Change toggle button text
        ToggleButton.Text = Capturing ? "Stop" : "Start";
        // Disable configurations while capturing
        SourceLanguageModelDropdown.Disabled = Capturing;
        TargetLanguageDropdown.Disabled = Capturing;
        TranslationServiceDropdown.Disabled = Capturing;
        CaptureIntervalSlider.Editable = !Capturing;
        // Destroy overlay window
        OverlayWindow?.QueueFree();
        OverlayWindow = null;
        // Spawn overlay window
        if (Capturing) {
            SpawnOverlayWindow();
        }
    }
    private void Reset() {
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
    private async Task CaptureAsync() {
        // Get handle for active window
        nint ActiveWindowHandle = CaptureUtility.GetActiveWindowHandle();
        // Get chosen source language model for OCR
        string SourceLanguageModel = SourceLanguageModelDropdown.GetItemText(SourceLanguageModelDropdown.Selected);
        // Recognise page from active window
        using TesseractOCR.Page Page = await Task.Run(() => OCRUtility.Recognise(ActiveWindowHandle, SourceLanguageModel));

        // Move and scale overlay window to cover captured window
        Rect2I OverlayWindowRect2I = CaptureUtility.GetWindowRect2I(ActiveWindowHandle);
        OverlayWindow.Position = OverlayWindowRect2I.Position;
        OverlayWindow.Size = OverlayWindowRect2I.Size;

        // Create labels for each block
        List<Label> Labels = [];
        await Task.Run(() => {
            foreach (TesseractOCR.Layout.Block Block in Page.Layout) {
                foreach (TesseractOCR.Layout.Paragraph Paragraph in Block.Paragraphs) {
                    if (Paragraph.BoundingBox is TesseractOCR.Rect BlockRect) {
                        // Create overlay label
                        Label OverlayLabel = new() {
                            Text = Paragraph.Text,
                            Position = new Vector2(BlockRect.X1, BlockRect.Y1),
                            RotationDegrees = Paragraph.Properties.DeskewAngle,
                            Size = new Vector2(BlockRect.Width, BlockRect.Height),
                        };
                        // Style overlay label
                        OverlayLabel.AddThemeColorOverride(StringNames.FontOutlineColor, new Color(0, 0, 0));
                        OverlayLabel.AddThemeConstantOverride(StringNames.OutlineSize, 8);
                        OverlayLabel.AddThemeFontSizeOverride(StringNames.FontSize, Paragraph.FontProperties.PointSize);
                        // Add overlay label
                        Labels.Add(OverlayLabel);
                        // Log recognition
                        RecognisedCharacters += OverlayLabel.Text.Length;
                    }
                }
            }
        });

        // Get chosen source and target languages
        string SourceLanguage = SourceLanguageDropdown.GetItemText(SourceLanguageDropdown.Selected);
        string TargetLanguage = TargetLanguageDropdown.GetItemText(TargetLanguageDropdown.Selected);
        // Translate each label
        if (Translator is not null) {
            List<Task> TranslateTasks = [];
            foreach (Label Label in Labels) {
                // Translate label
                async Task TranslateLabelAsync() {
                    // Try get translation from cache
                    string Translation = await TranslationCache.GetOrSetAsync(Label.Text, async CancelToken => {
                        // Otherwise, request translation
                        TranslatedCharacters += Label.Text.Length;
                        return (await Translator.TranslateAsync(Label.Text, TargetLanguage, SourceLanguage)).Translation;
                    });
                    // Set translation
                    Label.Text = Translation;
                }
                TranslateTasks.Add(TranslateLabelAsync());
            }
            await Task.WhenAll(TranslateTasks);
        }

        // Clear existing overlay
        foreach (Node Overlay in OverlayWindow.GetChildren()) {
            Overlay.QueueFree();
        }
        // Overlay each label
        foreach (Label Label in Labels) {
            OverlayWindow.AddChild(Label);
        }
    }
}