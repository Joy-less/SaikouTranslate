using System.Linq;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
public sealed class OCRUtility {
    public string DataDirectory = ".";

    private TesseractOCR.Engine Engine;

    public TesseractOCR.Page Recognise(byte[] ImageData, string Language) {
        // Create OCR engine for language
        Engine = new TesseractOCR.Engine(DataDirectory, Language);
        // Capture image
        using TesseractOCR.Pix.Image Image = TesseractOCR.Pix.Image.LoadFromMemory(ImageData);
        // Recognise text
        return Engine.Process(Image);
    }
    public TesseractOCR.Page Recognise(nint WindowHandle, string Language) {
        byte[] WindowCapture = CaptureUtility.CaptureWindow(WindowHandle, ImageFormat.Png);
        return Recognise(WindowCapture, Language);
    }
    public string[] GetAvailableLanguages() {
        return Directory.GetFiles(DataDirectory)
            .Where(File => Path.GetExtension(File).Equals(".traineddata", System.StringComparison.InvariantCultureIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();
    }
}