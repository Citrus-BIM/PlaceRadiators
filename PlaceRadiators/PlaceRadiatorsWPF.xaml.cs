using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlaceRadiators
{
    public partial class PlaceRadiatorsWPF : Window
    {
        Document Doc;
        List<Definition> SelectedRadiatorDefinitionsList;
        public Parameter SelectedWindowWidthParameter;
        public FamilySymbol SelectedRadiatorType;
        public string RadiatorWidthByButtonName;
        public Definition SelectedRadiatorWidthParameter;
        public Definition SelectedRadiatorThicknessParameter;

        public string PercentageLength;
        public string IndentFromLevel;
        public string IndentFromWall;

        PlaceRadiatorsSettings PlaceRadiatorsSettingsItem = null;
        public PlaceRadiatorsWPF(Document doc, List<Parameter> windowParameterList, List<Family> mechanicalEquipmentList)
        {
            Doc = doc;
            InitializeComponent();

            PlaceRadiatorsSettingsItem = PlaceRadiatorsSettings.GetSettings();
            if (PlaceRadiatorsSettingsItem != null)
            {
                comboBox_WindowWidthParameter.ItemsSource = windowParameterList;
                comboBox_WindowWidthParameter.DisplayMemberPath = "Definition.Name";
                if (windowParameterList.Count != 0)
                {
                    if (windowParameterList.FirstOrDefault(wp => wp.Definition.Name == PlaceRadiatorsSettingsItem.SelectedWindowWidthParameterName) != null)
                    {
                        comboBox_WindowWidthParameter.SelectedItem = windowParameterList.FirstOrDefault(wp => wp.Definition.Name == PlaceRadiatorsSettingsItem.SelectedWindowWidthParameterName);
                    }
                    else
                    {
                        comboBox_WindowWidthParameter.SelectedItem = comboBox_WindowWidthParameter.Items[0];
                    }
                }

                comboBox_RadiatorFamilySelection.ItemsSource = mechanicalEquipmentList;
                comboBox_RadiatorFamilySelection.DisplayMemberPath = "Name";
                if (mechanicalEquipmentList.Count != 0)
                {
                    if (mechanicalEquipmentList.FirstOrDefault(me => me.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorFamilyName) != null)
                    {
                        comboBox_RadiatorFamilySelection.SelectedItem = mechanicalEquipmentList.FirstOrDefault(me => me.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorFamilyName);
                    }
                    else
                    {
                        comboBox_RadiatorFamilySelection.SelectedItem = comboBox_RadiatorFamilySelection.Items[0];
                    }
                }

                if (comboBox_RadiatorTypeSelection.Items.Count != 0)
                {
                    if (comboBox_RadiatorTypeSelection.Items.Cast<FamilySymbol>().FirstOrDefault(fs => fs.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorTypeName) != null)
                    {
                        comboBox_RadiatorTypeSelection.SelectedItem = comboBox_RadiatorTypeSelection.Items.Cast<FamilySymbol>().FirstOrDefault(fs => fs.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorTypeName);
                    }
                    else
                    {
                        comboBox_RadiatorTypeSelection.SelectedItem = comboBox_RadiatorTypeSelection.Items[0];
                    }
                }

                if (PlaceRadiatorsSettingsItem.RadiatorWidthByButtonName == "radioButton_Type")
                {
                    radioButton_Type.IsChecked = true;
                }
                else
                {
                    radioButton_Instance.IsChecked = true;
                }

                textBox_PercentageLength.Text = PlaceRadiatorsSettingsItem.PercentageLength;
                textBox_IndentFromLevel.Text = PlaceRadiatorsSettingsItem.IndentFromLevel;
                textBox_IndentFromWall.Text = PlaceRadiatorsSettingsItem.IndentFromWall;
            }
            else
            {
                comboBox_WindowWidthParameter.ItemsSource = windowParameterList;
                comboBox_WindowWidthParameter.DisplayMemberPath = "Definition.Name";
                if (windowParameterList.Count != 0)
                {
                    comboBox_WindowWidthParameter.SelectedItem = comboBox_WindowWidthParameter.Items[0];
                }

                comboBox_RadiatorFamilySelection.ItemsSource = mechanicalEquipmentList;
                comboBox_RadiatorFamilySelection.DisplayMemberPath = "Name";
                if (mechanicalEquipmentList.Count != 0)
                {
                    comboBox_RadiatorFamilySelection.SelectedItem = comboBox_RadiatorFamilySelection.Items[0];
                }

                radioButton_Type.IsChecked = true;
            }

            radioButton_RadiatorWidthBy_Checked(null, null);
        }

        private void comboBox_RadiatorFamilySelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Family selectedRadiatorFamily = comboBox_RadiatorFamilySelection.SelectedItem as Family;
            List<ElementId> selectedRadiatorTypesList = selectedRadiatorFamily.GetFamilySymbolIds().ToList();
            List<FamilySymbol> radiatorTypesList = new List<FamilySymbol>();
            foreach (ElementId id in selectedRadiatorTypesList)
            {
                radiatorTypesList.Add(Doc.GetElement(id) as FamilySymbol);
            }
            radiatorTypesList = radiatorTypesList.OrderBy(fs => fs.Name, new AlphanumComparatorFastString()).ToList();

            comboBox_RadiatorTypeSelection.ItemsSource = radiatorTypesList;
            comboBox_RadiatorTypeSelection.DisplayMemberPath = "Name";
            if (radiatorTypesList.Count != 0)
            {
                comboBox_RadiatorTypeSelection.SelectedItem = comboBox_RadiatorTypeSelection.Items[0];
            }
        }
        private void comboBox_RadiatorTypeSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FamilySymbol selectedRadiatorFamilyType = comboBox_RadiatorTypeSelection.SelectedItem as FamilySymbol;
            if (selectedRadiatorFamilyType != null)
            {
                RadiatorWidthByButtonName = GetWidthBySelection();

                if (RadiatorWidthByButtonName == "radioButton_Type")
                {
                    ParameterSet selectedRadiatorFamilyTypeParameterSet = selectedRadiatorFamilyType.Parameters;
                    SelectedRadiatorDefinitionsList = new List<Definition>();
                    foreach (Parameter parameter in selectedRadiatorFamilyTypeParameterSet)
                    {
                        if (parameter.StorageType == StorageType.Double)
                        {
                            SelectedRadiatorDefinitionsList.Add(parameter.Definition);
                        }
                    }
                    SelectedRadiatorDefinitionsList = SelectedRadiatorDefinitionsList
                        .OrderBy(p => p.Name, new AlphanumComparatorFastString()).ToList();

                    comboBox_RadiatorWidthParameter.ItemsSource = SelectedRadiatorDefinitionsList;
                    comboBox_RadiatorWidthParameter.DisplayMemberPath = "Name";
                    if (SelectedRadiatorDefinitionsList.Count != 0)
                    {
                        comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items[0];
                    }

                    comboBox_RadiatorThicknessParameter.ItemsSource = SelectedRadiatorDefinitionsList;
                    comboBox_RadiatorThicknessParameter.DisplayMemberPath = "Name";
                    if (SelectedRadiatorDefinitionsList.Count != 0)
                    {
                        comboBox_RadiatorThicknessParameter.SelectedItem = comboBox_RadiatorThicknessParameter.Items[0];
                    }
                }
                else
                {
                    Document famDoc = Doc.EditFamily(selectedRadiatorFamilyType.Family);
                    if (famDoc != null)
                    {
                        SelectedRadiatorDefinitionsList = new List<Definition>();
                        FamilyManager mgr = famDoc.FamilyManager;
                        FamilyParameterSet familyParameterSet = mgr.Parameters;
                        foreach (FamilyParameter parameter in familyParameterSet)
                        {
                            if (parameter.IsInstance && parameter.StorageType == StorageType.Double)
                            {
                                SelectedRadiatorDefinitionsList.Add(parameter.Definition);
                            }
                        }

                        SelectedRadiatorDefinitionsList = SelectedRadiatorDefinitionsList
                            .OrderBy(p => p.Name, new AlphanumComparatorFastString()).ToList();

                        comboBox_RadiatorWidthParameter.ItemsSource = SelectedRadiatorDefinitionsList;
                        comboBox_RadiatorWidthParameter.DisplayMemberPath = "Name";
                        if (SelectedRadiatorDefinitionsList.Count != 0)
                        {
                            comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items[0];
                        }
                    }

                    comboBox_RadiatorThicknessParameter.ItemsSource = null;
                }

                if (PlaceRadiatorsSettingsItem != null)
                {
                    if (comboBox_RadiatorWidthParameter.Items.Count != 0)
                    {
                        if (comboBox_RadiatorWidthParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorWidthParameterName) != null)
                        {
                            comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorWidthParameterName);
                        }
                        else
                        {
                            comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items[0];
                        }
                    }

                    if (PlaceRadiatorsSettingsItem.RadiatorWidthByButtonName == "radioButton_Type")
                    {
                        if (comboBox_RadiatorThicknessParameter.Items.Count != 0)
                        {
                            if (comboBox_RadiatorThicknessParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorThicknessParameterName) != null)
                            {
                                comboBox_RadiatorThicknessParameter.SelectedItem = comboBox_RadiatorThicknessParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorThicknessParameterName);
                            }
                            else
                            {
                                comboBox_RadiatorThicknessParameter.SelectedItem = comboBox_RadiatorThicknessParameter.Items[0];
                            }
                        }
                    }
                }
            }
        }

        private void btn_Ok_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            DialogResult = true;
            Close();
        }
        private void btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        private void FillMEPParametersWPF_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                SaveSettings();

                DialogResult = true;
                Close();
            }

            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void radioButton_RadiatorWidthBy_Checked(object sender, RoutedEventArgs e)
        {
            RadiatorWidthByButtonName = GetWidthBySelection();

            if (RadiatorWidthByButtonName == "radioButton_Type")
            {
                if (label_RadiatorThicknessParameter != null)
                {
                    label_RadiatorThicknessParameter.IsEnabled = true;
                    comboBox_RadiatorThicknessParameter.IsEnabled = true;

                    FamilySymbol selectedRadiatorFamilyType = comboBox_RadiatorTypeSelection.SelectedItem as FamilySymbol;
                    if (selectedRadiatorFamilyType != null)
                    {
                        ParameterSet selectedRadiatorFamilyTypeParameterSet = selectedRadiatorFamilyType.Parameters;
                        SelectedRadiatorDefinitionsList = new List<Definition>();
                        foreach (Parameter parameter in selectedRadiatorFamilyTypeParameterSet)
                        {
                            if (parameter.StorageType == StorageType.Double)
                            {
                                SelectedRadiatorDefinitionsList.Add(parameter.Definition);
                            }
                        }
                        SelectedRadiatorDefinitionsList = SelectedRadiatorDefinitionsList
                            .OrderBy(p => p.Name, new AlphanumComparatorFastString()).ToList();

                        comboBox_RadiatorWidthParameter.ItemsSource = SelectedRadiatorDefinitionsList;
                        comboBox_RadiatorWidthParameter.DisplayMemberPath = "Name";
                        if (SelectedRadiatorDefinitionsList.Count != 0)
                        {
                            comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items[0];
                        }

                        comboBox_RadiatorThicknessParameter.ItemsSource = SelectedRadiatorDefinitionsList;
                        comboBox_RadiatorThicknessParameter.DisplayMemberPath = "Name";
                        if (SelectedRadiatorDefinitionsList.Count != 0)
                        {
                            comboBox_RadiatorThicknessParameter.SelectedItem = comboBox_RadiatorThicknessParameter.Items[0];
                        }
                    }
                }
            }
            else
            {
                if (label_RadiatorThicknessParameter != null)
                {
                    label_RadiatorThicknessParameter.IsEnabled = false;
                    comboBox_RadiatorThicknessParameter.IsEnabled = false;

                    FamilySymbol selectedRadiatorFamilyType = comboBox_RadiatorTypeSelection.SelectedItem as FamilySymbol;
                    if (selectedRadiatorFamilyType != null)
                    {
                        Document famDoc = Doc.EditFamily(selectedRadiatorFamilyType.Family);
                        if (famDoc != null)
                        {
                            SelectedRadiatorDefinitionsList = new List<Definition>();
                            FamilyManager mgr = famDoc.FamilyManager;
                            FamilyParameterSet familyParameterSet = mgr.Parameters;
                            foreach (FamilyParameter parameter in familyParameterSet)
                            {
                                if (parameter.IsInstance && parameter.StorageType == StorageType.Double)
                                {
                                    SelectedRadiatorDefinitionsList.Add(parameter.Definition);
                                }
                            }

                            SelectedRadiatorDefinitionsList = SelectedRadiatorDefinitionsList
                                .OrderBy(p => p.Name, new AlphanumComparatorFastString()).ToList();

                            comboBox_RadiatorWidthParameter.ItemsSource = SelectedRadiatorDefinitionsList;
                            comboBox_RadiatorWidthParameter.DisplayMemberPath = "Name";
                            if (SelectedRadiatorDefinitionsList.Count != 0)
                            {
                                comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items[0];
                            }
                        }
                    }

                    comboBox_RadiatorThicknessParameter.ItemsSource = null;
                }
            }

            if (PlaceRadiatorsSettingsItem != null)
            {
                if (comboBox_RadiatorWidthParameter.Items.Count != 0)
                {
                    if (comboBox_RadiatorWidthParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorWidthParameterName) != null)
                    {
                        comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorWidthParameterName);
                    }
                    else
                    {
                        comboBox_RadiatorWidthParameter.SelectedItem = comboBox_RadiatorWidthParameter.Items[0];
                    }
                }

                if (PlaceRadiatorsSettingsItem.RadiatorWidthByButtonName == "radioButton_Type")
                {
                    if (comboBox_RadiatorThicknessParameter.Items.Count != 0)
                    {
                        if (comboBox_RadiatorThicknessParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorThicknessParameterName) != null)
                        {
                            comboBox_RadiatorThicknessParameter.SelectedItem = comboBox_RadiatorThicknessParameter.Items.Cast<Definition>().FirstOrDefault(p => p.Name == PlaceRadiatorsSettingsItem.SelectedRadiatorThicknessParameterName);
                        }
                        else
                        {
                            comboBox_RadiatorThicknessParameter.SelectedItem = comboBox_RadiatorThicknessParameter.Items[0];
                        }
                    }
                }
            }
        }
        private string GetWidthBySelection()
        {
            return (radioButton_Type?.IsChecked == true)
                ? "radioButton_Type"
                : "radioButton_Instance";
        }
        private void SaveSettings()
        {
            PlaceRadiatorsSettingsItem = new PlaceRadiatorsSettings();

            SelectedWindowWidthParameter = comboBox_WindowWidthParameter.SelectedItem as Parameter;
            PlaceRadiatorsSettingsItem.SelectedWindowWidthParameterName = SelectedWindowWidthParameter.Definition.Name;

            PlaceRadiatorsSettingsItem.SelectedRadiatorFamilyName = (comboBox_RadiatorFamilySelection.SelectedItem as Family).Name;

            SelectedRadiatorType = comboBox_RadiatorTypeSelection.SelectedItem as FamilySymbol;
            PlaceRadiatorsSettingsItem.SelectedRadiatorTypeName = SelectedRadiatorType.Name;

            RadiatorWidthByButtonName = GetWidthBySelection();
            PlaceRadiatorsSettingsItem.RadiatorWidthByButtonName = RadiatorWidthByButtonName;

            SelectedRadiatorWidthParameter = comboBox_RadiatorWidthParameter.SelectedItem as Definition;
            PlaceRadiatorsSettingsItem.SelectedRadiatorWidthParameterName = SelectedRadiatorWidthParameter.Name;

            SelectedRadiatorThicknessParameter = comboBox_RadiatorThicknessParameter.SelectedItem as Definition;
            if (SelectedRadiatorThicknessParameter != null)
            {
                PlaceRadiatorsSettingsItem.SelectedRadiatorThicknessParameterName = SelectedRadiatorThicknessParameter.Name;
            }
            else
            {
                PlaceRadiatorsSettingsItem.SelectedRadiatorThicknessParameterName = "";
            }

            PercentageLength = textBox_PercentageLength.Text;
            PlaceRadiatorsSettingsItem.PercentageLength = PercentageLength;

            IndentFromLevel = textBox_IndentFromLevel.Text;
            PlaceRadiatorsSettingsItem.IndentFromLevel = IndentFromLevel;

            IndentFromWall = textBox_IndentFromWall.Text;
            PlaceRadiatorsSettingsItem.IndentFromWall = IndentFromWall;

            PlaceRadiatorsSettingsItem.SaveSettings();
        }
    }
}
