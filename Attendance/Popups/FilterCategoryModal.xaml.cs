using System.Collections.ObjectModel;
using Attendance.Data;
using Attendance.Models;
using Mopups.Pages;
using Mopups.Services;

namespace Attendance.Popups;

public partial class FilterCategoryModal : PopupPage
{
    private readonly Action<string> _onCategorySelected;
    private readonly DatabaseHelper _dbHelper;
    private ObservableCollection<Event> _events;
    private string selectedCategory;
    private string savedCategory;

    public FilterCategoryModal(DatabaseHelper dbHelper, Action<string> onCategorySelected, string prevCategory)
    {
        InitializeComponent();
        _dbHelper = dbHelper;
        _onCategorySelected = onCategorySelected;
        savedCategory = prevCategory;
        LoadCategories();
    }

    private async void LoadCategories()
    {
        var events = await _dbHelper.GetEventsAsync();
        _events = new ObservableCollection<Event>(events);

        var distinctCategories = _events
            .Select(e => e.Category)
            .Distinct()
            .ToList();

        CategoryPicker.ItemsSource = distinctCategories;

        if (!string.IsNullOrEmpty(savedCategory))
        {
            CategoryPicker.SelectedItem = savedCategory;
        }
    }

    private async void ApplyCategoryFilter_Clicked(object sender, EventArgs e)
    {
        _onCategorySelected?.Invoke(selectedCategory);
        await MopupService.Instance.PopAsync();
    }

    private void CategoryPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (CategoryPicker.SelectedIndex >= 0)
        {
            selectedCategory = CategoryPicker.SelectedItem.ToString();
        }
    }
}