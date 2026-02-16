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

# Tune .NET GC for 512MB Fargate container
ENV DOTNET_gcServer=0 \
    DOTNET_GCConserveMemory=9 \
    DOTNET_GCHeapHardLimitPercent=75 \
    DOTNET_EnableDiagnostics=0

RUN addgroup --system --gid 1000 appgroup && adduser --system --uid 1000 --ingroup appgroup appuser
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "BookingScheduleSystem.Web.dll"]
