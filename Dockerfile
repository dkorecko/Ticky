# Declare ARGs for build platform and target architecture for clarity
ARG BUILDPLATFORM
ARG TARGETARCH

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Ticky.Web/Ticky.Web.csproj", "Ticky.Web/"]
COPY ["Ticky.Internal/Ticky.Internal.csproj", "Ticky.Internal/"]
COPY ["Ticky.Base/Ticky.Base.csproj", "Ticky.Base/"]
RUN dotnet restore "Ticky.Web/Ticky.Web.csproj" -a $TARGETARCH
COPY . .
RUN dotnet build "Ticky.Web/Ticky.Web.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
RUN dotnet publish "Ticky.Web/Ticky.Web.csproj" -c Release -o /app/publish -a $TARGETARCH --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ticky.Web.dll"]