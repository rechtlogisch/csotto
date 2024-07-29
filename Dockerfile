FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY csotto.cs ./
RUN dotnet publish -a x64 -c Release

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/bin/Release/net8.0/linux-x64/publish/ ./
COPY ./certificate/*.pfx ./certificate/
COPY ./vendor/*.so /usr/share/dotnet/shared/Microsoft.NETCore.App/"$DOTNET_VERSION"/
ENTRYPOINT ["dotnet", "csotto.dll"]
