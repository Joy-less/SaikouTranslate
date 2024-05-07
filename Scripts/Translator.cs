using System;
using System.Threading.Tasks;
using Godot;

#pragma warning disable CA1416

// Download Tesseract model from https://github.com/tesseract-ocr/tessdata

public partial class Translator : Node {
    [Export] OptionButton SourceLanguageDropdown;
    [Export] OptionButton TargetLanguageDropdown;
    [Export] RichTextLabel CaptureIntervalLabel;
    [Export] Slider CaptureIntervalSlider;
    [Export] Button ToggleButton;

    private readonly OCRUtility OCRUtility = new();
    private Window OverlayWindow;
    private bool Capturing;
    private DateTime LastCapture;

    public override void _Ready() {
        // Setup controls
        FillFromLanguageDropdown();
        SetCaptureInterval();

        // Connect events
        CaptureIntervalSlider.ValueChanged += _ => SetCaptureInterval();
        ToggleButton.Pressed += Toggle;
    }
    public override void _Process(double Delta) {
        // Check if capturing
        if (Capturing) {
            // Check if interval has passed
            if (DateTime.UtcNow - LastCapture >= TimeSpan.FromSeconds(CaptureIntervalSlider.Value)) {
                GD.Print("capture");
                // Capture active window
                LastCapture = DateTime.UtcNow;
                Capture();
            }
        }
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
        };
        // Spawn overlay window
        GetTree().Root.AddChild(OverlayWindow);
        // Make overlay window unclickable
        WindowPassthroughUtility.SetWindowPassthrough(OverlayWindow, false);
        // Return overlay window
        return OverlayWindow;
    }
    public void FillFromLanguageDropdown() {
        SourceLanguageDropdown.Clear();
        foreach (string Language in OCRUtility.GetAvailableLanguages()) {
            SourceLanguageDropdown.AddItem(Language);
        }
    }

    private void SetCaptureInterval() {
        CaptureIntervalLabel.Text = $"[center]Capture Interval: {CaptureIntervalSlider.Value:0.0}";
    }
    private void Toggle() {
        // Toggle capturing flag
        Capturing = !Capturing;
        // Change toggle button text
        ToggleButton.Text = Capturing ? "Stop" : "Start";
        // Disable configurations while capturing
        SourceLanguageDropdown.Disabled = Capturing;
        TargetLanguageDropdown.Disabled = Capturing;
        CaptureIntervalSlider.Editable = !Capturing;
        // Destroy overlay window
        OverlayWindow?.QueueFree();
        OverlayWindow = null;
        // Spawn overlay window
        if (Capturing) {
            SpawnOverlayWindow();
        }
    }
    private async void Capture() {
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

        // Clear existing overlays
        foreach (Node OverlayNode in OverlayWindow.GetChildren()) {
            OverlayNode.QueueFree();
        }

        // Translate each block
        foreach (TesseractOCR.Layout.Block Block in Page.Layout) {
            foreach (TesseractOCR.Layout.Paragraph Paragraph in Block.Paragraphs) {
                //foreach (TesseractOCR.Layout.TextLine TextLine in Paragraph.TextLines) {
                //    foreach (TesseractOCR.Layout.Word Word in TextLine.Words) {
                        if (Paragraph.BoundingBox is TesseractOCR.Rect BlockRect) {
                            // Create overlay label
                            Label OverlayLabel = new() {
                                Text = Paragraph.Text,
                                Position = new Vector2(BlockRect.X1, BlockRect.Y1),
                                Size = new Vector2(BlockRect.Width, BlockRect.Height),
                            };
                            OverlayLabel.AddThemeColorOverride(StringNames.FontOutlineColor, new Color(0, 0, 0));
                            OverlayLabel.AddThemeConstantOverride(StringNames.OutlineSize, 8);
                            OverlayLabel.AddThemeFontSizeOverride(StringNames.FontSize, Paragraph.FontProperties.PointSize);
                            // Add overlay label
                            OverlayWindow.AddChild(OverlayLabel);
                        }
                //    }
                //}
            }
        }
    }
}