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
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using ImageProcessor.Admin.Models;
using ImageProcessor.Admin.Properties;
using Microsoft.AspNet.SignalR.Client;

namespace ImageProcessor.Admin.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        //private bool _azureStorageObjectsInitialized;
        //private CloudBlobContainer _originalImagesBlobContainer;
        //private CloudQueue _simpleWorkerRequestQueue;
        //private CloudQueue _multithreadWorkerRequestQueue;

        #region Property backing fields

        private IDisposable _connection;

        private Brush _connectionStateFill;

        private double _keywordCountFontSize;

        private readonly ObservableCollection<KeywordViewModel> _postedKeywords =
            new ObservableCollection<KeywordViewModel>();

        private readonly ObservableCollection<string> _originalImagePaths = new ObservableCollection<string>();

        private int _imagesPerKeyword = 3;

        private int _imageSearcherThreadCount = 4;

        private int _imageRetrieverThreadCount = 4;

        private bool _isSingleThreadMode = true;

        private bool _isMultiThreadMode;

        private int _imageProcessorThreadCount = 4;

        private int _processingMilliseconds;

        private readonly ObservableCollection<string> _resultImagePaths = new ObservableCollection<string>();

        #endregion Property backing fields

        public MainViewModel()
        {
            RefreshConnectionStateFill();

            StartReceivingKeywordsCommand = new RelayCommand(StartReceivingKeywords, () => Connection == null);
            StopReceivingKeywordsCommand = new RelayCommand(StopReceivingKeywords, () => Connection != null);
            ClearKeywordsCommand = new RelayCommand(ClearKeywords, () => PostedKeywords.Any());
            SearchImagesCommand = new RelayCommand(SearchImages, () => PostedKeywords.Any());
            ClearOriginalImagesCommand = new RelayCommand(ClearOriginalImages, () => OriginalImagePaths.Any());
            StartProcessingCommand = new RelayCommand(StartProcessing, () => OriginalImagePaths.Any());
            ClearResultImagesCommand = new RelayCommand(ClearResultImages, () => ResultImagePaths.Any());
            ClearTemporaryFilesCommand = new RelayCommand(ClearTemporaryFiles);

            _postedKeywords.CollectionChanged += PostedKeywordsOnCollectionChanged;
            _originalImagePaths.CollectionChanged += OriginalImagePathsOnCollectionChanged;
            _resultImagePaths.CollectionChanged += ResultImagePathsOnCollectionChanged;

            if (IsInDesignMode)
            {
                _postedKeywords.Add(new KeywordViewModel("デザイン用"));
                _postedKeywords.Add(new KeywordViewModel("ダミー (でかい)"));
                _postedKeywords.Add(new KeywordViewModel("キーワード"));
                _postedKeywords.ElementAt(1).NotifyPosted();
                _postedKeywords.ElementAt(1).NotifyPosted();
            }
#if DEBUG
            else
            {
                _postedKeywords.Add(new KeywordViewModel("ジョジョの奇妙な冒険"));
                _postedKeywords.Add(new KeywordViewModel("魔人ブウ"));
                _postedKeywords.Last().NotifyPosted();
                _postedKeywords.Last().NotifyPosted();
                _postedKeywords.Last().NotifyPosted();
                _postedKeywords.Last().NotifyPosted();
                _postedKeywords.Last().NotifyPosted();
            }
#endif
        }

        #region Keywords

        public IDisposable Connection
        {
            get { return _connection; }
            private set
            {
                var oldValue = _connection;
                if (oldValue != null && !ReferenceEquals(oldValue, value))
                {
                    oldValue.Dispose();
                }

                Set(() => Connection, ref _connection, value);

                RefreshConnectionStateFill();
                StartReceivingKeywordsCommand.RaiseCanExecuteChanged();
                StopReceivingKeywordsCommand.RaiseCanExecuteChanged();
            }
        }

        public Brush ConnectionStateFill
        {
            get { return _connectionStateFill; }
            private set { Set(() => ConnectionStateFill, ref _connectionStateFill, value); }
        }

        private void RefreshConnectionStateFill()
        {
            ConnectionStateFill = Connection != null
                ? new SolidColorBrush(Colors.LawnGreen)
                : new SolidColorBrush(Colors.Red);
        }

        public RelayCommand StartReceivingKeywordsCommand { get; private set; }

        private async void StartReceivingKeywords()
        {
            Debug.Assert(Connection == null, "Connection == null");

            var conn = new HubConnection(Settings.Default.WebSiteUrl);
            // Handling StateChanged event might be better than handling Closed event.
            var closedSub =
                Observable.FromEvent(eh => conn.Closed += eh, eh => conn.Closed -= eh)
                          .Subscribe(_ => DispatcherHelper.CheckBeginInvokeOnUI(() => Connection = null));

            var hubProxy = conn.CreateHubProxy("KeywordHub");
            hubProxy.On<string>("addPostedKeyword", OnKeywordPosted);

            try
            {
                await conn.Start();

                Connection = new CompositeDisposable(closedSub, conn);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show("サーバーに繋がりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnKeywordPosted(string keyword)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
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

        public RelayCommand StopReceivingKeywordsCommand { get; private set; }

        private void StopReceivingKeywords()
        {
            Debug.Assert(Connection != null, "Connection != null");
            Connection = null;
        }

        public double KeywordCountFontSize
        {
            get { return _keywordCountFontSize; }
            private set { Set(() => KeywordCountFontSize, ref _keywordCountFontSize, value); }
        }

        public ObservableCollection<KeywordViewModel> PostedKeywords
        {
            get { return _postedKeywords; }
        }

        private void PostedKeywordsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // ReSharper disable PossibleLossOfFraction
            KeywordCountFontSize = SystemFonts.MessageFontSize + PostedKeywords.Count / 5;
            // ReSharper restore PossibleLossOfFraction

            ClearKeywordsCommand.RaiseCanExecuteChanged();
            StartProcessingCommand.RaiseCanExecuteChanged();
        }

        public RelayCommand ClearKeywordsCommand { get; private set; }

        private void ClearKeywords()
        {
            PostedKeywords.Clear();
        }

        #endregion Keywords

        #region Search

        public RelayCommand SearchImagesCommand { get; private set; }

        private async void SearchImages()
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

                //await EnsureCloudStorage();
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
                                  .ObserveOnDispatcher()
                                  .Subscribe(args => OriginalImagePaths.Add(args.EventArgs.TemporaryFilePath)));

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

        public ObservableCollection<string> OriginalImagePaths
        {
            get { return _originalImagePaths; }
        }

        private void OriginalImagePathsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClearOriginalImagesCommand.RaiseCanExecuteChanged();
            StartProcessingCommand.RaiseCanExecuteChanged();
        }

        public int ImagesPerKeyword
        {
            get { return _imagesPerKeyword; }
            set { Set(() => ImagesPerKeyword, ref _imagesPerKeyword, value); }
        }

        public int ImageSearcherThreadCount
        {
            get { return _imageSearcherThreadCount; }
            set { _imageSearcherThreadCount = value; }
        }

        public int ImageRetrieverThreadCount
        {
            get { return _imageRetrieverThreadCount; }
            set { _imageRetrieverThreadCount = value; }
        }

        public RelayCommand ClearOriginalImagesCommand { get; private set; }

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

        #endregion Search

        #region Image processing

        public RelayCommand StartProcessingCommand { get; private set; }

        private async void StartProcessing()
        {
            ClearResultImages();

            var stopwatch = Stopwatch.StartNew();
            using (Observable.Interval(TimeSpan.FromMilliseconds(500.0))
                             .ObserveOnDispatcher()
                             .Subscribe(args => { ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds; }))
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

        private async Task StartProcessingSingleThreadAsync()
        {
            await Task.Run(async () =>
            {
                foreach (var orgImgFilePath in OriginalImagePaths)
                {
                    try
                    {
                        var resultImgFilePath = await Models.ImageProcessor.ProcessAsync(orgImgFilePath);
                        DispatcherHelper.CheckBeginInvokeOnUI(() => _resultImagePaths.Add(resultImgFilePath));
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
                              .ObserveOnDispatcher()
                              .Subscribe(OnImageProcessed));

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

        // Azure version that didn't work...

        //private async void StartProcessing()
        //{
        //    await EnsureCloudStorage();
        //    foreach (var requestMessage in ProcessingRequestMessages)
        //    {
        //        var reqMsgJson = JsonConvert.SerializeObject(requestMessage);
        //        await _simpleWorkerRequestQueue.AddMessageAsync(new CloudQueueMessage(reqMsgJson));
        //        await _multithreadWorkerRequestQueue.AddMessageAsync(new CloudQueueMessage(reqMsgJson));
        //    }
        //    ProcessingRequestMessages.Clear();
        //}

        public bool IsSingleThreadMode
        {
            get { return _isSingleThreadMode; }
            set { Set(() => IsSingleThreadMode, ref _isSingleThreadMode, value); }
        }

        public bool IsMultiThreadMode
        {
            get { return _isMultiThreadMode; }
            set { Set(() => IsMultiThreadMode, ref _isMultiThreadMode, value); }
        }

        public int ImageProcessorThreadCount
        {
            get { return _imageProcessorThreadCount; }
            set { Set(() => ImageProcessorThreadCount, ref _imageProcessorThreadCount, value); }
        }

        public int ProcessingMilliseconds
        {
            get { return _processingMilliseconds; }
            set { Set(() => ProcessingMilliseconds, ref _processingMilliseconds, value); }
        }

        public ObservableCollection<string> ResultImagePaths
        {
            get { return _resultImagePaths; }
        }

        private void ResultImagePathsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClearResultImagesCommand.RaiseCanExecuteChanged();
        }

        public RelayCommand ClearResultImagesCommand { get; private set; }

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

        #endregion Image processing

        public RelayCommand ClearTemporaryFilesCommand { get; private set; }

        private void ClearTemporaryFiles()
        {
            ClearOriginalImages();
            ClearResultImages();
        }

        //private async Task EnsureCloudStorage()
        //{
        //    if (_azureStorageObjectsInitialized) return;

        //    var storageAccount = CloudStorageAccount.Parse(Settings.Default.StorageConnectionString);

        //    Trace.TraceInformation("Creating original images blob container");
        //    var blobClient = storageAccount.CreateCloudBlobClient();
        //    _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
        //    await _originalImagesBlobContainer.CreateIfNotExistsAsync();

        //    Trace.TraceInformation("Creating simple worker request queue");
        //    var queueClient = storageAccount.CreateCloudQueueClient();
        //    _simpleWorkerRequestQueue = queueClient.GetQueueReference("simple-worker-requests");
        //    await _simpleWorkerRequestQueue.CreateIfNotExistsAsync();

        //    Trace.TraceInformation("Creating multi-thread worker request queue");
        //    _multithreadWorkerRequestQueue = queueClient.GetQueueReference("multithread-worker-requests");
        //    await _multithreadWorkerRequestQueue.CreateIfNotExistsAsync();

        //    _azureStorageObjectsInitialized = true;
        //}
    }
}