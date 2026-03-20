FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli
ENV PATH="$PATH:/root/.dotnet/tools"

COPY ["Kinnect/Kinnect.csproj", "Kinnect/"]
COPY ["Kinnect.Core/Kinnect.Core.csproj", "Kinnect.Core/"]
COPY ["Kinnect.Data/Kinnect.Data.csproj", "Kinnect.Data/"]
COPY ["Kinnect.Models/Kinnect.Models.csproj", "Kinnect.Models/"]
COPY ["Kinnect.Services/Kinnect.Services.csproj", "Kinnect.Services/"]

RUN dotnet restore "Kinnect/Kinnect.csproj"

COPY . .

WORKDIR "/src/Kinnect"
RUN libman restore
RUN dotnet build "Kinnect.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Kinnect.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

COPY --from=build /src/Kinnect/wwwroot/lib /app/publish/wwwroot/lib

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "Kinnect.dll"]
