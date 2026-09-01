FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /build
COPY . .
RUN dotnet restore
RUN dotnet build --configuration Release --no-restore
RUN dotnet publish --configuration Release --no-build -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# curl is required by the HEALTHCHECK below (and by docker-compose.yml's
# app healthcheck, which execs into this container) — the base aspnet image
# doesn't include it.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=builder /app .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "RideBooking.dll"]
