namespace OptionsScreener.Client

open System
open Bolero.Remoting
open OptionsScreener.Client.ViewModel

type StockService =
    { getStocks: unit -> Async<Stock[]>
      getStockInfo: DateTime * Symbol -> Async<StockInfo>
    }
    interface IRemoteService with
        member this.BasePath = "/stock"