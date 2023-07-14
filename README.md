# HNService

HNService is a restful service, intended to fetch given number of top rated Hacker News items from "https://github.com/HackerNews/API".
This service can be build for production environment, which needs the appSettings.Production.json to be added to project and included to appSettings configuration. 

Assumptions: 
  Service assumes that, once any detail of news item is updated at Hacker News server, there will not be any automatic/scheduled notifications to current service. Hence current servive (HNService) will request for fresh set of data at scheduled interval defined in minutes. This scheduling is configurable via addSettings, and defaulted to 30 minutes, in case of user/support team doesn’t set this value.

 Key Points: 
   Restful service fetches data from Hacker News server, caches in memory till next scheduled refresh.
   Data fetch from Hacker service is throttled to number of cores available on server (-2), bypassing framework throttling.
   Services Polly NuGet package for caching and retry policies.
   This uses Serilog NuGet package for logging.
   Service uses Dependency Injection, hence loosely coupled interfaces are easy to test.
   Service uses .Net 7.0 framework.
   Api will gracefully return error code in case of any exception being caught.
   This service can be easily deployed via DevOps and using containerization to could hosted. 
   Service is using Swagger interface for ease of testing from web browser.
   
Possible Improvements :
  Although this is simple service for data get, however it’s good to have Unit Test cases.
  Secrets and configuration to be fetched from cloud key vault, defaulting to appsettings.json file.
  Data fetch from Hacker service throttling can be made configurable. 
