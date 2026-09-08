# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for dependency caching
COPY ["src/BistQuant.Domain/BistQuant.Domain.csproj", "src/BistQuant.Domain/"]
COPY ["src/BistQuant.Application/BistQuant.Application.csproj", "src/BistQuant.Application/"]
COPY ["src/BistQuant.Infrastructure/BistQuant.Infrastructure.csproj", "src/BistQuant.Infrastructure/"]
COPY ["src/BistQuant.API/BistQuant.API.csproj", "src/BistQuant.API/"]

RUN dotnet restore "src/BistQuant.API/BistQuant.API.csproj"

# Copy full source and publish
COPY . .
WORKDIR "/src/src/BistQuant.API"
RUN dotnet publish "BistQuant.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BistQuant.API.dll"]
