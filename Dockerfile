FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FinvestimaAPI.csproj .
RUN dotnet restore FinvestimaAPI.csproj

COPY . .
RUN dotnet publish FinvestimaAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# PDFtoImage/SkiaSharp's native PDFium binary dynamically links against libgssapi_krb5,
# which isn't present in the slim aspnet runtime image — without it, any PDF upload
# (preview or extraction) crashes the background job silently, which looks like a hang.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "FinvestimaAPI.dll"]
