# Set the base image as the .NET 6.0 SDK (this includes the runtime)
FROM mcr.microsoft.com/dotnet/sdk:6.0 as build-env

# Copy everything and publish the release (publish implicitly restores and builds)
WORKDIR /app
COPY . ./
RUN dotnet publish ./NotifyBumpPackages/NotifyBumpPackages.csproj -c Release -o out --no-self-contained

# Label the container
LABEL maintainer="ALeksei Beliakov<beliakov@mindbox.ru>"
LABEL repository="https://github.com/beliakov-mb/NotifyBumpPackages"
LABEL homepage="https://github.com/beliakov-mb/NotifyBumpPackages"

# Label as GitHub action
LABEL com.github.actions.name="NotifyBumpPackages"
# Limit to 160 characters
LABEL com.github.actions.description="NotifyBumpPackages"
# See branding:
# https://docs.github.com/actions/creating-actions/metadata-syntax-for-github-actions#branding
LABEL com.github.actions.icon="alert-triangle"
LABEL com.github.actions.color="blue"

# Relayer the .NET SDK, anew with the build output
FROM mcr.microsoft.com/dotnet/sdk:6.0
COPY --from=build-env /app/out .
ENTRYPOINT [ "dotnet", "/NotifyBumpPackages.exe" ]