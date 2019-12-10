using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Lawnchair
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    long version = 20191210001;

    Settings settings = null;

    String searchPlaceholderText = "Search by author, category, name or tags. Partial matches will be shown.";

    DebugWindow debugWindow = new DebugWindow();
    List<LawnchairMetadata> cachedMetadataList = new List<LawnchairMetadata>(); // All script metadata
    List<LawnchairMetadata> filteredMetadataList = new List<LawnchairMetadata>(); // Script metadata (sourced from cachedMetadataList) that is then filtered by keywords typed into the searchbox
    ICollectionView scriptRepositoryListViewCollectionView = null;

    /* 
     * The debug window can be shown by pressing [CTRL + D]. A RichTextBox in that window shows output that is useful for debugging. This method will update that content and auto-scroll down to the last line.
     */
    private void AppendToDebugOutput(String message)
    {
      debugWindow.debugOutputRichTextBox.AppendText("[" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "] " + message + Environment.NewLine);
      debugWindow.debugOutputRichTextBox.ScrollToEnd();
    }

    private void ExecuteScript(LawnchairMetadata metadata)
    {
      try
      {
        String scriptFullPath = metadata.ScriptRootPath + metadata.ScriptRelativePath;
        MessageBoxResult response = MessageBox.Show(
            metadata.Name + Environment.NewLine +
            "----------------" + Environment.NewLine +
            "Author comments:" + Environment.NewLine + Environment.NewLine +
            metadata.Comments + Environment.NewLine + Environment.NewLine +
            "----------------" + Environment.NewLine +
            "Click OK to run this script otherwise click CANCEL to return to the previous screen.",
            "Run Script?",
            MessageBoxButton.OKCancel);

        if (response == MessageBoxResult.OK)
        {
          String scriptArguments = metadata.ScriptArguments.Replace("@scriptFullPath", scriptFullPath);
          AppendToDebugOutput("Calling script executor [" + metadata.ScriptExecutor + "]");
          AppendToDebugOutput(metadata.ScriptExecutor + " " + scriptArguments);
          Process process = new Process
          {
            StartInfo = new ProcessStartInfo()
            {
              Arguments = scriptArguments,
              FileName = metadata.ScriptExecutor,
              UseShellExecute = true,
            }
          };
          process.Start();
          process.WaitForExit();
        }
      }
      catch (Exception exception)
      {
        HandleException(exception);
      }
    }

    private void FilterScripts(String searchString)
    {
      Dictionary<LawnchairMetadata, int> keywordRanking = new Dictionary<LawnchairMetadata, int>();
      Dictionary<LawnchairMetadata, int> wholeStringRanking = new Dictionary<LawnchairMetadata, int>();
      String[] keywords = searchString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      bool hideCategories = false; // Only true if metadata can be matched against the entire search string. Categories being shown would break ranking order in the search results.

      AppendToDebugOutput(@"\\\\\\\\\\\\\\\\\\\\\\\\\");
      AppendToDebugOutput("searchString [" + searchString + "]");
      // First rank by whole string -- if there are no spaces in the searchString just move on to split keywords logic
      if (searchString.Trim().Split().Count() > 1)
      {
        foreach (LawnchairMetadata metadata in cachedMetadataList)
        {
          if (IsMetadataMatch(metadata, searchString))
          {
            hideCategories = true; // Categories being shown would break ranking order in the search results.
            if (wholeStringRanking.ContainsKey(metadata))
            {
              wholeStringRanking[metadata]++;
            }
            else
            {
              wholeStringRanking.Add(metadata, 1);
            }
          }
        }
      }
      // Then rank by split keywords
      foreach (String keyword in keywords)
      {
        foreach (LawnchairMetadata metadata in cachedMetadataList.Where(o => wholeStringRanking.ContainsKey(o) == false))
        {
          if (IsMetadataMatch(metadata, keyword))
          {
            if (keywordRanking.ContainsKey(metadata))
            {
              keywordRanking[metadata]++;
            }
            else
            {
              keywordRanking.Add(metadata, 1);
            }
          }
        }
      }
      AppendToDebugOutput("-------------------------");
      AppendToDebugOutput("Ranking sorting:");
      Queue<LawnchairMetadata> rankedMetadata = new Queue<LawnchairMetadata>();
      foreach (KeyValuePair<LawnchairMetadata, int> kvp in wholeStringRanking.OrderBy(x => x.Value))
      {
        AppendToDebugOutput("[wholeString][" + kvp.Value + "] " + kvp.Key.Name);
        rankedMetadata.Enqueue(kvp.Key);
      }
      foreach (KeyValuePair<LawnchairMetadata, int> kvp in keywordRanking.OrderBy(x => x.Value))
      {
        AppendToDebugOutput("[keywordRanking][" + kvp.Value + "] " + kvp.Key.Name);
        rankedMetadata.Enqueue(kvp.Key);
      }
      AppendToDebugOutput("-------------------------");
      AppendToDebugOutput("Search ranking results:");
      foreach (LawnchairMetadata metadata in rankedMetadata)
      {
        AppendToDebugOutput(metadata.Name);
      }
      AppendToDebugOutput(@"\\\\\\\\\\\\\\\\\\\\\\\\\");

      ShowScripts(rankedMetadata, hideCategories);
    }

    private List<LawnchairMetadata> GetScriptRepositoryMetadataFiles(String scriptMetadataFilename, String scriptRepositoryPath)
    {
      List<LawnchairMetadata> metadataList = new List<LawnchairMetadata>();
      foreach (String metadataFile in Directory.EnumerateFiles(scriptRepositoryPath, settings.ScriptMetadataFilename, SearchOption.AllDirectories))
      {
        try
        {
          // Get the metadata JSON content as a string
          AppendToDebugOutput("Found " + settings.ScriptMetadataFilename + " at [" + metadataFile + "]");
          String json = File.ReadAllText(metadataFile);
          AppendToDebugOutput("Metadata file contents:");
          AppendToDebugOutput(json);

          // Replace placeholder content in the JSON string
          FileInfo metadataFileInfo = new FileInfo(metadataFile);
          String folderPath = metadataFileInfo.Directory.FullName;
          // If the JSON contains @category, replace with the name of the metadata file's parent folder. If the category value needs to be something else then the author can replace the @category in the JSON with a hardcoded category name
          if (json.Contains("@category"))
          {
            String category = folderPath.Replace(scriptRepositoryPath + @"\", null);
            json = json.Replace("\"@category\"", JsonConvert.ToString(category));
          }

          // If the JSON contains @scriptRootPath, replace with the name of the metadata file's parent folder's absolute path. If the scriptRootPath value needs to be something else then the author can replace the @scriptRootPath in the JSON with a hardcoded absolute path to the folder the script resides in
          if (json.Contains("@scriptRootPath"))
          {
            json = json.Replace("\"@scriptRootPath\"", JsonConvert.ToString(folderPath));
          }
          AppendToDebugOutput("JSON after formatting and variable expansions:");
          AppendToDebugOutput(json);
          if (json.Trim().First() == '[' && json.Trim().Last() == ']')
          {
            AppendToDebugOutput("JSON appears to have multiple sets of metadata");
            foreach (LawnchairMetadata metadata in JsonConvert.DeserializeObject<List<LawnchairMetadata>>(json))
            {
              metadataList.Add(metadata);
            }
          }
          else
          {
            LawnchairMetadata metadata = JsonConvert.DeserializeObject<LawnchairMetadata>(json);
            metadataList.Add(metadata);
          }
        }
        catch (Exception exception)
        {
          MessageBox.Show(
              "An error occurred while processing the metadata from " + "[" + metadataFile + "]." + Environment.NewLine + Environment.NewLine +
              "Generally, this means that a script has a badly formatted metadata file. We'll have to skip this script and it will not show in the list until that script's author corrects this problem." + Environment.NewLine + Environment.NewLine +
              "If you are the script author, more information on the specific exception can be found in the debug panel, accessible by pressing CTRL + D after dismissing this message.",
          "Error");
          AppendToDebugOutput("Exception thrown:");
          AppendToDebugOutput(exception.ToString());
        }
      }

      if (metadataList.Count == 0 && Directory.EnumerateFiles(scriptRepositoryPath, settings.ScriptMetadataFilename, SearchOption.AllDirectories).Count() > 0)
      {
        throw new Exception("Metadata files were found (recursively) under the starting root path [" + scriptRepositoryPath + "] but none were successfully loaded. Though it is possible all metadata files are incorrectly formatted the more likely scenario is an access problem to the scriptRepositoryPath or some other problem with this applications code.");
      }

      // Return to caller
      return metadataList;
    }

    private Settings GetSettings()
    {
      try
      {
        Settings settings = null;
        String settingsJson = "";

        // List all embedded resources
        AppendToDebugOutput("Embedded resource names:");
        foreach (String resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        {
          AppendToDebugOutput(resourceName);
        }
        String settingsFile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\lawnchair\settings.json";
        AppendToDebugOutput("Settings file path [" + settingsFile + "]");

        // If settings file does not exist (usually happens during first run)
        if (File.Exists(settingsFile) == false)
        {
          AppendToDebugOutput("Settings file does not exist, creating");
          String resourceName = "Lawnchair.settings.json";
          using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
          {
            String lawnchairAppDataPath = Directory.GetParent(settingsFile).FullName;
            if (Directory.Exists(lawnchairAppDataPath) == false)
            {
              AppendToDebugOutput("Creating directory [" + lawnchairAppDataPath + "]");
              Directory.CreateDirectory(lawnchairAppDataPath);
            }
            AppendToDebugOutput("Found embedded resource [" + resourceName + "]");
            using (var file = new FileStream(settingsFile, FileMode.Create, FileAccess.Write))
            {
              AppendToDebugOutput("Writing to file [" + settingsFile + "]");
              resource.CopyTo(file);
            }
          }
          MessageBox.Show("On the next screen, please select the location of your script repository. This can be a local path, mapped drive, or UNC path. If you are unsure what folder to select ask for someone to supply you with a prefilled settings file.", "First time setup");
          CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog()
          {
            IsFolderPicker = true
          };
          if (commonOpenFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
          {
            AppendToDebugOutput("Folder chosen [" + commonOpenFileDialog.FileName + "]");
            AppendToDebugOutput("Deserializing settings [" + settingsFile + "]");
            settingsJson = File.ReadAllText(settingsFile);
            settings = JsonConvert.DeserializeObject<Settings>(settingsJson);
            settings.ScriptRepositoryPaths = new string[] { commonOpenFileDialog.FileName };
            AppendToDebugOutput("First folder in the array [" + settings.ScriptRepositoryPaths[0] + "]");
            File.WriteAllText(settingsFile, JsonConvert.SerializeObject(settings));
          }
          else
          {
            MessageBox.Show("Lawnchair does not understand where to look for scripts.Either rerun this program and choose a location where the scripts are located or ask for someone to supply you with a prefilled settings file.", "Error");

            File.Delete(settingsFile);
            Directory.Delete(Directory.GetParent(settingsFile).FullName);
            Environment.Exit(0);
          }
        }
        else // Use the existing settings file
        {
          AppendToDebugOutput("Using existing settings file [" + settingsFile + "]");
          settingsJson = File.ReadAllText(settingsFile);
          AppendToDebugOutput("Contents of settings.json (before deserialization)");
          AppendToDebugOutput(settingsJson);
          settings = JsonConvert.DeserializeObject<Settings>(settingsJson);
        }

        return settings;
      }
      catch (Exception exception)
      {
        HandleException(exception);
      }

      return null;
    }

    private void HandleException(Exception exception)
    {
      AppendToDebugOutput("Exception thrown:");
      AppendToDebugOutput(exception.ToString());
      debugWindow.ShowDialog();

      throw exception;
    }

    private Boolean IsMetadataMatch(LawnchairMetadata metadata, String query)
    {
      bool match = false; // Default value

      if (null != metadata.Tags) // If tags are present
      {
        if (metadata.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
          AppendToDebugOutput("Matched [" + query + "] to Author [" + metadata.Author + "]");
          match = true;
        }
        if (metadata.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
          AppendToDebugOutput("Matched [" + query + "] to Category [" + metadata.Category + "]");
          match = true;
        }
        if (metadata.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
          AppendToDebugOutput("Matched [" + query + "] to Name [" + metadata.Name + "]");
          match = true;
        }
        if (metadata.Tags.Contains(query, StringComparer.OrdinalIgnoreCase))
        {
          AppendToDebugOutput("Matched [" + query + "] to one of the Tags [" + metadata.Tags.Where(o => o == query).Last() + "]");
          match = true;
        }
      }
      else
      {
        if (metadata.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
          AppendToDebugOutput("Matched [" + query + "] to Author [" + metadata.Author + "]");
          match = true;
        }
        if (metadata.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
          AppendToDebugOutput("Matched [" + query + "] to Category [" + metadata.Category + "]");
          match = true;
        }
        if (metadata.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
          AppendToDebugOutput("Matched [" + query + "] to Name [" + metadata.Name + "]");
          match = true;
        }
      }

      return match;
    }

    public MainWindow()
    {
      InitializeComponent();
      LoadSettingsAndReset();
    }

    private void GetAvailableScripts(Settings settings)
    {
      try
      {
        // Clear metadata lists
        cachedMetadataList.Clear();

        // Get metadata files and load the ListView with contents
        foreach (String scriptRepositoryPath in settings.ScriptRepositoryPaths)
        {
          AppendToDebugOutput("Looking for script repository metadata files (recursively) in [" + scriptRepositoryPath + "]");
          cachedMetadataList = cachedMetadataList.Concat(GetScriptRepositoryMetadataFiles(settings.ScriptMetadataFilename, scriptRepositoryPath)).ToList();
        }

        ShowScripts(cachedMetadataList, false);
      }
      catch (Exception exception)
      {
        HandleException(exception);
      }
    }

    private void ShowScripts(IEnumerable<LawnchairMetadata> metadataList, Boolean hideCategories)
    {
      scriptRepositoryListView.ItemsSource = metadataList;

      if (hideCategories)
      {
        return;
      }
      else
      {
        // Define categories for ListView
        CollectionView collectionView = (CollectionView)CollectionViewSource.GetDefaultView(scriptRepositoryListView.ItemsSource);
        PropertyGroupDescription propertyGroupDescription = new PropertyGroupDescription("Category");
        collectionView.GroupDescriptions.Add(propertyGroupDescription);
        scriptRepositoryListViewCollectionView = CollectionViewSource.GetDefaultView(scriptRepositoryListView.ItemsSource);
      }
    }

    private void ShowVersion(long version)
    {
      try
      {
        AppendToDebugOutput("version.ToString(): [" + version.ToString() + "]; Length [" + version.ToString().Length + "]");
        String versionString = version.ToString().Substring(0, 8);
        String subVersionString = version.ToString().Substring(8, 3);
        DateTime versionDate = DateTime.ParseExact(versionString, "yyyyMMdd", null);
        String versionInformation = "Version " + versionDate.ToString("yyyy-MM-dd") + " SV " + subVersionString;
        AppendToDebugOutput("Setting textBlockVersion text to: [" + versionInformation + "]");
        textBlockVersion.Text = versionInformation;
      }
      catch (Exception exception)
      {
        HandleException(exception);
      }
    }

    private void LoadSettingsAndReset()
    {
      settings = GetSettings();
      searchTextBox.Text = searchPlaceholderText;
      ShowVersion(version);
      GetAvailableScripts(settings);
    }

    private void MainWindow_Closed(object sender, EventArgs e)
    {
      debugWindow.OverrideDefaultCloseBehavior = true;
      debugWindow.Close();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
      if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.D)
      {
        debugWindow.Show();
      }

      if (e.Key == Key.F5)
      {
        LoadSettingsAndReset();
      }
    }

    private void ScriptRepositoryListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (((FrameworkElement)e.OriginalSource).DataContext is LawnchairMetadata)
      {
        LawnchairMetadata metadata = ((FrameworkElement)e.OriginalSource).DataContext as LawnchairMetadata;
        ExecuteScript(metadata);
      }
    }

    private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
      if (searchTextBox.Text == searchPlaceholderText)
      {
        searchTextBox.Clear();
      }
    }

    private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
      if (String.IsNullOrWhiteSpace(searchTextBox.Text))
      {
        searchTextBox.Text = searchPlaceholderText;
      }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
      if (String.IsNullOrWhiteSpace(searchTextBox.Text) == false && searchTextBox.Text != searchPlaceholderText)
      {
        FilterScripts(searchTextBox.Text);
      }
      else // Reset
      {
        if (null != scriptRepositoryListViewCollectionView)
        {
          scriptRepositoryListViewCollectionView.Filter = null;
        }
      }
    }

    private void scriptRepositoryListView_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter)
      {
        if (((FrameworkElement)e.OriginalSource).DataContext is LawnchairMetadata)
        {
          LawnchairMetadata metadata = ((FrameworkElement)e.OriginalSource).DataContext as LawnchairMetadata;
          ExecuteScript(metadata);
        }
      }
    }
  }
}
