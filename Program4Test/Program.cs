using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Sockets;
using System;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Program4Test
{
    class Program
    {
        private static readonly ManualResetEvent ExitEvent = new(false);

        static async Task Main(string[] _)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
            AssemblyLoadContext.Default.Unloading += DefaultOnUnloading;
            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            // Note: Change "Defaul" to "Testnet" if You want Use This Key and Secret. (BinanceFututreAPI.cs Lines 49 and 58)
            string ApiKey = "2c29ac12944e8ac3d416daae184dc6a4268631da80f22983a97f2637fd91da08";
            string ApiSecret = "d90434dddbb074fed751178b720c749a073c0c8fd40b6c44c2d69c55b367a5a5";
            BinanceAPI.BinanceFutureAPI bf = new(ApiKey, ApiSecret);

            // Connect
            bool conn = await Task.Run(() => bf.Connect());
            Console.WriteLine("Connection: " + conn);

            // Balance
            JsonObject balance = await Task.Run(() => bf.GetBalances());
            Console.WriteLine("Balance: " + JsonSerializer.Serialize(balance));

            // Subscribe
            bool sub = await Task.Run(() => bf.SubscribeAccountUpdates(OnLeverageUpdate,
                OnMarginUpdate,
                OnAccountUpdate, OnOrderUpdate, OnListenKeyExpired));
            Console.WriteLine("Subscribe: " + sub);

            //await Task.Run(() => bf.PlaceOrder(
            //            Symbol: "NEARUSDT",
            //            Side: Binance.Net.Enums.OrderSide.Sell,
            //            Type: Binance.Net.Enums.FuturesOrderType.Market,
            //            Quantity: (decimal)560,
            //            PositionSide: Binance.Net.Enums.PositionSide.Both,
            //            ReduceOnly: true,
            //            NewClientOrderId: "ios_djhfdfgsfaghjdk",
            //            ActivationPrice: 0,
            //            CallbackRate: 0,
            //            WorkingType: Binance.Net.Enums.WorkingType.Mark
            //            ));

            ExitEvent.WaitOne();
        }

        private static void OnLeverageUpdate(DataEvent<BinanceFuturesStreamConfigUpdate> obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj.Data));
        }

        private static void OnMarginUpdate(DataEvent<BinanceFuturesStreamMarginUpdate> obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj.Data));
        }

        private static void OnAccountUpdate(DataEvent<BinanceFuturesStreamAccountUpdate> obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj));
        }

        private static void OnOrderUpdate(DataEvent<BinanceFuturesStreamOrderUpdate> obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj.Data));
        }

        private static void OnListenKeyExpired(DataEvent<Binance.Net.Objects.Models.BinanceStreamEvent> obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj.Data));
        }

        private static void CurrentDomainOnProcessExit(object sender, EventArgs eventArgs)
        {
            ExitEvent.Set();
        }

        private static void DefaultOnUnloading(AssemblyLoadContext assemblyLoadContext)
        {
            ExitEvent.Set();
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            ExitEvent.Set();
        }
    }
}
