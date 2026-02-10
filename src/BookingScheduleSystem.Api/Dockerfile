FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY BookingScheduleSystem.Contracts/BookingScheduleSystem.Contracts.csproj BookingScheduleSystem.Contracts/
COPY BookingScheduleSystem.Api/BookingScheduleSystem.Api.csproj BookingScheduleSystem.Api/
RUN dotnet restore BookingScheduleSystem.Api/BookingScheduleSystem.Api.csproj

# Copy everything and build
COPY . .
RUN dotnet publish BookingScheduleSystem.Api/BookingScheduleSystem.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BookingScheduleSystem.Api.dll"]
