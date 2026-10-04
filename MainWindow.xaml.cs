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
    public class ExtractedData
    {
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

    public class MachineModel
    {
        [JsonIgnore]
        public string VariableName { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconOrPrefab { get; set; } = string.Empty;
        public string? NextTierId { get; set; }
    }

    public class RecipeItem
    {
        public int Quantity { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string TransportType { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
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

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Sélectionner le dossier contenant les fichiers .cs"
            };

            if (dialog.ShowDialog() == true)
            {
                string folder = dialog.FolderName;
                TxtFolderPath.Text = folder;
                LblStatus.Text = "Analyse des fichiers C# en cours...";
                BtnSaveJson.IsEnabled = false;

                _data = await Task.Run(() => ProcessFolder(folder));

                DgProducts.ItemsSource = _data.Products;
                DgMachines.ItemsSource = _data.Machines;
                DgRecipes.ItemsSource = _data.Recipes;

                LblStatus.Text = $"Terminé : {_data.Products.Count} produit(s), {_data.Machines.Count} machine(s) et {_data.Recipes.Count} recette(s).";
                BtnSaveJson.IsEnabled = _data.Products.Count > 0 || _data.Machines.Count > 0 || _data.Recipes.Count > 0;
            }
        }

        private void BtnSaveJson_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Fichier JSON (*.json)|*.json",
                FileName = "captain_of_industry_data.json"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(_data, options);
                File.WriteAllText(saveDialog.FileName, json);

                MessageBox.Show($"Données enregistrées avec succès dans :\n{saveDialog.FileName}",
                    "Exportation réussie", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private ExtractedData ProcessFolder(string folderPath)
        {
            var aggregatedData = new ExtractedData();
            var productsCatalog = new Dictionary<string, ProductInfo>(StringComparer.OrdinalIgnoreCase);

            var fallbackColors = LoadFallbackColors(folderPath);
            var files = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories).ToList();

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

            foreach (var file in files)
            {
                try
                {
                    string code = File.ReadAllText(file);
                    ParseSourceCode(code, aggregatedData, productsCatalog);
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

                // Recherche de l'icône explicite (.svg ou .png)[cite: 3]
                var iconMatch = Regex.Match(args, @"\""([^\""]+\.(?:svg|png))\""");
                if (iconMatch.Success)
                {
                    product.IconPath = iconMatch.Groups[1].Value;
                    product.IsFallbackIcon = false;
                }
                else
                {
                    // Déduction automatique de l'icône si absente
                    product.IconPath = $"Assets/Base/Products/Icons/{varId}.svg";
                    product.IsFallbackIcon = true;
                }

                // Recherche du nom lisible[cite: 3]
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

                // Détection couleur décimale native[cite: 3]
                var colorMatch = Regex.Match(args, @"(?<![A-Za-z0-9_])([1-9][0-9]{6,8})(?![A-Za-z0-9_])");
                if (colorMatch.Success && long.TryParse(colorMatch.Groups[1].Value, out long colorVal))
                {
                    product.Color = ConvertColorToHex(colorVal);
                    product.IsFallbackColor = false;
                }

                catalog[varId] = product;
            }
        }

        private void ParseSourceCode(string code, ExtractedData data, Dictionary<string, ProductInfo> productsCatalog)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var localMachines = new List<MachineModel>();
            var machineVarMap = new Dictionary<string, MachineModel>();
            var descVars = new Dictionary<string, string>();
            string baseName = "Machine";

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
                    if (variable.Initializer != null && variable.Initializer.Value.ToString().Contains("Loc.Str"))
                    {
                        var matches = Regex.Matches(variable.Initializer.Value.ToString(), @"\""([^\""]*)\""");
                        if (matches.Count >= 2)
                        {
                            descVars[variable.Identifier.Text] = matches[1].Groups[1].Value;
                        }
                    }
                }
            }

            foreach (var localDecl in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                var declStr = localDecl.ToString();
                if (declStr.Contains("MachineProtoBuilder.Start"))
                {
                    var variable = localDecl.Declaration.Variables.FirstOrDefault();
                    if (variable == null) continue;

                    string varName = variable.Identifier.Text;
                    var machine = new MachineModel { VariableName = varName };

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

                    var idMatch = Regex.Match(declStr, @"Ids\.Machines\.(\w+)");
                    if (idMatch.Success) machine.Id = idMatch.Groups[1].Value;

                    var prefabMatch = Regex.Match(declStr, @"SetPrefabPath\([""']([^""']+)[""']\)");
                    if (prefabMatch.Success) machine.IconOrPrefab = prefabMatch.Groups[1].Value;
                    else
                    {
                        var iconMatch = Regex.Match(declStr, @"SetIcon\([""']([^""']+)[""']\)");
                        if (iconMatch.Success) machine.IconOrPrefab = iconMatch.Groups[1].Value;
                    }

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

                    machineVarMap[varName] = machine;
                    localMachines.Add(machine);
                }
            }

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

            foreach (var expr in root.DescendantNodes().OfType<ExpressionStatementSyntax>())
            {
                var stmtStr = expr.ToString();
                if (stmtStr.Contains("RecipeProtoBuilder.Start"))
                {
                    var recipe = ParseRecipeCall(stmtStr, machineVarMap, productsCatalog);
                    if (recipe != null)
                    {
                        data.Recipes.Add(recipe);
                    }
                }
            }

            foreach (var localFunc in root.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            {
                var bodyStr = localFunc.ToString();
                if (bodyStr.Contains("RecipeProtoBuilder.Start"))
                {
                    var recipe = new RecipeModel();
                    string funcName = localFunc.Identifier.Text;
                    var invocation = root.DescendantNodes()
                                         .OfType<InvocationExpressionSyntax>()
                                         .FirstOrDefault(i => i.Expression.ToString() == funcName);

                    if (invocation != null)
                    {
                        var args = invocation.ArgumentList.Arguments.Select(a => a.ToString()).ToList();
                        if (args.Count >= 4)
                        {
                            recipe.RecipeId = args[1].Replace("Ids.Recipes.", "");
                            string machineVar = args[2];
                            string duration = args[3].Replace(".Seconds()", "s");

                            string machineId = machineVarMap.TryGetValue(machineVar, out var m) ? m.Id : machineVar;
                            recipe.MachineBindings.Add(new MachineBinding
                            {
                                MachineId = machineId,
                                Duration = duration,
                                OutputMultiplier = 1
                            });
                        }
                    }

                    ParseInputsOutputs(bodyStr, recipe, productsCatalog);
                    data.Recipes.Add(recipe);
                }
            }
        }

        private RecipeModel? ParseRecipeCall(string code, Dictionary<string, MachineModel> machineMap, Dictionary<string, ProductInfo> productsCatalog)
        {
            var recipe = new RecipeModel();
            var idMatch = Regex.Match(code, @"RecipeProtoBuilder\.Start\(Ids\.Recipes\.(\w+)\)");
            if (!idMatch.Success) return null;

            recipe.RecipeId = idMatch.Groups[1].Value;
            ParseInputsOutputs(code, recipe, productsCatalog);

            var bindMatches = Regex.Matches(code, @"BindTo\((\w+),\s*([0-9\.]+)\.Seconds\(\)(?:,\s*(\d+))?\)");
            foreach (Match b in bindMatches)
            {
                string machineVar = b.Groups[1].Value;
                string duration = b.Groups[2].Value + "s";
                int multiplier = b.Groups[3].Success ? int.Parse(b.Groups[3].Value) : 1;

                string targetMachineId = machineMap.TryGetValue(machineVar, out var m) ? m.Id : machineVar;

                recipe.MachineBindings.Add(new MachineBinding
                {
                    MachineId = targetMachineId,
                    Duration = duration,
                    OutputMultiplier = multiplier
                });
            }

            return recipe;
        }

        private void ParseInputsOutputs(string code, RecipeModel recipe, Dictionary<string, ProductInfo> productsCatalog)
        {
            var inMatches = Regex.Matches(code, @"AddInput\((\d+),\s*Ids\.Products\.(\w+)\)");
            foreach (Match m in inMatches)
            {
                int qty = int.Parse(m.Groups[1].Value);
                string prodId = m.Groups[2].Value;

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

            var outMatches = Regex.Matches(code, @"AddOutput\((\d+),\s*Ids\.Products\.(\w+)\)");
            foreach (Match m in outMatches)
            {
                int qty = int.Parse(m.Groups[1].Value);
                string prodId = m.Groups[2].Value;

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
    }
}