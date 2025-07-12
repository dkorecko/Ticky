# Declare ARGs for build platform and target architecture for clarity
ARG BUILDPLATFORM

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy project files first for layer caching
COPY ["Ticky.Web/Ticky.Web.csproj", "Ticky.Web/"]
COPY ["Ticky.Internal/Ticky.Internal.csproj", "Ticky.Internal/"]
COPY ["Ticky.Base/Ticky.Base.csproj", "Ticky.Base/"]

# Map Docker's TARGETARCH to the arch used in .NET RIDs and restore dependencies
RUN export DOTNET_ARCH=$(case ${TARGETARCH} in \
	"amd64") echo "x64" ;; \
	"arm64") echo "arm64" ;; \
	"arm") echo "arm" ;; \
	*) echo "Unsupported architecture: ${TARGETARCH}"; exit 1 ;; \
	esac) && \
	dotnet restore "Ticky.Web/Ticky.Web.csproj" -r "linux-${DOTNET_ARCH}"

# Copy the rest of the source code
COPY . .

# Build and publish the application for the target runtime
RUN export DOTNET_ARCH=$(case ${TARGETARCH} in \
	"amd64") echo "x64" ;; \
	"arm64") echo "arm64" ;; \
	"arm") echo "arm" ;; \
	esac) && \
	dotnet publish "Ticky.Web/Ticky.Web.csproj" \
	-c Release \
	-o /app/publish \
	-r "linux-${DOTNET_ARCH}" \
	--no-restore \
	--self-contained false

# Final image stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ticky.Web.dll"]