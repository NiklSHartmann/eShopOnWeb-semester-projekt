# Bruges af pipelinen. Kompilerer IKKE - kopierer kun det output,
# som Build-stagen allerede har publiceret ("build once").
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY . .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Web.dll"]
