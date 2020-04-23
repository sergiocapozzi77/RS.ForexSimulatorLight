// <copyright file="CommandType.cs" company="Racing Solutions Ltd">
// Copyright (c) Racing Solutions Ltd</copyright>
namespace RS.Trading.ForexSimulator.Models
{
    public enum CommandType
    {
        None,
        GetOpenTrades,
        StepChart,
        LockChart,
        ChartInfo,
        StepBackChart,
        CurrentPrices,
        UnlockChart
    }
}