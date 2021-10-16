module OptionsScreener.Client.Main

open System
open Elmish
open Bolero
open Bolero.Html
open Bolero.Remoting
open Bolero.Remoting.Client
open Bolero.Templating.Client
open OptionsScreener.Client.ViewModel

open FSharp.Data


/// Routing endpoints definition.
type Page =
    | [<EndPoint "/">] Home

type Model =
    {
        nextExpire: DateTime
        page: Page
        stockList: Stock[]
        selectedStock: StockInfo option
        error: string option
    }
    
type StockService =
    { getStocks: unit -> Async<Stock[]>
      getStockInfo: DateTime * Symbol -> Async<StockInfo>
    }
    interface IRemoteService with
        member this.BasePath = "/stock"


/// The Elmish application's update messages.
type Message =
    | SetPage of Page
    | LoadStockList
    | GotStockList of Stock[]
    | LoadStock of DateTime * Symbol
    | GotStockInfo of StockInfo
    | Error of exn
    | ClearError
    
/// Connects the routing system to the Elmish application.
let router = Router.infer SetPage (fun model -> model.page)

type Main = Template<"wwwroot/main.html">

let update remote message model =
    match message with
    | SetPage page ->
        { model with page = page }, Cmd.none

    | LoadStockList ->
        let cmd = Cmd.OfAsync.either remote.getStocks () GotStockList Error
        model, cmd
        
    | GotStockList value ->
        { model with stockList = value }, Cmd.none
        
    | LoadStock (expDate, stock) ->
        let cmd = Cmd.OfAsync.either remote.getStockInfo (expDate, stock) GotStockInfo Error 
        model, cmd 
        
    | GotStockInfo value ->
        { model with selectedStock = Some value }, Cmd.none
        
    | Error exn ->
        { model with error = Some exn.Message }, Cmd.none
    | ClearError ->
        { model with error = None }, Cmd.none

let inline formatMoney v = $"%.2f{v}"
let inline formatMoney' v = v |> Option.map formatMoney |> Option.defaultValue " - "

let rec findNthDay (day: DayOfWeek) (occurence: int) (date: DateTime) =
    let daysSince = (7 * (occurence - 1))
    let startFrom = DateTime(date.Year, date.Month, 1)
    let firstDay = Seq.initInfinite (fun i -> startFrom.AddDays(float i)) |> Seq.find (fun d -> d.DayOfWeek = day)
    let finalDay = firstDay.AddDays (float daysSince)
    
    if finalDay >= date then finalDay else findNthDay day occurence (date.AddMonths 1)
    
/// Next expiration date is 3rd Friday of each month 
let nextExpirationDate (date: DateTime) = findNthDay DayOfWeek.Friday 3 date

        
let stockRow' row =
    let optionCssClass v = v |> Option.filter (fun x -> x.inTheMoney) |> Option.map (fun _ -> "is-selected") |> Option.defaultValue ""
    Main.OptionRow()
        .Strike(formatMoney row.strike)
        .CallAsk(row.call |> Option.map (fun x -> x.ask) |> formatMoney')
        .CallBid(row.call |> Option.map (fun x -> x.bid) |> formatMoney')
        .PutAsk(row.put |> Option.map (fun x -> x.ask) |> formatMoney')
        .PutBid(row.put |> Option.map (fun x -> x.bid) |> formatMoney')
        .CallRowClass(optionCssClass row.call)
        .PutRowClass(optionCssClass row.put)
        .Elt()
        
// Fill in Home Page data
let homePage model dispatch =
    Main.Home()
        .StockInfo(cond model.selectedStock <| function
                   | None -> Main.NoStockSelected().Elt()
                   | Some stock ->
                       let p = stock.strikes |> Array.map stockRow' |> Array.toList |> concat
                       Main.SelectedStock()
                           .Symbol(stock.symbol.Value)
                           .Name(stock.symbol.Value)
                           .Price($"%.2f{stock.marketPrice}")
                           .Currency(stock.currency)
                           .Rows(p)
                           .Elt()
                   )
        .Elt()
     
let stockListItem (model: Model) dispatch (stock: Stock) =
    Main.StockListItem()
        .LoadStock(fun _ -> dispatch (LoadStock (model.nextExpire, stock.symbol)))
        .Symbol(stock.symbol.Value)
        .Name(stock.name)
        .Elt()
        
let view model dispatch =
    Main()
        .StockList(model.stockList |> Array.map (stockListItem model dispatch) |> Array.toList |> concat)
        .Body(
            cond model.page <| function
            | Home -> homePage model dispatch
        )
        .Error(
            cond model.error <| function
            | None -> empty
            | Some err ->
                Main.ErrorNotification()
                    .Text(err)
                    .Hide(fun _ -> dispatch ClearError)
                    .Elt()
        )
        .Elt()

let initModel date =
    {
        nextExpire = nextExpirationDate date
        page = Home
        stockList = Array.empty
        selectedStock = None
        error = None
    }

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let model = initModel DateTime.UtcNow
        let stockService = this.Remote<StockService>()
        let update = update stockService
        Program.mkProgram (fun _ -> model, Cmd.ofMsg LoadStockList) update view
        |> Program.withRouter router
#if DEBUG
        |> Program.withHotReload
#endif
