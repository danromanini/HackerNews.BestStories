FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/BestStories.Core/BestStories.Core.csproj src/BestStories.Core/
COPY src/BestStories.Infrastructure/BestStories.Infrastructure.csproj src/BestStories.Infrastructure/
COPY src/BestStories.Api/BestStories.Api.csproj src/BestStories.Api/
RUN dotnet restore src/BestStories.Api/BestStories.Api.csproj

COPY src/ src/
RUN dotnet publish src/BestStories.Api/BestStories.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "BestStories.Api.dll"]
