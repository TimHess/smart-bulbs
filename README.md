# CF Summit 2018 - Smart Bulb Demo

## Overview

This repository consists of two .NET Core applications and one .NET Standard library. All code is C#. One of the applications is ASP.NET MVC (SmartBulbs.Web) and the other is a .NET Core Console application (SmartBulbs.Console). SmartBulbs.Common is for sharing models between the two applications.

## Tools used

- ASP.NET Core (2.1.0-preview1-final)
  - SmartBulbs.Web: provides a UI for getting a CredHub password, reading tweets on demand, and viewing sentiment analysis and color results
    - MVC: for views and HTTP endpoints
    - SignalR: powers the Observation deck
- .NET Core Console application
  - SmartBulbs.Console: polls twitter for new #cfsummit tweets

### Software Libraries

- [LifxIoT](https://www.nuget.org/packages/LifxIoT/) (for calling the LIFX API)
- [LinqToTwitter](https://www.nuget.org/packages/linqtotwitter/5.0.0-beta1) (for interactions with Twitter)
- [Steeltoe](https://steeltoe.io)
  - Service Discovery (for the SmartBulbs.Console to discover SmartBulbs.Web)
  - CloudFoundry Configuration (to read Cloud Foundry environment variables)
  - CredHub Client (for interactions with CredHub)
  - Circuit Breaker (around CredHub call, generates a GUID if create password request fails)
  - Management (for application management and monitoring)

### 3rd Party Services

- Microsoft Cognitive Services Text Analysis API
  - [Sentiment Analysis](https://azure.microsoft.com/en-us/services/cognitive-services/text-analytics/) - determines how the writer is feeling based on the words in their message
- [LIFX](https://www.lifx.com/) HTTP API - controls the smart bulbs

## Twitter monitoring overview

<img src="./img/twitter-monitor.png" alt="Twitter Monitor Diagram" height=500px>
