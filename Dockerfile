# syntax=docker/dockerfile:1

# Build Stage: Compile and publish the application.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Set the working directory for the source code.
WORKDIR /source

# Copy all files into the container.
COPY . .

# Change to the project folder.
WORKDIR /source/FairwayFinder/src/FairwayFinder.Web

# (Optional) Restore packages if not done implicitly.
RUN dotnet restore

# Publish the app in Release configuration to the /app directory.
RUN dotnet publish -c Release -o /app

# Final Stage: Create the runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final

# Set the working directory.
WORKDIR /app

# Copy the published output from the build stage.
COPY --from=build /app .

# (Optional) If you need to run as a non-privileged user, ensure that APP_UID is defined.
USER $APP_UID

# Specify the entry point for the container.
ENTRYPOINT ["dotnet", "FairwayFinder.Web.dll"]
