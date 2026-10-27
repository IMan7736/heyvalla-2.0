# Build context: apps/api  (docker build -f apps/api/Dockerfile apps/api)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Dhathuru.sln ./
COPY src ./src
RUN dotnet restore src/Dhathuru.Api/Dhathuru.Api.csproj
RUN dotnet publish src/Dhathuru.Api/Dhathuru.Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dhathuru.Api.dll"]
