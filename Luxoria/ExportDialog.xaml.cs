using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;

namespace Luxoria
{
    public class Gallery
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Email { get; set; }
    }

    public partial class ExportOptionsDialog : Window
    {
        private HttpClient httpClient = new HttpClient();
        private const string API_URL = "http://localhost:3000/api/galleries";
        private ComboBox galleryComboBox;

        public string SelectedExportType => cmbExportType.Text;
        public string FileName;
        public Gallery SelectedGallery = new Gallery();

        private void AddGallerySelectLabel()
        {
            if (!panelOptions.Children.OfType<Label>().Any(l => l.Content.ToString() == "Select a gallery"))
            {
                panelOptions.Children.Add(new Label { Content = "Select a gallery" });
            }
        }

        public ExportOptionsDialog(string fileSystemName)
        {
            FileName = fileSystemName;
            InitializeComponent();
            cmbExportType.SelectedIndex = 0;
            InitializeGalleryComboBox();
            LoadGalleriesAsync();
        }

        private void InitializeGalleryComboBox()
        {
            galleryComboBox = new ComboBox { Width = 180, Margin = new Thickness(0, 10, 0, 0) };
            galleryComboBox.SelectionChanged += GalleryComboBox_SelectionChanged;
            AddGallerySelectLabel();
            panelOptions.Children.Add(galleryComboBox);
        }


        private async Task LoadGalleriesAsync(string selectedName = null)
        {
            try
            {
                var response = await httpClient.GetAsync(API_URL);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var galleryResponse = JsonConvert.DeserializeObject<Dictionary<string, List<Gallery>>>(content);
                    var galleries = galleryResponse["galleries"];
                    UpdateGalleryDropdown(galleries, selectedName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading galleries: {ex.Message}");
            }
        }
        private void UpdateGalleryDropdown(List<Gallery> galleries, string selectedName)
        {
            galleryComboBox.Items.Clear();
            int selectedIndex = -1;

            for (int i = 0; i < galleries.Count; i++)
            {
                galleryComboBox.Items.Add(galleries[i].Name);
                if (galleries[i].Name == selectedName)
                {
                    selectedIndex = i;
                }
            }
            galleryComboBox.Items.Add("Create new gallery...");

            if (selectedIndex >= 0)
            {
                galleryComboBox.SelectedIndex = selectedIndex;
            }
            else
            {
                galleryComboBox.SelectedIndex = galleryComboBox.Items.Count - 2;
            }
        }




        private void GalleryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (galleryComboBox.SelectedItem == null) return;

            var selectedItem = galleryComboBox.SelectedItem.ToString();
            if (selectedItem == "Create new gallery...")
            {
                ShowCreateGalleryDialog();
            }
        }


        private void ShowCreateGalleryDialog()
        {
            panelOptions.Children.Clear();

            panelOptions.Children.Add(new Label { Content = "Gallery Name" });
            var nameTextBox = new TextBox { Width = 180, Margin = new Thickness(0, 10, 0, 0) };
            panelOptions.Children.Add(nameTextBox);

            panelOptions.Children.Add(new Label { Content = "Description" });
            var descriptionTextBox = new TextBox { Width = 180, Margin = new Thickness(0, 10, 0, 0) };
            panelOptions.Children.Add(descriptionTextBox);

            panelOptions.Children.Add(new Label { Content = "Email" });
            var emailTextBox = new TextBox { Width = 180, Margin = new Thickness(0, 10, 0, 0) };
            panelOptions.Children.Add(emailTextBox);

            var submitButton = new Button { Content = "Create Gallery", Width = 180, Margin = new Thickness(0, 10, 0, 0) };
            submitButton.Click += async (sender, e) => await CreateGallery(nameTextBox.Text, descriptionTextBox.Text, emailTextBox.Text);
            panelOptions.Children.Add(submitButton);
        }

        private async Task CreateGallery(string name, string description, string email)
        {
            var newGallery = new { name = name, description = description, email = email };
            var json = JsonConvert.SerializeObject(newGallery);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var result = await httpClient.PostAsync($"{API_URL}/create", content);
            if (result.IsSuccessStatusCode)
            {
                var resultContent = await result.Content.ReadAsStringAsync();
                var createdGallery = JsonConvert.DeserializeObject<Gallery>(resultContent);
                ResetExportMenu(createdGallery.Name);
            }
            else
            {
                MessageBox.Show("Failed to create gallery");
            }
        }




        private void ResetExportMenu(string selectGalleryName)
        {
            panelOptions.Children.Clear();

            panelOptions.Children.Add(new Label { Content = "Quality Setting" });
            panelOptions.Children.Add(new Slider { Minimum = 1, Maximum = 100, Value = 100 });

            panelOptions.Children.Add(new Label { Content = "File name" });
            panelOptions.Children.Add(new TextBox { Width = 180, Margin = new Thickness(0, 10, 0, 0), Text = FileName });

            AddGallerySelectLabel();

            InitializeGalleryComboBox();
            LoadGalleriesAsync(selectGalleryName);
        }





        private void CmbExportType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            panelOptions.Children.Clear();

            panelOptions.Children.Add(new Label { Content = "Quality Setting" });
            panelOptions.Children.Add(new Slider { Minimum = 1, Maximum = 100, Value = 100 });

            panelOptions.Children.Add(new Label { Content = "File name" });
            panelOptions.Children.Add(new TextBox { Width = 180, Margin = new Thickness(0, 10, 0, 0), Text = FileName });
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
