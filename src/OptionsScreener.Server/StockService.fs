module OptionsScreener.Server.StockService

open System
open Bolero.Remoting.Server
open Microsoft.AspNetCore.Hosting
open OptionsScreener
open FSharp.Data
open OptionsScreener.Client.ViewModel

[<Literal>]
let sp500ListPage = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies"
type sp500Page = HtmlProvider<sp500ListPage>

type optionsApi = JsonProvider<"data/options.json", ResolutionFolder=__SOURCE_DIRECTORY__>


type StockService(ctx: IRemoteContext, env: IWebHostEnvironment) =
    inherit RemoteHandler<Client.StockService>()
    
    override this.Handler = {
        getStocks =
            fun () ->
                async {
                    let! value = sp500Page.AsyncLoad(sp500ListPage)
                    let arr =
                        value.Tables.``S&P 500 component stocksEdit``.Rows
                        |> Seq.map (fun x -> { symbol = Symbol x.Symbol; name = x.Security})
                        |> Seq.toArray
                    return arr
                }
                
        getStockInfo =
            fun (expirationDate, symbol) ->
                async {
                    let stamp = int (expirationDate - DateTime.UnixEpoch).TotalSeconds
                    let url = $"https://query2.finance.yahoo.com/v7/finance/options/{symbol.Value}?date={stamp}"
                    let! data = optionsApi.AsyncLoad(url)
                    let res = data.OptionChain.Result |> Array.head
                    
                    let calls =
                        res.Options
                        |> Array.collect (fun x -> x.Calls)
                        |> Seq.map (fun x -> (x.Strike, { ask = x.Ask; bid = x.Bid; strike = x.Strike; inTheMoney = x.InTheMoney; volume = x.Volume |> Option.defaultValue 0; volatility = x.ImpliedVolatility |> Option.defaultValue 0M }))
                        |> Map.ofSeq
                        
                    let puts =
                        res.Options
                        |> Seq.collect (fun x -> x.Puts)
                        |> Seq.map (fun x -> (x.Strike, { ask = x.Ask; bid = x.Bid; strike = x.Strike; inTheMoney = x.InTheMoney; volume = x.Volume |> Option.defaultValue 0; volatility = x.ImpliedVolatility |> Option.defaultValue 0M }))
                        |> Map.ofSeq
                        
                    let lines =
                        res.Strikes
                        |> Seq.map (fun x ->  { strike = x; call = Map.tryFind x calls; put = Map.tryFind x puts })
                        |> Seq.toArray
                        
                    return
                        { symbol = symbol
                          marketPrice = res.Quote.RegularMarketPrice
                          currency = res.Quote.Currency
                          strikes = lines }
                }
    }