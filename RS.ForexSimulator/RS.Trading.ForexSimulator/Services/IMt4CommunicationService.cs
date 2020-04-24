// <copyright file="IMt4CommunicationService.cs" company="RS Ltd.">
// Copyright (c) RS Ltd.</copyright>

using System;

using RS.Trading.ForexSimulator.Models;

namespace RS.Trading.ForexSimulator.Services
{
    public interface IMt4CommunicationService
    {
        void Connect();
        void GetOpenTrades();
        event EventHandler<string> DataReceived;

        void StepChart();
        void LockChart();

        event EventHandler<string> Error;

        event EventHandler<Prices> PricesUpdated;

        void GetChartInfo();

        event EventHandler<ChartInfo> ChartInfo;

        void GetCurrentPrices();
        void StepBackChart();
        void UnlockChart();
        void Stop();
    }
}