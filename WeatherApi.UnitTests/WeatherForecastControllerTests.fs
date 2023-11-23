namespace WeatherApi.UnitTests

open Microsoft.AspNetCore.Mvc
open NSubstitute
open System
open System.Threading.Tasks
open WeatherApi
open WeatherApi.Controllers
open Xunit
open Xunit.Abstractions

type WeatherForecastControllerTests(output: ITestOutputHelper) =

    [<Fact>]
    member this.``GetAsync should return a list of weather forecasts``() =
        task {
            // Arrange
            let (location: Location) = "41.8755616,-87.624421"
            let today = DateTime.Today

            let (expectedForecastResult: Result<WeatherForecast, string>) =
                Ok
                    { TemperatureCurrentC = 7
                      TemperatureCurrentF = 45
                      DailyForecast =
                        [| DailyForecast.Create today 7 45 12 55 "Overcast"
                           DailyForecast.Create (today.AddDays(1)) 3 38 17 62 "Drizzle"
                           DailyForecast.Create (today.AddDays(2)) 3 38 17 62 "Drizzle"
                           DailyForecast.Create (today.AddDays(3)) 9 48 17 62 "Clear"
                           DailyForecast.Create (today.AddDays(4)) 9 48 20 68 "Overcast"
                           DailyForecast.Create (today.AddDays(5)) 14 57 23 74 "Overcast"
                           DailyForecast.Create (today.AddDays(6)) 19 67 29 85 "Partly cloudy" |] }
                    
            let mockWeatherForecastIO = Substitute.For<IWeatherForecastIO>()
            mockWeatherForecastIO
                .CallWeatherServiceAsync(Arg.Any(), Arg.Any(), Arg.Any())
                .Returns(expectedForecastResult)
            |> ignore

            let ctrl = WeatherForecastController(mockWeatherForecastIO)

            // Act
            let! result = ctrl.GetAsync location

            // Assert
            Assert.NotNull(result)

            Assert.IsAssignableFrom<ObjectResult>(result)
            |> ignore

            let objectValue = (result :?> ObjectResult).Value :?> WeatherForecast
            Assert.Equal(7, objectValue.TemperatureCurrentC)
        }

    [<Fact>]
    member this.``GetAsync should return a list of weather forecasts (alternate)``() =
        task {
            // Arrange
            let (location: Location) = "29.7589382,-95.3676974"
            let today = DateTime.Today

            let (expectedForecastResult: Result<WeatherForecast, string>) =
                Ok
                    { TemperatureCurrentC = 7
                      TemperatureCurrentF = 45
                      DailyForecast =
                        [| DailyForecast.Create today 7 45 12 55 "Overcast"
                           DailyForecast.Create (today.AddDays(1)) 3 38 17 62 "Drizzle"
                           DailyForecast.Create (today.AddDays(2)) 3 38 17 62 "Drizzle"
                           DailyForecast.Create (today.AddDays(3)) 9 48 17 62 "Clear"
                           DailyForecast.Create (today.AddDays(4)) 9 48 20 68 "Overcast"
                           DailyForecast.Create (today.AddDays(5)) 14 57 23 74 "Overcast"
                           DailyForecast.Create (today.AddDays(6)) 19 67 29 85 "Partly cloudy" |] }

            let mutable timesCalled = 0 
            let mockWeatherForecastIOCustom =
                { new IWeatherForecastIO with
                    member _.CallWeatherServiceAsync(_, _, _) =
                        task{
                            timesCalled <- timesCalled + 1
                            return expectedForecastResult
                        }
                    member _.LogError(error, args) = ()
                    member _.LogInformation(error, args) = () }


            let ctrl = WeatherForecastController(mockWeatherForecastIOCustom)

            // Act
            let! result = ctrl.GetAsync location

            // Assert
            Assert.NotNull(result)
            Assert.Equal(1, timesCalled)

            Assert.IsAssignableFrom<ObjectResult>(result)
            |> ignore

            let objectValue = (result :?> ObjectResult).Value :?> WeatherForecast
            Assert.Equal(7, objectValue.TemperatureCurrentC)
        }

    [<Fact>]
    member this.``GetAsync should return an error for an invalid location``() =
        task {
            // Arrange
            let (location: Location) = "123"
            let errorMsg = "Invalid location."
            let (expectedForecastResult: Result<WeatherForecast, string>) = Error errorMsg
            
            let mockWeatherForecastIO = Substitute.For<IWeatherForecastIO>()
            mockWeatherForecastIO
                .CallWeatherServiceAsync(Arg.Any(), Arg.Any(), Arg.Any())
                .Returns(expectedForecastResult)
            |> ignore

            let ctrl = WeatherForecastController(mockWeatherForecastIO)

            // Act
            let! result = ctrl.GetAsync location

            // Assert
            Assert.IsAssignableFrom<BadRequestObjectResult>(result)
            |> ignore

            let badRequest = result :?> BadRequestObjectResult
            Assert.Equal(Nullable(400), badRequest.StatusCode)
            Assert.Equal(errorMsg, badRequest.Value.ToString())
        }
