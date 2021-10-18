module OptionsScreener.Client.ViewModel

type Symbol =
    Symbol of string
    with
        member this.Value = let (Symbol v) = this in v

type Stock = { symbol: Symbol; name: string }

type Option =
    { ask: decimal option
      bid: decimal option
      strike: decimal
      volume: int
      volatility: decimal
      inTheMoney: bool }

type StrikeLine =
    { strike: decimal
      call: Option option
      put: Option option }
    
type StockInfo =
    { symbol: Symbol
      marketPrice: decimal
      currency: string
      strikes: StrikeLine array }