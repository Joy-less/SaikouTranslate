using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GTranslate;
using GTranslate.Translators;

#pragma warning disable CA1416

// Download Tesseract model from https://github.com/tesseract-ocr/tessdata

public partial class WindowTranslator : Node {
    [Export] OptionButton SourceLanguageDropdown;
    [Export] RichTextLabel CaptureIntervalLabel;
    [Export] Slider CaptureIntervalSlider;
    [Export] OptionButton TranslationServiceDropdown;
    [Export] OptionButton TargetLanguageDropdown;
    [Export] Button ToggleButton;
    [Export] Button ResetButton;

    private readonly OCRUtility OCRUtility = new();
    private ITranslator Translator;
    private Window OverlayWindow;
    private bool Capturing;

    public override void _Ready() {
        // Setup controls
        FillSourceLanguageDropdown();
        FillTargetLanguageDropdown();
        FillTranslationServiceDropdown();
        SetCaptureInterval();
        SetTranslationService();

        // Connect events
        CaptureIntervalSlider.ValueChanged += _ => SetCaptureInterval();
        TranslationServiceDropdown.ItemSelected += _ => SetTranslationService();
        ToggleButton.Pressed += Toggle;
        ResetButton.Pressed += Reset;

        // Start capturing when started
        _ = CaptureLoopAsync();
    }
    public Window SpawnOverlayWindow() {
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
        // Return overlay window
        return OverlayWindow;
    }
    public async Task CaptureLoopAsync(CancellationToken CancelToken = default) {
        while (true) {
            // Wait for next capture
            await Task.Delay(TimeSpan.FromSeconds(CaptureIntervalSlider.Value), CancelToken);
            // Try capture active window
            if (Capturing) {
                try {
                    await CaptureAsync();
                } catch {}
            }
        }
    }

    private void FillSourceLanguageDropdown() {
        // Fill dropdown with source languages
        SourceLanguageDropdown.Clear();
        foreach (string Language in OCRUtility.GetAvailableLanguages()) {
            SourceLanguageDropdown.AddItem(Language);
        }
    }
    private void FillTargetLanguageDropdown() {
        // Fill dropdown with target languages
        TargetLanguageDropdown.Clear();
        foreach (Language Language in Language.LanguageDictionary.Values) {
            TargetLanguageDropdown.AddItem(Language.Name);
        }
        // Select English by default
        TargetLanguageDropdown.Selected = Language.LanguageDictionary.Values.Select(Language => Language.Name).ToList().IndexOf("English");
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
    private void SetCaptureInterval() {
        CaptureIntervalLabel.Text = $"[center]Capture Interval: {CaptureIntervalSlider.Value:0.0}s";
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
    }
    private void Toggle() {
        // Toggle capturing flag
        Capturing = !Capturing;
        // Change toggle button text
        ToggleButton.Text = Capturing ? "Stop" : "Start";
        // Disable configurations while capturing
        SourceLanguageDropdown.Disabled = Capturing;
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
    private async Task CaptureAsync() {
        // Get handle for active window
        nint ActiveWindowHandle = CaptureUtility.GetActiveWindowHandle();
        // Get chosen source language
        string SourceLanguage = SourceLanguageDropdown.GetItemText(SourceLanguageDropdown.Selected);
        // Recognise page from active window
        using TesseractOCR.Page Page = await Task.Run(() => OCRUtility.Recognise(ActiveWindowHandle, SourceLanguage));

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
                            Size = new Vector2(BlockRect.Width, BlockRect.Height),
                        };
                        // Style overlay label
                        OverlayLabel.AddThemeColorOverride(StringNames.FontOutlineColor, new Color(0, 0, 0));
                        OverlayLabel.AddThemeConstantOverride(StringNames.OutlineSize, 8);
                        OverlayLabel.AddThemeFontSizeOverride(StringNames.FontSize, Paragraph.FontProperties.PointSize);
                        // Add overlay label
                        Labels.Add(OverlayLabel);
                    }
                }
            }
        });

        // Get chosen target language
        string TargetLanguage = TargetLanguageDropdown.GetItemText(TargetLanguageDropdown.Selected);
        // Translate each label
        if (Translator is not null) {
            List<Task> TranslateTasks = [];
            foreach (Label Label in Labels) {
                async Task TranslateLabelAsync() {
                    Label.Text = (await Translator.TranslateAsync(Label.Text, TargetLanguage, SourceLanguage)).Translation;
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