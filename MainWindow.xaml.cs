using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace CoiDataExtractor
{
    // ==========================================
    // Modèles de données
    // ==========================================
    public class AnalysisLogEntry
    {
        public string FileName { get; set; } = string.Empty;
        public int MachinesCount { get; set; }
        public int RecipesCount { get; set; }
        public string Status { get; set; } = "OK";
    }


    public class ExtractedData
    {
        [JsonPropertyOrder(-6)]
        [JsonPropertyName("_generator")]
        public string Generator { get; set; } = $"CoiDataExtractor v{LocalizationManager.AppVersion}";
        //public string Generator { get; set; } = $"CoiDataExtractor v{MainWindow.AppVersion}";

        [JsonPropertyOrder(-5)]
        [JsonPropertyName("_repository")]
        public string Repository { get; set; } = "https://github.com/Raph42/CoiDataExtractor";

        [JsonPropertyOrder(-4)]
        [JsonPropertyName("_license")]
        public string License { get; set; } = "MIT License (https://opensource.org/licenses/MIT)";

        [JsonPropertyOrder(-3)]
        [JsonPropertyName("_notice")]
        public string Notice { get; set; } = "Generated automatically from Captain of Industry game files.";

        [JsonPropertyOrder(-2)]
        [JsonPropertyName("gameVersion")]
        public string GameVersion { get; set; } = string.Empty;

        public List<ProductInfo> Products { get; set; } = new();
        public List<MachineModel> Machines { get; set; } = new();
        public List<RecipeModel> Recipes { get; set; } = new();

        [JsonIgnore]
        public List<AnalysisLogEntry> Logs { get; set; } = new();

    }


    public class ProductInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TransportType { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsFallbackColor { get; set; } = false;

        public string IconPath { get; set; } = string.Empty;

        public string Image { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsFallbackIcon { get; set; } = false;
    }


    public class ResourceFallbackItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;
    }


    public class CostItem
    {
        public double Quantity { get; set; }
        public string ProductId { get; set; } = string.Empty;
    }


    public class ParsedMachineCost
    {
        public int Workers { get; set; } = 0;
        public List<CostItem> Maintenance { get; set; } = new();
        public List<CostItem> Materials { get; set; } = new();
    }


    public class MachineModel
    {
        [JsonIgnore]
        public string SourceFile { get; set; } = string.Empty;

        [JsonIgnore]
        public string VariableName { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Renommé dans le JSON et stocké sous forme de nombre entier en kW
        [JsonPropertyName("ElectricityConsumptionKW")]
        public int ElectricityConsumptionKW { get; set; } = 0;

        // Nombre d'ouvriers
        public int Workers { get; set; } = 0;

        // Maintenance sous forme d'objet JSON { Quantity, ProductId }
        public List<CostItem> Maintenance { get; set; } = new();

        // Affichage lisible pour le DataGrid WPF (exclue du JSON)
        [JsonIgnore]
        public string MaintenanceDisplay => Maintenance.Count > 0
            ? string.Join(", ", Maintenance.Select(m => $"{m.Quantity}x {m.ProductId}"))
            : "-";


        // Matériaux de construction (ex: [{ Quantity: 30, ProductId: "ConstructionParts2" }])
        public List<CostItem> ConstructionCost { get; set; } = new();

        // Propriété d'affichage lisible pour le DataGrid WPF (exclue du JSON)
        [JsonIgnore]
        public string ConstructionCostDisplay => ConstructionCost.Count > 0
            ? string.Join(", ", ConstructionCost.Select(c => $"{c.Quantity}x {c.ProductId}"))
            : "Free";


        public string IconOrPrefab { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string? NextTierId { get; set; }
    }


    public class RecipeItem
    {
        public double Quantity { get; set; }
        public string ProductId { get; set; } = string.Empty;

        // Ignorés lors de l'enregistrement JSON mais utilisés pour l'affichage WPF
        [JsonIgnore]
        public string ProductName { get; set; } = string.Empty;

        [JsonIgnore]
        public string TransportType { get; set; } = string.Empty;

        [JsonIgnore]
        public string Color { get; set; } = string.Empty;

        [JsonIgnore]
        public string IconPath { get; set; } = string.Empty;
    }


    public class MachineBinding
    {
        public string MachineId { get; set; } = string.Empty;
        public string DurationSeconde { get; set; } = string.Empty;
        public int OutputMultiplier { get; set; } = 1;
    }


    public class RecipeModel
    {
        public string RecipeId { get; set; } = string.Empty;

        // Nom du fichier source pour l'affichage (exclu du JSON)
        [JsonIgnore]
        public string SourceFile { get; set; } = string.Empty;

        public List<RecipeItem> Inputs { get; set; } = new();
        public List<RecipeItem> Outputs { get; set; } = new();
        public List<MachineBinding> MachineBindings { get; set; } = new();

        [JsonIgnore]
        public string InputsDisplay => string.Join(", ", Inputs.Select(i =>
            $"{i.Quantity}x {(!string.IsNullOrEmpty(i.ProductName) ? i.ProductName : i.ProductId)} ({i.TransportType})"));

        [JsonIgnore]
        public string OutputsDisplay => string.Join(", ", Outputs.Select(o =>
            $"{o.Quantity}x {(!string.IsNullOrEmpty(o.ProductName) ? o.ProductName : o.ProductId)} ({o.TransportType})"));

        // Affichage propre sans double parenthèse ni ".Seconds(s)"
        [JsonIgnore]
        public string BindingsDisplay => string.Join(" | ", MachineBindings.Select(b =>
        {
            // Nettoie tout résidu de ".Seconds", de parenthèses '(' ou ')' et de 's'
            string dur = (b.DurationSeconde ?? "")
                .Replace(".Seconds()", "")
                .Replace("Seconds()", "")
                .Replace("(", "")
                .Replace(")", "")
                .TrimEnd('s')
                .Trim();

            string mult = b.OutputMultiplier > 1 ? $", x{b.OutputMultiplier}" : "";
            return $"{b.MachineId} ({dur}s{mult})";
        }));

    }


    public partial class MainWindow : Window
    {
        private ExtractedData _data = new();
        //public const string AppVersion = "1.02";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationManager.Instance;

            var dialog = new OpenFolderDialog
            {
                Title = "Sélectionner le dossier contenant les fichiers .cs"
            };

            if (dialog.ShowDialog() == true)
            {
                string folder = dialog.FolderName;

                // 1. Vérification de la présence obligatoire de Ids.cs
                bool hasIdsFile = Directory.EnumerateFiles(folder, "Ids.cs", SearchOption.AllDirectories).Any();

                if (!hasIdsFile)
                {
                    MessageBox.Show(
                        loc.DialogMissingIdsText,
                        loc.DialogMissingIdsTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    LblStatus.Text = loc.StatusMissingIds;
                    return;
                }

                // 2. Vérification de Costs.cs
                bool hasCostsFile = Directory.EnumerateFiles(folder, "Costs.cs", SearchOption.AllDirectories).Any();
                if (!hasCostsFile)
                {
                    MessageBox.Show(
                        loc.DialogMissingCostsText,
                        loc.DialogMissingIdsTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    LblStatus.Text = loc.StatusMissingCosts;
                    return;
                }

                TxtFolderPath.Text = folder;
                LblStatus.Text = loc.StatusParsing;
                BtnSaveJson.IsEnabled = false;

                _data = await Task.Run(() => ProcessFolder(folder));

                DgProducts.ItemsSource = _data.Products;
                DgMachines.ItemsSource = _data.Machines;
                DgRecipes.ItemsSource = _data.Recipes;
                DgLogs.ItemsSource = _data.Logs;

                // Affichage du récapitulatif total dans l'onglet Logs
                LblLogSummary.Text = LocalizationManager.Instance.GetLogSummary(_data.Products.Count, _data.Machines.Count, _data.Recipes.Count);
                PnlLogSummary.Visibility = Visibility.Visible;

                LblStatus.Text = loc.GetStatusDone(_data.Products.Count, _data.Machines.Count, _data.Recipes.Count);
                //LblStatus.Text = $"{_data.Products.Count} products, {_data.Machines.Count} machines, {_data.Recipes.Count} recipes loaded.";

                BtnSaveJson.IsEnabled = _data.Products.Count > 0 || _data.Machines.Count > 0 || _data.Recipes.Count > 0;
            }
        }


        private void BtnSaveJson_Click(object sender, RoutedEventArgs e)
        {
            var loc = LocalizationManager.Instance;

            var saveDialog = new SaveFileDialog
            {
                Filter = "Fichier JSON (*.json)|*.json",
                FileName = "captain_of_industry_data.json"
            };

            if (saveDialog.ShowDialog() == true)
            {
                // 1. Récupération des préfixes configurés (avec valeurs par défaut de secours)
                string resPrefix = string.IsNullOrWhiteSpace(TxtResourcePathPrefix.Text)
                    ? "/images/resources/"
                    : TxtResourcePathPrefix.Text.Trim();
                if (!resPrefix.EndsWith("/")) resPrefix += "/";

                string bldPrefix = string.IsNullOrWhiteSpace(TxtBuildingPathPrefix.Text)
                    ? "/images/buildings/"
                    : TxtBuildingPathPrefix.Text.Trim();
                if (!bldPrefix.EndsWith("/")) bldPrefix += "/";

                // 2. Application de la propriété Image pour chaque produit
                foreach (var prod in _data.Products)
                {
                    prod.Image = $"{resPrefix}{prod.Id}.png";
                }

                // 3. Application de la propriété Image pour chaque machine
                foreach (var machine in _data.Machines)
                {
                    machine.Image = $"{bldPrefix}{machine.Id}.png";
                }

                // 4. Métadonnées de version
                string version = string.IsNullOrWhiteSpace(TxtGameVersion.Text) ? "0.8.7D" : TxtGameVersion.Text.Trim();

                // Ajout des métadonnées / commentaire en tête du JSON
                _data.GameVersion = version;

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(_data, options);
                File.WriteAllText(saveDialog.FileName, json);

                MessageBox.Show($"{loc.DialogExportSuccessText}{saveDialog.FileName}",
                    loc.DialogExportSuccessTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private ExtractedData ProcessFolder(string folderPath)
        {
            var aggregatedData = new ExtractedData();
            var productsCatalog = new Dictionary<string, ProductInfo>(StringComparer.OrdinalIgnoreCase);
            var costsCatalog = new Dictionary<string, ParsedMachineCost>(StringComparer.OrdinalIgnoreCase);

            var fallbackColors = LoadFallbackColors(folderPath);
            var files = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories).ToList();

            // 1. Priorité à Ids.cs pour construire le catalogue de produits
            var idsFile = files.FirstOrDefault(f => Path.GetFileName(f).Equals("Ids.cs", StringComparison.OrdinalIgnoreCase));
            if (idsFile != null)
            {
                string name = Path.GetFileName(idsFile);

                try
                {
                    string idsCode = File.ReadAllText(idsFile);
                    ParseProducts(idsCode, productsCatalog);

                    aggregatedData.Logs.Add(new AnalysisLogEntry
                    {
                        FileName = name,
                        Status = $"{productsCatalog.Count} products extracted"
                    });
                }
                catch (Exception ex)
                {
                    aggregatedData.Logs.Add(new AnalysisLogEntry
                    {
                        FileName = name,
                        Status = $"Error: {ex.Message}"
                    });
                }

                files.Remove(idsFile);
            }

            // 2. Traitement prioritaire de Costs.cs
            var costsFile = files.FirstOrDefault(f => Path.GetFileName(f).Equals("Costs.cs", StringComparison.OrdinalIgnoreCase));
            if (costsFile != null)
            {
                string name = Path.GetFileName(costsFile);

                try
                {
                    string costsCode = File.ReadAllText(costsFile);
                    ParseCosts(costsCode, costsCatalog);

                    aggregatedData.Logs.Add(new AnalysisLogEntry
                    {
                        FileName = name,
                        Status = $"{costsCatalog.Count} machine costs extracted"
                    });
                }
                catch (Exception ex)
                {
                    aggregatedData.Logs.Add(new AnalysisLogEntry
                    {
                        FileName = name,
                        Status = $"Error: {ex.Message}"
                    });
                }

                files.Remove(costsFile);
            }

            // 3. Application de la couleur de repli (resources.json)
            foreach (var product in productsCatalog.Values)
            {
                if (string.IsNullOrEmpty(product.Color))
                {
                    string normalizedKey = NormalizeId(product.Id);
                    if (fallbackColors.TryGetValue(normalizedKey, out var fallbackColor))
                    {
                        product.Color = fallbackColor;
                        product.IsFallbackColor = true;
                    }
                }
            }

            aggregatedData.Products = productsCatalog.Values.OrderBy(p => p.Name).ToList();

            // 4. Traitement de tous les autres fichiers sources (Machines & Recettes)
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                try
                {
                    string code = File.ReadAllText(file);
                    int prevMachinesCount = aggregatedData.Machines.Count;
                    int prevRecipesCount = aggregatedData.Recipes.Count;

                    ParseSourceCode(code, aggregatedData, productsCatalog, costsCatalog, fileName);

                    int fileMachines = aggregatedData.Machines.Count - prevMachinesCount;
                    int fileRecipes = aggregatedData.Recipes.Count - prevRecipesCount;

                    aggregatedData.Logs.Add(new AnalysisLogEntry
                    {
                        FileName = fileName,
                        MachinesCount = fileMachines,
                        RecipesCount = fileRecipes,
                        Status = "OK"
                    });
                }
                catch (Exception ex)
                {
                    aggregatedData.Logs.Add(new AnalysisLogEntry
                    {
                        FileName = fileName,
                        Status = $"Error: {ex.Message}"
                    });
                }
            }

            // Post-traitement : Résolution des recettes sans .BindTo() standard
            foreach (var recipe in aggregatedData.Recipes)
            {
                if (recipe.MachineBindings.Count == 0)
                {
                    // 1. Tours de refroidissement (Cooling towers)
                    if (recipe.RecipeId.StartsWith("Steam") && recipe.RecipeId.Contains("Condensation"))
                    {
                        string targetTower = recipe.RecipeId.EndsWith("T2") ? "CoolingTowerT2" : "CoolingTowerT1";
                        recipe.MachineBindings.Add(new MachineBinding
                        {
                            MachineId = targetTower,
                            DurationSeconde = "20",
                            OutputMultiplier = 1
                        });
                    }
                    // 2. Déversement liquide dans l'océan (Waste dump)
                    else if (recipe.RecipeId.EndsWith("Dumping"))
                    {
                        recipe.MachineBindings.Add(new MachineBinding
                        {
                            MachineId = "WasteDump",
                            DurationSeconde = "10",
                            OutputMultiplier = 1
                        });
                    }
                }
            }


            return aggregatedData;
        }


        private Dictionary<string, string> LoadFallbackColors(string selectedFolder)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string pathInApp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources.json");
            string pathInFolder = Path.Combine(selectedFolder, "resources.json");

            string targetPath = File.Exists(pathInApp) ? pathInApp : (File.Exists(pathInFolder) ? pathInFolder : string.Empty);

            if (!string.IsNullOrEmpty(targetPath))
            {
                try
                {
                    string json = File.ReadAllText(targetPath);
                    var items = JsonSerializer.Deserialize<List<ResourceFallbackItem>>(json);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (!string.IsNullOrEmpty(item.Color))
                            {
                                dict[NormalizeId(item.Id)] = item.Color;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            return dict;
        }


        private static string NormalizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            string normalized = id.ToLowerInvariant().Replace("_", "").Replace("-", "");
            if (normalized.EndsWith("iv")) normalized = normalized[..^2] + "4";
            else if (normalized.EndsWith("iii")) normalized = normalized[..^3] + "3";
            else if (normalized.EndsWith("ii")) normalized = normalized[..^2] + "2";
            else if (normalized.EndsWith("i")) normalized = normalized[..^1] + "1";

            return normalized;
        }

        private static string ConvertColorToHex(long colorInt)
        {
            if (colorInt <= 0) return string.Empty;
            return $"#{(colorInt & 0xFFFFFF):X6}";
        }


        private void ParseProducts(string code, Dictionary<string, ProductInfo> catalog)
        {
            var regex = new Regex(@"(\w+)\s*=\s*ProductBuilder\.(Loose|Fluid|Unit|Molten|Virtual)\s*\(([\s\S]*?)\);", RegexOptions.Multiline);
            var matches = regex.Matches(code);

            foreach (Match match in matches)
            {
                string varId = match.Groups[1].Value.Trim();
                string builderType = match.Groups[2].Value.Trim();
                string args = match.Groups[3].Value;

                var product = new ProductInfo
                {
                    Id = varId,
                    TransportType = builderType switch
                    {
                        "Loose" => "Loose",
                        "Unit" => "Flat",
                        "Fluid" => "Pipe",
                        "Molten" => "Pipe",
                        "Virtual" => "Virtual",
                        _ => builderType
                    }
                };

                // Recherche de l'icône explicite (.svg ou .png)
                var iconMatch = Regex.Match(args, @"\""([^\""]+\.(?:svg|png))\""");
                if (iconMatch.Success)
                {
                    product.IconPath = iconMatch.Groups[1].Value;
                    product.IsFallbackIcon = false;
                }
                else
                {
                    product.IconPath = $"Assets/Base/Products/Icons/{varId}.svg";
                    product.IsFallbackIcon = true;
                }

                // Recherche du nom lisible
                var stringLiterals = Regex.Matches(args, @"\""([^\""]+)\""");
                foreach (Match lit in stringLiterals)
                {
                    string val = lit.Groups[1].Value;
                    if (!val.EndsWith(".svg") && !val.EndsWith(".png") && !val.EndsWith(".mat") && !val.Equals("unit product", StringComparison.OrdinalIgnoreCase))
                    {
                        product.Name = val;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(product.Name))
                {
                    product.Name = varId;
                }

                // Couleur native C#
                var colorMatch = Regex.Match(args, @"(?<![A-Za-z0-9_])([1-9][0-9]{6,8})(?![A-Za-z0-9_])");
                if (colorMatch.Success && long.TryParse(colorMatch.Groups[1].Value, out long colorVal))
                {
                    product.Color = ConvertColorToHex(colorVal);
                    product.IsFallbackColor = false;
                }

                catalog[varId] = product;
            }
        }


        // Extrait les matériaux (CP, CP2, Steel, Product(...) etc.), la maintenance et les ouvriers
        private void ParseCosts(string code, Dictionary<string, ParsedMachineCost> costsCatalog)
        {
            // Cibler le bloc Machines dans Costs.cs
            var machinesClassMatch = Regex.Match(code, @"class\s+Machines\s*\{([\s\S]*?)\n\s*\}");
            string content = machinesClassMatch.Success ? machinesClassMatch.Groups[1].Value : code;

            // Découper chaque déclaration de coût (ex: public static EntityCostsTpl Nom => ...;)
            var declMatches = Regex.Matches(content, @"public\s+static\s+(?:readonly\s+)?EntityCostsTpl\s+(\w+)\s*=>\s*([^;]+);");
            foreach (Match m in declMatches)
            {
                string costName = m.Groups[1].Value.Trim();
                string body = m.Groups[2].Value.Trim();

                var costInfo = new ParsedMachineCost();

                // 1. Workers
                var wMatch = Regex.Match(body, @"\.Workers\((\d+)\)");
                if (wMatch.Success) costInfo.Workers = int.Parse(wMatch.Groups[1].Value);

                // 2. Maintenance (T1, T2, T3, T1Early) -> convertie en CostItem
                var maintMatch = Regex.Match(body, @"\.Maintenance(T1Early|T1|T2|T3)\((?:\(Fix32\))?([0-9\.]+)\)");
                if (maintMatch.Success)
                {
                    string tier = maintMatch.Groups[1].Value.Replace("Early", ""); // "T1", "T2", "T3"
                    if (double.TryParse(maintMatch.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double rawVal))
                    {
                        costInfo.Maintenance.Add(new CostItem
                        {
                            //Quantity = (int)Math.Round(rawVal),
                            Quantity = rawVal,
                            ProductId = $"Maintenance{tier}" // Ex: "MaintenanceT1", "MaintenanceT2"
                        });
                    }
                }

                // 3. Matériaux de construction
                // Raccourcis CP, CP2, CP3, CP4
                var cpMatches = Regex.Matches(body, @"\.(CP[2-4]?)\((\d+)\)");
                foreach (Match cp in cpMatches)
                {
                    string cpType = cp.Groups[1].Value;
                    int qty = int.Parse(cp.Groups[2].Value);
                    string prodId = cpType switch
                    {
                        "CP" => "ConstructionParts",
                        "CP2" => "ConstructionParts2",
                        "CP3" => "ConstructionParts3",
                        "CP4" => "ConstructionParts4",
                        _ => cpType
                    };
                    costInfo.Materials.Add(new CostItem { Quantity = qty, ProductId = prodId });
                }

                // Matériaux standards spécifiques .Steel(20), .Concrete(40), .Electronics(20)...
                var stdMatMatches = Regex.Matches(body, @"\.(Steel|Concrete|Iron|Copper|Electronics|Electronics2|Electronics3)\((\d+)\)");
                foreach (Match sm in stdMatMatches)
                {
                    // CoolingTowerT1, CoolingTowerT2 et SmokeStackLarge, le produit utilisé est "ProductId": "Concrete".
                    // Or, dans le catalogue Products, l'identifiant exact est "ConcreteSlab" (Concrete n'existe pas).   
                    string matId = sm.Groups[1].Value == "Concrete" ? "ConcreteSlab" : sm.Groups[1].Value;

                    costInfo.Materials.Add(new CostItem
                    {
                        ProductId = matId,
                        Quantity = int.Parse(sm.Groups[2].Value)
                    });
                }

                // Format générique .Product(120, Ids.Products.SolarCell)
                var genProdMatches = Regex.Matches(body, @"\.Product\((\d+),\s*Ids\.Products\.(\w+)\)");
                foreach (Match gp in genProdMatches)
                {
                    costInfo.Materials.Add(new CostItem
                    {
                        Quantity = int.Parse(gp.Groups[1].Value),
                        ProductId = gp.Groups[2].Value
                    });
                }

                costsCatalog[costName] = costInfo;
            }
        }



        private void ParseSourceCode(
            string code, ExtractedData data,
            Dictionary<string, ProductInfo> productsCatalog,
            Dictionary<string, ParsedMachineCost> costsCatalog,
            string sourceFileName)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var localMachines = new List<MachineModel>();
            var machineVarMap = new Dictionary<string, MachineModel>();

            var descVars = new Dictionary<string, string>();
            // Dictionnaire des variables de quantité (ex: quantity = 1)
            var numVars = new Dictionary<string, double>();
            string baseName = "Machine";


            // =========================================================================
            // Support spécifique pour les générateurs (PowerGeneratorsData.cs)
            // =========================================================================
            var powerConstants = new Dictionary<string, int>();
            var constMatches = Regex.Matches(code, @"(\w+)\s*=\s*(\d+)\.(?:Kw|KwMech|Mw|MwMech)\(\);");
            foreach (Match m in constMatches)
            {
                powerConstants[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            }

            // Extraction des générateurs ElectricityGeneratorFromMechPowerProto
            var genMatches = Regex.Matches(code,
                @"ElectricityGeneratorFromMechPowerProto\s+(\w+)\s*=\s*registrator\.PrototypesDb\.Add\s*\(\s*new\s+ElectricityGeneratorFromMechPowerProto\s*\(\s*Ids\.Machines\.(\w+),\s*Proto\.CreateStr\([^,]+,\s*""([^""]+)""(?:,\s*""([^""]*)"")?\).*?MECH_GEN_(\w+)_MAX_INPUT.*?MECH_GEN_(\w+)_MAX_OUTPUT",
                RegexOptions.Singleline);

            foreach (Match m in genMatches)
            {
                string varName = m.Groups[1].Value;
                string machineId = m.Groups[2].Value;
                string machineName = m.Groups[3].Value;
                string machineDesc = m.Groups[4].Value;
                string inputKey = $"MECH_GEN_{m.Groups[5].Value}_MAX_INPUT";
                string outputKey = $"MECH_GEN_{m.Groups[6].Value}_MAX_OUTPUT";

                // 1. Déclaration de la machine
                var machine = new MachineModel
                {
                    VariableName = varName,
                    Id = machineId,
                    Name = machineName,
                    Description = machineDesc,
                    SourceFile = sourceFileName,
                    IconOrPrefab = machineId.Contains("T2")
                        ? "Assets/Base/Machines/PowerPlant/GeneratorT2.prefab"
                        : "Assets/Base/Machines/PowerPlant/Generator.prefab"
                };

                // Coût depuis costsCatalog si présent
                if (costsCatalog.TryGetValue(machineId, out var costInfo))
                {
                    machine.Workers = costInfo.Workers;
                    machine.Maintenance = costInfo.Maintenance;
                    machine.ConstructionCost = costInfo.Materials
                        .Select(mat => new CostItem { Quantity = mat.Quantity, ProductId = mat.ProductId })
                        .ToList();
                }

                machineVarMap[varName] = machine;
                localMachines.Add(machine);

                // 2. Création de la pseudo-recette (conversion Puissance Mécanique -> Électricité)
                int inPower = powerConstants.TryGetValue(inputKey, out var pIn) ? pIn : 0;
                int outPower = powerConstants.TryGetValue(outputKey, out var pOut) ? pOut : 0;

                var genRecipe = new RecipeModel
                {
                    RecipeId = $"{machineId}_Generation",
                    SourceFile = sourceFileName
                };

                var inputItem = new RecipeItem
                {
                    Quantity = inPower,
                    ProductId = "MechanicalPower",
                    ProductName = "Mechanical Power",
                    TransportType = "Shaft"
                };
                if (productsCatalog.TryGetValue("MechanicalPower", out var pMech))
                {
                    inputItem.ProductName = pMech.Name;
                    inputItem.Color = pMech.Color;
                    inputItem.IconPath = pMech.IconPath;
                }
                genRecipe.Inputs.Add(inputItem);

                var outputItem = new RecipeItem
                {
                    Quantity = outPower,
                    ProductId = "Electricity",
                    ProductName = "Electricity",
                    TransportType = "Virtual"
                };
                if (productsCatalog.TryGetValue("Electricity", out var pElec))
                {
                    outputItem.ProductName = pElec.Name;
                    outputItem.Color = pElec.Color;
                    outputItem.IconPath = pElec.IconPath;
                }
                genRecipe.Outputs.Add(outputItem);

                genRecipe.MachineBindings.Add(new MachineBinding
                {
                    MachineId = machineId,
                    DurationSeconde = "60",
                    OutputMultiplier = 1
                });

                data.Recipes.Add(genRecipe);
            }



            // 1. Textes et variables locales (Loc.Str et double quantity)
            foreach (var localDecl in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                var text = localDecl.ToString();
                if (text.Contains("__name"))
                {
                    //var match = Regex.Match(text, @"\""([^\""]+)\""");
                    //if (match.Success) baseName = match.Groups[1].Value;

                    // Récupère toutes les chaînes entre guillemets dans Loc.Str("cle__name", "Nom Réel", ...)
                    var strMatches = Regex.Matches(text, @"\""([^\""]*)\""");
                    if (strMatches.Count >= 2)
                    {
                        // Le 2e argument est le nom lisible (ex: "Assembly")
                        baseName = strMatches[1].Groups[1].Value;
                    }
                    else if (strMatches.Count == 1)
                    {
                        baseName = strMatches[0].Groups[1].Value.Replace("__name", "").Trim();
                    }
                }


                foreach (var variable in localDecl.Declaration.Variables)
                {
                    if (variable.Initializer != null)
                    {
                        string initVal = variable.Initializer.Value.ToString().Trim();

                        // Capture Loc.Str
                        if (initVal.Contains("Loc.Str"))
                        {
                            var matches = Regex.Matches(initVal, @"\""([^\""]*)\""");
                            if (matches.Count >= 2)
                            {
                                descVars[variable.Identifier.Text] = matches[1].Groups[1].Value;
                            }
                        }
                        // Capture des variables entières (ex: int quantity = 1; int quantity2 = 2;)
                        //else if (int.TryParse(initVal, out int parsedInt))
                        //{
                        //    intVars[variable.Identifier.Text] = parsedInt;
                        //}
                        // Capture des variables numériques (ex: int quantity = 1; ou double quantity = 1.5;)
                        else if (double.TryParse(initVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
                        {
                            numVars[variable.Identifier.Text] = parsedVal;
                        }


                    }

                }

            }


            // 2. Extraction des machines
            foreach (var localDecl in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                var declStr = localDecl.ToString();
                if (declStr.Contains("MachineProtoBuilder.Start"))
                {
                    var variable = localDecl.Declaration.Variables.FirstOrDefault();
                    if (variable == null) continue;

                    string varName = variable.Identifier.Text;

                    var machine = new MachineModel
                    {
                        VariableName = varName,
                        SourceFile = sourceFileName
                    };


                    // Extraction et conversion en kW (int)
                    // Gère .SetElectricityConsumption(Electricity.FromKw(500)), 200.Kw(), 1.5.Mw(), 2.Mw(), etc.
                    var elecMatch = Regex.Match(declStr, @"SetElectricityConsumption\(\s*(?:Electricity\.FromKw\((\d+)\)|([0-9\.]+)\.(Kw|Mw)\(\))\s*\)", RegexOptions.IgnoreCase);
                    if (elecMatch.Success)
                    {
                        if (elecMatch.Groups[1].Success && int.TryParse(elecMatch.Groups[1].Value, out int fromKw))
                        {
                            machine.ElectricityConsumptionKW = fromKw;
                        }
                        else if (double.TryParse(elecMatch.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                        {
                            string unit = elecMatch.Groups[3].Value.ToUpperInvariant();
                            if (unit == "MW")
                            {
                                machine.ElectricityConsumptionKW = (int)Math.Round(val * 1000); // 1.5 MW -> 1500 kW
                            }
                            else
                            {
                                machine.ElectricityConsumptionKW = (int)Math.Round(val);
                            }
                        }
                    }
                    //else
                    //{
                    //    // Recherche alternative plus permissive si la syntaxe varie
                    //    var genericElecMatch = Regex.Match(declStr, @"SetElectricityConsumption\(([^)]+)\)");
                    //    if (genericElecMatch.Success)
                    //    {
                    //        string raw = genericElecMatch.Groups[1].Value.Trim();
                    //        // Nettoyage rapide si c'est une valeur simple
                    //        machine.ElectricityConsumption = raw.Replace("Electricity.", "").Replace("()", "");
                    //    }
                    //}


                    // Nom
                    //var nameMatch = Regex.Match(declStr, @"Start\(\$""\{locStr\}\s*([^""]+)""");
                    //if (nameMatch.Success)
                    //{
                    //    machine.Name = $"{baseName} {nameMatch.Groups[1].Value.Trim()}";
                    //}
                    //else
                    //{
                    //    var fallbackNameMatch = Regex.Match(declStr, @"Start\([""']([^""']+)[""']");
                    //    machine.Name = fallbackNameMatch.Success ? fallbackNameMatch.Groups[1].Value : varName;
                    //}

                    // Nom
                    // Nom (Gère : $"{locStr} V".AsLoc(), Start("Nom", ...), etc.)
                    var nameMatch = Regex.Match(declStr, @"Start\(\s*(?:\$""\{locStr\}\s*([^""]+)""(?:\.AsLoc\(\))?|[""']([^""']+)[""'])");
                    if (nameMatch.Success)
                    {
                        if (nameMatch.Groups[1].Success)
                        {
                            string suffix = nameMatch.Groups[1].Value.Trim();
                            machine.Name = $"{baseName} {suffix}".Trim();
                        }
                        else
                        {
                            machine.Name = nameMatch.Groups[2].Value.Trim();
                        }
                    }
                    else
                    {
                        machine.Name = varName;
                    }

                    // Nettoyage de secours si un tag "__name" subsiste
                    if (machine.Name.Contains("__name"))
                    {
                        machine.Name = Regex.Replace(machine.Name, @"\w+__name\s*", "Assembly ").Trim();
                    }


                    // ID
                    var idMatch = Regex.Match(declStr, @"Ids\.Machines\.(\w+)");
                    if (idMatch.Success) machine.Id = idMatch.Groups[1].Value;


                    // Prefab / Icône
                    var prefabMatch = Regex.Match(declStr, @"SetPrefabPath\([""']([^""']+)[""']\)");
                    if (prefabMatch.Success) machine.IconOrPrefab = prefabMatch.Groups[1].Value;
                    else
                    {
                        var iconMatch = Regex.Match(declStr, @"SetIcon\([""']([^""']+)[""']\)");
                        if (iconMatch.Success) machine.IconOrPrefab = iconMatch.Groups[1].Value;
                    }


                    // Description
                    var descMatch = Regex.Match(declStr, @"Description\(([^)]+)\)");
                    if (descMatch.Success)
                    {
                        string descArg = descMatch.Groups[1].Value.Trim();
                        if (descVars.TryGetValue(descArg, out var resolvedDesc))
                        {
                            machine.Description = resolvedDesc;
                        }
                        else
                        {
                            var literalMatch = Regex.Match(descArg, @"\""([^\""]+)\""");
                            if (literalMatch.Success) machine.Description = literalMatch.Groups[1].Value;
                        }
                    }


                    // Extraction du coût et du nombre de Workers
                    // Ex: .SetCost(Costs.Machines.AssemblyRoboticT1) ou .SetCost(Costs.Machines.Mixer)
                    var costMatch = Regex.Match(declStr, @"SetCost\(\s*Costs\.Machines\.(\w+)");
                    if (costMatch.Success)
                    {
                        string costKey = costMatch.Groups[1].Value;
                        if (costsCatalog.TryGetValue(costKey, out var costInfo))
                        {
                            machine.Workers = costInfo.Workers;
                            machine.Maintenance = costInfo.Maintenance
                                .Select(m => new CostItem { Quantity = m.Quantity, ProductId = m.ProductId })
                                .ToList();
                            machine.ConstructionCost = costInfo.Materials
                                .Select(m => new CostItem { Quantity = m.Quantity, ProductId = m.ProductId })
                                .ToList();
                        }
                    }



                    machineVarMap[varName] = machine;
                    localMachines.Add(machine);
                }
            }



            // =========================================================================
            // 2b. Extraction des métadonnées (Proto.CreateStr, Loc.Str) et des prototypes directs
            // =========================================================================

            var protoNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var protoDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // A. Dictionnaire de résolution des variables locales d'ID (ex: StaticEntityProto.ID id = Ids.Machines.SolarPanel;)
            var idVarMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var idDeclMatches = Regex.Matches(code, @"(\w+)\s*=\s*Ids\.Machines\.(\w+);");
            foreach (Match m in idDeclMatches)
            {
                idVarMap[m.Groups[1].Value] = m.Groups[2].Value; // "id" -> "SolarPanel", "id2" -> "SolarPanelMono"
            }

            // B. Extraction des descriptions déclarées via Loc.Str / Loc.Str1 (ex: Ids.Machines.SolarPanelMono... + "__desc")
            var locDescMatches = Regex.Matches(code,
                @"Loc\.Str(?:1)?\(\s*(?:Ids\.Machines\.)?(\w+)(?:\.ToString\(\)|\.Value)?\s*\+\s*""__desc""\s*,\s*""([^""]+)""");
            foreach (Match m in locDescMatches)
            {
                protoDescriptions[m.Groups[1].Value] = m.Groups[2].Value;
            }

            // C. Extraction des noms et descriptions via Proto.CreateStr ou Proto.CreateStrFormatDesc1
            // Gère à la fois Ids.Machines.ID et les variables locales résolues (ex: Proto.CreateStr(id, "Solar panel", ...))
            var protoStrMatches = Regex.Matches(code,
                @"Proto\.(?:CreateStr|CreateStrFormatDesc1)\(\s*(?:Ids\.Machines\.)?(\w+)\s*,\s*""([^""]+)""(?:,\s*""([^""]*)"")?");
            foreach (Match m in protoStrMatches)
            {
                string rawId = m.Groups[1].Value;
                string pId = idVarMap.TryGetValue(rawId, out var mappedId) ? mappedId : rawId;
                string pName = m.Groups[2].Value;

                protoNames[pId] = pName;

                if (m.Groups[3].Success && !string.IsNullOrWhiteSpace(m.Groups[3].Value))
                {
                    protoDescriptions[pId] = m.Groups[3].Value;
                }
            }

            // C2. Extraction des noms déclarés via des builders alternatifs :
            // Ex: WellPumpProtoBuilder.Start("Oil pump", ..., Ids.Machines.OilPump)
            // Ou: wellInjectionPumpProtoBuilder.Start("Gas injection pump", Ids.Machines.GasInjectionPump)
            var builderStartMatches = Regex.Matches(code,
                @"\.Start\(\s*""([^""]+)""[\s\S]*?Ids\.Machines\.(\w+)");
            foreach (Match m in builderStartMatches)
            {
                string bName = m.Groups[1].Value.Trim();
                string bId = m.Groups[2].Value.Trim();
                if (!protoNames.ContainsKey(bId))
                {
                    protoNames[bId] = bName;
                }
            }

            // Extraction de la description Description("...", ...) si présente après .Start
            var builderDescMatches = Regex.Matches(code,
                @"Ids\.Machines\.(\w+)[\s\S]*?\.Description\(\s*""([^""]+)""");
            foreach (Match m in builderDescMatches)
            {
                string bId = m.Groups[1].Value.Trim();
                string bDesc = m.Groups[2].Value.Trim();
                if (!protoDescriptions.ContainsKey(bId))
                {
                    protoDescriptions[bId] = bDesc;
                }
            }


            // D. Extraction et enregistrement de chaque machine référencée par Ids.Machines dans le fichier
            var allMachineIdMatches = Regex.Matches(code, @"Ids\.Machines\.(\w+)");
            var distinctMachineIds = allMachineIdMatches.Select(m => m.Groups[1].Value).Distinct().ToList();

            foreach (string mId in distinctMachineIds)
            {
                // Vérifier si la machine n'a pas déjà été créée (par MachineProtoBuilder ou PowerGenerators)
                var existing = localMachines.FirstOrDefault(m => m.Id == mId) ?? data.Machines.FirstOrDefault(m => m.Id == mId);
                if (existing != null)
                {
                    // Si le nom était resté l'ID brut ou __desc, on lui applique son vrai nom
                    if (protoNames.TryGetValue(mId, out var rName)) existing.Name = rName;
                    if (string.IsNullOrEmpty(existing.Description) && protoDescriptions.TryGetValue(mId, out var rDesc)) existing.Description = rDesc;
                    continue;
                }

                string displayName = protoNames.TryGetValue(mId, out var resolvedName) ? resolvedName : mId;
                string displayDesc = protoDescriptions.TryGetValue(mId, out var resolvedDesc) ? resolvedDesc : string.Empty;

                var newMachine = new MachineModel
                {
                    VariableName = mId,
                    Id = mId,
                    Name = displayName,
                    Description = displayDesc,
                    SourceFile = sourceFileName
                };

                // Prefab / Icône
                var pfbMatch = Regex.Match(code, $@"(?:{mId}|new\s+\w+Proto)[\s\S]*?\""([^\""]+\.prefab)\""");
                if (pfbMatch.Success)
                {
                    newMachine.IconOrPrefab = pfbMatch.Groups[1].Value;
                }

                // Coûts depuis costsCatalog
                string costKey = mId;
                if (costsCatalog.TryGetValue(costKey, out var cInfo) ||
                    costsCatalog.TryGetValue(mId.Replace("T1", "").Replace("Large", ""), out cInfo) ||
                    costsCatalog.TryGetValue(mId.Replace("Mono", ""), out cInfo))
                {
                    newMachine.Workers = cInfo.Workers;
                    newMachine.Maintenance = cInfo.Maintenance;
                    newMachine.ConstructionCost = cInfo.Materials
                        .Select(mat => new CostItem { Quantity = mat.Quantity, ProductId = mat.ProductId })
                        .ToList();
                }

                // Consommation / production électrique
                var elecMatch = Regex.Match(code, $@"(?:{mId})[\s\S]*?(\d+)\.Kw\(\)");
                if (elecMatch.Success)
                {
                    newMachine.ElectricityConsumptionKW = int.Parse(elecMatch.Groups[1].Value);
                }

                localMachines.Add(newMachine);
                machineVarMap[mId] = newMachine;
            }

            // Associer les variables de variables locales aux machines
            foreach (var kvp in idVarMap)
            {
                var matched = localMachines.FirstOrDefault(m => m.Id == kvp.Value);
                if (matched != null)
                {
                    machineVarMap[kvp.Key] = matched; // Associe "id" -> SolarPanel, "id2" -> SolarPanelMono
                }
            }



            // 3. Chaînage des Tiers (SetNextTier)
            foreach (var expr in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var exprStr = expr.ToString();

                if (exprStr.Contains(".SetNextTier(") || exprStr.Contains(".SetNextTierIndirect("))
                {
                    //var match = Regex.Match(exprStr, @"(\w+)\.SetNextTier\((\w+)\)");
                    var match = Regex.Match(exprStr, @"(\w+)\.SetNextTier(?:Indirect)?\((\w+)\)");

                    if (match.Success)
                    {
                        string currentVar = match.Groups[1].Value;
                        string nextVar = match.Groups[2].Value;

                        if (machineVarMap.TryGetValue(currentVar, out var curr) &&
                            machineVarMap.TryGetValue(nextVar, out var next))
                        {
                            curr.NextTierId = next.Id;
                        }
                    }
                }
            }

            data.Machines.AddRange(localMachines);


            // 4. Extraction des variables de durée (locales ET statiques comme DURATION = 20.Seconds())
            var durationVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // A. Variables statiques assignées dans la classe ou dans le constructeur statique (ex: DURATION = 20.Seconds();)
            var staticDurationMatches = Regex.Matches(code, @"([A-Za-z0-9_]+)\s*=\s*(?:Duration\.)?([0-9\.]+)\.Seconds\(\);");
            foreach (Match m in staticDurationMatches)
            {
                durationVars[m.Groups[1].Value] = m.Groups[2].Value; // Enregistre "20"
            }

            // B. Déclarations de variables locales (ex: Duration duration = 20.Seconds(); ou FromKeyframes)
            foreach (var localDecl in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                string declStr = localDecl.ToString();
                if (declStr.Contains("Duration"))
                {
                    foreach (var variable in localDecl.Declaration.Variables)
                    {
                        if (variable.Initializer != null)
                        {
                            string initVal = variable.Initializer.Value.ToString();
                            var secMatch = Regex.Match(initVal, @"([0-9\.]+)\.Seconds\(\)");
                            if (secMatch.Success)
                            {
                                durationVars[variable.Identifier.Text] = secMatch.Groups[1].Value;
                            }
                            else if (initVal.Contains("FromKeyframes"))
                            {
                                var kfMatch = Regex.Match(initVal, @"FromKeyframes\((\d+)\)");
                                if (kfMatch.Success)
                                {
                                    durationVars[variable.Identifier.Text] = kfMatch.Groups[1].Value + "kf";
                                }
                            }
                        }
                    }
                }
            }

            // 5. Extraction unifiée de chaque recette individuelle
            var recipeInvocations = root.DescendantNodes()
                                        .OfType<InvocationExpressionSyntax>()
                                        .Where(i => i.ToString().StartsWith("registrator.RecipeProtoBuilder.Start") ||
                                                    i.ToString().Contains(".RecipeProtoBuilder.Start"));

            foreach (var invocation in recipeInvocations)
            {
                var statement = invocation.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
                if (statement == null) continue;

                string stmtStr = statement.ToString();

                var idMatch = Regex.Match(stmtStr, @"RecipeProtoBuilder\.Start\(\s*Ids\.Recipes\.(\w+)\s*\)");
                if (!idMatch.Success) continue;

                string recipeId = idMatch.Groups[1].Value;

                if (data.Recipes.Any(r => r.RecipeId == recipeId)) continue;

                var recipe = new RecipeModel
                {
                    RecipeId = recipeId,
                    SourceFile = sourceFileName
                };

                // On passe désormais intVars à ParseInputsOutputs !
                ParseInputsOutputs(stmtStr, recipe, productsCatalog, numVars);


                // Extraction des BindTo supportant les chiffres directs et les variables (ex: duration2)
                //var bindMatches = Regex.Matches(stmtStr, @"BindTo\((\w+),\s*([A-Za-z0-9_\.]+?)(?:\.Seconds\(\))?(?:,\s*(\d+))?\)");

                // 1. Regex acceptant des expressions comme "2 * OilDistillationData.DURATION" ou "20.Seconds()"
                var bindMatches = Regex.Matches(stmtStr, @"BindTo\((\w+),\s*([^,\)]+?)(?:,\s*(\d+))?\)");

                foreach (Match b in bindMatches)
                {
                    string machineVar = b.Groups[1].Value;
                    string durationExpr = b.Groups[2].Value.Trim();
                    int multiplier = b.Groups[3].Success ? int.Parse(b.Groups[3].Value) : 1;

                    // Nettoyage complet des suffixes .Seconds(), parenthèses superflues et espaces
                    durationExpr = Regex.Replace(durationExpr, @"\.?Seconds(?:\(\))?", "", RegexOptions.IgnoreCase)
                                        .Replace("(", "")
                                        .Replace(")", "")
                                        .Trim();

                    // 2. Évaluation de l'expression
                    string duration = EvaluateDurationExpression(durationExpr, durationVars);

                    // Sécurité supplémentaire : s'assurer qu'aucune parenthèse ne persiste dans DurationSeconde
                    duration = duration.Replace("(", "").Replace(")", "").TrimEnd('s').Trim();

                    //string targetMachineId = machineVarMap.TryGetValue(machineVar, out var m) ? m.Id : machineVar;


                    // Résolution robuste de la machine cible (Priorité 1)
                    string targetMachineId;

                    // 1. Résolution prioritaire via le mapping de variables (oceanWaterPumpProto -> OceanWaterPumpT1, etc.)
                    if (machineVarMap.TryGetValue(machineVar, out var m))
                    {
                        targetMachineId = m.Id;
                    }
                    // 2. Vérification par nom de recette si jamais la variable n'a pas été trouvée
                    else if (recipeId.StartsWith("OceanWaterPumpingT2", StringComparison.OrdinalIgnoreCase) ||
                             recipeId.StartsWith("OceanWaterPumping2xT2", StringComparison.OrdinalIgnoreCase))
                    {
                        targetMachineId = "OceanWaterPumpLarge";
                    }
                    else if (recipeId.StartsWith("OceanWaterPumping", StringComparison.OrdinalIgnoreCase))
                    {
                        targetMachineId = "OceanWaterPumpT1";
                    }
                    // 3. Correspondance directe avec une machine connue
                    else if (data.Machines.Any(mac => mac.Id.Equals(machineVar, StringComparison.OrdinalIgnoreCase)))
                    {
                        targetMachineId = data.Machines.First(mac => mac.Id.Equals(machineVar, StringComparison.OrdinalIgnoreCase)).Id;
                    }
                    // 4. Repli sur les machines déclarées dans le fichier
                    else if (localMachines.Count > 0)
                    {
                        if (machineVar.Equals("machine", StringComparison.OrdinalIgnoreCase) ||
                            machineVar.Equals("machineProto", StringComparison.OrdinalIgnoreCase))
                        {
                            targetMachineId = localMachines[0].Id;
                        }
                        else
                        {
                            targetMachineId = ResolveFallbackMachineId(machineVar, localMachines);
                        }
                    }
                    else
                    {
                        targetMachineId = machineVar switch
                        {
                            "wellPumpProto" => "WellPump",
                            "wellPumpProto2" => "WellPumpT2",
                            _ => machineVar
                        };
                    }

                    // Cas particulier pour CarbonDioxideInjection -> GasInjectionPump
                    // WellPumpsData.cs : Recipe CarbonDioxideInjection était liée à OilPump au lieu de GasInjectionPump
                    if (recipeId.Equals("CarbonDioxideInjection", StringComparison.OrdinalIgnoreCase))
                    {
                        targetMachineId = "GasInjectionPump";
                    }



                    recipe.MachineBindings.Add(new MachineBinding
                    {
                        MachineId = targetMachineId,
                        DurationSeconde = duration,
                        OutputMultiplier = multiplier
                    });
                }


                data.Recipes.Add(recipe);
            }
        }


        private string ResolveFallbackMachineId(string varName, List<MachineModel> localMachines)
        {
            if (localMachines.Count == 0) return varName;

            // Détection du tier par suffixe numérique (machineProto2 -> T2, etc.)
            var tierMatch = Regex.Match(varName, @"(\d+)$");
            if (tierMatch.Success && int.TryParse(tierMatch.Groups[1].Value, out int tierNum))
            {
                if (tierNum >= 1 && tierNum <= localMachines.Count)
                {
                    return localMachines[tierNum - 1].Id;
                }
            }

            return localMachines[0].Id;
        }


        private string EvaluateDurationExpression(string expr, Dictionary<string, string> durationVars)
        {
            expr = expr.Trim();

            // Cas d'une multiplication : ex "2 * OilDistillationData.DURATION"
            var mulMatch = Regex.Match(expr, @"^(\d+)\s*\*\s*(.+)$");
            if (mulMatch.Success)
            {
                int factor = int.Parse(mulMatch.Groups[1].Value);
                string rightPart = mulMatch.Groups[2].Value.Trim();
                string resolvedRight = ResolveDurationToken(rightPart, durationVars);

                if (double.TryParse(resolvedRight, out double val))
                {
                    return (factor * val).ToString();
                }
                return $"{factor * 20}"; // Repli par défaut si DURATION est supposé à 20s
            }

            return ResolveDurationToken(expr, durationVars);
        }


        private string ResolveDurationToken(string token, Dictionary<string, string> durationVars)
        {
            token = token.Trim();

            // Chiffre direct (ex: "20" ou "20s")
            if (double.TryParse(token.TrimEnd('s'), out _))
            {
                return token.TrimEnd('s');
            }

            // Gestion de la référence externe "OilDistillationData.DURATION"
            if (token.Contains("OilDistillationData.DURATION") || token.Equals("DURATION", StringComparison.OrdinalIgnoreCase))
            {
                return "20"; // Durée standard de distillation
            }

            // Recherche dans les variables locales du fichier
            if (durationVars.TryGetValue(token, out var resolved))
            {
                return resolved.TrimEnd('s');
            }

            return token;
        }


        // Détection tolérant les arguments optionnels (ports "D", "E" ou outputAtStart: true)
        // Méthode de parsing des inputs et outputs avec prise en compte des variables entières
        private void ParseInputsOutputs(string code, RecipeModel recipe, Dictionary<string, ProductInfo> productsCatalog, Dictionary<string, double> numVars)
        {
            // Regex acceptant soit des chiffres décimaux ([0-9\.]+), soit un nom de variable ([A-Za-z0-9_]+)
            var inMatches = Regex.Matches(code, @"AddInput\(\s*([A-Za-z0-9_\.]+)\s*,\s*Ids\.Products\.(\w+)(?:,[^)]*)?\)");

            foreach (Match m in inMatches)
            {
                string rawQty = m.Groups[1].Value;
                string prodId = m.Groups[2].Value;

                double qty = 1.0;
                if (double.TryParse(rawQty, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double directQty))
                {
                    qty = directQty;
                }
                else if (numVars.TryGetValue(rawQty, out double varQty))
                {
                    qty = varQty;
                }

                var item = new RecipeItem { Quantity = qty, ProductId = prodId };
                if (productsCatalog.TryGetValue(prodId, out var prodInfo))
                {
                    item.ProductName = prodInfo.Name;
                    item.TransportType = prodInfo.TransportType;
                    item.Color = prodInfo.Color;
                    item.IconPath = prodInfo.IconPath;
                }

                recipe.Inputs.Add(item);
            }

            var outMatches = Regex.Matches(code, @"AddOutput\(\s*([A-Za-z0-9_\.]+)\s*,\s*Ids\.Products\.(\w+)(?:,[^)]*)?\)");
            foreach (Match m in outMatches)
            {
                string rawQty = m.Groups[1].Value;
                string prodId = m.Groups[2].Value;

                double qty = 1.0;
                if (double.TryParse(rawQty, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double directQty))
                {
                    qty = directQty;
                }
                else if (numVars.TryGetValue(rawQty, out double varQty))
                {
                    qty = varQty;
                }

                var item = new RecipeItem { Quantity = qty, ProductId = prodId };
                if (productsCatalog.TryGetValue(prodId, out var prodInfo))
                {
                    item.ProductName = prodInfo.Name;
                    item.TransportType = prodInfo.TransportType;
                    item.Color = prodInfo.Color;
                    item.IconPath = prodInfo.IconPath;
                }

                recipe.Outputs.Add(item);
            }

        }


        // Ajout de la méthode de changement de langue :
        private void CmbLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbLanguage.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string lang)
            {
                LocalizationManager.Instance.CurrentLanguage = lang;

                // Mise à jour du texte de statut si des données ont déjà été chargées
                if (_data.Products.Count > 0 || _data.Machines.Count > 0 || _data.Recipes.Count > 0)
                {
                    LblStatus.Text = LocalizationManager.Instance.GetStatusDone(_data.Products.Count, _data.Machines.Count, _data.Recipes.Count);
                    LblLogSummary.Text = LocalizationManager.Instance.GetLogSummary(_data.Products.Count, _data.Machines.Count, _data.Recipes.Count);
                }
            }
        }


    }

}