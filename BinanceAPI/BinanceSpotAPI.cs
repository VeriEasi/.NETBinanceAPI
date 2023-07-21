using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Spot.Socket;
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
    public class BinanceSpotAPI
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
        public BinanceSpotAPI(string APIKey, string APISecret)
        {
            this.APIKey = APIKey;
            this.APISecret = APISecret;
        }

        // Destructor
        ~BinanceSpotAPI()
        {
            Client?.Dispose();
            SocketClient?.Dispose();
        }

        public async Task<bool> Connect()
        {
            Client = new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(APIKey, APISecret),
                SpotApiOptions = new BinanceApiClientOptions
                {
                    BaseAddress = BinanceApiAddresses.Default.RestClientAddress,
                }
            });

            SocketClient = new BinanceSocketClient(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(APIKey, APISecret),
                SpotStreamsOptions = new BinanceApiClientOptions
                {
                    BaseAddress = BinanceApiAddresses.Default.SocketClientAddress,
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
            var listenKey = await Client.SpotApi.Account.StartUserStreamAsync();
            if (!listenKey.Success) return null;
            else return listenKey.Data;
        }

        private async Task<bool> StartKeepAlive()
        {
            var result = await Client.SpotApi.Account.KeepAliveUserStreamAsync(listenKey: ListenKey);
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
            var result = await Client.SpotApi.Account.StopUserStreamAsync(listenKey: ListenKey);
            if (!result.Success) return false;
            else return true;
        }

        public async Task<long> GetServerTime()
        {
            CancellationTokenSource CTS = new(5000);
            DateTime time = (await Client.SpotApi.ExchangeData.GetServerTimeAsync(ct: CTS.Token)).Data;
            if (time == new DateTime(1, 1, 1, 0, 0, 0)) return 0;
            return (long)((time.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds);
        }

        public async Task<JsonObject> GetAPIKeyPermissions()
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.SpotApi.Account.GetAPIKeyPermissionsAsync(ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> GetAccountInfo()
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.SpotApi.Account.GetAccountInfoAsync(ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> GetBalances()
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.SpotApi.Account.GetBalancesAsync(ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> GetOpenOrders(string Symbol = null)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.SpotApi.Trading.GetOpenOrdersAsync(Symbol, ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<bool> SubscribeAccountUpdates(Action<DataEvent<BinanceStreamOrderUpdate>> OnOrderUpdate,
            Action<DataEvent<BinanceStreamOrderList>> OnOcoOrderUpdate,
            Action<DataEvent<BinanceStreamPositionsUpdate>> OnPositionUpdate,
            Action<DataEvent<BinanceStreamBalanceUpdate>> OnBalanceUpdate,
            CancellationToken CT = default)
        {
            var result = await SocketClient.SpotStreams.SubscribeToUserDataUpdatesAsync(
                listenKey: ListenKey,
                onOrderUpdateMessage: OnOrderUpdate,
                onOcoOrderUpdateMessage: OnOcoOrderUpdate,
                onAccountPositionMessage: OnPositionUpdate,
                onAccountBalanceUpdate: OnBalanceUpdate,
                ct: CT);

            if (!result.Success) return false;
            else return true;
        }

        public async Task<bool> SubscribeAllTickerUpdates(Action<DataEvent<IEnumerable<IBinanceTick>>> OnNewTickers,
            CancellationToken CT = default)
        {
            var result = await SocketClient.SpotStreams.SubscribeToAllTickerUpdatesAsync(
                onMessage: OnNewTickers,
                ct: CT);

            if (!result.Success) return false;
            else return true;
        }

        public async Task<bool> SubscribeTickerUpdate(string Symbol,
            Action<DataEvent<IBinanceTick>> OnNewTicker,
            CancellationToken CT = default)
        {
            var result = await SocketClient.SpotStreams.SubscribeToTickerUpdatesAsync(
                symbol: Symbol,
                onMessage: OnNewTicker,
                ct: CT);

            if (!result.Success) return false;
            else return true;
        }

        public async Task<JsonObject> PlaceOrder(string Symbol, OrderSide Side, SpotOrderType Type,
            decimal? Quantity = null, decimal? QuoteQuantity = null, string NewClientOrderId = null, decimal? Price = null,
            TimeInForce? TimeInForce = null, decimal? StopPrice = null, decimal? IcebergQty = null, 
            OrderResponseType? OrderResponseType = null, int? TrailingData = null, int? ReceiveWindow = null)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.SpotApi.Trading.PlaceOrderAsync(Symbol, Side, Type, Quantity, QuoteQuantity, NewClientOrderId,
                Price, TimeInForce, StopPrice, IcebergQty, OrderResponseType, TrailingData, ReceiveWindow, CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }

        public async Task<JsonObject> CancelOrder(string Symbol, long? OrderId = null, string ClientOrderId = null)
        {
            CancellationTokenSource CTS = new(5000);
            var result = await Client.SpotApi.Trading.CancelOrderAsync(Symbol, OrderId, ClientOrderId, ct: CTS.Token);
            return JsonNode.Parse(JsonSerializer.Serialize(result)).AsObject();
        }
    }
}
