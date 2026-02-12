FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY BookingScheduleSystem.Contracts/BookingScheduleSystem.Contracts.csproj BookingScheduleSystem.Contracts/
COPY BookingScheduleSystem.Web/BookingScheduleSystem.Web.csproj BookingScheduleSystem.Web/
RUN dotnet restore BookingScheduleSystem.Web/BookingScheduleSystem.Web.csproj

# Copy everything and build
COPY . .
RUN dotnet publish BookingScheduleSystem.Web/BookingScheduleSystem.Web.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
RUN addgroup --system --gid 1000 appgroup && adduser --system --uid 1000 --ingroup appgroup appuser
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "BookingScheduleSystem.Web.dll"]
