using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Futures.Socket;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace BinanceAPI
{
    public class BinanceFutureAPI
    {
        // Private Fields:
#nullable enable
        private BinanceClient? Client { get; set; }
        private BinanceSocketClient? SocketClient { get; set; }
#nullable disable
        private string APIKey { get; set; }
        private string APISecret { get; set; }
        private string ListenKey { get; set; }

        // Constructor:
        public BinanceFutureAPI(string APIKey, string APISecret)
        {
            this.APIKey = APIKey;
            this.APISecret = APISecret;
        }

        // Destructor
        ~BinanceFutureAPI()
        {
            Client?.Dispose();
            SocketClient?.Dispose();
        }

        public async Task<bool> Connect()
        {
            Client = new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(APIKey, APISecret),
                UsdFuturesApiOptions = new BinanceApiClientOptions
                {
                    BaseAddress = BinanceApiAddresses.TestNet.UsdFuturesRestClientAddress,
                }
            });

            SocketClient = new BinanceSocketClient(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(APIKey, APISecret),
                UsdFuturesStreamsOptions = new BinanceApiClientOptions
                {
                    BaseAddress = BinanceApiAddresses.TestNet.UsdFuturesSocketClientAddress,
                }
            });

            // Check Connection
            long checkConnect = await Task.Run(() => GetServerTime());
            if (checkConnect == 0) return false;
            // Check Listen Key
            var lk = await GetListenKey();
            if (lk == null) return false;
            ListenKey = lk.ToString();
            // Check Ping Pong
            if (await StartKeepAlive() == false) return false;
            System.Timers.Timer KeepAliveTimer = new();
            KeepAliveTimer.Elapsed += new ElapsedEventHandler(KeepAlive);
            KeepAliveTimer.Interval = 1000 * 600;
            KeepAliveTimer.Enabled = true;
            return true;
        }

        private async Task<string> GetListenKey()
        {
            var listenKey = await Client.UsdFuturesApi.Account.StartUserStreamAsync();
            if (!listenKey.Success) return null;
            else return listenKey.Data;
        }

        private async Task<bool> StartKeepAlive()
        {
            var result = await Client.UsdFuturesApi.Account.KeepAliveUserStreamAsync(listenKey: ListenKey);
            if (!result.Success) return false;
            return true;
        }

        private void KeepAlive(object Source, ElapsedEventArgs Event)
        {
            bool Success = false;
            int RetryCount = 0;
            while (!Success && RetryCount < 5)
            {
                Task<bool> KATask = Task.Run(() => StartKeepAlive());
                KATask.Wait();
                Success = KATask.Result;
                RetryCount++;
            }
        }

        private async Task<bool> StopKeepAlive()
        {
            var result = await Client.UsdFuturesApi.Account.StopUserStreamAsync(listenKey: ListenKey);
            if (!result.Success) return false;
            else return true;
        }

        public async Task<long> GetServerTime()
        {
            CancellationTokenSource CTS = new(5000);
            DateTime time = (await Client.UsdFuturesApi.ExchangeData.GetServerTimeAsync(ct: CTS.Token)).Data;
            if (time == new DateTime(1, 1, 1, 0, 0, 0)) return 0;
            return (long)((time.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds);
        }

        public async Task<JsonObject> GetAccountInfo()
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.UsdFuturesApi.Account.GetAccountInfoAsync(ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> GetBalances()
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.UsdFuturesApi.Account.GetBalancesAsync(ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> GetOpenOrders(string Symbol = null)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.UsdFuturesApi.Trading.GetOpenOrdersAsync(Symbol, ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> GetOpenPositions(string Symbol = null)
        {
            CancellationTokenSource CTS = new(5000);
            List<JsonNode> Positions = new();
            var result = await Client.UsdFuturesApi.Account.GetPositionInformationAsync(Symbol, ct:CTS.Token);
            if (result.Success != true) return new JsonObject()
            {
                { "Success", false },
                { "Error", result.Error.ToString() }
            };
            foreach(BinancePositionDetailsUsdt obj in result.Data)
                if (obj.Quantity != 0) Positions.Add(JsonNode.Parse(JsonSerializer.Serialize(obj)));
            return new JsonObject()
            {
                { "Data", JsonNode.Parse(JsonSerializer.Serialize(Positions.ToArray())) },
                { "Success", true },
                { "Error", "" }
            };
        }

        public async Task<bool> SubscribeAccountUpdates(Action<DataEvent<BinanceFuturesStreamConfigUpdate>> OnLeverageUpdate,
            Action<DataEvent<BinanceFuturesStreamMarginUpdate>> OnMarginUpdate,
            Action<DataEvent<BinanceFuturesStreamAccountUpdate>> OnAccountUpdate,
            Action<DataEvent<BinanceFuturesStreamOrderUpdate>> OnOrderUpdate,
            Action<DataEvent<Binance.Net.Objects.Models.BinanceStreamEvent>> OnListenKeyExpired,
            CancellationToken CT = default)
        {
            var result = await SocketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(
                listenKey: ListenKey,
                onLeverageUpdate: OnLeverageUpdate,
                onMarginUpdate: OnMarginUpdate,
                onAccountUpdate: OnAccountUpdate,
                onOrderUpdate: OnOrderUpdate,
                onListenKeyExpired: OnListenKeyExpired,
                ct: CT);

            if (!result.Success) return false;
            else return true;
        }

        public async Task<bool> SubscribeAllMarketPrice(Action<DataEvent<IEnumerable<BinanceFuturesStreamMarkPrice>>> OnNewStockBoard,
            CancellationToken CT = default)
        {
            var result = await SocketClient.UsdFuturesStreams.SubscribeToAllMarkPriceUpdatesAsync(
                updateInterval: 1000,
                onMessage: OnNewStockBoard,
                ct: CT);

            if (!result.Success) return false;
            else return true;
        }

        public async Task<bool> SubscribeMarketPrice(string Symbol,
            Action<DataEvent<BinanceFuturesUsdtStreamMarkPrice>> OnNewPrice,
            CancellationToken CT = default)
        {
            var result = await SocketClient.UsdFuturesStreams.SubscribeToMarkPriceUpdatesAsync(
                symbol: Symbol,
                updateInterval: 1000,
                onMessage: OnNewPrice,
                ct: CT);

            if (!result.Success) return false;
            else return true;
        }

        public async Task<JsonObject> ChangeLeverage(string Symbol, int Leverage)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(Symbol, Leverage, ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> ChangeMarginType(string Symbol, FuturesMarginType Margin)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.UsdFuturesApi.Account.ChangeMarginTypeAsync(Symbol, Margin, ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> PlaceOrder(string Symbol, OrderSide Side, FuturesOrderType Type,
            decimal? Quantity = null, decimal? Price = null, PositionSide? PositionSide = null,
            TimeInForce? TimeInForce = null, bool? ReduceOnly = null, string NewClientOrderId = null,
            decimal? StopPrice = null, decimal? ActivationPrice = null, decimal? CallbackRate = null,
            WorkingType? WorkingType = null, bool? ClosePosition = null, OrderResponseType? OrderResponseType = null,
            bool? PriceProtect = null, int? ReceiveWindow = null)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(Symbol, Side, Type, Quantity, Price, PositionSide,
                TimeInForce, ReduceOnly, NewClientOrderId, StopPrice, ActivationPrice, CallbackRate,
                WorkingType, ClosePosition, OrderResponseType, PriceProtect, ReceiveWindow, CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> CancelOrder(string Symbol, long? OrderId = null, string ClientOrderId = null)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.UsdFuturesApi.Trading.CancelOrderAsync(Symbol, OrderId, ClientOrderId, ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }
    }
}
