FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ConsoleSharp/ConsoleSharp.csproj", "ConsoleSharp/"]
RUN dotnet restore "ConsoleSharp/ConsoleSharp.csproj"
COPY . .
RUN dotnet build -c $BUILD_CONFIGURATION
RUN dotnet test

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ConsoleSharp/ConsoleSharp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsoleSharp.dll"]