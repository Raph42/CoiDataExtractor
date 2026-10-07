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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32;

namespace CoiDataExtractor
{
    // ==========================================
    // Modèles de données
    // ==========================================
    public class ExtractedData
    {
        [JsonPropertyOrder(-6)]
        [JsonPropertyName("_generator")]
        public string Generator { get; set; } = $"CoiDataExtractor v{MainWindow.AppVersion}";

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
        public int Quantity { get; set; }
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
        public string ElectricityConsumption { get; set; } = "0 kW";

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
        public string? NextTierId { get; set; }
    }


    public class RecipeItem
    {
        public int Quantity { get; set; }
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
        public string Duration { get; set; } = string.Empty;
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

        [JsonIgnore]
        public string BindingsDisplay => string.Join(" | ", MachineBindings.Select(b =>
            $"{b.MachineId} ({b.Duration}{(b.OutputMultiplier > 1 ? $", x{b.OutputMultiplier}" : "")})"));
    }


    public partial class MainWindow : Window
    {
        private ExtractedData _data = new();
        public const string AppVersion = "1.03";


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
                try
                {
                    string idsCode = File.ReadAllText(idsFile);
                    ParseProducts(idsCode, productsCatalog);
                }
                catch
                {
                }
                files.Remove(idsFile);
            }

            // 2. Traitement prioritaire de Costs.cs
            var costsFile = files.FirstOrDefault(f => Path.GetFileName(f).Equals("Costs.cs", StringComparison.OrdinalIgnoreCase));
            if (costsFile != null)
            {
                try
                {
                    string costsCode = File.ReadAllText(costsFile);
                    ParseCosts(costsCode, costsCatalog);
                }
                catch { }
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
                try
                {
                    string code = File.ReadAllText(file);
                    string fileName = Path.GetFileName(file);
                    ParseSourceCode(code, aggregatedData, productsCatalog, costsCatalog, fileName);
                }
                catch
                {
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
                            Quantity = (int)Math.Round(rawVal),
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
                    costInfo.Materials.Add(new CostItem
                    {
                        ProductId = sm.Groups[1].Value,
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
            var intVars = new Dictionary<string, int>();
            string baseName = "Machine";

            // 1. Textes et variables locales (Loc.Str et int quantity)
            foreach (var localDecl in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                var text = localDecl.ToString();
                if (text.Contains("__name"))
                {
                    var match = Regex.Match(text, @"\""([^\""]+)\""");
                    if (match.Success) baseName = match.Groups[1].Value;
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
                        else if (int.TryParse(initVal, out int parsedInt))
                        {
                            intVars[variable.Identifier.Text] = parsedInt;
                        }
                    }

                }

            }

            // 2. Extraction des machines (conservée impérativement)
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


                    // Électricité
                    // Extraction de la consommation électrique
                    // Gère : .SetElectricityConsumption(Electricity.FromKw(500)), .SetElectricityConsumption(200.Kw()), etc.
                    var elecMatch = Regex.Match(declStr, @"SetElectricityConsumption\(\s*(?:Electricity\.FromKw\((\d+)\)|(\d+(?:\.\d+)?)\.(Kw|Mw)\(\))\s*\)");
                    if (elecMatch.Success)
                    {
                        if (elecMatch.Groups[1].Success)
                        {
                            machine.ElectricityConsumption = $"{elecMatch.Groups[1].Value} kW";
                        }
                        else
                        {
                            string val = elecMatch.Groups[2].Value;
                            string unit = elecMatch.Groups[3].Value.ToUpper();
                            machine.ElectricityConsumption = $"{val} {unit}";
                        }
                    }
                    else
                    {
                        // Recherche alternative plus permissive si la syntaxe varie
                        var genericElecMatch = Regex.Match(declStr, @"SetElectricityConsumption\(([^)]+)\)");
                        if (genericElecMatch.Success)
                        {
                            string raw = genericElecMatch.Groups[1].Value.Trim();
                            // Nettoyage rapide si c'est une valeur simple
                            machine.ElectricityConsumption = raw.Replace("Electricity.", "").Replace("()", "");
                        }
                    }


                    // Nom
                    var nameMatch = Regex.Match(declStr, @"Start\(\$""\{locStr\}\s*([^""]+)""");
                    if (nameMatch.Success)
                    {
                        machine.Name = $"{baseName} {nameMatch.Groups[1].Value.Trim()}";
                    }
                    else
                    {
                        var fallbackNameMatch = Regex.Match(declStr, @"Start\([""']([^""']+)[""']");
                        machine.Name = fallbackNameMatch.Success ? fallbackNameMatch.Groups[1].Value : varName;
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


            // 3. Chaînage des Tiers (SetNextTier)
            foreach (var expr in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var exprStr = expr.ToString();
                if (exprStr.Contains(".SetNextTier("))
                {
                    var match = Regex.Match(exprStr, @"(\w+)\.SetNextTier\((\w+)\)");
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


            // 4. Extraction des variables de durée locales (duration, duration2, totalDuration...)
            var durationVars = new Dictionary<string, string>();

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
                                durationVars[variable.Identifier.Text] = secMatch.Groups[1].Value + "s";
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
                ParseInputsOutputs(stmtStr, recipe, productsCatalog, intVars);

                // Extraction des BindTo supportant les chiffres directs et les variables (ex: duration2)
                var bindMatches = Regex.Matches(stmtStr, @"BindTo\((\w+),\s*([A-Za-z0-9_\.]+?)(?:\.Seconds\(\))?(?:,\s*(\d+))?\)");
                foreach (Match b in bindMatches)
                {
                    string machineVar = b.Groups[1].Value;
                    string durationRaw = b.Groups[2].Value.Trim();
                    int multiplier = b.Groups[3].Success ? int.Parse(b.Groups[3].Value) : 1;

                    string duration;
                    if (double.TryParse(durationRaw, out _))
                    {
                        duration = durationRaw + "s";
                    }
                    else if (durationVars.TryGetValue(durationRaw, out var resolvedDur))
                    {
                        duration = resolvedDur;
                    }
                    else
                    {
                        duration = durationRaw;
                    }

                    string targetMachineId = machineVarMap.TryGetValue(machineVar, out var m) ? m.Id : machineVar;

                    recipe.MachineBindings.Add(new MachineBinding
                    {
                        MachineId = targetMachineId,
                        Duration = duration,
                        OutputMultiplier = multiplier
                    });
                }

                data.Recipes.Add(recipe);
            }
        }

        // Détection tolérant les arguments optionnels (ports "D", "E" ou outputAtStart: true)
        // Méthode de parsing des inputs et outputs avec prise en compte des variables entières
        private void ParseInputsOutputs(string code, RecipeModel recipe, Dictionary<string, ProductInfo> productsCatalog, Dictionary<string, int> intVars)
        {
            // Regex acceptant soit des chiffres (\d+), soit un nom de variable ([A-Za-z0-9_]+)
            var inMatches = Regex.Matches(code, @"AddInput\(\s*([A-Za-z0-9_]+)\s*,\s*Ids\.Products\.(\w+)(?:,[^)]*)?\)");
            foreach (Match m in inMatches)
            {
                string rawQty = m.Groups[1].Value;
                string prodId = m.Groups[2].Value;

                int qty = 1;
                if (int.TryParse(rawQty, out int directQty))
                {
                    qty = directQty;
                }
                else if (intVars.TryGetValue(rawQty, out int varQty))
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

            var outMatches = Regex.Matches(code, @"AddOutput\(\s*([A-Za-z0-9_]+)\s*,\s*Ids\.Products\.(\w+)(?:,[^)]*)?\)");
            foreach (Match m in outMatches)
            {
                string rawQty = m.Groups[1].Value;
                string prodId = m.Groups[2].Value;

                int qty = 1;
                if (int.TryParse(rawQty, out int directQty))
                {
                    qty = directQty;
                }
                else if (intVars.TryGetValue(rawQty, out int varQty))
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
                }
            }
        }


    }

}