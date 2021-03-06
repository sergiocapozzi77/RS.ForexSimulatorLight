using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Threading;
using NLog;
using RS.Trading.ForexSimulator.Models;
using RS.Trading.ForexSimulator.Services;

namespace RS.Trading.ForexSimulator.ViewModel
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public ObservableCollection<OrderViewModel> OpenOrders { get; set; }
        public ObservableCollection<OrderViewModel> ClosedOrders { get; set; }

        private IMt4CommunicationService communicationService;
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(IMt4CommunicationService communicationService)
        {
            this.communicationService = communicationService;

            ConnectCommand = new RelayCommand(Connect, () => !IsConnected);
            OpenTradesCommand = new RelayCommand(GetOpenTrades, () => IsConnected && HasChartInfo && CurrentPrice != null && IsChartLocked);
            StepChartCommand = new RelayCommand(StepChart, () => IsConnected && HasChartInfo && CurrentPrice != null && IsChartLocked);
            StepBackChartCommand = new RelayCommand(StepBackChart, () => IsConnected && HasChartInfo && CurrentPrice != null && IsChartLocked);
            BuyCommand = new RelayCommand(Buy, () => IsConnected && HasChartInfo && CurrentPrice != null && IsChartLocked);
            SellCommand = new RelayCommand(Sell, () => IsConnected && HasChartInfo && CurrentPrice != null && IsChartLocked);
            CloseTradeCommand = new RelayCommand<OrderViewModel>(CloseTrade, x => IsConnected && HasChartInfo && CurrentPrice != null && IsChartLocked);
            ToggleLockCommand = new RelayCommand<OrderViewModel>(ToggleLock, true);

            WindowLoaded = new RelayCommand(Loaded);
            OpenOrders = new ObservableCollection<OrderViewModel>();
            ClosedOrders = new ObservableCollection<OrderViewModel>();

            if (!IsInDesignMode)
            {
          
            }

            this.communicationService.DataReceived += CommunicationService_DataReceived;
            this.communicationService.PricesUpdated += CommunicationService_PricesUpdated;
            this.communicationService.ChartInfo += CommunicationService_ChartInfo;
            this.communicationService.Error += CommunicationService_Error;

            IsChartLocked = false;
            Logs = new ObservableCollection<string>();
            this.LotSize = 0.01;
            this.Spread = 10;
            OpenOrders.CollectionChanged += OpenOrders_CollectionChanged;
        }

        private void CommunicationService_Error(object sender, string e)
        {
           this.AddErrorLog(e);
        }

        private void ToggleLock(OrderViewModel obj)
        {
            if (IsChartLocked)
            {
                //unlock
                this.UnlockChart();
            }
            else
            {
                //lock
                this.LockChart();
                this.communicationService.GetCurrentPrices();
            }
        }

        private void UnlockChart()
        {
            this.communicationService.UnlockChart();
            IsChartLocked = false;
        }

        private void CloseTrade(OrderViewModel obj)
        {
            OpenOrders.Remove(obj);
            ClosedOrders.Add(obj);
            DispatcherHelper.CheckBeginInvokeOnUI(() => CalculatePNL());
        }

        private void OpenOrders_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() => CalculatePNL());
        }

        private void CommunicationService_ChartInfo(object sender, ChartInfo e)
        {
            this.ChartInfo = e;
            IsChartLocked = true;
            DispatcherHelper.CheckBeginInvokeOnUI(() => CommandManager.InvalidateRequerySuggested());
        }

        private void Sell()
        {
            var order = new Order(CurrentPrice.Close - GetSpreadInPip() * this.chartInfo.Point, CurrentOrderId++, LotSize, null, null, OrderType.Sell);
            this.OpenOrders.Add(new OrderViewModel(order));
            AddInfoLog($"Sell order placed at price {CurrentPrice.Close} with lot {LotSize} and spread {Spread}");
        }

        public int CurrentOrderId { get; set; }

        private void Buy()
        {
            var order = new Order(CurrentPrice.Close + GetSpreadInPip() * this.chartInfo.Point, CurrentOrderId++, LotSize, null, null, OrderType.Buy);
            this.OpenOrders.Add(new OrderViewModel(order));
            AddInfoLog($"Buy order placed at price {CurrentPrice.Close} with lot {LotSize} and spread {Spread}");
        }

        public int Spread
        {
            get { return spread; }
            set
            {
                spread = value;
                this.RaisePropertyChanged();
            }
        }

        double GetSpreadInPip()
        {
            if (this.chartInfo.Digits == 3 ||
                this.chartInfo.Digits == 5)
            {
                return this.Spread * 0.1;
            }
            else
            {
                return this.Spread * 0.01;
            }
        }

        public double CurrentPL
        {
            get { return currentPL; }
            set
            {
                currentPL = value;
                this.RaisePropertyChanged();
            }
        }

        public double Profit
        {
            get { return profit; }
            set
            {
                profit = value;
                this.RaisePropertyChanged();
            }
        }

        public double TotalPL
        {
            get { return totalPL; }
            set
            {
                totalPL = value;
                this.RaisePropertyChanged();
            }
        }

        void CalculatePNL()
        {
            foreach (var order in OpenOrders)
            {
                double priceDiff;
                if (order.Model.Type == OrderType.Sell)
                {
                    priceDiff = order.Model.Open - CurrentPrice.Close;
                }
                else
                {
                    priceDiff = CurrentPrice.Close - order.Model.Open;
                }

                order.PL = ((priceDiff) / this.chartInfo.Point) * ChartInfo.PipValue * order.Model.Size;

            }

            CurrentPL = OpenOrders.Sum( x => x.PL);
            TotalPL = CurrentPL + ClosedOrders.Sum( x => x.PL);
            Profit = ClosedOrders.Sum( x => x.PL);
        }

        public Prices CurrentPrice
        {
            get { return currentPrice; }
            set
            {
                currentPrice = value;
                this.RaisePropertyChanged();
            }
        }

        private void CommunicationService_PricesUpdated(object sender, Prices price)
        {
            CurrentPrice = price;
            this.AddInfoLog("Prices updated: " + price.Close);
            DispatcherHelper.CheckBeginInvokeOnUI(() => CalculatePNL());
            DispatcherHelper.CheckBeginInvokeOnUI(() => CommandManager.InvalidateRequerySuggested()); 
        }

        public double LotSize
        {
            get { return lotSize; }
            set
            {
                lotSize = value;
                this.RaisePropertyChanged();
            }
        }

        public bool IsChartLocked
        {
            get { return isChartLocked; }
            set
            {
                isChartLocked = value; 
                this.RaisePropertyChanged();
                LockButtonCaption = IsChartLocked ? "Unlock Chart" : "Lock Chart";
            }
        }

        public string LockButtonCaption
        {
            get { return lockButtonCaption; }
            set
            {
                lockButtonCaption = value;
                this.RaisePropertyChanged();
            }
        }

        private void Loaded()
        {
            this.Connect();
        }

        private void LockChart()
        {
            this.communicationService.LockChart(); 
        }

        private void GetCurrentPrices()
        {
            this.communicationService.GetCurrentPrices();
        }

        private void StepChart()
        {
            if (!this.IsChartLocked)
            {
                this.communicationService.LockChart();
            }

            communicationService.StepChart();
        }

        private void StepBackChart()
        {
            if (!this.IsChartLocked)
            {
                this.communicationService.LockChart();
            }

            communicationService.StepBackChart();
        }

        private void CommunicationService_DataReceived(object sender, string data)
        {
            this.AddInfoLog(data);
        }

        private void AddInfoLog(string data)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() => this.Logs.Add(data));
            this.logger.Info(data);
        }

        private void AddErrorLog(string data)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() => this.Logs.Add("ERROR: " + data));
            this.logger.Error(data);
        }

        private void GetOpenTrades()
        {
            communicationService.GetOpenTrades();
        }

        public bool IsConnected { get; set; }

        private void Connect()
        {
            communicationService.Connect();
            IsConnected = true;
        }

        public ICommand BuyCommand { get; set; }
        public ICommand SellCommand { get; set; }
        public ICommand ConnectCommand { get; set; }
        public ICommand ToggleLockCommand { get; set; }

        public ICommand OpenTradesCommand { get; set; }

        public ICommand StepChartCommand { get; set; }
        public ICommand StepBackChartCommand { get; set; }

        public ICommand WindowLoaded { get; set; }
        public ICommand CloseTradeCommand { get; set; }

        private ObservableCollection<string> logs;
        private bool isChartLocked;
        private double lotSize;
        private Prices currentPrice;
        private ChartInfo chartInfo;
        private double totalPL;
        private double currentPL;
        private string lockButtonCaption;

        public ObservableCollection<string> Logs
        {
            get { return logs; }
            set
            {
                logs = value; 
                this.RaisePropertyChanged();
            }
        }

        public ChartInfo ChartInfo
        {
            get { return chartInfo; }
            set
            {
                chartInfo = value;
                if (this.chartInfo != null)
                {
                    HasChartInfo = true;
                }

                this.RaisePropertyChanged();
            }
        }

        public bool HasChartInfo { get; private set; }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
        private double profit;
        private int spread;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                this.communicationService.UnlockChart();
                this.communicationService.Stop();

                disposedValue = true;
            }
        }
        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}