using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using Microsoft.AspNet.SignalR.Client;

namespace ImageProcessor.Admin
{
    public class MainViewModel : ViewModelBase
    {
        private HubConnection connection;
        private IHubProxy echoHubProxy;

        public MainViewModel()
        {
            this.connection = new HubConnection("http://localhost:6694/EchoApp");
            this.echoHubProxy = connection.CreateHubProxy("echoHub");
            //this.echoHubProxy.On<string>("echo", this.ReceiveMessage);
            //this.connection.Start().ContinueWith(_ =>
            //{
            //    DispatcherHelper.CheckBeginInvokeOnUI(() => this.IsInitialized = true);
            //});
        } 
    }
}
