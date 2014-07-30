using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using ImageProcessor.Admin.Properties;
using Microsoft.AspNet.SignalR.Client;

namespace ImageProcessor.Admin.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region Property backing fields

        private IDisposable _connection;

        private Brush _connectionStateFill;

        private double _keywordCountFontSize;

        private readonly ObservableCollection<KeywordViewModel> _postedKeywords =
            new ObservableCollection<KeywordViewModel>();

        #endregion Property backing fields

        public MainViewModel()
        {
            RefreshConnectionStateFill();

            StartReceivingKeywordsCommand = new RelayCommand(StartReceivingKeywords, () => Connection == null);
            StopReceivingKeywordsCommand = new RelayCommand(StopReceivingKeywords, () => Connection != null);
            StartProcessingCommand = new RelayCommand(StartProcessing);

            _postedKeywords.CollectionChanged += PostedKeywordsOnCollectionChanged;

            if (IsInDesignMode)
            {
                _postedKeywords.Add(new KeywordViewModel("デザイン用"));
                _postedKeywords.Add(new KeywordViewModel("ダミー (でかい)"));
                _postedKeywords.Add(new KeywordViewModel("キーワード"));
                _postedKeywords.ElementAt(1).NotifyPosted();
                _postedKeywords.ElementAt(1).NotifyPosted();
            }
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
        }

        public RelayCommand StartProcessingCommand { get; private set; }

        private void StartProcessing()
        {
        }
    }
}