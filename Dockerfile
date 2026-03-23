FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props nuget.config .editorconfig ./
COPY src/Ruvarr/Ruvarr.csproj src/Ruvarr/
RUN dotnet restore src/Ruvarr/Ruvarr.csproj

COPY src/ src/
RUN dotnet publish src/Ruvarr -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV Ruvarr__FfmpegPath=/usr/bin/ffmpeg
RUN mkdir -p /app/data && chown app:app /app/data
RUN mkdir -p /downloads/tv /downloads/movies && chown -R app:app /downloads
USER app
ENTRYPOINT ["dotnet", "Ruvarr.dll"]
