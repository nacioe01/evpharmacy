
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly UserSession _session;
    private readonly IServiceProvider _services;
    private NavigationItem? _selectedItem;
    private PageViewModel? _currentPage;

    public MainViewModel(UserSession session, IServiceProvider services)
    {
        _session = session;
        _services = services;
        _session.Changed += (_, _) => Refresh();

        LogoutCommand = new RelayCommand(() => LogoutRequested?.Invoke(this, EventArgs.Empty));

        Refresh();
    }

    /// <summary>Raised when the user asks to log out; the window handles the flow.</summary>
    public event EventHandler? LogoutRequested;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    public ICommand LogoutCommand { get; }

    public string Username => _session.CurrentUser?.Username ?? "User";

    public string UserInitial =>
        string.IsNullOrWhiteSpace(Username) ? "U" : Username[..1].ToUpperInvariant();

    public string UserRole => _session.IsAdmin ? "Administrator" : "Staff";

    public NavigationItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value is not null)
            {
                CurrentPage = value.CreatePage();
            }
        }
    }

    public PageViewModel? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public void NavigateTo(string key)
    {
        var item = NavigationItems.FirstOrDefault(i => i.Key == key);
        if (item is not null)
        {
            SelectedItem = item;
        }
    }

    /// <summary>Rebuilds user-dependent state (e.g. after logging in as another user).</summary>
    private void Refresh()
    {
        BuildNavigation();

        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(UserRole));

        _selectedItem = null;
        if (_session.IsSignedIn)
        {
            NavigateTo(PageKeys.Dashboard);
        }
        else
        {
            CurrentPage = null;
            OnPropertyChanged(nameof(SelectedItem));
        }
    }

    private void BuildNavigation()
    {
        NavigationItems.Clear();

        NavigationItems.Add(new NavigationItem(
            PageKeys.Dashboard, "Dashboard", "",
            () => new DashboardViewModel(Username, NavigateTo, _services.GetRequiredService<InventoryService>()),
            section: "OVERVIEW"));

        NavigationItems.Add(new NavigationItem(
            PageKeys.PointOfSale, "Point of Sale", "",
            () => Planned("Point of Sale", "Sell medicines quickly and accurately", "",
                "Barcode scanning, cart, FEFO stock selection, payments and receipts.",
                "Phase 4"),
            section: "OPERATIONS"));

        NavigationItems.Add(new NavigationItem(
            PageKeys.Medicines, "Medicines", "",
            () => _services.GetRequiredService<MedicinesViewModel>()));

        NavigationItems.Add(new NavigationItem(
            PageKeys.Inventory, "Inventory", "",
            () => _services.GetRequiredService<InventoryViewModel>()));

        NavigationItems.Add(new NavigationItem(
            PageKeys.Suppliers, "Suppliers", "",
            () => _services.GetRequiredService<SuppliersViewModel>()));

        NavigationItems.Add(new NavigationItem(
            PageKeys.Lists, "Catalog lists", "",
            () => _services.GetRequiredService<ListsViewModel>()));

        NavigationItems.Add(new NavigationItem(
            PageKeys.Reports, "Reports", "",
            () => Planned("Reports", "Sales and inventory insights", "",
                "Sales summaries, inventory valuation, expiry and stock movement reports.",
                "Phase 8")));

        if (_session.IsAdmin)
        {
            NavigationItems.Add(new NavigationItem(
                PageKeys.Users, "Users", "",
                () => Planned("Users", "Staff accounts and roles", "",
                    "Create staff accounts, assign roles and review audit logs.",
                    "Phase 7"),
                section: "ADMINISTRATION"));

            NavigationItems.Add(new NavigationItem(
                PageKeys.Settings, "Settings", "",
                () => Planned("Settings", "Store, tax and backup settings", "",
                    "Store details, VAT and discount rules, receipt setup and database backup.",
                    "Phase 5 and 7")));
        }
    }

    private static PlaceholderViewModel Planned(
        string title, string subtitle, string glyph, string description, string phase) =>
        new(title, subtitle, glyph, description, phase);
}
