using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoiDataExtractor
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        public const string AppVersion = "1.032";
        public static LocalizationManager Instance { get; } = new();

        private string _currentLanguage = "en";
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged(string.Empty); // Notifie tous les bindings
                }
            }
        }

        public bool IsEnglish => CurrentLanguage == "en";

        // ==========================================
        // Textes traduits (Anglais par défaut)
        // ==========================================
        public string WindowTitle => IsEnglish ? "Captain of Industry - Data Extractor" : "Captain of Industry - Extracteur JSON";
        public string BtnBrowse => IsEnglish ? "📁 Select Folder..." : "📁 Choisir un dossier...";
        public string BtnSaveJson => IsEnglish ? "💾 Save As (JSON)..." : "💾 Enregistrer sous (JSON)...";

        public string LblGameVersion => IsEnglish ? "Game Version:" : "Version du jeu :";
        public string JsonCommentPrefix => IsEnglish ? "Exported from Captain of Industry version" : "Exporté depuis la version de Captain of Industry";
        
        // Onglets
        public string TabProducts => IsEnglish ? " 📦 Products " : " 📦 Produits ";
        public string TabMachines => IsEnglish ? " 🏭 Machines " : " 🏭 Machines ";
        public string TabRecipes => IsEnglish ? " ⚙️ Recipes " : " ⚙️ Recettes ";

        // Colonnes Produits
        public string ColProductId => IsEnglish ? "Product ID" : "Identifiant (ID)";
        public string ColProductName => IsEnglish ? "Product Name" : "Nom du Produit";
        public string ColTransportType => IsEnglish ? "Transport Type" : "Type de Convoyeur";
        public string ColColor => IsEnglish ? "Color" : "Couleur";
        public string ColIconPath => IsEnglish ? "Icon Path" : "Chemin Icône";

        // Colonnes Machines
        public string ColMachineName => IsEnglish ? "Name" : "Nom";
        public string ColMachineId => IsEnglish ? "Machine ID" : "Identifiant (ID)";
        public string ColElectricity => CurrentLanguage == "fr" ? "Électricité (KW)" : "Electricity (KW)";
        public string ColNextTier => IsEnglish ? "Next Tier" : "Tier Suivant";
        public string ColIconPrefab => IsEnglish ? "Icon / Prefab" : "Icône / Prefab";
        public string ColDescription => IsEnglish ? "Description" : "Description";

        // Colonnes Recettes
        public string ColSourceFile => IsEnglish ? "Source File" : "Fichier source";
        public string ColRecipeId => IsEnglish ? "Recipe ID" : "Recette ID";
        public string ColInputs => IsEnglish ? "Inputs" : "Ingrédients (Inputs)";
        public string ColOutputs => IsEnglish ? "Outputs" : "Produits (Outputs)";
        public string ColMachineDuration => IsEnglish ? "Machines & Duration" : "Machines & Durée";

        // Statuts & Dialogues
        public string StatusReady => IsEnglish ? "Please select a folder containing the game's source .cs files..." : "Veuillez sélectionner un dossier contenant les fichiers sources .cs...";
        public string StatusParsing => IsEnglish ? "Analyzing C# files..." : "Analyse des fichiers C# en cours...";
        public string StatusMissingIds => IsEnglish ? "Analysis canceled: Ids.cs file missing." : "Analyse annulée : fichier Ids.cs manquant.";
        public string DialogMissingIdsTitle => IsEnglish ? "Required File Missing" : "Fichier requis manquant";
        
        // Colonnes Cost
        public string ColWorkers => CurrentLanguage == "fr" ? "Ouvriers" : "Workers";
        public string ColCost => CurrentLanguage == "fr" ? "Coût de construction" : "Construction cost";
        public string ColMaintenance => CurrentLanguage == "fr" ? "Maintenance" : "Maintenance";


        // Message d'erreur si Costs.cs est manquant
        public string DialogMissingCostsText => CurrentLanguage == "fr"
            ? "Le fichier obligatoire « Costs.cs » est introuvable dans le dossier sélectionné.\n\nL'analyse a été annulée. Veuillez vérifier les fichiers sources décompilés du jeu."
            : "The required file 'Costs.cs' was not found in the selected folder.\n\nAnalysis has been aborted. Please verify the game decompiled source files.";

        public string StatusMissingCosts => CurrentLanguage == "fr"
            ? "Analyse annulée : fichier Costs.cs manquant."
            : "Analysis aborted: Costs.cs missing.";

        // Message d'erreur si Ids.cs est manquant
        public string DialogMissingIdsText => IsEnglish
            ? "The mandatory file \"Ids.cs\" was not found in the selected folder.\n\nAnalysis canceled. Please select the folder containing the game prototypes."
            : "Le fichier obligatoire « Ids.cs » est introuvable dans le dossier sélectionné.\n\nL'analyse a été annulée. Veuillez sélectionner le dossier contenant les fichiers sources du jeu.";
        public string DialogExportSuccessTitle => IsEnglish ? "Export Successful" : "Exportation réussie";
        public string DialogExportSuccessText => IsEnglish ? "Data successfully saved to:\n" : "Données enregistrées avec succès dans :\n";
        public string OpenFolderDialogTitle => IsEnglish ? "Select folder containing .cs files" : "Sélectionner le dossier contenant les fichiers .cs";

        public string GetStatusDone(int productsCount, int machinesCount, int recipesCount)
        {
            return IsEnglish
                ? $"Done: {productsCount} product(s), {machinesCount} machine(s), and {recipesCount} recipe(s) found."
                : $"Terminé : {productsCount} produit(s), {machinesCount} machine(s) et {recipesCount} recette(s).";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}