using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageProcessor.Admin.Models;
using ImageProcessor.Admin.Properties;
using Microsoft.AspNetCore.SignalR.Client;

namespace ImageProcessor.Admin.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        #region Property backing fields

        private IDisposable? _connection;

        [ObservableProperty]
        private Brush _connectionStateFill = new SolidColorBrush(Colors.Red);

        [ObservableProperty]
        private double _keywordCountFontSize;

        private readonly ObservableCollection<KeywordViewModel> _postedKeywords =
            new ObservableCollection<KeywordViewModel>();

        private readonly ObservableCollection<string> _originalImagePaths = new ObservableCollection<string>();

        [ObservableProperty]
        private int _imagesPerKeyword = 3;

        [ObservableProperty]
        private int _imageSearcherThreadCount = 4;

        [ObservableProperty]
        private int _imageRetrieverThreadCount = 4;

        [ObservableProperty]
        private bool _isSingleThreadMode = true;

        [ObservableProperty]
        private bool _isMultiThreadMode;

        [ObservableProperty]
        private int _imageProcessorThreadCount = 4;

        [ObservableProperty]
        private int _processingMilliseconds;

        private readonly ObservableCollection<string> _resultImagePaths = new ObservableCollection<string>();

        #endregion Property backing fields

        public MainViewModel()
        {
            RefreshConnectionStateFill();

            _postedKeywords.CollectionChanged += PostedKeywordsOnCollectionChanged;
            _originalImagePaths.CollectionChanged += OriginalImagePathsOnCollectionChanged;
            _resultImagePaths.CollectionChanged += ResultImagePathsOnCollectionChanged;

#if DEBUG
            _postedKeywords.Add(new KeywordViewModel("ジョジョの奇妙な冒険"));
            _postedKeywords.Add(new KeywordViewModel("魔人ブウ"));
            _postedKeywords.Last().NotifyPosted();
            _postedKeywords.Last().NotifyPosted();
            _postedKeywords.Last().NotifyPosted();
            _postedKeywords.Last().NotifyPosted();
            _postedKeywords.Last().NotifyPosted();
#endif
        }

        #region Keywords

        public IDisposable? Connection
        {
            get => _connection;
            private set
            {
                var oldValue = _connection;
                if (oldValue != null && !ReferenceEquals(oldValue, value))
                {
                    oldValue.Dispose();
                }

                SetProperty(ref _connection, value);

                RefreshConnectionStateFill();
            }
        }

        private void RefreshConnectionStateFill()
        {
            ConnectionStateFill = Connection != null
                ? new SolidColorBrush(Colors.LawnGreen)
                : new SolidColorBrush(Colors.Red);
        }

        [RelayCommand(CanExecute = nameof(CanStartReceivingKeywords))]
        private async Task StartReceivingKeywords()
        {
            Debug.Assert(Connection == null, "Connection == null");

            var connection = new HubConnectionBuilder()
                .WithUrl(Settings.Default.WebSiteUrl + "/hubs/keyword")
                .Build();

            connection.Closed += async (error) =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() => Connection = null);
            };

            connection.On<string>("AddPostedKeyword", OnKeywordPosted);

            try
            {
                await connection.StartAsync();
                Connection = connection as IDisposable;
            }
            catch (HttpRequestException)
            {
                MessageBox.Show("サーバーに繋がりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanStartReceivingKeywords() => Connection == null;

        private void OnKeywordPosted(string keyword)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existingKeyword =
                    PostedKeywords.FirstOrDefault(
                        k => string.Equals(k.Value, keyword, StringComparison.OrdinalIgnoreCase));
                if (existingKeyword == null)
                {
                    PostedKeywords.Add(new KeywordViewModel(keyword));
                    return;
                }
                existingKeyword.NotifyPosted();
            });
        }

        [RelayCommand(CanExecute = nameof(CanStopReceivingKeywords))]
        private void StopReceivingKeywords()
        {
            Debug.Assert(Connection != null, "Connection != null");
            Connection = null;
        }

        private bool CanStopReceivingKeywords() => Connection != null;

        public ObservableCollection<KeywordViewModel> PostedKeywords => _postedKeywords;

        private void PostedKeywordsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // ReSharper disable PossibleLossOfFraction
            KeywordCountFontSize = SystemFonts.MessageFontSize + PostedKeywords.Count / 5;
            // ReSharper restore PossibleLossOfFraction

            ClearKeywordsCommand.NotifyCanExecuteChanged();
            SearchImagesCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanClearKeywords))]
        private void ClearKeywords()
        {
            PostedKeywords.Clear();
        }

        private bool CanClearKeywords() => PostedKeywords.Any();

        #endregion Keywords

        #region Search

        [RelayCommand(CanExecute = nameof(CanSearchImages))]
        private async Task SearchImages()
        {
            ClearOriginalImages();

            // Channel of Producer-Consumer pattern
            using (var searcherChannel = new BlockingCollection<string>())
            using (var retrieverChannel = new BlockingCollection<Uri>())
            {
                var keywordCount = PostedKeywords.Count;
                var processedKeywordCount = 0;

                foreach (var keyword in PostedKeywords)
                {
                    searcherChannel.Add(keyword.Value);
                }
                searcherChannel.CompleteAdding();

                var searcherTasks = new List<Task>();
                var subscriptions = new List<IDisposable>();
                for (int i = 0; i < ImageSearcherThreadCount; i++)
                {
                    var searcher = new ImageSearcher(searcherChannel, ImagesPerKeyword);
                    subscriptions.Add(
                        Observable.FromEventPattern<ImageSearchedEventArgs>(
                            eh => searcher.ImageSearched += eh,
                            eh => searcher.ImageSearched -= eh)
                                  .Subscribe(args =>
                                  {
                                      // ReSharper disable AccessToDisposedClosure
                                      foreach (var result in args.EventArgs.ImageSearchResult.d.results)
                                      {
                                          retrieverChannel.Add(new Uri(result.MediaUrl));
                                      }
                                      if (keywordCount <= Interlocked.Increment(ref processedKeywordCount))
                                      {
                                          retrieverChannel.CompleteAdding();
                                      }
                                      // ReSharper restore AccessToDisposedClosure
                                  }));
                    subscriptions.Add(
                        Observable.FromEventPattern<ExceptionThrownEventArgs<string>>(
                            eh => searcher.ExceptionThrown += eh,
                            eh => searcher.ExceptionThrown -= eh)
                                  .Subscribe(args =>
                                  {
                                      if (keywordCount <= Interlocked.Increment(ref processedKeywordCount))
                                      {
                                          // ReSharper disable AccessToDisposedClosure
                                          retrieverChannel.CompleteAdding();
                                          // ReSharper restore AccessToDisposedClosure
                                      }
                                  }));

                    searcherTasks.Add(Task.Run(async () => await searcher.Run()));
                }

                var retrieverTasks = new List<Task>();
                for (int i = 0; i < ImageSearcherThreadCount; i++)
                {
                    var searcher = new ImageRetriever(retrieverChannel);
                    subscriptions.Add(
                        Observable.FromEventPattern<ImageRetrievedEventArgs>(
                            eh => searcher.ImageRetrieved += eh,
                            eh => searcher.ImageRetrieved -= eh)
                                      .Subscribe(args =>
                                  {
                                      Application.Current.Dispatcher.Invoke(() =>
                                          OriginalImagePaths.Add(args.EventArgs.TemporaryFilePath));
                                  }));

                    retrieverTasks.Add(Task.Run(async () => await searcher.Run()));
                }

                await Task.WhenAll(searcherTasks);
                await Task.WhenAll(retrieverTasks);

                foreach (var subscription in subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }

        private bool CanSearchImages() => PostedKeywords.Any();

        public ObservableCollection<string> OriginalImagePaths => _originalImagePaths;

        private void OriginalImagePathsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ClearOriginalImagesCommand.NotifyCanExecuteChanged();
            StartProcessingCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanClearOriginalImages))]
        private void ClearOriginalImages()
        {
            foreach (var path in _originalImagePaths)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("An error occurred when deleting an original image file {0}: {1}", path, ex);
                }
            }
            _originalImagePaths.Clear();
        }

        private bool CanClearOriginalImages() => OriginalImagePaths.Any();

        #endregion Search

        #region Image processing

        [RelayCommand(CanExecute = nameof(CanStartProcessing))]
        private async Task StartProcessing()
        {
            ClearResultImages();

            var stopwatch = Stopwatch.StartNew();
            using (Observable.Interval(TimeSpan.FromMilliseconds(500.0))
                             .Subscribe(args =>
                             {
                                 Application.Current.Dispatcher.Invoke(() =>
                                     ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds);
                             }))
            {
                if (IsSingleThreadMode)
                {
                    await StartProcessingSingleThreadAsync();
                }
                if (IsMultiThreadMode)
                {
                    await StartProcessingMultiThreadAsync();
                }

                stopwatch.Stop();
                ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds;
            }
        }

        private bool CanStartProcessing() => OriginalImagePaths.Any();

        private async Task StartProcessingSingleThreadAsync()
        {
            await Task.Run(async () =>
            {
                foreach (var orgImgFilePath in OriginalImagePaths)
                {
                    try
                    {
                        var resultImgFilePath = await Models.ImageProcessor.ProcessAsync(orgImgFilePath);
                        await Application.Current.Dispatcher.InvokeAsync(() => _resultImagePaths.Add(resultImgFilePath));
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("An error occurred when processing {0}: {1}", orgImgFilePath, ex);
                    }
                }
            });
        }

        private async Task StartProcessingMultiThreadAsync()
        {
            // Channel of Producer-Consumer pattern
            var channel = new BlockingCollection<string>();
            foreach (var orgImgFilePath in OriginalImagePaths)
            {
                channel.Add(orgImgFilePath);
            }
            channel.CompleteAdding();

            var tasks = new List<Task>();
            var subscriptions = new List<IDisposable>();
            for (int i = 0; i < ImageProcessorThreadCount; i++)
            {
                var processor = new Models.ImageProcessor(channel);
                subscriptions.Add(
                    Observable.FromEventPattern<ImageProcessedEventArgs>(eh => processor.ImageProcessed += eh,
                                                                         eh => processor.ImageProcessed -= eh)
                              .Subscribe(args =>
                              {
                                  Application.Current.Dispatcher.Invoke(() => OnImageProcessed(args));
                              }));

                tasks.Add(Task.Run(async () => await processor.Run()));
            }
            await Task.WhenAll(tasks);
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }

        private void OnImageProcessed(EventPattern<ImageProcessedEventArgs> args)
        {
            _resultImagePaths.Add(args.EventArgs.ResultFileName);
        }

        public ObservableCollection<string> ResultImagePaths => _resultImagePaths;

        private void ResultImagePathsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ClearResultImagesCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanClearResultImages))]
        private void ClearResultImages()
        {
            ProcessingMilliseconds = 0;
            foreach (var resultImagePath in _resultImagePaths)
            {
                try
                {
                    File.Delete(resultImagePath);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("An error occurred when deleting a result image file {0}: {1}", resultImagePath, ex);
                }
            }
            _resultImagePaths.Clear();
        }

        private bool CanClearResultImages() => ResultImagePaths.Any();

        #endregion Image processing

        [RelayCommand]
        private void ClearTemporaryFiles()
        {
            ClearOriginalImages();
            ClearResultImages();
        }
    }
}
