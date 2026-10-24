FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli
ENV PATH="$PATH:/root/.dotnet/tools"

COPY ["SelfHosted/Kinnect/Kinnect/Kinnect.csproj", "SelfHosted/Kinnect/Kinnect/"]
COPY ["SelfHosted/Kinnect/Kinnect.Core/Kinnect.Core.csproj", "SelfHosted/Kinnect/Kinnect.Core/"]
COPY ["SelfHosted/Kinnect/Kinnect.Data/Kinnect.Data.csproj", "SelfHosted/Kinnect/Kinnect.Data/"]
COPY ["SelfHosted/Kinnect/Kinnect.Data.Sqlite/Kinnect.Data.Sqlite.csproj", "SelfHosted/Kinnect/Kinnect.Data.Sqlite/"]
COPY ["SelfHosted/Kinnect/Kinnect.Data.Sql/Kinnect.Data.Sql.csproj", "SelfHosted/Kinnect/Kinnect.Data.Sql/"]
COPY ["SelfHosted/Kinnect/Kinnect.Data.Npgsql/Kinnect.Data.Npgsql.csproj", "SelfHosted/Kinnect/Kinnect.Data.Npgsql/"]
COPY ["SelfHosted/Kinnect/Kinnect.Data.MySql/Kinnect.Data.MySql.csproj", "SelfHosted/Kinnect/Kinnect.Data.MySql/"]
COPY ["SelfHosted/Kinnect/Kinnect.Models/Kinnect.Models.csproj", "SelfHosted/Kinnect/Kinnect.Models/"]
COPY ["SelfHosted/Kinnect/Kinnect.Services/Kinnect.Services.csproj", "SelfHosted/Kinnect/Kinnect.Services/"]
COPY ["Components/MyWebTree/MyWebTree.Components/MyWebTree.Components.csproj", "Components/MyWebTree/MyWebTree.Components/"]
COPY ["Components/MyWebTree/MyWebTree.Core/MyWebTree.Core.csproj", "Components/MyWebTree/MyWebTree.Core/"]
COPY ["Components/EasyChatz/EasyChatz.AspNetCore/EasyChatz.AspNetCore.csproj", "Components/EasyChatz/EasyChatz.AspNetCore/"]
COPY ["Components/EasyChatz/EasyChatz.Components/EasyChatz.Components.csproj", "Components/EasyChatz/EasyChatz.Components/"]
COPY ["Components/EasyChatz/EasyChatz.Core/EasyChatz.Core.csproj", "Components/EasyChatz/EasyChatz.Core/"]
COPY ["Components/EasyChatz/EasyChatz.Services/EasyChatz.Services.csproj", "Components/EasyChatz/EasyChatz.Services/"]
COPY ["Components/EasyChatz/EasyChatz.Data/EasyChatz.Data.csproj", "Components/EasyChatz/EasyChatz.Data/"]
COPY ["Components/EasyChatz/EasyChatz.Data.Sqlite/EasyChatz.Data.Sqlite.csproj", "Components/EasyChatz/EasyChatz.Data.Sqlite/"]
COPY ["Components/EasyChatz/EasyChatz.Data.Sql/EasyChatz.Data.Sql.csproj", "Components/EasyChatz/EasyChatz.Data.Sql/"]
COPY ["Components/EasyChatz/EasyChatz.Data.Npgsql/EasyChatz.Data.Npgsql.csproj", "Components/EasyChatz/EasyChatz.Data.Npgsql/"]
COPY ["Components/EasyChatz/EasyChatz.Data.MySql/EasyChatz.Data.MySql.csproj", "Components/EasyChatz/EasyChatz.Data.MySql/"]

RUN dotnet restore "SelfHosted/Kinnect/Kinnect/Kinnect.csproj"

COPY SelfHosted/Kinnect/ SelfHosted/Kinnect/
COPY Components/MyWebTree/ Components/MyWebTree/
COPY Components/EasyChatz/ Components/EasyChatz/

WORKDIR "/src/SelfHosted/Kinnect/Kinnect"
RUN libman restore
RUN dotnet build "Kinnect.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Kinnect.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

COPY --from=build /src/SelfHosted/Kinnect/Kinnect/wwwroot/lib /app/publish/wwwroot/lib

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "Kinnect.dll"]
