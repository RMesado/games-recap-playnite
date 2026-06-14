using GamesRecap.Models;
using GamesRecap.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace GamesRecap.ViewModels
{
    public class BrowserViewModel : ObservableObject
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly GamesRecapApiClient apiClient;
        private readonly LocalDatabase database;
        private readonly IPlayniteAPI playniteApi;

        private string searchText;
        private string selectedSort = "newest";
        private bool isLoading;
        private string errorMessage;
        private int currentPage = 1;
        private int totalPages;
        private int totalCards;
        private HashSet<int> wishlistedIds = new HashSet<int>();
        private bool hasLoadedOnce;
        private int selectedShowcaseYear;
        private DateTime? releaseDateFrom;
        private DateTime? releaseDateTo;
        private int requestGeneration;
        private bool isWishlistFilterActive;

        private string platformFilterSearch;
        private string genreFilterSearch;
        private string tagFilterSearch;
        private string showcaseFilterSearch;

        public ObservableCollection<CardViewModel> Cards { get; } = new ObservableCollection<CardViewModel>();
        public ObservableCollection<FilterItem> PlatformFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> GenreFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> TagFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> ShowcaseFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<SortOption> SortOptions { get; } = new ObservableCollection<SortOption>();
        public ObservableCollection<int> AvailableYears { get; } = new ObservableCollection<int>();
        public ObservableCollection<YearChipItem> ShowcaseYearChips { get; } = new ObservableCollection<YearChipItem>();
        public ObservableCollection<FilterItem> ShowcaseIndividualChips { get; } = new ObservableCollection<FilterItem>();

        private ICollectionView platformView;
        private ICollectionView genreView;
        private ICollectionView tagView;
        private ICollectionView showcaseView;

        public string SearchText
        {
            get => searchText;
            set
            {
                if (searchText == value) return;
                searchText = value;
                OnPropertyChanged(nameof(SearchText));
                _ = LoadCardsAsync(1);
            }
        }

        public string SelectedSort
        {
            get => selectedSort;
            set
            {
                if (selectedSort == value) return;
                selectedSort = value;
                OnPropertyChanged(nameof(SelectedSort));
                _ = LoadCardsAsync(1);
            }
        }

        public bool IsLoading
        {
            get => isLoading;
            set => SetValue(ref isLoading, value);
        }

        public bool IsWishlistFilterActive
        {
            get => isWishlistFilterActive;
            set
            {
                if (isWishlistFilterActive == value) return;
                isWishlistFilterActive = value;
                OnPropertyChanged(nameof(IsWishlistFilterActive));
                _ = LoadCardsAsync(1);
            }
        }

        public int WishlistCount => wishlistedIds.Count;

        public string ErrorMessage
        {
            get => errorMessage;
            set => SetValue(ref errorMessage, value);
        }

        public int CurrentPage
        {
            get => currentPage;
            set
            {
                if (currentPage == value) return;
                currentPage = value;
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
            }
        }

        public int TotalPages
        {
            get => totalPages;
            set
            {
                if (totalPages == value) return;
                totalPages = value;
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
            }
        }

        public int TotalCards
        {
            get => totalCards;
            set
            {
                if (totalCards == value) return;
                totalCards = value;
                OnPropertyChanged(nameof(TotalCards));
            }
        }

        public int SelectedShowcaseYear
        {
            get => selectedShowcaseYear;
            set
            {
                if (selectedShowcaseYear == value) return;
                selectedShowcaseYear = value;
                OnPropertyChanged(nameof(SelectedShowcaseYear));
                OnPropertyChanged(nameof(ShowcaseFilterHeader));
                RefreshShowcaseFilter();
                OnPropertyChanged(nameof(ShowcaseSelectAllState));
            }
        }

        public DateTime? ReleaseDateFrom
        {
            get => releaseDateFrom;
            set
            {
                if (releaseDateFrom == value) return;
                releaseDateFrom = value;
                OnPropertyChanged(nameof(ReleaseDateFrom));
                _ = LoadCardsAsync(1);
            }
        }

        public DateTime? ReleaseDateTo
        {
            get => releaseDateTo;
            set
            {
                if (releaseDateTo == value) return;
                releaseDateTo = value;
                OnPropertyChanged(nameof(ReleaseDateTo));
                _ = LoadCardsAsync(1);
            }
        }

        public string PlatformFilterSearch
        {
            get => platformFilterSearch;
            set
            {
                if (platformFilterSearch == value) return;
                platformFilterSearch = value;
                OnPropertyChanged(nameof(PlatformFilterSearch));
                ApplyFilterText(platformView, value);
            }
        }

        public string GenreFilterSearch
        {
            get => genreFilterSearch;
            set
            {
                if (genreFilterSearch == value) return;
                genreFilterSearch = value;
                OnPropertyChanged(nameof(GenreFilterSearch));
                ApplyFilterText(genreView, value);
            }
        }

        public string TagFilterSearch
        {
            get => tagFilterSearch;
            set
            {
                if (tagFilterSearch == value) return;
                tagFilterSearch = value;
                OnPropertyChanged(nameof(TagFilterSearch));
                ApplyFilterText(tagView, value);
            }
        }

        public string ShowcaseFilterSearch
        {
            get => showcaseFilterSearch;
            set
            {
                if (showcaseFilterSearch == value) return;
                showcaseFilterSearch = value;
                OnPropertyChanged(nameof(ShowcaseFilterSearch));
                ApplyFilterText(showcaseView, value);
            }
        }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public string PlatformFilterHeader => FormatFilterHeader("Platforms", PlatformFilters);
        public string GenreFilterHeader => FormatFilterHeader("Genres", GenreFilters);
        public string TagFilterHeader => FormatFilterHeader("Tags", TagFilters);
        public string ShowcaseFilterHeader => FormatFilterHeader("Showcases", ShowcaseFilters);

        public bool? ShowcaseSelectAllState
        {
            get
            {
                var visible = showcaseView?.Cast<FilterItem>().ToList();
                if (visible == null || visible.Count == 0) return false;
                var selected = visible.Count(f => f.IsSelected);
                if (selected == visible.Count) return true;
                if (selected == 0) return false;
                return null;
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand GoBackToLibraryCommand { get; }
        public ICommand ToggleWishlistFilterCommand { get; }
        public RelayCommand<int> ToggleWishlistCommand { get; }
        public RelayCommand<string> OpenTrailerCommand { get; }
        public RelayCommand<FilterItem> RemoveFilterCommand { get; }
        public ICommand ClearYearFilterCommand { get; }
        public ICommand ClearAllFiltersCommand { get; }
        public ICommand ClearReleaseDateFromCommand { get; }
        public ICommand ClearReleaseDateToCommand { get; }
        public ICommand SelectAllPlatformsCommand { get; }
        public ICommand SelectAllGenresCommand { get; }
        public ICommand SelectAllTagsCommand { get; }
        public ICommand SelectAllShowcasesCommand { get; }
        public ICommand ToggleSelectAllShowcasesCommand { get; }
        public ICommand DeselectAllInYearCommand { get; }

        public BrowserViewModel(GamesRecapApiClient apiClient, LocalDatabase database, IPlayniteAPI playniteApi)
        {
            this.apiClient = apiClient;
            this.database = database;
            this.playniteApi = playniteApi;

            SearchCommand = new RelayCommand(() => _ = LoadCardsAsync(1));
            NextPageCommand = new RelayCommand(() => _ = LoadCardsAsync(CurrentPage + 1), () => HasNextPage);
            PrevPageCommand = new RelayCommand(() => _ = LoadCardsAsync(CurrentPage - 1), () => HasPreviousPage);
            ToggleWishlistCommand = new RelayCommand<int>(gameId => ToggleWishlist(gameId));
            OpenTrailerCommand = new RelayCommand<string>(url => OpenTrailer(url));
            RemoveFilterCommand = new RelayCommand<FilterItem>(item => { if (item != null) item.IsSelected = false; });
            ClearYearFilterCommand = new RelayCommand(() => { SelectedShowcaseYear = 0; });
            ClearAllFiltersCommand = new RelayCommand(ClearAllFilters);
            SelectAllPlatformsCommand = new RelayCommand(() => SelectAll(PlatformFilters));
            SelectAllGenresCommand = new RelayCommand(() => SelectAll(GenreFilters));
            SelectAllTagsCommand = new RelayCommand(() => SelectAll(TagFilters));
            SelectAllShowcasesCommand = new RelayCommand(() => SelectAllVisibleShowcases());
            ToggleSelectAllShowcasesCommand = new RelayCommand(() => ToggleSelectAllShowcases());
            DeselectAllInYearCommand = new RelayCommand<int>(year => DeselectAllInYear(year));
            ClearReleaseDateFromCommand = new RelayCommand(() => ReleaseDateFrom = null);
            ClearReleaseDateToCommand = new RelayCommand(() => ReleaseDateTo = null);
            GoBackToLibraryCommand = new RelayCommand(() => playniteApi.MainView.SwitchToLibraryView());
            ToggleWishlistFilterCommand = new RelayCommand(() => IsWishlistFilterActive = !IsWishlistFilterActive);

            LoadWishlistState();
            _ = LoadCardsAsync(1);
        }

        private void ClearAllFilters()
        {
            SearchText = string.Empty;
            SelectedSort = "newest";
            SelectedShowcaseYear = 0;
            ReleaseDateFrom = null;
            ReleaseDateTo = null;
            DeselectAll(PlatformFilters);
            DeselectAll(GenreFilters);
            DeselectAll(TagFilters);
            DeselectAll(ShowcaseFilters);
            ShowcaseYearChips.Clear();
            ShowcaseIndividualChips.Clear();
            _ = LoadCardsAsync(1);
        }

        private static void SelectAll(ObservableCollection<FilterItem> items)
        {
            foreach (var item in items)
                item.IsSelected = true;
        }

        private void SelectAllVisibleShowcases()
        {
            var visible = showcaseView?.Cast<FilterItem>().ToList();
            if (visible == null) return;
            foreach (var item in visible)
                item.IsSelected = true;
        }

        private void ToggleSelectAllShowcases()
        {
            var visible = showcaseView?.Cast<FilterItem>().ToList();
            if (visible == null || visible.Count == 0) return;
            var allSelected = visible.All(f => f.IsSelected);
            foreach (var item in visible)
                item.IsSelected = !allSelected;
        }

        private static void DeselectAll(ObservableCollection<FilterItem> items)
        {
            foreach (var item in items)
                item.IsSelected = false;
        }

        private static string FormatFilterHeader(string name, ObservableCollection<FilterItem> items)
        {
            var count = items.Count(f => f.IsSelected);
            return count > 0 ? $"{name} ({count})" : name;
        }

        private static void ApplyFilterText(ICollectionView view, string text)
        {
            if (view == null) return;
            if (string.IsNullOrEmpty(text))
                view.Filter = null;
            else
                view.Filter = item => item is FilterItem fi &&
                    fi.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task LoadCardsAsync(int page)
        {
            var currentGen = System.Threading.Interlocked.Increment(ref requestGeneration);
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var filters = BuildActiveFilters();
                filters.Page = page;

                var props = await apiClient.FetchCardsAsync(filters);

                if (currentGen != requestGeneration) return;

                if (props == null)
                {
                    ErrorMessage = "No se pudo conectar con el servidor";
                    return;
                }

                Cards.Clear();
                if (props.Pages?.Data != null)
                {
                    foreach (var card in props.Pages.Data)
                        Cards.Add(new CardViewModel(card, this));
                }

                if (props.Pages != null)
                {
                    CurrentPage = props.Pages.CurrentPage;
                    TotalPages = props.Pages.LastPage;
                    TotalCards = props.Pages.Total;
                }

                    if (props.Options != null && !hasLoadedOnce)
                    PopulateFilters(props.Options);

                hasLoadedOnce = true;
            }
            catch (Exception ex) when (currentGen != requestGeneration)
            {
                // Stale request superseded by a newer one, ignore
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading cards");
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private ActiveFilters BuildActiveFilters()
        {
            var filters = new ActiveFilters
            {
                Platforms = GetSelectedIds(PlatformFilters),
                ExcludePlatforms = GetExcludedIds(PlatformFilters),
                Genres = GetSelectedIds(GenreFilters),
                ExcludeGenres = GetExcludedIds(GenreFilters),
                Tags = GetSelectedIds(TagFilters),
                ExcludeTags = GetExcludedIds(TagFilters),
                Showcases = GetSelectedIds(ShowcaseFilters),
                Sort = SelectedSort
            };

            if (!string.IsNullOrEmpty(SearchText))
                filters.Q = SearchText;

            if (ReleaseDateFrom.HasValue)
                filters.ReleaseFrom = ReleaseDateFrom.Value.ToString("yyyy-MM-dd");

            if (ReleaseDateTo.HasValue)
                filters.ReleaseTo = ReleaseDateTo.Value.ToString("yyyy-MM-dd");

            if (IsWishlistFilterActive && wishlistedIds.Count > 0)
            {
                filters.WishlistedIds = wishlistedIds.ToList();
                filters.WishlistedMode = "include";
            }

            return filters;
        }

        private static List<int> GetSelectedIds(ObservableCollection<FilterItem> items)
        {
            return items.Where(f => f.IsSelected).Select(f => f.Id).ToList();
        }

        private static List<int> GetExcludedIds(ObservableCollection<FilterItem> items)
        {
            return items.Where(f => f.IsExcluded).Select(f => f.Id).ToList();
        }

        private void PopulateFilters(FilterOptions options)
        {
            void PopulateList(ObservableCollection<FilterItem> target, IEnumerable<OptionItem> source)
            {
                target.Clear();
                if (source == null) return;
                foreach (var item in source)
                {
                    var fi = new FilterItem { Id = item.Id, Name = item.Name };
                    fi.PropertyChanged += OnFilterChanged;
                    target.Add(fi);
                }
            }

            PopulateList(PlatformFilters, options.Platforms);
            PopulateList(GenreFilters, options.Genres);
            PopulateList(TagFilters, options.Tags);

            ShowcaseFilters.Clear();
            var years = new HashSet<int>();
            if (options.Showcases != null)
            {
                foreach (var s in options.Showcases)
                {
                    var year = ParseYear(s.StartAt);
                    if (year > 0) years.Add(year);

                    var fi = new FilterItem { Id = s.Id, Name = s.Name, Year = year };
                    fi.PropertyChanged += OnFilterChanged;
                    ShowcaseFilters.Add(fi);
                }
            }

            AvailableYears.Clear();
            AvailableYears.Add(0);
            foreach (var y in years.OrderByDescending(y => y))
                AvailableYears.Add(y);

            var currentYear = DateTime.Now.Year;
            if (years.Count > 0)
            {
                var closest = years.OrderBy(y => Math.Abs(y - currentYear)).First();
                SelectedShowcaseYear = closest;
                SelectShowcasesForYear(closest);
                UpdateShowcaseChips();
            }

            SortOptions.Clear();
            if (options.Sorts != null)
            {
                foreach (var s in options.Sorts)
                    SortOptions.Add(s);
            }

            RefreshFilterViews();
        }

        private void RefreshFilterViews()
        {
            platformView = CollectionViewSource.GetDefaultView(PlatformFilters);
            genreView = CollectionViewSource.GetDefaultView(GenreFilters);
            tagView = CollectionViewSource.GetDefaultView(TagFilters);
            showcaseView = CollectionViewSource.GetDefaultView(ShowcaseFilters);
            RefreshShowcaseFilter();
        }

        private void RefreshShowcaseFilter()
        {
            if (showcaseView == null) return;
            if (SelectedShowcaseYear > 0)
                showcaseView.Filter = item => item is FilterItem fi && fi.Year == SelectedShowcaseYear;
            else
                showcaseView.Filter = null;
        }

        private void OnFilterChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(FilterItem.IsSelected) &&
                e.PropertyName != nameof(FilterItem.IsExcluded))
                return;

            OnPropertyChanged(nameof(PlatformFilterHeader));
            OnPropertyChanged(nameof(GenreFilterHeader));
            OnPropertyChanged(nameof(TagFilterHeader));
            OnPropertyChanged(nameof(ShowcaseFilterHeader));
            OnPropertyChanged(nameof(ShowcaseSelectAllState));
            UpdateShowcaseChips();

            _ = LoadCardsAsync(1);
        }

        private void SelectShowcasesForYear(int year)
        {
            foreach (var fi in ShowcaseFilters)
                fi.IsSelected = fi.Year == year;
        }

        private static int ParseYear(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return 0;
            if (DateTime.TryParse(dateStr, out var dt))
                return dt.Year;
            return 0;
        }

        private void UpdateShowcaseChips()
        {
            ShowcaseYearChips.Clear();
            ShowcaseIndividualChips.Clear();

            var selectedByYear = ShowcaseFilters
                .Where(f => f.IsSelected && f.Year > 0)
                .GroupBy(f => f.Year)
                .ToList();

            foreach (var item in ShowcaseFilters.Where(f => f.IsSelected && f.Year == 0))
                ShowcaseIndividualChips.Add(item);

            foreach (var group in selectedByYear)
            {
                var year = group.Key;
                var allInYear = ShowcaseFilters.Where(f => f.Year == year).ToList();
                if (group.Count() == allInYear.Count && allInYear.Count > 0)
                {
                    ShowcaseYearChips.Add(new YearChipItem
                    {
                        Year = year,
                        ChipText = $"All showcases in {year}",
                        DeselectAllInYearCommand = DeselectAllInYearCommand
                    });
                }
                else
                {
                    foreach (var item in group)
                        ShowcaseIndividualChips.Add(item);
                }
            }
        }

        private void DeselectAllInYear(int year)
        {
            foreach (var fi in ShowcaseFilters.Where(f => f.Year == year))
                fi.IsSelected = false;
        }

        public void LoadWishlistState()
        {
            try
            {
                var ids = database.GetWishlistedIds();
                wishlistedIds = new HashSet<int>(ids);
                OnPropertyChanged(nameof(WishlistCount));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading wishlist state");
            }
        }

        public bool IsGameWishlisted(int gameId) => wishlistedIds.Contains(gameId);

        public void ToggleWishlist(int gameId)
        {
            try
            {
                var isWishlisted = !wishlistedIds.Contains(gameId);
                database.SetGameState(gameId, isWishlisted, false, false);

                if (isWishlisted)
                    wishlistedIds.Add(gameId);
                else
                    wishlistedIds.Remove(gameId);

                OnPropertyChanged(nameof(WishlistCount));

                foreach (var card in Cards)
                    if (card.GameId == gameId)
                        card.NotifyWishlistChanged();

                if (IsWishlistFilterActive)
                    _ = LoadCardsAsync(1);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error toggling wishlist");
            }
        }

        private static void OpenTrailer(string url)
        {
            if (!string.IsNullOrEmpty(url))
                Process.Start(url);
        }
    }

    public class FilterItem : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Year { get; set; }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        private bool isExcluded;
        public bool IsExcluded
        {
            get => isExcluded;
            set
            {
                if (isExcluded == value) return;
                isExcluded = value;
                OnPropertyChanged(nameof(IsExcluded));
            }
        }
    }

    public class YearChipItem
    {
        public int Year { get; set; }
        public string ChipText { get; set; }
        public ICommand DeselectAllInYearCommand { get; set; }
    }

    public class CardViewModel : ObservableObject
    {
        private readonly Card card;
        private readonly BrowserViewModel parent;

        public int Id => card.Id;
        public int GameId => card.GameId;
        public string Title => card.Game?.Title;
        public string CoverUrl => card.Game?.CoverImageUrl;
        public string DisplayImageUrl => !string.IsNullOrEmpty(card.Game?.ScreenshotUrl)
            ? card.Game.ScreenshotUrl : card.Game?.CoverImageUrl;
        public string ShowcaseName => card.Showcase?.Name;
        public string ShowcaseEventName => card.Showcase?.EventName;
        public string TrailerUrl => card.Media?.FirstOrDefault()?.Url;
        public bool HasTrailer => !string.IsNullOrEmpty(TrailerUrl);
        public string ReleaseDate => card.Game?.ReleaseDate;

        public bool IsWishlisted => parent != null && parent.IsGameWishlisted(GameId);

        public List<GrTag> Tags => card.Game?.Tags ?? card.Tags ?? new List<GrTag>();

        public string PublisherName => card.Game?.Publisher?.Name;
        public List<GrPlatform> PlatformsList => card.Game?.Platforms ?? new List<GrPlatform>();
        public List<GrGenre> GenresList => card.Game?.Genres ?? new List<GrGenre>();

        public string ShowcaseDate
        {
            get
            {
                if (card.Showcase?.StartAt == null) return null;
                if (DateTime.TryParse(card.Showcase.StartAt, out var dt))
                    return dt.ToString("d MMM yyyy").ToUpper();
                return null;
            }
        }

        public bool HasShowcase => !string.IsNullOrEmpty(ShowcaseName);
        public bool HasTags => Tags != null && Tags.Count > 0;

        public string PlatformNames
        {
            get
            {
                if (card.Game?.Platforms == null || card.Game.Platforms.Count == 0)
                    return null;
                return string.Join(", ", card.Game.Platforms.Select(p => p.Name));
            }
        }

        public CardViewModel(Card card, BrowserViewModel parent)
        {
            this.card = card;
            this.parent = parent;
        }

        public void NotifyWishlistChanged()
        {
            OnPropertyChanged(nameof(IsWishlisted));
        }
    }
}
