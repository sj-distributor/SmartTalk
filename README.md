# SmartTalk

## Brief

This project, called SmartTalk, is mainly aimed at realizing communication between humans and AI, allowing AI to help humans solve as many problems or tasks as possible in multiple dimensions, channels and scenarios.

## Modules

1. Realtime AI实时对话模块（AiSpeechAssistantController）
   * AI 助理知识库
   * Talk with AI in multiple dimensions, channels and scenarios
2. 电话下单模块（PhoneOrderController）
3. 用户管理模块（AccountController、SecurityController）

## Structure

- root/
  - src/
    - SmartTalk.Api/
      - Authentication/
        - ApiKey/
        - OME/
        - Wiltechs/
      - Controllers/
        - AccountController.cs
        - AiSpeechAssistantController.cs
        - PhoneOrderController.cs
        - SecurityController.cs
      - Extensions/
      - Filters/
      - appsettings.json
      - Program.cs
      - Startup.cs
    - SmartTalk.Core/
      - DbUpFile/
      - Domain/
        - AiSpeechAssistant/
        - PhoneOrder/
        - Security/
      - Extensions/
      - Handlers/
        - CommandHandlers/
          - AiSpeechAssistant/
          - PhoneOrder/
          - Security/
        - EventHandlers/
        - RequestHandlers/
          - AiSpeechAssistant/
          - PhoneOrder/
          - Security/
      - Ioc/
      - Jobs/
      - Mappings/
      - Middlewares/
      - Services/
        - AiSpeechAssistant/
        - PhoneOrder/
        - Security/
      - Settings/
      - SmartTalkModule.cs
    - SmartTalk.Messages/
      - Commands/
        - AiSpeechAssistant/
        - PhoneOrder/
        - Security/
      - Dto/
        - AiSpeechAssistant/
        - PhoneOrder/
        - Security/
      - Enums/
        - AiSpeechAssistant/
        - PhoneOrder/
        - Security/
      - Requests/
        - AiSpeechAssistant/
        - PhoneOrder/
        - Security/
    - SmartTalk.IntegrationTests/
  - .gitignore
  - package.json
  - README.md
  - Nuget.Config
  - WebApiDockerfile

## Architecture

Using this repository as the architecture: [Mediator.Net](https://github.com/mayuanyang/Mediator.Net)

![9a065420-db09-11e6-8dbc-ca0069894e1c](https://cloud.githubusercontent.com/assets/3387099/21959127/9a065420-db09-11e6-8dbc-ca0069894e1c.png)

## Nuget Package

* Architecture: Mediator.Net
* IOC：Autofac(6.4.0)
* Mapping：AutoMapper(12.0.1)
* DateBase: dbup、dbup-mysql、efcore
* Background job: hangfire.pro.redis
* Log: Serilog
* Test: Xunit、NSubstitute、Shouldly
* Json: Newtonsoft.Json

