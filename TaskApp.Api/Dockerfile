FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TaskApp.Core/TaskApp.Core.csproj TaskApp.Core/
COPY TaskApp.Api/TaskApp.Api.csproj TaskApp.Api/
RUN dotnet restore TaskApp.Api/TaskApp.Api.csproj

COPY TaskApp.Core/ TaskApp.Core/
COPY TaskApp.Api/ TaskApp.Api/
RUN dotnet publish TaskApp.Api/TaskApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .

RUN mkdir -p /data
VOLUME ["/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "TaskApp.Api.dll"]
