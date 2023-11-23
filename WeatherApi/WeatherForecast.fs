namespace WeatherApi

open System
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions
open System.Text.Json.Serialization
open System.Threading
open System.Threading.Tasks

(*
Sample locations lat,long
New York: 40.741895,-73.989308
Chicago:  41.8755616,-87.624421
Houston:  29.7589382,-95.3676974
*)

type Location = string
type Json = string
type ErrorMessage = string
type MessageTemplate = string
type Latitude = float
type Longitude = float

// Forecast response model
type DailyForecast =
    { Date: DateTime
      TemperatureMinC: int
      TemperatureMinF: int
      TemperatureMaxC: int
      TemperatureMaxF: int
      Summary: string }
    static member Create date minC minF maxC maxF summary =
        { Date = date
          TemperatureMinC = minC
          TemperatureMinF = minF
          TemperatureMaxC = maxC
          TemperatureMaxF = maxF
          Summary = summary }

type WeatherForecast =
    { TemperatureCurrentC: int
      TemperatureCurrentF: int
      DailyForecast: DailyForecast array }

type Coordinates =
    { Latitude: float
      Longitude: float }

module Coordinates =
    let ofString (location:string) : Coordinates option =
        let pattern = "(-{0,1}\d{1,3}\.\d{5,7}),(-{0,1}\d{1,3}\.\d{5,7})"
        let mtch = Regex.Match(location, pattern)
        if mtch.Success then
            Some { Latitude = float mtch.Groups.[1].Value
                   Longitude = float mtch.Groups.[2].Value }
        else
            None
    
// I/O operations interface
type IWeatherForecastIO =
    abstract CallWeatherServiceAsync: Latitude * Longitude * CancellationToken -> Task<Result<WeatherForecast, ErrorMessage>>
    abstract LogError: MessageTemplate * obj array -> unit
    abstract LogInformation: MessageTemplate * obj array -> unit
    
// Concrete implementation using the open-meteo free weather API.    
module OpenMeteoWeatherService =
    
    // Partial open-meteo response models.
    type Current = {
        Time: DateTimeOffset
        [<JsonPropertyName("temperature_2m")>]
        Temperature2m: float
    }

    type Daily = {
        Time: DateTimeOffset array
        [<JsonPropertyName("temperature_2m_max")>]
        Temperature2mMax: float array
        [<JsonPropertyName("temperature_2m_min")>]
        Temperature2mMin: float array
        WeatherCode: int array
    }

    type WeatherData = {
        Current: Current
        Daily: Daily
    }
    
    let WeatherServiceUrl = "https://api.open-meteo.com/v1/forecast"
    let TimeZone = "America/Chicago"
    
    let createRequestUrl (coordinates: Coordinates) =
        let current = "temperature_2m"
        let daily = "temperature_2m_max,temperature_2m_min,weathercode"
        $"{WeatherServiceUrl}?latitude={coordinates.Latitude}&longitude={coordinates.Longitude}&current={current}&daily={daily}&timezone={TimeZone}"
        
    let codeToSummary code =
        match code with
        | 0 -> "Clear"
        | 1 -> "Mainly clear"
        | 2 -> "Partly cloudy"
        | 3 -> "Overcast"
        | 45
        | 48 -> "Fog"
        | 51
        | 53
        | 55 -> "Drizzle"
        | 56
        | 57 -> "Freezing drizzle"
        | 61
        | 63
        | 65
        | 80
        | 81
        | 82 -> "Rain"
        | 66
        | 67 -> "Freezing rain"
        | 71
        | 73
        | 75 
        | 77
        | 85
        | 86 -> "Snow"
        | _ -> ""
    
    let parseResponse (weatherDataJson:string) : WeatherForecast =
        let options = JsonSerializerOptions(JsonSerializerDefaults.Web)
        let weatherData = JsonSerializer.Deserialize<WeatherData>(weatherDataJson, options)
        
        let dailyForecasts =
            (Array.zip3  weatherData.Daily.Temperature2mMin
                         weatherData.Daily.Temperature2mMax
                         weatherData.Daily.WeatherCode)
            |> Array.zip weatherData.Daily.Time
            |> Array.map (fun (date, (min, max, code)) ->
                { Date = date.Date
                  TemperatureMinC = int min
                  TemperatureMinF = int (min |> toFahrenheit)
                  TemperatureMaxC = int max
                  TemperatureMaxF = int (max |> toFahrenheit)
                  Summary = code |> codeToSummary })
                    
        { TemperatureCurrentC = int weatherData.Current.Temperature2m
          TemperatureCurrentF =  int (weatherData.Current.Temperature2m |> toFahrenheit)
          DailyForecast = dailyForecasts }
        
    let getForecastAsync
        (httpClientFactory:IHttpClientFactory)  // Dependency
        (latitude: Latitude)
        (longitude: Longitude)
        (ct:CancellationToken) : Task<Result<WeatherForecast, string>> =
        task{
            try
                let coordinates = { Latitude = latitude; Longitude = longitude }
                let httpClient = httpClientFactory.CreateClient()
                let url = createRequestUrl coordinates
                let! response = httpClient.GetAsync(url, ct)
                let! json = response.Content.ReadAsStringAsync()
                let weatherForecast = parseResponse json
                return Ok weatherForecast
            with
            | ex ->
                return Error $"Error calling the API: {ex.Message}"
        }
