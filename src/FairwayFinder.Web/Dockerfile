FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/FairwayFinder.Web/FairwayFinder.Web.csproj", "src/FairwayFinder.Web/"]
COPY ["src/FairwayFinder.Identity/FairwayFinder.Identity.csproj", "src/FairwayFinder.Identity/"]
COPY ["src/FairwayFinder.Data/FairwayFinder.Data.csproj", "src/FairwayFinder.Data/"]
COPY ["src/FairwayFinder.Shared/FairwayFinder.Shared.csproj", "src/FairwayFinder.Shared/"]
COPY ["src/FairwayFinder.Features/FairwayFinder.Features.csproj", "src/FairwayFinder.Features/"]
COPY ["src/FairwayFinder.Agents/FairwayFinder.Agents.csproj", "src/FairwayFinder.Agents/"]
RUN dotnet restore "src/FairwayFinder.Web/FairwayFinder.Web.csproj"
COPY . .
WORKDIR "/src/src/FairwayFinder.Web"
RUN dotnet build "./FairwayFinder.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FairwayFinder.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FairwayFinder.Web.dll"]
