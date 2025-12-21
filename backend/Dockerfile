FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["CnabApi.csproj", "./"]
RUN dotnet restore "CnabApi.csproj"

COPY . .
RUN dotnet build "CnabApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CnabApi.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:PublishTrimmed=false /p:DebugType=none

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=publish /app/publish .

# Create wwwroot directory for static files (Swagger UI)
RUN mkdir -p /app/wwwroot

# Remove any test assemblies and other unnecessary files
RUN rm -f /app/*.Test* /app/xunit* /app/Moq* /app/testhost*

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CnabApi.dll"]
