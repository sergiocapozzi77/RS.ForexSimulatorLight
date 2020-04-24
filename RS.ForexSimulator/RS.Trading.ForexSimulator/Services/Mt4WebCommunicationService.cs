// <copyright file="Mt4WebCommunicationService.cs" company="Virtbits Ltd.">
// Copyright (c) Virtbits Ltd.</copyright>

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using RS.Trading.ForexSimulator.Models;

namespace RS.Trading.ForexSimulator.Services
{
    internal class Mt4WebCommunicationService : IMt4CommunicationService
    {
        private readonly BlockingCollection<CommandQueueElement> commandQueue = new BlockingCollection<CommandQueueElement>();
        private readonly ConcurrentDictionary<string, CommandQueueElement> requestSent = new ConcurrentDictionary<string, CommandQueueElement>();
        private CancellationTokenSource cancellationTokenSource;

        private DateTime lastPriceUpdateTime = DateTime.MinValue;
        private HttpListener listener;

        public event EventHandler<ChartInfo> ChartInfo;

        public event EventHandler<string> DataReceived;

        public event EventHandler<string> Error;

        public event EventHandler<Prices> PricesUpdated;

        public void Connect()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(
                this.RunServer,
                this.cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning);
        }

        public void GetChartInfo()
        {
            this.commandQueue.Add(new CommandQueueElement(CommandType.ChartInfo));
        }

        public void GetCurrentPrices()
        {
            this.commandQueue.Add(new CommandQueueElement(CommandType.CurrentPrices));
        }

        public void GetOpenTrades()
        {
            this.commandQueue.Add(new CommandQueueElement(CommandType.GetOpenTrades));
        }

        public void LockChart()
        {
            this.commandQueue.Add(new CommandQueueElement(CommandType.LockChart));
        }

        public void StepBackChart()
        {
            this.commandQueue.Add(new CommandQueueElement(CommandType.StepBackChart));
        }

        public void StepChart()
        {
            this.commandQueue.Add(new CommandQueueElement(CommandType.StepChart));
        }

        public void Stop()
        {
            SpinWait.SpinUntil(() => this.commandQueue.Count == 0, TimeSpan.FromSeconds(5));

            this.cancellationTokenSource.Cancel();
            this.listener.Stop();
        }

        public void UnlockChart()
        {
            this.commandQueue.Add(new CommandQueueElement(CommandType.UnlockChart));
        }

        private void ParsePoll(HttpListenerResponse response)
        {
            using (var writer = new StreamWriter(response.OutputStream))
            {
                string resp;
                if (this.commandQueue.TryTake(out var queueCommand))
                {
                    this.requestSent.TryAdd(queueCommand.ReqId, queueCommand);
                    resp = JsonConvert.SerializeObject(queueCommand);
                }
                else
                {
                    resp = JsonConvert.SerializeObject(new CommandQueueElement(CommandType.None));
                }

                writer.Write(resp);
            }
        }

        private void ParseRequest(HttpListenerResponse response, Mql4PostCommand cmd)
        {
            if (cmd == null)
            {
                return;
            }

            if (cmd.Type == Mql4CommandType.POLL.ToString())
            {
                this.ParsePoll(response);
            }
            else if (cmd.Type == Mql4CommandType.RESP.ToString())
            {
                this.ParseResponse(response, cmd);
            }
        }

        private void ParseResponse(HttpListenerResponse response, Mql4PostCommand cmd)
        {
            if (this.requestSent.TryRemove(cmd.ReqId, out var originalRequest))
            {
                if (originalRequest.Command == CommandType.StepChart ||
                    originalRequest.Command == CommandType.StepBackChart ||
                    originalRequest.Command == CommandType.CurrentPrices)
                {
                    if (originalRequest.ReqTime > this.lastPriceUpdateTime)
                    {
                        this.lastPriceUpdateTime = originalRequest.ReqTime;
                        var priceData = JsonConvert.DeserializeObject<Prices>(cmd.Data);
                        this.PricesUpdated?.Invoke(this, priceData);
                    }
                }
                else if (originalRequest.Command == CommandType.ChartInfo ||
                         originalRequest.Command == CommandType.LockChart)
                {
                    var chartInfo = JsonConvert.DeserializeObject<ChartInfo>(cmd.Data);
                    this.ChartInfo?.Invoke(this, chartInfo);
                }
            }

            SendOkResponse(response);
        }

        private void Process(object o)
        {
            try
            {
                var context = o as HttpListenerContext;

                if (context?.Request.InputStream == null)
                {
                    return;
                }

                var request = new StreamReader(context.Request.InputStream).ReadToEnd();
                var cmd = JsonConvert.DeserializeObject<Mql4PostCommand>(request);
                this.ParseRequest(context.Response, cmd);
            }
            catch (Exception e)
            {
                this.Error?.Invoke(this, e.Message);
            }
        }

        private void RunServer(object obj)
        {
            var cancellationToken = (CancellationToken)obj;

            var prefix = "http://localhost:80/";
            this.listener = new HttpListener();
            this.listener.Prefixes.Add(prefix);

            try
            {
                this.listener.Start();
            }
            catch (HttpListenerException hlex)
            {
                this.Error?.Invoke(this, $"Unable to start server. Check that your port 80 is not used. {hlex.Message}");
                return;
            }

            while (this.listener.IsListening &&
                   !cancellationToken.IsCancellationRequested)
            {
                ThreadPool.QueueUserWorkItem(this.Process, this.listener.GetContext());
            }

            this.listener.Close();
        }

        private static void SendOkResponse(HttpListenerResponse response)
        {
            using (var writer = new StreamWriter(response.OutputStream))
            {
                writer.Write("OK");
            }
        }
    }
}