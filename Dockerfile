# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["HermesNotifier.Api/HermesNotifier.Api.csproj", "HermesNotifier.Api/"]
RUN dotnet restore "HermesNotifier.Api/HermesNotifier.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/HermesNotifier.Api"
RUN dotnet build "HermesNotifier.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "HermesNotifier.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HermesNotifier.Api.dll"]
