using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Authentication;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
            Client.Dispose();
            SocketClient.Dispose();
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
            Console.WriteLine(checkConnect);
            // Check Listen Key
            var lk = await GetListenKey();
            if (lk == null) return false;
            ListenKey = lk.ToString();
            // Check Ping Pong
            return await StartKeepAlive();
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
            else return true;
        }

        private async Task<bool> StopKeepAlive()
        {
            var result = await Client.UsdFuturesApi.Account.StopUserStreamAsync(listenKey: ListenKey);
            if (!result.Success) return false;
            else return true;
        }

        public async Task<long> GetServerTime()
        {
            DateTime time = (await Client.UsdFuturesApi.ExchangeData.GetServerTimeAsync()).Data;
            if (time == new DateTime(1, 1, 1, 0, 0, 0)) return 0;
            return (long)((time.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds);
        }

        public async Task<JObject> GetAccountInfo()
        {
            var result = await Client.UsdFuturesApi.Account.GetAccountInfoAsync();
            return JObject.Parse(JsonSerializer.Serialize(result));
        }

        public async Task<bool> SubscribeAccountUpdates(Action<CryptoExchange.Net.Sockets.DataEvent<Binance.Net.Objects.Models.Futures.Socket.BinanceFuturesStreamConfigUpdate>> OnLeverageUpdate,
            Action<CryptoExchange.Net.Sockets.DataEvent<Binance.Net.Objects.Models.Futures.Socket.BinanceFuturesStreamMarginUpdate>> OnMarginUpdate,
            Action<CryptoExchange.Net.Sockets.DataEvent<Binance.Net.Objects.Models.Futures.Socket.BinanceFuturesStreamAccountUpdate>> OnAccountUpdate,
            Action<CryptoExchange.Net.Sockets.DataEvent<Binance.Net.Objects.Models.Futures.Socket.BinanceFuturesStreamOrderUpdate>> OnOrderUpdate,
            Action<CryptoExchange.Net.Sockets.DataEvent<Binance.Net.Objects.Models.BinanceStreamEvent>> OnListenKeyExpired)
        {
            var result = await SocketClient.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(
                listenKey: ListenKey,
                onLeverageUpdate: OnLeverageUpdate,
                onMarginUpdate: OnMarginUpdate,
                onAccountUpdate: OnAccountUpdate,
                onOrderUpdate: OnOrderUpdate,
                onListenKeyExpired: OnListenKeyExpired);

            if (!result.Success) return false;
            else return true;
        }
        
        public async Task<JObject> ChangeLeverage(string Symbol, int Leverage)
        {
            var result = await Client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(Symbol, Leverage);
            return JObject.Parse(JsonSerializer.Serialize(result));
        }

        public async Task<JObject> ChangeMarginType(string Symbol, FuturesMarginType Margin)
        {
            var result = await Client.UsdFuturesApi.Account.ChangeMarginTypeAsync(Symbol, Margin);
            return JObject.Parse(JsonSerializer.Serialize(result));
        }

        public async Task<JObject> ChangePositionMargin(string Symbol)
        {
            var result = await Client.UsdFuturesApi.Account.GetMarginChangeHistoryAsync(Symbol);
            List<BinanceFuturesMarginChangeHistoryResult> r = (List<BinanceFuturesMarginChangeHistoryResult>)result.Data;
            if (!result.Success) return JObject.Parse(JsonSerializer.Serialize(result));

            var res = await Client.UsdFuturesApi.Account.ModifyPositionMarginAsync(
                Symbol,
                Math.Abs(r[^1].Quantity),
                r[^1].Type,
                r[^1].PositionSide);
            return JObject.Parse(JsonSerializer.Serialize(res));
        }

        public async Task<JObject> PlaceOrder(string Symbol, OrderSide Side, FuturesOrderType Type,
            decimal? Quantity, decimal? Price = null, PositionSide? PositionSide = null,
            TimeInForce? TimeInForce = null, bool? ReduceOnly = null, string? NewClientOrderId = null,
            decimal? StopPrice = null, decimal? ActivationPrice = null, decimal? CallbackRate = null,
            WorkingType? WorkingType = null, bool? ClosePosition = null, OrderResponseType? OrderResponseType = null,
            bool? PriceProtect = null, int? ReceiveWindow = null, CancellationToken CT = default)
        {
            var result = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(Symbol, Side, Type, Quantity, Price, PositionSide,
                TimeInForce, ReduceOnly, NewClientOrderId, StopPrice, ActivationPrice, CallbackRate,
                WorkingType, ClosePosition, OrderResponseType, PriceProtect, ReceiveWindow, CT);
            return JObject.Parse(JsonSerializer.Serialize(result));
        }

        public async Task<bool> GetTradesHistory(string Symbol)
        {
            var result = await Client.UsdFuturesApi.Account.GetIncomeHistoryAsync(Symbol);
            //if (!result.Success) return null;
            //var options = new JsonSerializerOptions { WriteIndented = true };
            //Console.WriteLine(JsonSerializer.Serialize(result, options));
            return true;
        }
    }
}
