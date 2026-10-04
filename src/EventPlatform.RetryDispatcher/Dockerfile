FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/EventPlatform.Contracts ./src/EventPlatform.Contracts
COPY src/EventPlatform.Infrastructure ./src/EventPlatform.Infrastructure
COPY src/EventPlatform.RetryDispatcher ./src/EventPlatform.RetryDispatcher
RUN dotnet restore src/EventPlatform.RetryDispatcher/EventPlatform.RetryDispatcher.csproj
RUN dotnet publish src/EventPlatform.RetryDispatcher/EventPlatform.RetryDispatcher.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "EventPlatform.RetryDispatcher.dll"]
