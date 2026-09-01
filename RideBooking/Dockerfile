FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /build
COPY . .
RUN dotnet restore
RUN dotnet build --configuration Release --no-restore
RUN dotnet publish --configuration Release --no-build -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=builder /app .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "RideBooking.dll"]
