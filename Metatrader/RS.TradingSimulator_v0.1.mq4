//+------------------------------------------------------------------+
//|                                                      ProjectName |
//|                                      Copyright 2018, CompanyName |
//|                                       http://www.companyname.net |
//+------------------------------------------------------------------+
#include <JAson.mqh>

//+------------------------------------------------------------------+
//|                                     RS.TradingSimulator_v0.1.mq4 |
//|                                           Copyright 2019, Sergio |
//|                                             https://www.mql5.com |
//+------------------------------------------------------------------+
#property copyright "Copyright 2019, Sergio"
#property link      "https://www.mql5.com"
#property version   "1.00"
#property strict

long chartHandle;

// Json library: https://www.mql5.com/en/code/13663

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit() {

    EventSetMillisecondTimer(250);
    chartHandle = ChartID();
    if(chartHandle > 0) {
        //--- disable auto scroll
        ChartSetInteger(chartHandle, CHART_AUTOSCROLL, false);
        //--- set a shift from the right chart border
        ChartSetInteger(chartHandle, CHART_SHIFT, true);
    }

//---
    return(INIT_SUCCEEDED);
}
//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason) {
//--- destroy timer
    EventKillTimer();
    ChartSetInteger(chartHandle, CHART_MOUSE_SCROLL, true);

}
//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick() {

}
//+------------------------------------------------------------------+
//| Timer function                                                   |
//+------------------------------------------------------------------+
void OnTimer() {
    string headers;
    char post[], result[];
    int res;
//--- Reset the last error code
    ResetLastError();

    CJAVal jv;
    jv["Type"] = "POLL";
    jv["ReqId"] = "";
    jv["Data"] = "";
    //Print(jv.Serialize());
    ArrayResize(post, StringToCharArray(jv.Serialize(), post, 0, WHOLE_ARRAY) - 1);

    int timeout = 500;
    res = WebRequest("POST", "http://localhost", "Content-Type: text/plain\r\n", timeout, post, result, headers);
    if(res == -1) {
        Print("Error in WebRequest. Error code  =", GetLastError());
    } else {
        string response = CharArrayToString(result);
        //Print("Server responded " + response);

        jv.Deserialize(result);
        string command = jv["Command"].ToStr();
        string reqId = jv["ReqId"].ToStr();
        string commandArgument = NULL;
        if(jv["CommandArgument"] != NULL) {
            commandArgument = jv["CommandArgument"].ToStr();
        }

        ParseCommand(command, reqId, commandArgument);
    }
}

//+------------------------------------------------------------------+
void PostResponse(string response, string reqId) {
    Print("Posting response");
    string headers;
    char post[], result[];
    int res;
    int timeout = 500;
//--- Reset the last error code
    ResetLastError();

    CJAVal jv;
    jv["Type"] = "RESP";
    jv["ReqId"] = reqId;
    jv["Data"] = response;
    ArrayResize(post, StringToCharArray(jv.Serialize(), post, 0, WHOLE_ARRAY) - 1);

    res = WebRequest("POST", "http://localhost", "Content-Type: text/plain\r\n", timeout, post, result, headers);
    if(res == -1) {
        Print("Error in WebRequest. Error code  =", GetLastError());
    } else {
        Print("Response posted " + response);
    }
}

//+------------------------------------------------------------------+
void ParseCommand(string command, string reqId, string commandArgument) {
    if(command == "None") {
        return;
    } else if(command == "GetOpenTrades") {
        string openOrders = GetOpenOrders();
        PostResponse(openOrders, reqId);
    } else if(command == "StepChart") {
        string barPrices = StepChart(1);
        PostResponse(barPrices, reqId);
    } else if(command == "StepBackChart") {
        string barPrices = StepChart(-1);
        PostResponse(barPrices, reqId);
    } else if(command == "LockChart") {
        LockChart();
        string info = ChartInfo();
        PostResponse(info, reqId);
    } else if(command == "UnlockChart") {
        UnlockChart();
    } else if(command == "ChartInfo") {
        string info = ChartInfo();
        PostResponse(info, reqId);
    } else if(command == "CurrentPrices") {
        string barPrices = GetCurrentPrices();
        PostResponse(barPrices, reqId);
    }
}

void LockChart()
{
    ChartSetInteger(chartHandle, CHART_MOUSE_SCROLL, false);
    Print("Chart "+ Symbol() +" locked");
}

void UnlockChart()
{
    ChartSetInteger(chartHandle, CHART_MOUSE_SCROLL, true);
    Print("Chart "+ Symbol() +" unlocked");
}

//+------------------------------------------------------------------+
string ChartInfo() {
    string ret;

    // Broker digits
    double point = Point;
    if((Digits == 3) || (Digits == 5)) {
        point *= 10;
    }

    double PipValue = ((MarketInfo(Symbol(), MODE_TICKVALUE) * point) / MarketInfo(Symbol(), MODE_TICKSIZE));

    CJAVal jv;
    jv["PipValue"] = PipValue;
    jv["Point"] = point;
    jv["Digits"] = Digits;
    ret = jv.Serialize();

    return ret;
}
//+------------------------------------------------------------------+
string StepChart(int shift) {
    ChartNavigate(chartHandle, CHART_CURRENT_POS, shift);
    return GetCurrentPrices();
}

//+------------------------------------------------------------------+
string GetCurrentPrices() {
    int bars_count = WindowBarsPerChart();
    int bar = WindowFirstVisibleBar();
    Print("Prices at bar " + (bar - bars_count));
    Print("Close " + Close[bar - bars_count]);

    CJAVal jv;
    jv["Low"] = Low[bar - bars_count];
    jv["High"] = High[bar - bars_count];
    jv["Open"] = Open[bar - bars_count];
    jv["Close"] = Close[bar - bars_count];

    return jv.Serialize();
}

//+------------------------------------------------------------------+
// GET OPEN ORDERS
string GetOpenOrders() {

    string zmq_ret;
    bool found = false;

    zmq_ret = zmq_ret + "'_action': 'OPEN_TRADES'";
    zmq_ret = zmq_ret + ", '_trades': {";

    for(int i = OrdersTotal() - 1; i >= 0; i--) {
        found = true;

        if (OrderSelect(i, SELECT_BY_POS) == true) {

            zmq_ret = zmq_ret + IntegerToString(OrderTicket()) + ": {";

            zmq_ret = zmq_ret + "'_magic': " + IntegerToString(OrderMagicNumber()) + ", '_symbol': '" + OrderSymbol() + "', '_lots': " + DoubleToString(OrderLots()) + ", '_type': " + IntegerToString(OrderType()) + ", '_open_price': " + DoubleToString(OrderOpenPrice()) + ", '_open_time': '" + TimeToStr(OrderOpenTime(), TIME_DATE | TIME_SECONDS) + "', '_SL': " + DoubleToString(OrderStopLoss()) + ", '_TP': " + DoubleToString(OrderTakeProfit()) + ", '_pnl': " + DoubleToString(OrderProfit()) + ", '_comment': '" + OrderComment() + "'";

            if (i != 0)
                zmq_ret = zmq_ret + "}, ";
            else
                zmq_ret = zmq_ret + "}";
        }
    }
    zmq_ret = zmq_ret + "}";

    return zmq_ret;

}
//+------------------------------------------------------------------+

//+------------------------------------------------------------------+
