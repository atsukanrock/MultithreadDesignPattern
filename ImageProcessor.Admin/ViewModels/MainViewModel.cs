﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using ImageProcessor.Admin.Models;
using ImageProcessor.Admin.Properties;
using ImageProcessor.Storage.Queue.Messages;
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

        private readonly ObservableCollection<ProcessingRequestMessage> _processingRequestMessages =
            new ObservableCollection<ProcessingRequestMessage>();

        private int _imagesPerKeyword = 3;

        private int _imageSearcherThreadCount = 4;

        private bool _isSingleThreadMode = true;

        private bool _isMultiThreadMode;

        private double _imageProcessorThreadCount = 4;

        private int _processingMilliseconds;

        private readonly ObservableCollection<string> _resultImagePaths = new ObservableCollection<string>();

        #endregion Property backing fields

        public MainViewModel()
        {
            RefreshConnectionStateFill();

            StartReceivingKeywordsCommand = new RelayCommand(StartReceivingKeywords, () => Connection == null);
            StopReceivingKeywordsCommand = new RelayCommand(StopReceivingKeywords, () => Connection != null);
            SearchImagesCommand = new RelayCommand(SearchImages, () => PostedKeywords.Any());
            StartProcessingCommand = new RelayCommand(StartProcessing, () => ProcessingRequestMessages.Any());

            _postedKeywords.CollectionChanged += PostedKeywordsOnCollectionChanged;
            _processingRequestMessages.CollectionChanged += ProcessingRequestMessagesOnCollectionChanged;

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
                _postedKeywords.ElementAt(1).NotifyPosted();
                _postedKeywords.ElementAt(1).NotifyPosted();
            }
#endif
        }

        #region Keywords

        private IDisposable Connection
        {
            get { return _connection; }
            set
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

            StartProcessingCommand.RaiseCanExecuteChanged();
        }

        #endregion Keywords

        #region Search

        public RelayCommand SearchImagesCommand { get; private set; }

        private async void SearchImages()
        {
            ProcessingRequestMessages.Clear();

            // Channel of Producer-Consumer pattern
            var channel = new BlockingCollection<string>();
            foreach (var keyword in PostedKeywords.ToArray())
            {
                channel.Add(keyword.Value);
            }
            channel.CompleteAdding();

            //await EnsureCloudStorage();
            var tasks = new List<Task>();
            // in order to restrict the number of concurrent accesses
            for (int i = 0; i < ImageSearcherThreadCount; i++)
            {
                var searcher = new ImageSearcher(channel, ImagesPerKeyword);
                Observable.FromEventPattern<ImageSearchedEventArgs>(eh => searcher.OriginalImageGathered += eh,
                                                                    eh => searcher.OriginalImageGathered -= eh)
                          .ObserveOnDispatcher()
                          .Subscribe(OnOriginalImageGathered);

                tasks.Add(Task.Run((Func<Task>)searcher.Run));
            }
            await Task.WhenAll(tasks);
        }

        private void OnOriginalImageGathered(EventPattern<ImageSearchedEventArgs> args)
        {
            var msg = args.EventArgs.ProcessingRequestMessage;
            Trace.TraceInformation("Original images have been gathered for: {0}", msg.Keyword);

            var keywordIndex =
                PostedKeywords.TakeWhile(
                    kwd => !string.Equals(kwd.Value, msg.Keyword, StringComparison.OrdinalIgnoreCase)).Count();
            PostedKeywords.RemoveAt(keywordIndex);

            ProcessingRequestMessages.Add(args.EventArgs.ProcessingRequestMessage);
        }

        public ObservableCollection<ProcessingRequestMessage> ProcessingRequestMessages
        {
            get { return _processingRequestMessages; }
        }

        private void ProcessingRequestMessagesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
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

        #endregion Search

        #region Image processing

        public RelayCommand StartProcessingCommand { get; private set; }

        private async void StartProcessing()
        {
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
            foreach (var fileName in ProcessingRequestMessages.SelectMany(reqMsg => reqMsg.FileNames))
            {
                try
                {
                    var resultFileName = await Models.ImageProcessor.ProcessAsync(fileName);
                    _resultImagePaths.Add(resultFileName);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("An error occurred when processing {0}: {1}", fileName, ex);
                }
            }
        }

        private async Task StartProcessingMultiThreadAsync()
        {
            // Channel of Producer-Consumer pattern
            var channel = new BlockingCollection<string>();
            foreach (var fileName in ProcessingRequestMessages.SelectMany(reqMsg => reqMsg.FileNames))
            {
                channel.Add(fileName);
            }
            channel.CompleteAdding();

            var threadCount = (int)ImageProcessorThreadCount;
            var tasks = new List<Task>();
            for (int i = 0; i < threadCount; i++)
            {
                var processor = new Models.ImageProcessor(channel);
                Observable.FromEventPattern<ImageProcessedEventArgs>(eh => processor.ImageProcessed += eh,
                                                                     eh => processor.ImageProcessed -= eh)
                          .ObserveOnDispatcher()
                          .Subscribe(OnImageProcessed);

                tasks.Add(Task.Run((Func<Task>)processor.Run));
            }
            await Task.WhenAll(tasks);
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

        public double ImageProcessorThreadCount
        {
            get { return _imageProcessorThreadCount; }
            set { Set(() => ImageProcessorThreadCount, ref _imageProcessorThreadCount, Math.Floor(value)); }
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

        #endregion Image processing
    }
}