using System;
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
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ImageProcessor.Admin.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private bool _azureStorageObjectsInitialized;
        private CloudBlobContainer _originalImagesBlobContainer;
        private CloudQueue _simpleWorkerRequestQueue;
        private CloudQueue _multithreadWorkerRequestQueue;

        #region Property backing fields

        private IDisposable _connection;

        private Brush _connectionStateFill;

        private double _keywordCountFontSize;

        private readonly ObservableCollection<KeywordViewModel> _postedKeywords =
            new ObservableCollection<KeywordViewModel>();

        private readonly ObservableCollection<ProcessingRequestMessage> _processingRequestMessages =
            new ObservableCollection<ProcessingRequestMessage>();

        #endregion Property backing fields

        public MainViewModel()
        {
            RefreshConnectionStateFill();

            StartReceivingKeywordsCommand = new RelayCommand(StartReceivingKeywords, () => Connection == null);
            StopReceivingKeywordsCommand = new RelayCommand(StopReceivingKeywords, () => Connection != null);
            GatherOriginalImagesCommand = new RelayCommand(GatherOriginalImages, () => PostedKeywords.Any());
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
                _postedKeywords.Add(new KeywordViewModel("dirk nowitzki"));
                _postedKeywords.Add(new KeywordViewModel("超サイヤ人"));
                _postedKeywords.ElementAt(1).NotifyPosted();
                _postedKeywords.ElementAt(1).NotifyPosted();
            }
#endif
        }

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

        public RelayCommand GatherOriginalImagesCommand { get; private set; }

        private async void GatherOriginalImages()
        {
            ProcessingRequestMessages.Clear();

            // Channel of Producer-Consumer pattern
            var channel = new BlockingCollection<string>();
            foreach (var keyword in PostedKeywords.ToArray())
            {
                channel.Add(keyword.Value);
            }
            channel.CompleteAdding();

            await EnsureCloudStorage();
            var tasks = new List<Task>();
            // in order to restrict the number of concurrent accesses
            for (int i = 0; i < 5; i++)
            {
                var gatherer = new OriginalImageGatherer(channel, _originalImagesBlobContainer);
                Observable.FromEventPattern<OriginalImageGatheredEventArgs>(eh => gatherer.OriginalImageGathered += eh,
                                                                            eh => gatherer.OriginalImageGathered -= eh)
                          .ObserveOnDispatcher()
                          .Subscribe(OnOriginalImageGathered);

                tasks.Add(gatherer.Run());
            }
            await Task.WhenAll(tasks);
        }

        private void OnOriginalImageGathered(EventPattern<OriginalImageGatheredEventArgs> args)
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

        public RelayCommand StartProcessingCommand { get; private set; }

        private async void StartProcessing()
        {
            await EnsureCloudStorage();
            foreach (var requestMessage in ProcessingRequestMessages)
            {
                var reqMsgJson = JsonConvert.SerializeObject(requestMessage);
                await _simpleWorkerRequestQueue.AddMessageAsync(new CloudQueueMessage(reqMsgJson));
                await _multithreadWorkerRequestQueue.AddMessageAsync(new CloudQueueMessage(reqMsgJson));
            }
            ProcessingRequestMessages.Clear();
        }

        private async Task EnsureCloudStorage()
        {
            if (_azureStorageObjectsInitialized) return;

            var storageAccount = CloudStorageAccount.Parse(Settings.Default.StorageConnectionString);

            Trace.TraceInformation("Creating original images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
            await _originalImagesBlobContainer.CreateIfNotExistsAsync();

            Trace.TraceInformation("Creating simple worker request queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            _simpleWorkerRequestQueue = queueClient.GetQueueReference("simple-worker-requests");
            await _simpleWorkerRequestQueue.CreateIfNotExistsAsync();

            Trace.TraceInformation("Creating multi-thread worker request queue");
            _multithreadWorkerRequestQueue = queueClient.GetQueueReference("multithread-worker-requests");
            await _multithreadWorkerRequestQueue.CreateIfNotExistsAsync();

            _azureStorageObjectsInitialized = true;
        }
    }
}