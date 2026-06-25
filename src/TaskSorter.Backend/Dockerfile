FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/TaskSorter.slnx src/TaskSorter.slnx
COPY src/TaskSorter.Core/TaskSorter.Core.csproj src/TaskSorter.Core/
COPY src/TaskSorter.Backend/TaskSorter.Backend.csproj src/TaskSorter.Backend/
RUN dotnet restore src/TaskSorter.Backend/TaskSorter.Backend.csproj -p:NuGetAudit=false

COPY src src
RUN dotnet publish src/TaskSorter.Backend/TaskSorter.Backend.csproj -c Release -o /app/publish /p:UseAppHost=false -p:NuGetAudit=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TaskSorter.Backend.dll"]
