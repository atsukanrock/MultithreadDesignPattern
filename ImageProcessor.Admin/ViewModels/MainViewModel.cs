using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
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

        private readonly ObservableCollection<KeywordViewModel> _postedKeywords =
            new ObservableCollection<KeywordViewModel>();

        private HubConnection _connection;

        private double _keywordCountFontSize;

        #endregion Property backing fields

        public MainViewModel()
        {
            StartReceivingKeywordsCommand = new RelayCommand(StartReceivingKeywords);
            StopReceivingKeywordsCommand = new RelayCommand(StopReceivingKeywords);
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

        private void PostedKeywordsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // ReSharper disable PossibleLossOfFraction
            KeywordCountFontSize = SystemFonts.MessageFontSize + PostedKeywords.Count / 5;
            // ReSharper restore PossibleLossOfFraction
        }

        public ObservableCollection<KeywordViewModel> PostedKeywords
        {
            get { return _postedKeywords; }
        }

        public HubConnection Connection
        {
            get { return _connection; }
            private set { Set(() => this.Connection, ref _connection, value); }
        }

        public ICommand StartReceivingKeywordsCommand { get; private set; }

        public ICommand StopReceivingKeywordsCommand { get; private set; }

        public double KeywordCountFontSize
        {
            get { return _keywordCountFontSize; }
            private set { Set(() => KeywordCountFontSize, ref _keywordCountFontSize, value); }
        }

        public ICommand StartProcessingCommand { get; private set; }

        private async void StartReceivingKeywords()
        {
            var connection = new HubConnection(Settings.Default.WebSiteUrl);
            connection.Closed += ConnectionOnClosed;

            var hubProxy = connection.CreateHubProxy("KeywordHub");
            hubProxy.On<string>("addPostedKeyword", OnKeywordPosted);

            try
            {
                await connection.Start();
            }
            catch (HttpRequestException)
            {
                // Do something when it's impossible to connect to the SignalR server.
                MessageBox.Show("サーバーに繋がりませんわー", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.Connection = connection;
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

        private void ConnectionOnClosed()
        {
            Connection = null;
        }

        private void StopReceivingKeywords()
        {
            if (Connection == null) return;

            Connection.Stop();
            Connection = null;
        }

        private void StartProcessing()
        {
        }
    }
}