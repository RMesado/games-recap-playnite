using GamesRecap.Models;
using GamesRecap.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GamesRecap.ViewModels
{
    public class BrowserViewModel : ObservableObject
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly GamesRecapApiClient apiClient;
        private readonly LocalDatabase database;

        private string searchText;
        private string selectedSort = "newest";
        private bool isLoading;
        private string errorMessage;
        private int currentPage = 1;
        private int totalPages;
        private int totalCards;
        private HashSet<int> wishlistedIds = new HashSet<int>();
        private bool hasLoadedOnce;

        public ObservableCollection<CardViewModel> Cards { get; } = new ObservableCollection<CardViewModel>();
        public ObservableCollection<FilterItem> PlatformFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> GenreFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> TagFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> ShowcaseFilters { get; } = new ObservableCollection<FilterItem>();
        public ObservableCollection<SortOption> SortOptions { get; } = new ObservableCollection<SortOption>();

        public string SearchText
        {
            get => searchText;
            set => SetValue(ref searchText, value);
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
            set
            {
                if (isLoading == value) return;
                isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(ShowLoadingSpinner));
            }
        }

        public bool ShowLoadingSpinner => IsLoading && !hasLoadedOnce;

        public string ErrorMessage
        {
            get => errorMessage;
            set
            {
                if (errorMessage == value) return;
                errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
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

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public RelayCommand SearchCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand<int> ToggleWishlistCommand { get; }
        public RelayCommand<string> OpenTrailerCommand { get; }

        public BrowserViewModel(GamesRecapApiClient apiClient, LocalDatabase database)
        {
            this.apiClient = apiClient;
            this.database = database;

            SearchCommand = new RelayCommand(() => _ = LoadCardsAsync(1));
            NextPageCommand = new RelayCommand(() => _ = LoadCardsAsync(CurrentPage + 1), () => HasNextPage);
            PrevPageCommand = new RelayCommand(() => _ = LoadCardsAsync(CurrentPage - 1), () => HasPreviousPage);
            ToggleWishlistCommand = new RelayCommand<int>(gameId => ToggleWishlist(gameId));
            OpenTrailerCommand = new RelayCommand<string>(url => OpenTrailer(url));

            LoadWishlistState();
            _ = LoadCardsAsync(1);
        }

        public async Task LoadCardsAsync(int page)
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var filters = BuildActiveFilters();
                filters.Page = page;

                var props = await apiClient.FetchCardsAsync(filters);

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
                Genres = GetSelectedIds(GenreFilters),
                Tags = GetSelectedIds(TagFilters),
                Showcases = GetSelectedIds(ShowcaseFilters),
                Sort = SelectedSort
            };

            if (!string.IsNullOrEmpty(SearchText))
                filters.Q = SearchText;

            return filters;
        }

        private static List<int> GetSelectedIds(ObservableCollection<FilterItem> items)
        {
            return items.Where(f => f.IsSelected).Select(f => f.Id).ToList();
        }

        private void PopulateFilters(FilterOptions options)
        {
            void PopulateList(ObservableCollection<FilterItem> target, IEnumerable<OptionItem> source, Action onChanged)
            {
                target.Clear();
                if (source == null) return;
                foreach (var item in source)
                {
                    var fi = new FilterItem { Id = item.Id, Name = item.Name };
                    fi.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(FilterItem.IsSelected))
                            _ = LoadCardsAsync(1);
                    };
                    target.Add(fi);
                }
            }

            PopulateList(PlatformFilters, options.Platforms, null);
            PopulateList(GenreFilters, options.Genres, null);
            PopulateList(TagFilters, options.Tags, null);

            ShowcaseFilters.Clear();
            if (options.Showcases != null)
            {
                foreach (var s in options.Showcases)
                {
                    var fi = new FilterItem { Id = s.Id, Name = s.Name };
                    fi.PropertyChanged += (s_, e) =>
                    {
                        if (e.PropertyName == nameof(FilterItem.IsSelected))
                            _ = LoadCardsAsync(1);
                    };
                    ShowcaseFilters.Add(fi);
                }
            }

            SortOptions.Clear();
            if (options.Sorts != null)
            {
                foreach (var s in options.Sorts)
                    SortOptions.Add(s);
            }
        }

        public void LoadWishlistState()
        {
            try
            {
                var ids = database.GetWishlistedIds();
                wishlistedIds = new HashSet<int>(ids);
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

                foreach (var card in Cards)
                    if (card.GameId == gameId)
                        card.NotifyWishlistChanged();
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
    }

    public class CardViewModel : ObservableObject
    {
        private readonly Card card;
        private readonly BrowserViewModel parent;

        public int Id => card.Id;
        public int GameId => card.GameId;
        public string Title => card.Game?.Title;
        public string CoverUrl => card.Game?.CoverImageUrl;
        public string ShowcaseName => card.Showcase?.Name;
        public string ShowcaseEventName => card.Showcase?.EventName;
        public string TrailerUrl => card.Media?.FirstOrDefault()?.Url;
        public bool HasTrailer => !string.IsNullOrEmpty(TrailerUrl);
        public string ReleaseDate => card.Game?.ReleaseDate;
        public string Kind => card.Game?.Kind;

        public bool IsWishlisted => parent != null && parent.IsGameWishlisted(GameId);

        public string PlatformNames
        {
            get
            {
                if (card.Game?.Platforms == null || card.Game.Platforms.Count == 0)
                    return null;
                return string.Join(", ", card.Game.Platforms.Select(p => p.Name));
            }
        }

        public string GenreNames
        {
            get
            {
                if (card.Game?.Genres == null || card.Game.Genres.Count == 0)
                    return null;
                return string.Join(", ", card.Game.Genres.Select(g => g.Name));
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
