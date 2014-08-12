using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Forms;
using TextBox = System.Windows.Controls.TextBox;

namespace AdLookup
{
    public partial class MainWindow
    {
        private readonly EmployeeService _employeeService;
        private readonly List<Employee> _employees;
        private readonly Dictionary<string, Func<string, Task<List<Employee>>>> _lookupOptions;
        private readonly Dictionary<char, string> _csvTypes;
        private const string SimpleSearchDefaultText = "Name/short/email/emp.id";
        private const string FileSearchDefaultText = "Filepath...";
        private const string AdSearchDefaultText = "AD group name";

        public MainWindow()
        {
            InitializeComponent();

            Log.Initialize(LblLogDisplay, LblStatusBarLog, GrdLogDisplay);

            _employeeService = new EmployeeService();
            _employees = new List<Employee>();

            _lookupOptions = new Dictionary<string, Func<string, Task<List<Employee>>>>
            {
                {"short name", async s => await _employeeService.GetEmployeeByShortName(s)},
                {"full name", async s => await _employeeService.GetEmployeeByDisplayName(s)},
                {"e-mail", async s => await _employeeService.GetEmployeeByEmail(s)},
                {"emp.id", async s =>
                    {
                        int parsedEmpNr;
                        if (Int32.TryParse(s, out parsedEmpNr))
                            return await _employeeService.GetEmployeeByEmpNr(parsedEmpNr);
                        throw new Exception("Could not parse emp nr to int");
                    }
                }
            };

            _csvTypes = new Dictionary<char, string>
            {
                {',', "comma"},
                {';', "semi colon"}
            };

            CmbCsvTypes.ItemsSource = _csvTypes;
            CmbLookupField.ItemsSource = _lookupOptions;

            CmbCsvTypes.SelectedIndex = 0;
            CmbLookupField.SelectedIndex = 0;

            SetTextfieldToDefaultValue(TxtName, SimpleSearchDefaultText);
            SetTextfieldToDefaultValue(TxtFilePath, FileSearchDefaultText);
            SetTextfieldToDefaultValue(TxtAdGroupName, AdSearchDefaultText);

            HideBusyIndicator();
            HideLogDisplay();
            HideAdGroupsPanel();

            SetWindowTitle();
        }

        private void SetWindowTitle()
        {
            var domain = ConfigurationManager.AppSettings["Domain"];
            var adUrl = ConfigurationManager.AppSettings["AD_Url"];
            Title = String.Format("AD Lookup | Host: {0} | Domain: {1}", adUrl, domain);
        }

        private async void BtnSimpleSearch_OnClick(object sender, RoutedEventArgs e)
        {
            var query = TxtName.Text;

            var queryDefinition = new QueryDefinition
            {
                Query = query,

                CanSearch = q => q != SimpleSearchDefaultText,

                CreateLogMessage =
                    entries =>
                        String.Format("Found {0} {1} for query \"{2}\"", entries.Count,
                            entries.Count > 0 ? "entries" : "entry", query),

                DoSearch = async qry => await TryFindEmployee(qry)
            };

            await ProcessQuery(queryDefinition);
        }

        private async Task<List<Employee>> TryFindEmployee(string userInput)
        {
            var employee = (await _employeeService.GetEmployeeByShortName(userInput) ??
                            await _employeeService.GetEmployeeByDisplayName(userInput)) ??
                            await _employeeService.GetEmployeeByEmail(userInput);

            if (employee == null)
            {
                int parsedEmpNr;
                if (Int32.TryParse(userInput, out parsedEmpNr))
                    employee = await _employeeService.GetEmployeeByEmpNr(parsedEmpNr);
            }
            return employee;
        }

        private void BtnExportToCsv_OnClick(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = @"csv files (*.csv)|*.csv",
                FilterIndex = 1
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var header = "Employee ID,Firstname,Lastname,Shortname,E-mail,Department,Location,Office phone,Mobile phone,Title";

                var lines =
                    _employees.Select(emp => String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                        emp.EmployeeId, emp.FirstName, emp.LastName, emp.ShortName, emp.Email,
                        emp.Department, emp.Location, emp.OfficePhone, emp.Mobile, emp.Title)).ToList();

                lines.Insert(0, header);

                string fileName = saveFileDialog.FileName;
                File.AppendAllLines(fileName, lines, Encoding.UTF8);
            }
        }

        private void BtnSearchFile_OnClick(object sender, RoutedEventArgs e)
        {
            ResetLogAndEmployeesList();

            string filePath = TxtFilePath.Text;
            char separator = GetSelectedSeparator();
            var action = GetSelectedSearchAction();

            if (!String.IsNullOrEmpty(filePath) && filePath != FileSearchDefaultText && action != null)
                SearchFileByCriteria(filePath, separator, action);
        }

        private char GetSelectedSeparator()
        {
            var selectedItem = (KeyValuePair<char, string>)CmbCsvTypes.SelectedItem;
            char key = _csvTypes.Single(i => i.Value == selectedItem.Value).Key;
            return key;
        }

        private Func<string, Task<List<Employee>>> GetSelectedSearchAction()
        {
            var selectedItem = (KeyValuePair<string, Func<string, Task<List<Employee>>>>)CmbLookupField.SelectedItem;
            return _lookupOptions.Single(i => i.Key == selectedItem.Key).Value;
        }

        private async void SearchFileByCriteria(string path, char separator, Func<string, Task<List<Employee>>> searchAction)
        {
            var queryDefinition = new QueryDefinition
            {
                DoSearch = async filePath =>
                {
                    var allLines = File.ReadAllLines(filePath, Encoding.UTF8);

                    var employeeEntries = new List<Employee>();

                    foreach (var line in allLines)
                    {
                        var entries = line.Split(separator);

                        foreach (var entry in entries.Where(ent => !String.IsNullOrEmpty(ent)))
                        {
                            var employees = await searchAction(entry);
                            if (employees == null || !employees.Any())
                                LogNotFoundSearchResult(entry);
                            else
                                employeeEntries.AddRange(employees);
                        }
                    }

                    return employeeEntries;
                },

                CanSearch = query => query != FileSearchDefaultText,

                CreateLogMessage =
                    entries =>
                        "Found " + _employees.Count + ((_employees.Count > 1) ? " matches " : " match ") + " in file \"" +
                        path + "\"",

                Query = path
            };

            await ProcessQuery(queryDefinition);
        }

        private void LogNotFoundSearchResult(string searchEntry)
        {
            Log.ToDisplayPanel(Log.Severity.Warning, "Could not find any entries in AD for \"" + searchEntry + "\"");
        }

        private void HideLogDisplay()
        {
            GrdLogDisplay.Visibility = Visibility.Hidden;
        }

        private void BindGrid()
        {
            DataGrid.ItemsSource = null;
            DataGrid.ItemsSource = _employees;
        }

        private void BtnOpenFile_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SetTextfieldForUserInput(TxtFilePath);
                TxtFilePath.Text = openFileDialog.FileName;
            }
        }

        private void TxtName_OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtName.Text == SimpleSearchDefaultText)
                SetTextfieldForUserInput(TxtName);
        }

        private void TxtName_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (IsTextfieldEmpty(TxtName))
                SetTextfieldToDefaultValue(TxtName, SimpleSearchDefaultText);
        }

        private bool IsTextfieldEmpty(TextBox textBox)
        {
            return String.IsNullOrEmpty(textBox.Text);
        }

        private void SetTextfieldForUserInput(TextBox textBox)
        {
            textBox.Text = String.Empty;
            textBox.Foreground = new SolidColorBrush(Colors.Black);
        }

        private void SetTextfieldToDefaultValue(TextBox textBox, string text)
        {
            textBox.Text = text;
            textBox.Foreground = new SolidColorBrush(Colors.Gray);
        }

        private void TxtFilePath_OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtFilePath.Text == FileSearchDefaultText)
                SetTextfieldForUserInput(TxtFilePath);
        }

        private void TxtFilePath_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (IsTextfieldEmpty(TxtFilePath))
                SetTextfieldToDefaultValue(TxtFilePath, FileSearchDefaultText);
        }

        private async void BtnMembersInAdGroup_OnClick(object sender, RoutedEventArgs e)
        {
            var query = TxtAdGroupName.Text;

            var queryDefinition = new QueryDefinition
            {
                CanSearch = q => q != AdSearchDefaultText,

                CreateLogMessage =
                    entries =>
                        "Found " + entries.Count + ((entries.Count > 1) ? " members" : " member") +
                        " in the AD group \"" + query + " \"",

                DoSearch = async q =>
                {
                    var employeeEntries = await _employeeService.GetMembersInGroup(q);
                    if (employeeEntries == null || !employeeEntries.Any())
                    {
                        LogNotFoundSearchResult(query);
                        return new List<Employee>();
                    }
                    return employeeEntries;
                },

                Query = query
            };

            await ProcessQuery(queryDefinition);
        }

        private async Task ProcessQuery(QueryDefinition queryDefinition, Action customBindGrid = null)
        {
            if (!queryDefinition.CanSearch(queryDefinition.Query)) return;

            ShowBusyIndicator();

            ResetLogAndEmployeesList();

            try
            {
                var employeeEntries = await queryDefinition.DoSearch(queryDefinition.Query);

                if (employeeEntries != null && employeeEntries.Any())
                    _employees.AddRange(employeeEntries);

                Log.ToStatusBar(queryDefinition.CreateLogMessage(employeeEntries));
            }
            catch (Exception ex)
            {
                Log.ToDisplayPanel(Log.Severity.Error, "An error occurred during search: " + ex.Message);
            }

            if (customBindGrid == null)
                BindGrid();
            else
                customBindGrid();

            Log.Show();

            HideBusyIndicator();
        }

        private void ResetLogAndEmployeesList()
        {
            Log.Clear();
            _employees.Clear();
        }

        private void TxtAdGroupName_OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtAdGroupName.Text == AdSearchDefaultText)
                SetTextfieldForUserInput(TxtAdGroupName);
        }

        private void TxtAdGroupName_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (IsTextfieldEmpty(TxtAdGroupName))
                SetTextfieldToDefaultValue(TxtAdGroupName, AdSearchDefaultText);
        }

        private void ShowBusyIndicator()
        {
            GrdBusyIndicator.Visibility = Visibility.Visible;
        }

        private void HideBusyIndicator()
        {
            GrdBusyIndicator.Visibility = Visibility.Collapsed;
        }

        private void BtnHideLogDisplay_OnClick(object sender, RoutedEventArgs e)
        {
            HideLogDisplay();
        }

        private async void FindGroupsContextMenu_OnClick(object sender, RoutedEventArgs e)
        {
            Employee emp = DataGrid.SelectedItem as Employee;

            if (emp != null)
            {
                ShowBusyIndicator();

                try
                {
                    var groups = await _employeeService.FindGroupsForMember(emp.ShortName);
                    AdGroupsDataGrid.ItemsSource = null;
                    AdGroupsDataGrid.ItemsSource = groups.Select(str => new { Groups = str }).OrderBy(str => str.Groups);
                    if (groups != null && groups.Any())
                        Log.ToStatusBar(String.Format("Found {0} groups for {1}", groups.Count, emp.DisplayName));
                }
                catch (Exception ex)
                {
                    Log.ToDisplayPanel(Log.Severity.Error, ex.Message);
                }

                ShowAdGroupsPanel();

                HideBusyIndicator();
            }
        }

        private void ShowAdGroupsPanel()
        {
            AdGroupsPanelScroller.Visibility = Visibility.Visible;
        }

        private void HideAdGroupsPanel()
        {
            AdGroupsPanelScroller.Visibility = Visibility.Collapsed;
        }

        private void BtnExportAdGroupsCsv_OnClick(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = @"csv files (*.csv)|*.csv",
                FilterIndex = 1
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var employee = (Employee)DataGrid.SelectedItem;

                    if (employee != null && !String.IsNullOrEmpty(employee.DisplayName))
                    {
                        var header = "AD groups which " + employee.DisplayName + " is a member of";

                        var lines = (AdGroupsDataGrid.Items.Cast<dynamic>().Select(entry => entry.Groups)).Cast<string>().ToList();

                        lines.Insert(0, header);

                        string fileName = saveFileDialog.FileName;
                        File.AppendAllLines(fileName, lines, Encoding.UTF8);

                        Log.ToStatusBar(String.Format("{0} saved successfully", saveFileDialog.FileName));
                    }
                    else
                        Log.ToDisplayPanel(Log.Severity.Error, "Could not find the employee when exporting groups to CSV");
                }
                catch (Exception ex)
                {
                    Log.ToDisplayPanel(Log.Severity.Error, ex.Message);
                }
            }
        }

        private void BtnHideAdGroupsPanels_OnClick(object sender, RoutedEventArgs e)
        {
            HideAdGroupsPanel();
        }
    }
}
