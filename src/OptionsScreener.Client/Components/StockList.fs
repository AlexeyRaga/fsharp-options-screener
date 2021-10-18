module OptionsScreener.Client.Components.StockList

open System
open Bolero
open Elmish
open OptionsScreener.Client
open OptionsScreener.Client.ViewModel
open Bolero.Html

type Operation<'a> = Started | Finished of 'a

type ViewTemplate = Template<"wwwroot/Components/StockList.html">

type Model =
    { searchTerm: string
      items: Stock array }
    
let emptyModel =
    { searchTerm = ""; items = Array.empty }
   
    
type Message =
    | LoadListItems of Operation<Stock array>
    | ItemSelected of Stock
    | Search of string
    | Error of exn
   
let init() = emptyModel, Cmd.ofMsg (LoadListItems Started)

let update (stockService: StockService) (message: Message) (model: Model) =
    match message with
    | LoadListItems Started ->
        let cmd = Cmd.OfAsync.either stockService.getStocks () (Finished >> LoadListItems) Error
        model, cmd
    | LoadListItems (Finished values) ->
        { model with items = values }, Cmd.none
    | ItemSelected _ ->
        model, Cmd.none
    | Search term ->
        { model with searchTerm = term }, Cmd.none
    | Error _ ->
        model, Cmd.none
        
let stockListItem (model: Model) dispatch (stock: Stock) =
    ViewTemplate.StockListItem()
        .LoadStock(fun _ -> dispatch (ItemSelected stock))
        .Symbol(stock.symbol.Value)
        .Name(stock.name)
        .Elt()
        
type Component() =
    inherit ElmishComponent<Model, Message>()

    override this.View model dispatch =
        let inline (=~) (a: string) (b: string) = a.Contains(b, StringComparison.InvariantCultureIgnoreCase)
        ViewTemplate()
            .SearchTerm(model.searchTerm, fun term -> dispatch (Search term))
            .StockList(
                model.items
                |> Seq.filter (fun x -> x.name =~ model.searchTerm || x.symbol.Value =~ model.searchTerm)
                |> Seq.map (stockListItem model dispatch)
                |> Seq.toList
                |> concat)
            .Elt()
        
let view model dispatch = ecomp<Component, Model, Message> [] model dispatch