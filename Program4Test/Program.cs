using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.Text.Json;
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

            string ApiKey = "2c29ac12944e8ac3d416daae184dc6a4268631da80f22983a97f2637fd91da08";
            string ApiSecret = "d90434dddbb074fed751178b720c749a073c0c8fd40b6c44c2d69c55b367a5a5";
            BinanceAPI.BinanceFutureAPI bf = new(ApiKey, ApiSecret);

            // Connect
            bool conn = await Task.Run(() => bf.Connect());
            Console.WriteLine(conn);

            

            while (true)
            {
                System.Threading.Thread.Sleep(1000);
                JObject res = await Task.Run(() => bf.GetAccountInfo());

                foreach (JObject obj in res["Data"]["Assets"])
                {
                    if ((string)obj["Asset"] == "USDT")
                    {
                        Console.WriteLine(obj["AvailableBalance"]);
                        break;
                    }
                }
            }
            

            


            // 
            //var tokenSource1 = new CancellationTokenSource();
            //var tokenSource2 = new CancellationTokenSource();
            //Task t1 = Task.Run(() => bf.SubscribeMarketPrice("BTCUSDT", ONNewPrice, tokenSource1.Token));
            //System.Threading.Thread.Sleep(10000);
            //Task t2 = Task.Run(() => bf.SubscribeMarketPrice("ETHUSDT", ONNewPrice, tokenSource2.Token));
            //System.Threading.Thread.Sleep(10000);
            //tokenSource2.Cancel();

            //Console.WriteLine("Canceled: {0} . Finished: {1} . Error: {2}",
            //           t2.IsCanceled, t2.IsCompleted, t2.IsFaulted);
            // Subscribe
            //await Task.Run(() => bf.SubscribeAccountUpdates(OnLeverageUpdate,
            //    OnMarginUpdate,
            //    OnAccountUpdate,
            //    OnOrderUpdate,
            //    OnListenKeyExpired));

            // TP Request
            //var result = await Task.Run(() => bf.PlaceOrder(Symbol: "BTCUSDT",
            //                Side: Binance.Net.Enums.OrderSide.Sell,
            //                Type: Binance.Net.Enums.FuturesOrderType.TakeProfitMarket,
            //                Quantity: 0,
            //                PositionSide: Binance.Net.Enums.PositionSide.Both,
            //                TimeInForce: Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled,
            //                NewClientOrderId: "aDSHGDAJDLNJSBFSMFNSHB",
            //                StopPrice: (decimal)21560.0,
            //                ActivationPrice: 0,
            //                CallbackRate: 0,
            //                WorkingType: 0,
            //                ClosePosition: true));
            //Console.WriteLine(result);

            //// SL Request
            //var result1 = await Task.Run(() => bf.PlaceOrder(Symbol: "BTCUSDT",
            //                Side: Binance.Net.Enums.OrderSide.Sell,
            //                Type: Binance.Net.Enums.FuturesOrderType.StopMarket,
            //                Quantity: 0,
            //                PositionSide: Binance.Net.Enums.PositionSide.Both,
            //                TimeInForce: Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled,
            //                NewClientOrderId: "aDSHGDAJDLNJSBFSMFNSHC",
            //                StopPrice: (decimal)20000.0,
            //                ActivationPrice: 0,
            //                CallbackRate: 0,
            //                WorkingType: 0,
            //                ClosePosition: true));
            //Console.WriteLine(result1);

            // Change Leverage
            //JObject cl = await Task.Run(() => bf.ChangeLeverage("BTCUSDT", 15));
            //Console.WriteLine(cl.ToString());

            // 
            //JObject cpm = await bf.ChangePositionMargin("BTCUSDT");
            //Console.WriteLine(cpm.ToString());

            ExitEvent.WaitOne();
        }

        private static void ONNewPrice(DataEvent<BinanceFuturesUsdtStreamMarkPrice> obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj.Data));
        }

        private static void OnStockBoard(DataEvent<IEnumerable<BinanceFuturesStreamMarkPrice>> obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj.Data));
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
            Console.WriteLine(JsonSerializer.Serialize(obj.Data));
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
