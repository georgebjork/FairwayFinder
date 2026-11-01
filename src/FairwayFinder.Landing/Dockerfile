# Builds the static landing site. DigitalOcean App Platform builds this image and serves the
# output_dir (/site) from its CDN as a static site; no container is left running.
# Build context is this folder (the component's source directory), so the project needs nothing else
# from the repo.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet run --configuration Release -- /site

FROM busybox:1.37
COPY --from=build /site /site
