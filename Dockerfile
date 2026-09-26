# ── Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ArtistPlatform.slnx ./
COPY src/ArtistPlatform.Domain/*.csproj src/ArtistPlatform.Domain/
COPY src/ArtistPlatform.Application/*.csproj src/ArtistPlatform.Application/
COPY src/ArtistPlatform.Infrastructure/*.csproj src/ArtistPlatform.Infrastructure/
COPY src/ArtistPlatform.Api/*.csproj src/ArtistPlatform.Api/
RUN dotnet restore src/ArtistPlatform.Api/ArtistPlatform.Api.csproj

COPY src/ src/
RUN dotnet publish src/ArtistPlatform.Api/ArtistPlatform.Api.csproj -c Release -o /app/publish

# ── Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArtistPlatform.Api.dll"]
