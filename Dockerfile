FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json ./
COPY src/BookingApp.Web/BookingApp.Web.csproj src/BookingApp.Web/
RUN dotnet restore src/BookingApp.Web/BookingApp.Web.csproj
COPY src/BookingApp.Web/ src/BookingApp.Web/
RUN dotnet publish src/BookingApp.Web/BookingApp.Web.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000
COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "BookingApp.Web.dll"]
