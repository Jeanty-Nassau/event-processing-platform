FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/EventPlatform.Contracts ./src/EventPlatform.Contracts
COPY src/EventPlatform.Infrastructure ./src/EventPlatform.Infrastructure
COPY src/EventPlatform.Api ./src/EventPlatform.Api
RUN dotnet restore src/EventPlatform.Api/EventPlatform.Api.csproj
RUN dotnet publish src/EventPlatform.Api/EventPlatform.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "EventPlatform.Api.dll"]
