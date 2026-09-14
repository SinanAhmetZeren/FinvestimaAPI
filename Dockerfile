FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FinvestimaAPI.csproj .
RUN dotnet restore FinvestimaAPI.csproj

COPY . .
RUN dotnet publish FinvestimaAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "FinvestimaAPI.dll"]
