using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FileOrganizer.Commands;
using FileOrganizer.Services;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Owns the Search tab. Fully self-contained: it maintains its own status line
    /// (SearchStatus) and the only shared state it reads is the destination folder,
    /// used as a fallback search root, obtained via ITransferSettingsProvider.
    /// </summary>
    public class SearchViewModel : ViewModelBase
    {
        private readonly ITransferSettingsProvider _settings;
        private CancellationTokenSource _searchCts;

        public SearchViewModel(ITransferSettingsProvider settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            RunSearchCommand = new RelayCommand(async _ => await RunSearchAsync());
            CancelSearchCommand = new RelayCommand(_ => _searchCts?.Cancel());
            BrowseSearchFolderCommand = new RelayCommand(_ => BrowseSearchFolder());
            OpenSearchHitFolderCommand = new RelayCommand(OpenSearchHitFolder);
            ClearSearchResultsCommand = new RelayCommand(_ =>
            {
                SearchResults.Clear();
                SearchStatus = "Results cleared.";
            });
        }

        public ObservableCollection<SearchHit> SearchResults { get; } = new ObservableCollection<SearchHit>();

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        private string _searchFolder = "";
        public string SearchFolder
        {
            get => _searchFolder;
            set => SetProperty(ref _searchFolder, value);
        }

        private bool _searchContents = true;
        public bool SearchContents
        {
            get => _searchContents;
            set => SetProperty(ref _searchContents, value);
        }

        private bool _searchIncludeSubfolders = true;
        public bool SearchIncludeSubfolders
        {
            get => _searchIncludeSubfolders;
            set => SetProperty(ref _searchIncludeSubfolders, value);
        }

        private bool _isSearching;
        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        private string _searchStatus = "Enter a search term and choose a folder.";
        public string SearchStatus
        {
            get => _searchStatus;
            set => SetProperty(ref _searchStatus, value);
        }

        public ICommand RunSearchCommand { get; }
        public ICommand CancelSearchCommand { get; }
        public ICommand BrowseSearchFolderCommand { get; }
        public ICommand OpenSearchHitFolderCommand { get; }
        public ICommand ClearSearchResultsCommand { get; }

        private void BrowseSearchFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SearchFolder = dialog.SelectedPath;
                SearchStatus = $"Search folder: {dialog.SelectedPath}";
            }
        }

        private void OpenSearchHitFolder(object parameter)
        {
            if (parameter is SearchHit hit && System.IO.File.Exists(hit.FullPath))
            {
                try
                {
                    // Open Explorer with the file selected.
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{hit.FullPath}\"");
                }
                catch (Exception ex)
                {
                    SearchStatus = $"Could not open folder: {ex.Message}";
                }
            }
        }

        private async Task RunSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                SearchStatus = "Enter a search term first.";
                return;
            }

            var folder = !string.IsNullOrWhiteSpace(SearchFolder)
                ? SearchFolder
                : _settings.DestinationFolder;

            if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
            {
                SearchStatus = "Choose a folder to search (or set a destination folder).";
                return;
            }

            SearchResults.Clear();
            IsSearching = true;
            _searchCts = new CancellationTokenSource();

            try
            {
                var service = new FileSearchService();
                var progress = new Progress<SearchProgress>(p =>
                {
                    SearchStatus = $"Scanned {p.FilesScanned} files, {p.Hits} hit(s)… {p.CurrentFile}";
                });

                var hits = await service.SearchAsync(
                    new[] { folder },
                    SearchQuery,
                    SearchContents,
                    SearchIncludeSubfolders,
                    progress,
                    _searchCts.Token);

                foreach (var hit in hits) SearchResults.Add(hit);

                if (_searchCts.IsCancellationRequested)
                    SearchStatus = $"Search cancelled — {SearchResults.Count} result(s) found so far.";
                else if (SearchResults.Count == 0)
                    SearchStatus = "No matches found.";
                else if (SearchResults.Count >= FileSearchService.MaxResults)
                    SearchStatus = $"Showing first {SearchResults.Count} results (limit reached). Narrow your search for more precision.";
                else
                    SearchStatus = $"{SearchResults.Count} result(s) found.";
            }
            catch (Exception ex)
            {
                SearchStatus = $"Search error: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
                _searchCts?.Dispose();
                _searchCts = null;
            }
        }
    }
}
