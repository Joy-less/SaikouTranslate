<img src="https://github.com/Joy-less/SaikouTranslate/blob/main/Images/Icon.png?raw=true" width="150" />

# Saikou Translate

Playing a game in a different language?

Saikou Translate is a tool to capture and translate text on the screen.

## Example

<img src="https://github.com/Joy-less/SaikouTranslate/blob/main/Images/Example.png?raw=true" width="600" />

## Disclaimers

- Windows-only (since there's no easy way to capture or overlay windows on other platforms).
- Output quality isn't perfect, especially for pixel text.
- Translation uses free web APIs. You might get rate limited.

## Tutorial

1. Download the latest version of Saikou Translate.

2. You need an OCR model to recognise text. Japanese and English models are included.

Otherwise, download a model: [Normal](https://github.com/tesseract-ocr/tessdata), [Fast](https://github.com/tesseract-ocr/tessdata_fast), [Best](https://github.com/tesseract-ocr/tessdata_best).

Put your models in the OCR folder.

3. Run Saikou Translate and set the `Source Language Model` to your OCR model. A `fast` model is recommended.

4. Set the translation service to your desired web API. Set `Any` to choose automatically.

5. Press start and focus on your game window.