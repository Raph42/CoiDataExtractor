# CoiDataExtractor

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)

**CoiDataExtractor** is a lightweight WPF utility designed to parse and extract prototypes (Products, Machines, and Recipes) from the decompiled C# source files of the game **Captain of Industry**, exporting clean and structured JSON files for calculators, wiki pages, or automation tools.

---

## 📸 Screenshots

### Application Interface
![Application Interface](docs/screenshot_app.png)

### Exported JSON Sample
![Exported JSON Result](docs/screenshot_json.png)

---

## ✨ Features

- **Products Extraction:** Resolves IDs, display names, transport/conveyor types (Flat, Loose, Pipe, Virtual), icons, and color hex codes.
- **Color Fallback:** Integrates fallback color definitions (`resources.json`) for products lacking hardcoded RGB data.
- **Machines Extraction:** Extracts identifiers, localized titles, descriptions, next-tier bindings, and prefab paths.
- **Recipes Parsing:** Accurately extracts inputs, outputs, port assignments, operational durations, and bound machines.
- **Multi-language Support:** Native interface in English with on-the-fly French language switching.
- **Source Tracing:** Displays the originating source file (`Source File`) directly in the recipes grid without polluting the exported JSON.

---

## 🛠️ Required Game Files

Because game assets and decompiled code cannot be redistributed, you must extract the prototype files from your own copy of **Captain of Industry** using a decompiler such as [ILSpy](https://github.com/icsharpcode/ILSpy) or [dnSpy](https://github.com/dnSpy/dnSpy).

Decompile `Mafi.Base.dll` and export the following source files into a folder:

1. **`\mafi.base\Mafi.Base\Ids.cs`** *(Mandatory — provides all internal product definitions and IDs)*
2. **All `.cs` files inside `\mafi.base\Mafi.Base.Prototypes.Machines\`** *(e.g., `FurnacesData.cs`, `AssemblyData.cs`, `AirSeparatorData.cs`...)*

> **Note:** The program automatically validates the presence of `Ids.cs`. If this file is missing from the selected directory (or its subdirectories), analysis will be aborted.

---

## 🚀 How to Use

1. Launch **CoiDataExtractor.exe**.
2. Click **📁 Select Folder...** and browse to the folder containing your extracted `.cs` files.
3. Verify the parsed data across the three tabs: **Products**, **Machines**, and **Recipes**.
4. Set the corresponding **Game Version** (e.g., `0.8.7D`) in the top bar.
5. Click **💾 Save As (JSON)...** to save your structured `captain_of_industry_data.json` file.

---

## 📄 JSON Export Format

The output JSON contains clean, ready-to-use lists. Recipe entries keep only essential references (`Quantity` and `ProductId`) while referencing the master `Products` catalog:

```json
{
  "_generator": "CoiDataExtractor",
  "_repository": "[https://github.com/Raph42/CoiDataExtractor](https://github.com/Raph42/CoiDataExtractor)",
  "_license": "MIT License ([https://opensource.org/licenses/MIT](https://opensource.org/licenses/MIT))",
  "_notice": "Generated automatically from Captain of Industry game files. Copyright (c) 2026 Raph42. All rights reserved.",
  "gameVersion": "0.8.7D",
  "Products": [
    {
      "Id": "BauxitePowder",
      "Name": "Bauxite Powder",
      "TransportType": "Loose",
      "Color": "#3d4966",
      "IconPath": "Assets/Base/Products/Icons/BauxitePowder.svg"
    }
  ],
  "Machines": [ ... ],
  "Recipes": [
    {
      "RecipeId": "BauxiteDigestion",
      "Inputs": [
        { "Quantity": 36, "ProductId": "BauxitePowder" },
        { "Quantity": 12, "ProductId": "Brine" }
      ],
      "Outputs": [
        { "Quantity": 18, "ProductId": "HydratedAlumina" },
        { "Quantity": 18, "ProductId": "RedMud" }
      ],
      "MachineBindings": [ ... ]
    }
  ]
}
