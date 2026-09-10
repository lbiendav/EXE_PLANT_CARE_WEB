FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY HomePlant.csproj ./
RUN dotnet restore HomePlant.csproj
COPY . ./
RUN dotnet publish HomePlant.csproj --configuration Release --no-restore \
    --output /app/publish /p:UseAppHost=false \
    && chmod -R a+rX /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "HomePlant.dll"]
