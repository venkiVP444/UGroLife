namespace LineAndLeaf.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open System.Diagnostics

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Authorization // Added for [Authorize] attribute
open LineAndLeaf.Models

type HomeController (logger : ILogger<HomeController>, config : IConfiguration) =
    inherit Controller()

    member this.Orders () =
        this.View()

    member this.Index () =
        this.View()

    member this.Privacy () =
        this.View()

    member this.Login () =
        this.View()

    member this.Register () =
        // Indent the 'let' bindings and the final expression within the member
        let apiBaseUrl = config.GetSection("AppConfig").GetValue<string>("ApiBaseUrl")
        let model = { LineAndLeaf.Models.ApiBaseUrl = apiBaseUrl }
        this.View(model)

    [<ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)>]
    member this.Error () =
        // Indent the 'let' binding and its body within the member
        let reqId =
            if isNull Activity.Current then
                this.HttpContext.TraceIdentifier
            else
                Activity.Current.Id
        this.View({ RequestId = reqId })

