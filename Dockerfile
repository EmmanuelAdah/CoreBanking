# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CoreBanking.sln ./
COPY CoreBanking.Domain/CoreBanking.Domain.csproj CoreBanking.Domain/
COPY CoreBanking.Application/CoreBanking.Application.csproj CoreBanking.Application/
COPY CoreBanking.Infrastructure/CoreBanking.Infrastructure.csproj CoreBanking.Infrastructure/
COPY CoreBanking.Api/CoreBanking.Api.csproj CoreBanking.Api/

RUN dotnet restore CoreBanking.Api/CoreBanking.Api.csproj

COPY . .
WORKDIR /src/CoreBanking.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CoreBanking.Api.dll"]