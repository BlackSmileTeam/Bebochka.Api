FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 44315

# mysql client tools for admin backup/restore (mysqldump/mysql)
RUN apt-get update \
    && apt-get install -y --no-install-recommends default-mysql-client ca-certificates \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Bebochka.Api.csproj", "./"]
RUN dotnet restore "Bebochka.Api.csproj"
COPY . .
RUN dotnet build "Bebochka.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Bebochka.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bebochka.Api.dll"]

