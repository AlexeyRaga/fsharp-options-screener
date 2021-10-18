module OptionsScreener.Client.Main

open System
open Elmish
open Bolero
open Bolero.Html
open Bolero.Remoting
open Bolero.Remoting.Client
open Bolero.Templating.Client
open OptionsScreener.Client.Components
open OptionsScreener.Client.ViewModel

/// Routing endpoints definition.
type Page =
    | [<EndPoint "/">] Home

type Model =
    {
        nextExpire: DateTime
        page: Page
        stockList: StockList.Model
        nearMarket: bool
        selectedStock: StockInfo option
        error: string option
    }

/// The Elmish application's update messages.
type Message =
    | SetPage of Page
    | StockListMessage of StockList.Message
    | LoadStock of Symbol
    | GotStockInfo of StockInfo
    | EnableNearMarket of bool
    | Error of exn
    | ClearError
    
/// Connects the routing system to the Elmish application.
let router = Router.infer SetPage (fun model -> model.page)

type Main = Template<"wwwroot/main.html">

let update remote message model =
    match message with
    | SetPage page ->
        { model with page = page }, Cmd.none
        
    | StockListMessage (StockList.Message.ItemSelected stock) ->
        model, Cmd.ofMsg (LoadStock stock.symbol)
        
    | StockListMessage msg ->
        let listModel, listCmd = StockList.update remote msg model.stockList
        { model with stockList = listModel }, Cmd.map StockListMessage listCmd
        
    | LoadStock stock ->
        let cmd = Cmd.OfAsync.either remote.getStockInfo (model.nextExpire, stock) GotStockInfo Error 
        model, cmd 
        
    | GotStockInfo value ->
        { model with selectedStock = Some value }, Cmd.none
        
    | EnableNearMarket value ->
        { model with nearMarket = value }, Cmd.none
        
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
    
module Option =
    let inline maybe z f = Option.fold (fun _ -> f) z 
        
let stockRow' row =
    let optionCssClass v = v |> Option.filter (fun x -> x.inTheMoney) |> Option.map (fun _ -> "is-selected") |> Option.defaultValue ""
    
    let inline withOpt o f state  = Option.fold f state o

    let template =
        Main.OptionRow()
            |> withOpt row.call (fun html x ->
                html.CallAsk(x.ask |> formatMoney')
                    .CallBid(x.bid |> formatMoney')
                    .CallVolume(string x.volume)
                    .CallVolatility(string x.volatility)
                )
            |> withOpt row.put (fun html x ->
                html.PutAsk(x.ask |> formatMoney')
                    .PutBid(x.bid |> formatMoney')
                    .PutVolume(string x.volume)
                    .PutVolatility(string x.volatility)
                )
    
    template
        .Strike(formatMoney row.strike)
        .CallRowClass(optionCssClass row.call)
        .PutRowClass(optionCssClass row.put)
        .Elt()
        
// Fill in Home Page data
let homePage model dispatch =
    Main.Home()
        .StockInfo(cond model.selectedStock <| function
                   | None -> Main.NoStockSelected().Elt()
                   | Some stock ->
                       let lines =
                            if model.nearMarket then 
                                let marketLineIndex = stock.strikes |> Array.findIndex (fun x -> x.strike > stock.marketPrice)
                                stock.strikes.[marketLineIndex-5 .. marketLineIndex+5]
                            else stock.strikes
                                
                       let p = lines |> Array.map stockRow' |> Array.toList |> concat
                       Main.SelectedStock()
                           .Symbol(stock.symbol.Value)
                           .NearMarket(model.nearMarket, fun x -> dispatch (EnableNearMarket x) )
                           .Name(stock.symbol.Value)
                           .Price($"%.2f{stock.marketPrice}")
                           .Currency(stock.currency)
                           .Rows(p)
                           .Elt()
                   )
        .Elt()
     
let view model dispatch =
    Main()
        .StockList(StockList.view model.stockList (StockListMessage >> dispatch))
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
    let stockListModel, stockListCmd = StockList.init()
    {
        nextExpire = nextExpirationDate date
        page = Home
        nearMarket = true
        stockList = stockListModel
        selectedStock = None
        error = None
    }, Cmd.map StockListMessage stockListCmd

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let model, cmd = initModel DateTime.UtcNow
        let stockService = this.Remote<StockService>()
        let update = update stockService
        Program.mkProgram (fun _ -> model, cmd) update view
        |> Program.withRouter router
#if DEBUG
        |> Program.withHotReload
#endif
