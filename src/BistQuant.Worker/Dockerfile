# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for dependency caching
COPY ["src/BistQuant.Domain/BistQuant.Domain.csproj", "src/BistQuant.Domain/"]
COPY ["src/BistQuant.Application/BistQuant.Application.csproj", "src/BistQuant.Application/"]
COPY ["src/BistQuant.Infrastructure/BistQuant.Infrastructure.csproj", "src/BistQuant.Infrastructure/"]
COPY ["src/BistQuant.Worker/BistQuant.Worker.csproj", "src/BistQuant.Worker/"]

RUN dotnet restore "src/BistQuant.Worker/BistQuant.Worker.csproj"

# Copy full source and publish
COPY . .
WORKDIR "/src/src/BistQuant.Worker"
RUN dotnet publish "BistQuant.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BistQuant.Worker.dll"]
